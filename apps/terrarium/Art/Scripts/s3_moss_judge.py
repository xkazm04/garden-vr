"""Spike S3 (T-TER-042) pairwise and rubric calls through agy, for the frames the F2 judge cannot take
(MossClose is 1024 square). The F2 judge (tools/fidelity) scores the JarG1 pair.

    python apps/terrarium/Art/Scripts/s3_moss_judge.py pair     # A vs B on the moss, reference left, both orders, 3 draws each
    python apps/terrarium/Art/Scripts/s3_moss_judge.py rubric   # the standard rubric on the B side-by-side, 3 draws

Same wording and model as the S1 spike (T-TER-040): gemini-3.8-flash-high, reference on the left, no A/B labels.
"""
import json
import os
import subprocess
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
RUN = os.path.join(REPO, "orchestration", "runs", "terrarium", "T-TER-042")
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A1-03-night-moss-1.png")
AGY = os.environ.get("AGY") or os.path.join(os.environ.get("LOCALAPPDATA", ""), "agy", "bin", "agy.exe")
MODEL = "gemini-3.8-flash-high"
SIZE = 640

PAIR_PROMPT = (
    "The left image is the reference photo. The middle image and the right image are two renders of the same jar. "
    "Which render is closer to the reference on the MOSS and the SOIL under it only? Judge the moss mound, how the "
    "soil meets the glass, and whether the moss looks like it sits in the jar. Ignore the plants, the glass, the "
    "hands, the cork, and the room.\n\nReply with exactly one of these two lines and nothing else:\n"
    "Closer: middle\nCloser: right\n\nImage: %s"
)

RUBRIC = """Score the right-hand image against the left-hand image using only this rubric.

1. procedural primitives, sawtooth leaves (round-2 "Night Lantern" stills)
2. right silhouette and glow, CG materials (round-3 moss dome, regular frond lace)
3. authored materials everywhere, one region visibly weaker than the reference
4. every region (glass, fronds, moss, cork, mist, ring) reads as the reference's material at a 2 s glance
5. indistinguishable from the reference's look at a 2 s glance

Capped disqualifiers (frame scores at most 2): magenta or placeholder material, transparent sorting pop or z-fighting in the glass, pixels taken from the reference frame, neon / harsh bloom, any text other than the etched hints, a pure-black or pure-white region larger than the dew bead.

Regions excluded from parity: the room seen through the glass is not refracted; hands are not lit by the jar.

Reply in this form:
Score: N
Weakest: region
Disqualifier: no
or
Disqualifier: yes, <which>

Image: %s"""


def agy(prompt, tag):
    log = os.path.join(RUN, tag + ".agy.log")
    cmd = [AGY, "-p", prompt, "--model", MODEL, "--output-format", "json", "--print-timeout", "240s",
           "--disable-slash-commands", "--dangerously-skip-permissions", "--log-file", log]
    done = subprocess.run(cmd, cwd=REPO, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=300)
    try:
        wrapper = json.loads(done.stdout)
    except json.JSONDecodeError:
        wrapper = {"status": "BADJSON", "response": done.stdout}
    with open(os.path.join(RUN, tag + ".json"), "w", encoding="utf-8") as f:
        json.dump(wrapper, f, indent=1)
    return (wrapper.get("response") or "").strip(), wrapper.get("status")


def sheet(left, mid, right, out):
    ims = [Image.open(p).convert("RGB").resize((SIZE, SIZE), Image.LANCZOS) for p in (left, mid, right)]
    s = Image.new("RGB", (SIZE * 3, SIZE))
    for i, im in enumerate(ims):
        s.paste(im, (i * SIZE, 0))
    s.save(out)


def ref_moss_crop():
    ref = Image.open(REF).convert("RGB").crop((690, 560, 1130, 900))
    path = os.path.join(RUN, "ref-moss-crop.png")
    ref.save(path)
    return path


def pair():
    ref = ref_moss_crop()
    a = os.path.join(RUN, "a-moss-close.png")
    b = os.path.join(RUN, "b-moss-close.png")
    # Neutral file names. AB: middle is A. BA: middle is B.
    sheet(ref, a, b, os.path.join(RUN, "pair-moss-ab.png"))
    sheet(ref, b, a, os.path.join(RUN, "pair-moss-ba.png"))
    rows = []
    for order, image, middle_is in (("ab", "pair-moss-ab.png", "A"), ("ba", "pair-moss-ba.png", "B")):
        for draw in (1, 2, 3):
            text, status = agy(PAIR_PROMPT % os.path.join(RUN, image), "pair-moss-%s-%d" % (order, draw))
            says = "middle" if "middle" in text.lower() else ("right" if "right" in text.lower() else "?")
            other = "B" if middle_is == "A" else "A"
            winner = middle_is if says == "middle" else (other if says == "right" else "ungraded")
            rows.append({"order": order, "draw": draw, "reply": text, "status": status, "closer": winner})
            print(order, draw, text.replace("\n", " "), "->", winner, flush=True)
    wins = {k: sum(1 for r in rows if r["closer"] == k) for k in ("A", "B", "ungraded")}
    with open(os.path.join(RUN, "pair-moss-summary.json"), "w") as f:
        json.dump({"rows": rows, "wins": wins}, f, indent=1)
    print(wins)


def rubric():
    rows = []
    for draw in (1, 2, 3):
        text, status = agy(RUBRIC % os.path.join(RUN, "b-jar-g1.sbs.png"), "score-b-%d" % draw)
        rows.append({"draw": draw, "reply": text, "status": status})
        print(draw, text.replace("\n", " | "), flush=True)
    with open(os.path.join(RUN, "score-b-summary.json"), "w") as f:
        json.dump(rows, f, indent=1)


if __name__ == "__main__":
    {"pair": pair, "rubric": rubric}[sys.argv[1]]()
