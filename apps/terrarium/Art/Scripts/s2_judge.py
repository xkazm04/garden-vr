"""Spike S2 (T-TER-044) standard rubric through agy, on the reference-beside-render side-by-side of a look.
The pairwise ranking of A, S1, B0, B1 and B2 is the F2 judge (tools/fidelity/fid.py judge --pairwise, criterion glass);
this script runs only the rubric (3 draws per look), with the same wording and model as S1, S3 and S4.

    python apps/terrarium/Art/Scripts/s2_judge.py rubric b1 [b2 ...]
"""
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
RUN = os.path.join(REPO, "orchestration", "runs", "terrarium", "T-TER-044")
AGY = os.environ.get("AGY") or os.path.join(os.environ.get("LOCALAPPDATA", ""), "agy", "bin", "agy.exe")
MODEL = "gemini-3.8-flash-high"

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


def rubric(look):
    rows = []
    for draw in (1, 2, 3):
        text, status = agy(RUBRIC % os.path.join(RUN, "%s-jar-g1.sbs.png" % look), "score-%s-%d" % (look, draw))
        rows.append({"draw": draw, "reply": text, "status": status})
        print(look, draw, text.replace(chr(10), " | "), flush=True)
    with open(os.path.join(RUN, "score-%s-summary.json" % look), "w") as f:
        json.dump(rows, f, indent=1)


if __name__ == "__main__":
    for look in sys.argv[2:]:
        rubric(look)
