# S2 light question, advisory. Six agy draws on the DialG1 pinch frames, three per order, two questions each.
# The F2 pairwise judge (tools/fidelity) is run separately.
#   python apps/sundial/Art/Scripts/roomlight_s2_judge.py
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
RUN = os.path.join(REPO, "orchestration", "runs", "sundial", "T-SUN-044")
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A2-05-field-notebook-1.png")
A = os.path.join(RUN, "pinch-a.png")
B = os.path.join(RUN, "pinch-b.png")
AGY = os.environ.get("AGY", os.path.join(os.environ.get("LOCALAPPDATA", ""), "agy", "bin", "agy.exe"))

ORDERS = (
    ("ab", "candidate A is %s and candidate B is %s" % (A, B)),
    ("ba", "candidate B is %s and candidate A is %s" % (B, A)),
)


QUESTIONS = (
    ("closer", "Which candidate is closer to the reference watercolour field notebook? Reply with A or B on the first line, then one sentence."),
    ("light", "In which candidate does the drawn object receive the room's light (the window light on the table, the shadows it casts)? Reply with A or B on the first line, then one sentence."),
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


def main():
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
