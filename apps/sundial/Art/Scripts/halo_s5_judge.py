# S5 halo questions, advisory. Twelve agy draws on the DialG1 pinch frames, three per order, two questions.
# The F2 pairwise judge (tools/fidelity) is run separately.
#   python apps/sundial/Art/Scripts/halo_s5_judge.py          # 12 draws
#   python apps/sundial/Art/Scripts/halo_s5_judge.py --tally  # judge-tally.json
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
RUN = os.path.join(REPO, "orchestration", "runs", "sundial", "T-SUN-047")
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A2-05-field-notebook-1.png")
A = os.path.join(RUN, "pinch-a.png")
B = os.path.join(RUN, "pinch-b.png")
AGY = os.environ.get("AGY", os.path.join(os.environ.get("LOCALAPPDATA", ""), "agy", "bin", "agy.exe"))

ORDERS = (
    ("ab", "candidate A is %s and candidate B is %s" % (A, B)),
    # Letters really swap here: in "ba" candidate A is the halo v2 (variant B) and candidate B is look A.
    ("ba", "candidate A is %s and candidate B is %s" % (B, A)),
)


QUESTIONS = (
    ("closer", "Which candidate is closer to the reference watercolour field notebook? Reply with A or B on the first line, then one sentence."),
    ("halo", "Look only at the gold pinch halo around the flowering plant. In which candidate does it read as one smooth closed glowing line drawn around the plant, like the reference, rather than as a broken, jagged or scribbled outline? Reply with A or B on the first line, then one sentence."),
)


def ask(order, spec, draw, key, question):
    prompt = "Reference notebook: %s. %s. %s" % (REF, spec, question)
    out = os.path.join(RUN, "judge-%s-%s-%d.json" % (key, order, draw))
    completed = subprocess.run(
        [AGY, "-p", prompt, "--model", "gemini-3.8-flash-high", "--output-format", "json"],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    payload = {
        "spec": spec,
        "question": key,
        "order": order,
        "draw": draw,
        "exit": completed.returncode,
        "stdout": completed.stdout,
        "stderr": completed.stderr[-2000:],
    }
    with open(out, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(payload, handle, indent=2)
        handle.write("\n")
    print("wrote", out, "exit", completed.returncode)
    if completed.returncode != 0:
        print(completed.stderr[-500:])
    return completed.returncode


def tally():
    """Reads the judge-*.json files. In order "ab" letter A is look A, in "ba" letter A is the halo v2."""
    import re
    counts = {}
    for name in sorted(os.listdir(RUN)):
        if not (name.startswith("judge-") and name.endswith(".json")) or name == "judge-tally.json":
            continue
        with open(os.path.join(RUN, name), encoding="utf-8") as handle:
            d = json.load(handle)
        text = d["stdout"]
        try:
            j = json.loads(text)
            text = j.get("response") or j.get("result") or j.get("text") or text
        except ValueError:
            pass
        m = re.match(r"\s*([AB])(?![A-Za-z])", str(text)) or re.search(r"(?<![A-Za-z])([AB])(?![A-Za-z])", str(text))
        pick = m.group(1) if m else "?"
        v2 = (pick == "B") if d["order"] == "ab" else (pick == "A")
        key = "ungraded" if pick == "?" else ("halo2" if v2 else "lookA")
        counts.setdefault(d["question"], {"halo2": 0, "lookA": 0, "ungraded": 0})[key] += 1
    out = os.path.join(RUN, "judge-tally.json")
    with open(out, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(counts, handle, indent=2)
        handle.write("\n")
    print(json.dumps(counts))


RUBRIC = ("Score 1-5 how closely the right image matches the left reference in hand-drawn quality, colour, line work and mood; "
          "list the 3 biggest differences. Image: %s")


def rubric():
    """The T-SUN-031 A3 rubric on the pinch side-by-sides (reference left, frame right), three draws each for A and B."""
    for tag in ("a", "b"):
        sbs = os.path.join(RUN, "pinch-%s.sbs.png" % tag)
        for draw in (1, 2, 3):
            out = os.path.join(RUN, "rubric-%s-%d.json" % (tag, draw))
            completed = subprocess.run(
                [AGY, "-p", RUBRIC % sbs, "--model", "gemini-3.8-flash-high", "--output-format", "json"],
                cwd=REPO, capture_output=True, text=True)
            with open(out, "w", encoding="utf-8", newline="\n") as handle:
                json.dump({"look": tag, "draw": draw, "exit": completed.returncode, "stdout": completed.stdout,
                           "stderr": completed.stderr[-2000:]}, handle, indent=2)
                handle.write("\n")
            print("wrote", out, "exit", completed.returncode)


def main():
    if len(sys.argv) > 1 and sys.argv[1] == "--tally":
        tally()
        return
    if len(sys.argv) > 1 and sys.argv[1] == "--rubric":
        rubric()
        return
    os.makedirs(RUN, exist_ok=True)
    failed = 0
    for key, question in QUESTIONS:
        for order, spec in ORDERS:
            for draw in (1, 2, 3):
                failed += ask(order, spec, draw, key, question) != 0
    if failed:
        raise SystemExit("%s judge calls failed" % failed)


if __name__ == "__main__":
    main()
