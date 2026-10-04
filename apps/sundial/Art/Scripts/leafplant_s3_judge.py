# S3 judge draws, advisory. Direct agy calls (Gemini), three draws per question and per order.
# The host re-scores with tools/fidelity (F2). Calls run four at a time; each takes about 3 minutes.
#
#   python apps/sundial/Art/Scripts/leafplant_s3_judge.py            # all 18 draws
#   python apps/sundial/Art/Scripts/leafplant_s3_judge.py only closer  # one question (finished draws are skipped)
#   python apps/sundial/Art/Scripts/leafplant_s3_judge.py summary    # parse the saved answers
import concurrent.futures
import json
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
RUN = os.path.join(REPO, "orchestration", "runs", "sundial", "T-SUN-045")
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A2-05-field-notebook-1.png")
AGY = os.environ.get("AGY", os.path.join(os.environ.get("LOCALAPPDATA", ""), "agy", "bin", "agy.exe"))
MODEL = "gemini-3.8-flash-high"
TIMEOUT = 1500

PINCH = {"a": os.path.join(RUN, "pinch-a.png"), "b": os.path.join(RUN, "pinch-b.png")}
MONTAGE = {"a": os.path.join(RUN, "orbit-close-a.montage.png"), "b": os.path.join(RUN, "orbit-close-b.montage.png")}

RUBRIC = (
    "Rubric levels: 1 flat-shaded 3D primitives; 2 drawn materials but plants or soil read as cut-outs or CG; "
    "3 every element drawn and painted, one region visibly plainer than the reference; "
    "4 reads as a hand-drawn film frame placed into the photographed room at a 2 second glance, in every region; "
    "5 indistinguishable from the reference's look."
)


def jobs():
    out = []
    for order, first, second in (("ab", "a", "b"), ("ba", "b", "a")):
        spec = "Candidate A is %s and candidate B is %s." % (PINCH[first], PINCH[second])
        q = ("Which candidate is closer to the reference watercolour field notebook, looking at the plants "
             "(the pink flowering plant in particular)? Reply with A or B on the first line, then one sentence.")
        for draw in (1, 2, 3):
            out.append(("closer", order, draw, "Reference notebook: %s. %s %s" % (REF, spec, q)))
    for variant in ("a", "b"):
        q = ("This image shows three views of the same pink flowering plant from slightly different camera angles "
             "(left, middle, right). Does the plant look like a flat cut-out or billboard card, or like a plant with "
             "volume? Reply with CUTOUT or VOLUME on the first line, then one sentence that says whether you see "
             "any cut-out, billboard or paper-card look.")
        for draw in (1, 2, 3):
            out.append(("cutout", variant, draw, "Image: %s. %s" % (MONTAGE[variant], q)))
    for variant in ("a", "b"):
        q = ("This is a crop of one pink flowering plant from a stylised, hand-drawn-looking scene. Does the plant look "
             "like a flat cut-out or billboard card, or like a plant with volume and drawn leaves? Reply with CUTOUT "
             "or VOLUME on the first line, then one sentence that says whether you see any cut-out, billboard or "
             "paper-card look.")
        for draw in (1, 2, 3):
            out.append(("cutstill", variant, draw, "Image: %s. %s" % (os.path.join(RUN, "g1-plant-crop-%s.png" % variant), q)))
    for variant in ("a", "b"):
        q = ("Score the PLANTS region of the candidate frame only (the pink flowering plant, the green plant and the "
             "lavender), against the reference frame. %s Reply with 'LEVEL: n' on the first line (n from 1 to 5), "
             "then two sentences." % RUBRIC)
        for draw in (1, 2, 3):
            out.append(("rubric", variant, draw, "Reference notebook: %s. Candidate frame: %s. %s" % (REF, PINCH[variant], q)))
    return out


def path_for(key, who, draw):
    return os.path.join(RUN, "judge-%s-%s-%d.json" % (key, who, draw))


def ask(job):
    key, who, draw, prompt = job
    out = path_for(key, who, draw)
    if os.path.isfile(out):
        try:
            if json.load(open(out, encoding="utf-8")).get("exit") == 0:
                return key, who, draw, 0
        except Exception:
            pass
    try:
        completed = subprocess.run(
            [AGY, "-p", prompt, "--model", MODEL, "--output-format", "json"],
            cwd=REPO, capture_output=True, text=True, timeout=TIMEOUT)
        code, stdout, stderr = completed.returncode, completed.stdout, completed.stderr[-2000:]
    except subprocess.TimeoutExpired:
        code, stdout, stderr = 124, "", "timeout after %s s" % TIMEOUT
    payload = {"question": key, "who": who, "draw": draw, "exit": code, "stdout": stdout, "stderr": stderr}
    with open(out, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(payload, handle, indent=2)
        handle.write("\n")
    print("wrote", out, "exit", code, flush=True)
    return key, who, draw, code


def answer_of(path):
    try:
        data = json.load(open(path, encoding="utf-8"))
        return json.loads(data["stdout"]).get("response", "").strip()
    except Exception:
        return None


def summary():
    rows = {}
    for key, who, draw, _ in jobs():
        text = answer_of(path_for(key, who, draw))
        first = (text or "").splitlines()[0].strip() if text else None
        rows.setdefault((key, who), []).append(first)
    tally = {}
    for (key, who), firsts in sorted(rows.items()):
        tally["%s %s" % (key, who)] = firsts
    # closer: map the letter back to the variant
    closer = {"a": 0, "b": 0, "none": 0}
    for (key, who), firsts in rows.items():
        if key != "closer":
            continue
        for f in firsts:
            letter = (f or "").strip().upper()[:1]
            if letter not in ("A", "B"):
                closer["none"] += 1
                continue
            variant = letter.lower() if who == "ab" else ("b" if letter == "A" else "a")
            closer[variant] += 1
    print(json.dumps({"tally": tally, "closer_votes_by_variant": closer}, indent=1))
    with open(os.path.join(RUN, "judge-summary.json"), "w", encoding="utf-8", newline="\n") as handle:
        json.dump({"tally": tally, "closer_votes_by_variant": closer}, handle, indent=1)


def main(argv):
    os.makedirs(RUN, exist_ok=True)
    if argv and argv[0] == "summary":
        summary()
        return
    failed = 0
    chosen = jobs()
    if argv and argv[0] == "only":
        chosen = [j for j in chosen if j[0] in argv[1:]]
    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
        for key, who, draw, code in pool.map(ask, chosen):
            failed += code != 0
    summary()
    if failed:
        raise SystemExit("%s judge calls failed" % failed)


if __name__ == "__main__":
    main(sys.argv[1:])
