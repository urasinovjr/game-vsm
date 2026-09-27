import os
from contextlib import asynccontextmanager
from hashlib import sha256
from secrets import token_urlsafe
from time import time
from typing import Annotated
from uuid import UUID, uuid4

from fastapi import Depends, FastAPI, Header, HTTPException
from pydantic import BaseModel
from pydantic import Field as InputField
from sqlalchemy import JSON, Column, update
from sqlmodel import Field, Session, SQLModel, create_engine, select

from server import engine as rules
from server.scenario import SCENARIO, Scenario


class Profile(SQLModel, table=True):
    id: str = Field(primary_key=True)
    name: str
    token_hash: str = Field(index=True, unique=True)


class Attempt(SQLModel, table=True):
    id: str = Field(primary_key=True)
    profile_id: str = Field(index=True, foreign_key="profile.id")
    revision: int = 0
    graph: dict = Field(sa_column=Column(JSON, nullable=False))
    state: dict = Field(sa_column=Column(JSON, nullable=False))


class DemoRequest(BaseModel):
    name: str = InputField(
        default="Стажёр", min_length=1, max_length=40, pattern=r"^[\w А-Яа-яЁё.\-]+$"
    )


class DemoResponse(BaseModel):
    id: str
    name: str
    token: str


class ActionRequest(BaseModel):
    request_id: UUID
    revision: int = InputField(ge=0)
    node: str = InputField(max_length=60)
    choice: str = InputField(max_length=60)
    expedite: bool = False


