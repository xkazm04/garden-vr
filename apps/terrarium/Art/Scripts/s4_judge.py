"""Spike S4 (T-TER-043) pairwise and rubric calls through agy. The F2 judge (tools/fidelity) scores the JarG1 fronds;
this script asks the frond question and the fiddlehead question on the same JarG1 crop, and runs the standard rubric.

    python apps/terrarium/Art/Scripts/s4_judge.py pair fronds       # A vs B, reference left, both orders, 3 draws each
    python apps/terrarium/Art/Scripts/s4_judge.py pair fiddlehead
    python apps/terrarium/Art/Scripts/s4_judge.py rubric            # the standard rubric on the B side-by-side, 3 draws

Same wording and model as the S1 and S3 spikes: gemini-3.8-flash-high, reference on the left, no A/B labels.
"""
import json
import os
import subprocess
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
RUN = os.path.join(REPO, "orchestration", "runs", "terrarium", "T-TER-043")
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A1-03-night-moss-1.png")
AGY = os.environ.get("AGY") or os.path.join(os.environ.get("LOCALAPPDATA", ""), "agy", "bin", "agy.exe")
MODEL = "gemini-3.8-flash-high"
SIZE = 640
BOX = (700, 420, 1130, 700)

TAIL = "\n\nReply with exactly one of these two lines and nothing else:\nCloser: middle\nCloser: right\n\nImage: %s"
HEAD = "The left image is the reference photo. The middle image and the right image are two renders of the same jar. "
PAIR_PROMPTS = {
    "fronds": HEAD + (
        "Which render is closer to the reference on the FERN FRONDS only? Judge the frond colour, how the leaflets "
        "look lit from behind, and the texture of the blade. Ignore the fiddlehead, the moss, the glass, the cork, "
        "and the room.") + TAIL,
    "fiddlehead": HEAD + (
        "Which render is closer to the reference on the FIDDLEHEAD (the curled green shoot) only? Judge the thickness "
        "of the stem, the tightness of the spiral, the fuzzy outline, and how it glows from the core. Ignore the fronds, "
        "the moss, the glass, the cork, and the room.") + TAIL,
}

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
    ims = [Image.open(p).convert("RGB").resize((SIZE, int(SIZE * 280 / 430)), Image.LANCZOS) for p in (left, mid, right)]
    s = Image.new("RGB", (SIZE * 3, ims[0].height))
    for i, im in enumerate(ims):
        s.paste(im, (i * SIZE, 0))
    s.save(out)


def crop_to(path, out):
    Image.open(path).convert("RGB").crop(BOX).save(out)
    return out


def pair(topic):
    ref = crop_to(REF, os.path.join(RUN, "ref-crop-%s.png" % topic))
    a = crop_to(os.path.join(RUN, "a-jar-g1.png"), os.path.join(RUN, "a-crop-%s.png" % topic))
    b = crop_to(os.path.join(RUN, "b-jar-g1.png"), os.path.join(RUN, "b-crop-%s.png" % topic))
    # Neutral file names. AB: middle is A. BA: middle is B.
    sheet(ref, a, b, os.path.join(RUN, "pair-%s-ab.png" % topic))
    sheet(ref, b, a, os.path.join(RUN, "pair-%s-ba.png" % topic))
    rows = []
    for order, middle_is in (("ab", "A"), ("ba", "B")):
        for draw in (1, 2, 3):
            image = os.path.join(RUN, "pair-%s-%s.png" % (topic, order))
            text, status = agy(PAIR_PROMPTS[topic] % image, "pair-%s-%s-%d" % (topic, order, draw))
            says = "middle" if "middle" in text.lower() else ("right" if "right" in text.lower() else "?")
            other = "B" if middle_is == "A" else "A"
            winner = middle_is if says == "middle" else (other if says == "right" else "ungraded")
            rows.append({"order": order, "draw": draw, "reply": text, "status": status, "closer": winner})
            print(order, draw, text.replace("\n", " "), "->", winner, flush=True)
    wins = {k: sum(1 for r in rows if r["closer"] == k) for k in ("A", "B", "ungraded")}
    with open(os.path.join(RUN, "pair-%s-summary.json" % topic), "w") as f:
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
    if sys.argv[1] == "pair":
        pair(sys.argv[2])
    else:
        rubric()
