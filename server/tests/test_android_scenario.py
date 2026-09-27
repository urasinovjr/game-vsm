from pathlib import Path

from server.scenario import SCENARIO


def test_android_scenario_matches_authored_graph():
    asset = Path(__file__).resolve().parents[2] / "app/android/app/src/main/assets/scenario.json"
    assert asset.read_text(encoding="utf-8") == SCENARIO.model_dump_json()