def create_app(database_url: str | None = None, clock=time) -> FastAPI:
    url = database_url or os.getenv("DATABASE_URL", "sqlite:///./gamevsm.db")
    db = create_engine(
        url, connect_args={"check_same_thread": False} if url.startswith("sqlite") else {}
    )

    @asynccontextmanager
    async def lifespan(app):
        SQLModel.metadata.create_all(db)
        yield
        db.dispose()

    app = FastAPI(title="Проводник ВСМ — учебная смена", version="0.1.0", lifespan=lifespan)
    app.state.db = db

    def session():
        with Session(db) as value:
            yield value

    DB = Annotated[Session, Depends(session)]

    def identity(db_session: DB, authorization: Annotated[str | None, Header()] = None) -> Profile:
        if not authorization or not authorization.startswith("Bearer "):
            raise HTTPException(401, "Нужен учебный профиль.")
        digest = sha256(authorization[7:].encode()).hexdigest()
        profile = db_session.exec(select(Profile).where(Profile.token_hash == digest)).first()
        if profile is None:
            raise HTTPException(401, "Учебный профиль не найден.")
        return profile

    User = Annotated[Profile, Depends(identity)]

    def owned(db_session, id, user):
        attempt = db_session.get(Attempt, id)
        if attempt is None or attempt.profile_id != user.id:
            raise HTTPException(404, "Смена не найдена.")
        return attempt

    def save(db_session, attempt, state):
        changed = db_session.execute(
            update(Attempt)
            .where(Attempt.id == attempt.id, Attempt.revision == attempt.revision)
            .values(state=state, revision=attempt.revision + 1)
            .execution_options(synchronize_session=False)
        )
        if changed.rowcount != 1:
            db_session.rollback()
            raise HTTPException(409, "Смена изменилась в другом окне. Загрузите её заново.")
        db_session.commit()
        db_session.refresh(attempt)

    def reconcile(db_session, attempt):
        graph = Scenario.model_validate(attempt.graph)
        state = rules.expire(attempt.state, graph, clock())
        if state is not attempt.state:
            save(db_session, attempt, state)
        return graph

    def view(attempt, graph):
        state = {k: v for k, v in attempt.state.items() if k != "receipts"}
        node = graph.nodes.get(state["node"])
        task = None
        if node:
            task = {k: v for k, v in node.model_dump().items() if k not in ("timeout", "choices")}
            task["choices"] = [
                {"id": c.id, "label": c.label} for c in node.choices if c.id != node.timeout
            ]
        return {
            "id": attempt.id,
            "revision": attempt.revision,
            "state": state,
            "task": task,
            "server_time": clock(),
            "training_status": "prototype-awaiting-expert-review",
        }

    @app.get("/api/health")
    def health() -> dict:
        return {"status": "ok", "scenario": SCENARIO.version}

    @app.post("/api/profiles", response_model=DemoResponse)
    def register(body: DemoRequest, db_session: DB):
        token = token_urlsafe(32)
        profile = Profile(
            id=str(uuid4()),
            name=body.name.strip() or "Стажёр",
            token_hash=sha256(token.encode()).hexdigest(),
        )
        db_session.add(profile)
        db_session.commit()
        return DemoResponse(id=profile.id, name=profile.name, token=token)

    @app.post("/api/attempts")
    def start(db_session: DB, user: User) -> dict:
        attempt = Attempt(
            id=str(uuid4()),
            profile_id=user.id,
            graph=SCENARIO.model_dump(),
            state=rules.new_attempt(SCENARIO, clock()),
        )
        db_session.add(attempt)
        db_session.commit()
        return view(attempt, SCENARIO)

    @app.get("/api/attempts/{id}")
    def get_attempt(id: UUID, db_session: DB, user: User) -> dict:
        attempt = owned(db_session, str(id), user)
        return view(attempt, reconcile(db_session, attempt))

    @app.post("/api/attempts/{id}/actions")
    def action(id: UUID, body: ActionRequest, db_session: DB, user: User) -> dict:
        attempt = owned(db_session, str(id), user)
        graph = reconcile(db_session, attempt)
        receipt_key = str(body.request_id)
        fingerprint = f"{body.node}:{body.choice}:{body.expedite}"
        prior = attempt.state["receipts"].get(receipt_key)
        if prior:
            if prior != fingerprint:
                raise HTTPException(
                    409, "Это действие уже отправлено с другим выбором. Загрузите смену заново."
                )
            return view(attempt, graph)
        if attempt.state["status"] != "active":
            raise HTTPException(409, "Смена уже завершена. Откройте разбор.")
        if attempt.revision != body.revision:
            raise HTTPException(409, "Смена уже продвинулась дальше. Загрузите её заново.")
        try:
            state = rules.act(attempt.state, graph, body.node, body.choice, clock(), body.expedite)
        except rules.InvalidAction as error:
            raise HTTPException(422, str(error)) from error
        state["receipts"][receipt_key] = fingerprint
        save(db_session, attempt, state)
        return view(attempt, graph)

    @app.get("/api/profile")
    def profile(db_session: DB, user: User) -> dict:
        attempts = db_session.exec(select(Attempt).where(Attempt.profile_id == user.id)).all()
        for a in attempts:
            reconcile(db_session, a)
        completed = [a for a in attempts if a.state["status"] == "complete"]
        best = max(completed, key=lambda a: a.state["quality"] + a.state["trust"], default=None)
        achievements = ["Первая смена"] if completed else []
        if any(
            not any(e["quality"] < 0 or e["trust"] < 0 for e in a.state["events"])
            for a in completed
        ):
            achievements.append("Внимание к людям")
        return {
            "id": user.id,
            "name": user.name,
            "completed": len(completed),
            "achievements": achievements,
            "competencies": best.state["competencies"] if best else {},
            "notifications": ["Разбор новой смены доступен"]
            if completed
            else ["Первая смена готова к прохождению"],
            "attempts": [
                {
                    "id": a.id,
                    "status": a.state["status"],
                    "quality": a.state["quality"],
                    "trust": a.state["trust"],
                    "started_at": a.state["started_at"],
                }
                for a in attempts
            ],
        }

    @app.get("/api/leaderboard")
    def leaderboard(db_session: DB, user: User) -> list[dict]:
        # Best completed result per profile; retries never accumulate leaderboard points.
        best = {}
        for a in db_session.exec(select(Attempt)).all():
            if a.state["status"] != "complete":
                continue
            score = a.state["quality"] + a.state["trust"]
            if a.profile_id not in best or best[a.profile_id]["score"] < score:
                p = db_session.get(Profile, a.profile_id)
                best[a.profile_id] = {"name": p.name, "score": score, "self": p.id == user.id}
        return sorted(best.values(), key=lambda item: item["score"], reverse=True)[:20]

    @app.get("/api/analytics")
    def analytics(db_session: DB, user: User) -> dict:
        attempts = db_session.exec(select(Attempt).where(Attempt.profile_id == user.id)).all()
        for a in attempts:
            reconcile(db_session, a)
        events = [e for a in attempts for e in a.state["events"]]
        duration = [
            a.state["finished_at"] - a.state["started_at"]
            for a in attempts
            if a.state["finished_at"]
        ]
        return {
            "attempts": len(attempts),
            "completed": sum(a.state["status"] == "complete" for a in attempts),
            "stopped": sum(a.state["status"] == "stopped" for a in attempts),
            "decisions": len(events),
            "errors": sum(e["quality"] < 0 or e["trust"] < 0 for e in events),
            "mean_duration_seconds": round(sum(duration) / len(duration)) if duration else None,
            "scenario_versions": sorted({a.state["version"] for a in attempts}),
        }

    return app


app = create_app()
