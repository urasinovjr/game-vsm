"""Real deployment smoke: one synthetic profile, complete shift and idempotent handover.

Usage: .venv/bin/python tools/check-game-api.py http://127.0.0.1:8080
Leaves the explicitly named QA profile/results in the local demo database.
"""

import sys
from uuid import uuid4

import httpx

base = sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:8080"
with httpx.Client(base_url=base, timeout=30) as client:
    response = client.post("/api/profiles", json={"name": "QA " + str(uuid4())[:8]})
    response.raise_for_status()
    client.headers["Authorization"] = "Bearer " + response.json()["token"]
    attempt = client.post("/api/attempts").json()
    decisions = 0
    while attempt["task"]:
        task = attempt["task"]
        body = {
            "request_id": str(uuid4()),
            "revision": attempt["revision"],
            "node": task["id"],
            "choice": task["choices"][0]["id"],
            "expedite": bool(task["wait"]),
        }
        url = "/api/attempts/" + attempt["id"] + "/actions"
        response = client.post(url, json=body)
        response.raise_for_status()
        attempt = response.json()
        decisions += 1
        if decisions > 50:
            raise RuntimeError("Graph did not terminate")
    duplicate = client.post(url, json=body)
    duplicate.raise_for_status()
    assert duplicate.json()["revision"] == attempt["revision"]
    assert attempt["state"]["status"] == "complete"
    assert attempt["state"]["quality"] == 100 and attempt["state"]["trust"] == 100
    assert client.get("/api/profile").json()["completed"] == 1
    assert client.get("/api/analytics").json()["errors"] == 0
    print(
        f"PASS: {decisions} decisions, complete, 100/100, saved, duplicate rejected as duplicate."
    )
