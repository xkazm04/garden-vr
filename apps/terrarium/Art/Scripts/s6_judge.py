"""T-TER-049 judge helpers. The pairwise runs are the F2 judge (tools/fidelity/fid.py judge --pairwise, see the report);
this script tallies a run's calls per unordered pair and per order, and runs the standard rubric (s5_judge) in this folder.

    python apps/terrarium/Art/Scripts/s6_judge.py tally pair-g1-overall.json     # writes pair-g1-overall-tally.json
    python apps/terrarium/Art/Scripts/s6_judge.py rubric c s6
"""
import json
import os
import sys
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import s5_judge  # noqa: E402

RUN = os.path.join(s5_judge.REPO, "orchestration", "runs", "terrarium", "T-TER-049")


def tally(name):
    path = os.path.join(RUN, name + ".calls.jsonl")
    pairs = defaultdict(lambda: defaultdict(int))
    orders = defaultdict(lambda: defaultdict(int))
    for line in open(path, encoding="utf-8"):
        call = json.loads(line)
        a, b = call["a"]["id"], call["b"]["id"]
        key = "|".join(sorted((a, b)))
        winner = (call.get("parsed") or {}).get("winner")
        who = a if winner == "A" else (b if winner == "B" else "ungraded")
        pairs[key][who] += 1
        orders["%s|%s" % (key, call["order"])][who] += 1
    out = {"pairs": {k: dict(v) for k, v in sorted(pairs.items())}, "by_order": {k: dict(v) for k, v in sorted(orders.items())}}
    dest = os.path.join(RUN, name.replace(".json", "") + "-tally.json")
    json.dump(out, open(dest, "w"), indent=1)
    print(json.dumps(out, indent=1))


if __name__ == "__main__":
    if sys.argv[1] == "tally":
        tally(sys.argv[2])
    elif sys.argv[1] == "rubric":
        s5_judge.RUN = RUN
        for look in sys.argv[2:]:
            s5_judge.rubric(look)
    else:
        raise SystemExit("tally <run.json> | rubric <look>...")
