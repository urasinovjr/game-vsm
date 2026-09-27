"""Pure attempt transitions. Only the server's clock and pinned graph decide outcomes."""

from copy import deepcopy
from uuid import uuid4

from server.scenario import Scenario


class InvalidAction(ValueError):
    pass


def new_attempt(scenario: Scenario, now: float) -> dict:
    return {
        "node": scenario.start,
        "version": scenario.version,
        "status": "active",
        "quality": 70,
        "trust": 70,
        "competencies": {"inspection": 0, "communication": 0, "safety": 0},
        "flags": [],
        "events": [],
        "receipts": {},
        "started_at": now,
        "entered_at": now,
        "deadline": None,
        "finished_at": None,
    }


def choose(state: dict, scenario: Scenario, choice_id: str, now: float) -> dict:
    if state["status"] != "active":
        raise InvalidAction("Смена уже завершена.")
    node = scenario.nodes[state["node"]]
    selected = next((c for c in node.choices if c.id == choice_id), None)
    if selected is None:
        raise InvalidAction("Такого действия нет в текущем задании.")
    result = deepcopy(state)
    before = {"quality": state["quality"], "trust": state["trust"]}
    for scale in ("quality", "trust"):
        result[scale] = max(0, min(100, result[scale] + getattr(selected, scale)))
    if selected.competency:
        result["competencies"][selected.competency] += (
            1 if selected.quality + selected.trust > 0 else -1
        )
    if selected.flag and selected.flag not in result["flags"]:
        result["flags"].append(selected.flag)
    result["events"].append(
        {
            "id": str(uuid4()),
            "node": node.id,
            "title": node.title,
            "choice": selected.id,
            "label": selected.label,
            "feedback": selected.feedback,
            "source": node.source,
            "quality": result["quality"] - before["quality"],
            "trust": result["trust"] - before["trust"],
            "at": now,
            "elapsed": now - state["entered_at"],
            "serious": selected.serious,
        }
    )
    result["node"] = selected.next
    result["entered_at"] = now
    result["deadline"] = None
    if selected.next in ("complete", "stopped"):
        result["status"] = selected.next
        result["finished_at"] = now
    else:
        next_node = scenario.nodes[selected.next]
        if next_node.seconds:
            result["deadline"] = now + next_node.seconds
    return result


def expire(state: dict, scenario: Scenario, now: float) -> dict:
    if state["status"] == "active" and state["deadline"] is not None and now >= state["deadline"]:
        return choose(state, scenario, scenario.nodes[state["node"]].timeout, now)
    return state


def act(
    state: dict,
    scenario: Scenario,
    node_id: str,
    choice_id: str,
    now: float,
    expedite: bool = False,
) -> dict:
    if state["node"] != node_id:
        raise InvalidAction("Задание уже сменилось. Продолжите с текущего.")
    node = scenario.nodes[node_id]
    if choice_id == node.timeout:
        raise InvalidAction("Этот исход наступает только по истечении времени.")
    if expedite and not node.wait:
        raise InvalidAction("Здесь нельзя перейти дальше, не выполнив задание.")
    if node.wait and not expedite and now < state["entered_at"] + node.wait:
        raise InvalidAction(
            "Этот участок пути ещё не пройден. Можно продолжить обход или перейти дальше."
        )
    return choose(state, scenario, choice_id, now)
