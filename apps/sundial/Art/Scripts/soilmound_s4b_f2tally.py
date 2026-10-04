# T-SUN-051: crescent against the T-SUN-046 mound from the F2 calls ledger, per criterion. A pair is decided when both orders name the same player;
# one order that names a player and one that ties is "lean"; the rest is a tie or position bias.
#   python apps/sundial/Art/Scripts/soilmound_s4b_f2tally.py orchestration/runs/sundial/T-SUN-051/judge-f2-crescent-vs-046.json.calls.jsonl
import json
import sys
from collections import defaultdict

rows = [json.loads(line) for line in open(sys.argv[1], encoding="utf-8")]
by = defaultdict(dict)
for r in rows:
    if r.get("kind") != "pairwise" or {r["left_id"], r["right_id"]} != {"mound046", "crescent"}:
        continue
    p = r.get("parsed") or {}
    w = p.get("winner")
    if w in ("A", "B"):
        who = r["a"]["id"] if w == "A" else r["b"]["id"]
    else:
        who = "tie" if w else "ungraded"
    by[(r["criterion"], r["draw"])][r["order"]] = who
out = {}
for (crit, draw), orders in sorted(by.items()):
    o = out.setdefault(crit, {"crescent": 0, "mound046": 0, "lean_crescent": 0, "lean_mound046": 0, "tie_or_bias": 0, "draws": []})
    ab, ba = orders.get("ab"), orders.get("ba")
    o["draws"].append({"draw": draw, "ab": ab, "ba": ba})
    if ab == ba == "crescent":
        o["crescent"] += 1
    elif ab == ba == "mound046":
        o["mound046"] += 1
    elif {ab, ba} == {"crescent", "tie"}:
        o["lean_crescent"] += 1
    elif {ab, ba} == {"mound046", "tie"}:
        o["lean_mound046"] += 1
    else:
        o["tie_or_bias"] += 1
print(json.dumps(out, indent=1))
json.dump(out, open(sys.argv[1].replace(".calls.jsonl", ".tally.json"), "w"), indent=1)
