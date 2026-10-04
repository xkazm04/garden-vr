# T-SUN-050 F1 table: distance to ref-1 per region and metric, look A against the sprint (and the all-three-drawn alternative).
# Reads the metrics.json files that `fid.cmd metrics <dir> --frame DialG1` wrote. Lower is closer. `*` marks a ladder-valid rung.
#   python apps/sundial/Art/Scripts/sprint_t050_f1.py > orchestration/runs/sundial/T-SUN-050/f1-table.md
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
RUN = os.path.normpath(os.path.join(HERE, "..", "..", "..", "..", "orchestration", "runs", "sundial", "T-SUN-050"))
REGIONS = ["paper", "ink", "washes", "wash.morning", "wash.midday", "wash.dusk", "soil", "plants", "plant.morning", "plant.midday", "plant.dusk", "frame"]
METRICS = ["ciede2000", "lab_emd", "dinov2", "dreamsim", "dists"]
SHORT = {"a": "A", "s": "sprint", "s3": "sprint+leafplant"}


def load(folder, tag):
    d = json.load(open(os.path.join(RUN, folder, "metrics.json")))
    for v in d["shots"].values():
        if v["file"].startswith("dial-g1-%s-" % tag):
            return v["vsReference"]
    raise SystemExit("no %s in %s" % (tag, folder))


def cell(a, b):
    ra = a["distance"]
    rb = b["distance"]
    star = "*" if b.get("valid") else ""
    arrow = "better" if rb < ra - 1e-9 else ("worse" if rb > ra + 1e-9 else "same")
    return "%.3f to %.3f %s%s" % (ra, rb, arrow, star)


def table(folder, tags):
    print("### %s (A to %s)\n" % (folder, ", ".join(SHORT[t] for t in tags[1:])))
    base = load(folder, tags[0])
    for tag in tags[1:]:
        other = load(folder, tag)
        print("**%s**\n" % SHORT[tag])
        print("| Region | " + " | ".join(METRICS) + " |")
        print("|---" * (len(METRICS) + 1) + "|")
        better = {m: 0 for m in METRICS}
        worse = {m: 0 for m in METRICS}
        for reg in REGIONS:
            if reg not in base or reg not in other:
                continue
            row = []
            for m in METRICS:
                a, b = base[reg][m], other[reg][m]
                row.append(cell(a, b))
                if b["distance"] < a["distance"] - 1e-9:
                    better[m] += 1
                elif b["distance"] > a["distance"] + 1e-9:
                    worse[m] += 1
            print("| %s | %s |" % (reg, " | ".join(row)))
        print("| rows closer / farther | " + " | ".join("%d / %d" % (better[m], worse[m]) for m in METRICS) + " |\n")


if __name__ == "__main__":
    table("f1", ["a", "s", "s3"])
    table("f1-pinch", ["a", "s"])
