import re
from uuid import uuid4

import pytest
from fastapi.testclient import TestClient

from server.app import create_app
from server.scenario import SCENARIO


@pytest.fixture
def game(tmp_path):
    now = [1000.0]
    app = create_app(f"sqlite:///{tmp_path}/test.db", clock=lambda: now[0])
    with TestClient(app) as client:
        token = client.post("/api/profiles", json={"name": "Тестовый стажёр"}).json()["token"]
        client.headers["Authorization"] = "Bearer " + token
        yield client, now


def start(client):
    response = client.post("/api/attempts")
    assert response.status_code == 200, response.text
    return response.json()


def decide(client, attempt, choice=None, request_id=None):
    body = {
        "request_id": request_id or str(uuid4()),
        "revision": attempt["revision"],
        "node": attempt["state"]["node"],
        "choice": choice or attempt["task"]["choices"][0]["id"],
        "expedite": bool(attempt["task"]["wait"]),
    }
    return client.post(f"/api/attempts/{attempt['id']}/actions", json=body), body


def reach(client, target):
    attempt = start(client)
    for _ in range(50):
        if attempt["state"]["node"] == target:
            return attempt
        response, _ = decide(client, attempt)
        assert response.status_code == 200, response.text
        attempt = response.json()
    pytest.fail("Target not reachable")


def test_full_shift_saved_debrief_profile_and_retry(game):
    client, _ = game
    attempt = reach(client, "handover")
    response, body = decide(client, attempt)
    result = response.json()
    assert result["state"]["status"] == "complete"
    assert result["state"]["quality"] == 100
    assert result["state"]["trust"] == 100
    assert result["task"] is None
    repeated = client.post(f"/api/attempts/{attempt['id']}/actions", json=body).json()
    assert len(repeated["state"]["events"]) == len(result["state"]["events"])
    assert repeated["revision"] == result["revision"]
    saved = client.get(f"/api/attempts/{attempt['id']}").json()
    assert saved["state"] == result["state"]
    profile = client.get("/api/profile").json()
    assert profile["completed"] == 1
    assert "Первая смена" in profile["achievements"]
    assert client.get("/api/leaderboard").json()[0]["score"] == 200
    assert client.get("/api/analytics").json()["errors"] == 0


def test_error_can_be_repaired_and_preserved_in_debrief(game):
    client, _ = game
    attempt = reach(client, "inspect")
    response, _ = decide(client, attempt, "ignore")
    bad = response.json()
    assert bad["state"]["quality"] == 60
    assert bad["state"]["node"] == "inspection_fix"
    fixed, _ = decide(client, bad)
    assert fixed.json()["state"]["quality"] == 63
    assert fixed.json()["state"]["events"][-2]["choice"] == "ignore"
    assert "bag_cleared" in fixed.json()["state"]["flags"]


def test_server_timeout_survives_reload_and_blocks_late_choice(game):
    client, now = game
    attempt = reach(client, "medical")
    assert attempt["state"]["deadline"] == 1060
    now[0] = 1060
    response, _ = decide(client, attempt, "call")
    assert response.status_code == 409
    saved = client.get(f"/api/attempts/{attempt['id']}").json()
    assert saved["state"]["status"] == "stopped"
    assert saved["state"]["events"][-1]["choice"] == "timeout"
    assert client.get("/api/profile").json()["completed"] == 0


def test_serious_violation_stops_and_wait_skip_cannot_bypass_medical(game):
    client, _ = game
    attempt = reach(client, "medical")
    body = {
        "request_id": str(uuid4()),
        "revision": attempt["revision"],
        "node": "medical",
        "choice": "call",
        "expedite": True,
    }
    assert client.post(f"/api/attempts/{attempt['id']}/actions", json=body).status_code == 422
    response, _ = decide(client, attempt, "abandon")
    assert response.json()["state"]["status"] == "stopped"


def test_old_revision_wrong_node_and_request_reuse(game):
    client, _ = game
    attempt = start(client)
    response, body = decide(client, attempt)
    assert response.status_code == 200
    body["request_id"] = str(uuid4())
    assert client.post(f"/api/attempts/{attempt['id']}/actions", json=body).status_code == 409
    new = response.json()
    response, body = decide(client, new)
    body["choice"] = "ignore"
    assert client.post(f"/api/attempts/{attempt['id']}/actions", json=body).status_code == 409


def test_attempt_ownership(game):
    client, _ = game
    attempt = start(client)
    token = client.post("/api/profiles", json={"name": "Другой стажёр"}).json()["token"]
    client.headers["Authorization"] = "Bearer " + token
    assert client.get(f"/api/attempts/{attempt['id']}").status_code == 404


def test_wait_uses_clock_unless_explicitly_expedited(game):
    client, now = game
    attempt = reach(client, "transfer")
    body = {
        "request_id": str(uuid4()),
        "revision": attempt["revision"],
        "node": "transfer",
        "choice": "continue",
        "expedite": False,
    }
    assert client.post(f"/api/attempts/{attempt['id']}/actions", json=body).status_code == 422
    now[0] += 90
    assert client.post(f"/api/attempts/{attempt['id']}/actions", json=body).status_code == 200


@pytest.mark.parametrize(
    "node,bad,repair_node,repair,next_node",
    [
        ("boarding1", "refuse", "boarding1_fix", "apologize", "boarding2"),
        ("boarding2", "send", "boarding2_fix", "guide", "depart"),
        ("service_request", "later", "service_fix", "repair", "bag_action"),
        ("phone", "shout", "phone_fix", "repair", "quiet2"),
    ],
)
def test_each_repair_branch_retains_consequence_after_reload(
    game, node, bad, repair_node, repair, next_node
):
    client, _ = game
    attempt = reach(client, node)
    response, _ = decide(client, attempt, bad)
    assert response.status_code == 200
    damaged = response.json()
    assert damaged["state"]["node"] == repair_node
    assert damaged["state"]["events"][-1]["feedback"]
    restored = client.get(f"/api/attempts/{attempt['id']}").json()
    assert restored["state"] == damaged["state"]
    response, _ = decide(client, restored, repair)
    assert response.status_code == 200, response.text
    fixed = response.json()
    assert fixed["state"]["node"] == next_node
    assert [e["choice"] for e in fixed["state"]["events"][-2:]] == [bad, repair]


# Речь прототипа и клавиши не должны попадать в реплики, цели и последствия.
# Оговорки об учебной адаптации живут в поле source и сюда не проверяются.
FORBIDDEN_IN_PLAYER_TEXT = re.compile(
    r"приблизительн|фонов|предметн|критическ|эпизод|\bузл|\bузел|прототип|реализац"
    r"|таймер|ускорени|сократить|спокойный участок|сервер|учебн|попытк"
    r"|клавиш|нажмите|\bF\b|\bEsc\b|WASD|\d+\s?(с|сек)\b",
    re.IGNORECASE,
)


def test_player_texts_use_world_speech_not_prototype_terms():
    offenders = []
    for node in SCENARIO.nodes.values():
        timeout = node.timeout
        texts = [node.title, node.text] + [
            part
            for c in node.choices
            for part in ((c.feedback,) if c.id == timeout else (c.label, c.feedback))
        ]
        offenders += [(node.id, t) for t in texts if FORBIDDEN_IN_PLAYER_TEXT.search(t)]
    assert offenders == []
