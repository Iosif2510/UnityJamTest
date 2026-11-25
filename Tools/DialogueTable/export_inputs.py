"""Export what a dialogue writer needs from the game data into inputs.json.

Reads the ScriptableObject assets directly, so the table is always generated from the same
characters, diseases, banned words and fallback lines the game uses.

Usage (from the repository root):  python Tools/DialogueTable/export_inputs.py
Requires PyYAML.
"""
import json
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "Assets" / "GameData"
OUT = Path(__file__).with_name("inputs.json")

REACTION_KEYS = ["rcv_ok", "rcv_bottle", "rcv_ink", "rcv_both", "aft_cured", "aft_cured_ink", "aft_odd", "aft_slime"]


def load(name):
    """Unity writes YAML 1.1 with its own tag header; drop that so PyYAML can read the document."""
    lines = []
    for line in (DATA / name).read_text(encoding="utf-8").splitlines():
        if line.startswith("%"):
            continue
        lines.append("---" if line.startswith("--- !u!") else line)
    return yaml.safe_load("\n".join(lines))["MonoBehaviour"]


def main():
    voices = {v["code"]: v for v in load("CharacterVoices.asset")["voices"]}
    customers = load("Customers.asset")
    jobs = {profile["id"]: profile.get("job", "") for profile in customers["profiles"]}
    spawn = list(customers["spawnPool"])

    characters = []
    for code in spawn:
        voice = voices.get(code)
        if voice is None:
            raise SystemExit(f"{code}: in the spawn pool but has no voice entry")
        characters.append({
            "code": code,
            "name": voice["displayName"],
            "job": jobs.get(code, ""),
            "speechStyle": voice["speechStyle"],
            "personality": voice["personality"],
            "chatTopics": list(voice["chatTopics"]),
        })

    catalog = load("DiseasesAndRecipes.asset")
    fallback = load("DialogueCatalog.asset")["lines"]
    symptom_examples = {line["diseaseId"]: line["text"] for line in fallback if line.get("diseaseId")}
    reactions = {line["key"]: line["text"] for line in fallback if line["key"] in REACTION_KEYS}
    missing = [key for key in REACTION_KEYS if key not in reactions]
    if missing:
        raise SystemExit(f"fallback reactions missing: {missing}")

    diseases = []
    for disease in catalog["diseases"]:
        diseases.append({
            "id": disease["id"],
            "name": disease["displayName"],
            "symptomMeanings": list(disease["symptomMeanings"]),
            "lineExamples": list(disease["lineExamples"]),
            "bannedWords": list(disease["bannedWords"]),
            "exampleSymptom": symptom_examples[disease["id"]],
        })

    OUT.write_text(json.dumps({
        "characters": characters,
        "diseases": diseases,
        "exampleReactions": reactions,
    }, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{len(characters)} characters x {len(diseases)} diseases = {len(characters) * len(diseases)} orders -> {OUT.name}")


if __name__ == "__main__":
    main()
