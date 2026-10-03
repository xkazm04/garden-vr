# F2 pairwise judge is not in tools/fidelity. Six agy draws, three per order.
#   python apps/sundial/Art/Scripts/watercolour_s1_judge.py
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
RUN = os.path.join(REPO, "orchestration", "runs", "sundial", "T-SUN-041")
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A2-05-field-notebook-1.png")
A = os.path.join(RUN, "dial-g1-a-nohalo.png")
B = os.path.join(RUN, "dial-g1-b-nohalo.png")
AGY = os.environ.get("AGY", os.path.join(os.environ.get("LOCALAPPDATA", ""), "agy", "bin", "agy.exe"))

ORDERS = (
    ("ab", "candidate A is %s and candidate B is %s" % (A, B)),
    ("ba", "candidate B is %s and candidate A is %s" % (B, A)),
)


def ask(order, spec, draw):
    prompt = (
        "Reference notebook: %s. %s. "
        "Which candidate is closer to the reference watercolour field notebook? "
        "Reply with A or B on the first line, then one sentence."
        % (REF, spec)
    )
    out = os.path.join(RUN, "judge-%s-%d.json" % (order, draw))
    completed = subprocess.run(
        [AGY, "-p", prompt, "--model", "gemini-3.8-flash-high", "--output-format", "json"],
        cwd=REPO,
        capture_output=True,
        text=True,
    )
    payload = {
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
    for order, spec in ORDERS:
        for draw in (1, 2, 3):
            failed += ask(order, spec, draw) != 0
    if failed:
        raise SystemExit("%s judge calls failed" % failed)


if __name__ == "__main__":
    main()
