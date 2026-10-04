# T-SUN-052 judge questions, advisory. agy draws on the DialG1 pinch frames, three per order, letters really swapped in "ba".
# The F2 pairwise judge (tools/fidelity) is run separately.
#   python apps/sundial/Art/Scripts/layout_t052_judge.py            # 3 questions x 2 orders x 3 draws, layout (B) vs look A
#   python apps/sundial/Art/Scripts/layout_t052_judge.py --rubric   # the T-SUN-031 A3 rubric, three draws each on A and B
#   python apps/sundial/Art/Scripts/layout_t052_judge.py --tally    # judge-tally.json
import json
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
RUN = os.path.join(REPO, "orchestration", "runs", "sundial", "T-SUN-052")
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A2-05-field-notebook-1.png")
A = os.path.join(RUN, "pinch-a.png")
B = os.path.join(RUN, "pinch-b.png")
AGY = os.environ.get("AGY", os.path.join(os.environ.get("LOCALAPPDATA", ""), "agy", "bin", "agy.exe"))

ORDERS = (
    ("ab", "candidate A is %s and candidate B is %s" % (A, B)),
    # Letters really swap here: in "ba" candidate A is the layout (variant B) and candidate B is look A.
    ("ba", "candidate A is %s and candidate B is %s" % (B, A)),
)

QUESTIONS = (
    ("closer", "Which candidate is closer to the reference watercolour field notebook? Reply with A or B on the first line, then one sentence."),
    ("layout", "Look only at layout and proportion, not at paint quality: how big the sundial is next to the pinching hand, how much of the face the soil bed fills, and how thick and warm the rim band is. Which candidate matches the reference's layout more closely? Reply with A or B on the first line, then one sentence."),
    ("halo", "Look only at the gold pinch halo around the flowering plant. In which candidate does it hug the pinched plant as one closed glowing line, like the reference, without running over the neighbouring plant? Reply with A or B on the first line, then one sentence."),
)

RUBRIC = ("Score 1-5 how closely the right image matches the left reference in hand-drawn quality, colour, line work and mood; "
          "list the 3 biggest differences. Image: %s")


def call(prompt, out, extra):
    completed = subprocess.run([AGY, "-p", prompt, "--model", "gemini-3.8-flash-high", "--output-format", "json"],
                               cwd=REPO, capture_output=True, text=True)
    payload = dict(extra)
    payload.update({"exit": completed.returncode, "stdout": completed.stdout, "stderr": completed.stderr[-2000:]})
    with open(out, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(payload, handle, indent=2)
        handle.write("\n")
    print("wrote", out, "exit", completed.returncode)
    return completed.returncode


def ask(order, spec, draw, key, question):
    prompt = "Reference notebook: %s. %s. %s" % (REF, spec, question)
    out = os.path.join(RUN, "judge-%s-%s-%d.json" % (key, order, draw))
    return call(prompt, out, {"spec": spec, "question": key, "order": order, "draw": draw})


def text_of(stdout):
    try:
        j = json.loads(stdout)
        return str(j.get("response") or j.get("result") or j.get("text") or stdout)
    except ValueError:
        return stdout


def tally():
    """In order "ab" letter A is look A, in "ba" letter A is the layout."""
    counts = {}
    for name in sorted(os.listdir(RUN)):
        if not (name.startswith("judge-") and name.endswith(".json")) or name in ("judge-tally.json",) or "f2" in name:
            continue
        with open(os.path.join(RUN, name), encoding="utf-8") as handle:
            d = json.load(handle)
        text = text_of(d["stdout"])
        m = re.match(r"\s*([AB])(?![A-Za-z])", text) or re.search(r"(?<![A-Za-z])([AB])(?![A-Za-z])", text)
        pick = m.group(1) if m else "?"
        layout = (pick == "B") if d["order"] == "ab" else (pick == "A")
        key = "ungraded" if pick == "?" else ("layout" if layout else "lookA")
        counts.setdefault(d["question"], {"layout": 0, "lookA": 0, "ungraded": 0})[key] += 1
    out = os.path.join(RUN, "judge-tally.json")
    with open(out, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(counts, handle, indent=2)
        handle.write("\n")
    print(json.dumps(counts))


def rubric():
    for tag in ("a", "b"):
        sbs = os.path.join(RUN, "pinch-%s.sbs.png" % tag)
        for draw in (1, 2, 3):
            out = os.path.join(RUN, "rubric-%s-%d.json" % (tag, draw))
            call(RUBRIC % sbs, out, {"look": tag, "draw": draw})


def main():
    if len(sys.argv) > 1 and sys.argv[1] == "--tally":
        tally()
        return
    if len(sys.argv) > 1 and sys.argv[1] == "--rubric":
        rubric()
        return
    failed = 0
    for key, question in QUESTIONS:
        for order, spec in ORDERS:
            for draw in (1, 2, 3):
                failed += ask(order, spec, draw, key, question) != 0
    if failed:
        raise SystemExit("%s judge calls failed" % failed)


if __name__ == "__main__":
    main()
