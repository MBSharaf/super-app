import json
import os
import time

PATH = os.environ.get("ANTIALGORITHM_HISTORY") or os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "history.jsonl"
)
LIMIT = 300


def load(kind=None):
    try:
        with open(PATH, encoding="utf-8") as f:
            lines = f.read().splitlines()
    except FileNotFoundError:
        return []
    out = []
    for line in lines:
        try:
            entry = json.loads(line)
        except ValueError:
            continue
        if kind is None or entry.get("type") == kind:
            out.append(entry)
    return out


def add(entry):
    entry = {"date": time.strftime("%Y-%m-%d"), **entry}
    rows = load()[-(LIMIT - 1):] + [entry]
    tmp = PATH + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        for row in rows:
            f.write(json.dumps(row, ensure_ascii=False) + "\n")
    os.replace(tmp, PATH)
