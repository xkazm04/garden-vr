"""T-TER-047 judge helpers. The JarG1 pairwise runs are the F2 judge (tools/fidelity/fid.py judge --pairwise, see the report).
This script tallies those calls, runs the standard rubric (s5_judge wording) on the JarG1 side-by-sides, and runs the seated-view
pair through agy (the F2 judge only takes the JarG1 frame; the seated view has no reference of its own, so the reference
photo is the look target and the judge is told the camera differs).

    python apps/terrarium/Art/Scripts/sprint_judge.py tally pair-g1-overall.json
    python apps/terrarium/Art/Scripts/sprint_judge.py rubric a sprint [...]
    python apps/terrarium/Art/Scripts/sprint_judge.py seated          # A vs sprint, both orders, 3 draws each
    python apps/terrarium/Art/Scripts/sprint_judge.py seatedrubric    # standard rubric on reference | seated frame, 3 draws per look
"""
import json
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import s5_judge  # noqa: E402
import s6_judge  # noqa: E402

RUN = os.path.join(s5_judge.REPO, "orchestration", "runs", "terrarium", "T-TER-047")
s5_judge.RUN = RUN
s6_judge.RUN = RUN
REF = os.path.join(s5_judge.REPO, "shared", "assets", "art-reference", "A1-03-night-moss-1.png")

SEATED_PROMPT = (
    "The left image is the reference photo of a glowing terrarium jar. The middle image and the right image are two renders of "
    "the same jar from a different, closer camera at a desk. Which render is closer to the reference's look: the glass, the moss, "
    "the inner glow, the cork and the mist? Ignore the camera, the desk and the room.\n\n"
    "Reply with exactly one of these two lines and nothing else:\nCloser: middle\nCloser: right\n\nImage: %s"
)


def seated_sheet(left, mid, right, out):
    ims = [Image.open(left).convert("RGB").crop((560, 0, 1360, 1024)).resize((600, 768), Image.LANCZOS)]
    for p in (mid, right):
        ims.append(Image.open(p).convert("RGB").crop((560, 150, 1360, 900)).resize((800, 750), Image.LANCZOS))
    ims[0] = Image.open(left).convert("RGB").crop((640, 100, 1260, 900)).resize((600, 774), Image.LANCZOS)
    w = sum(i.width for i in ims)
    s = Image.new("RGB", (w, 780), (0, 0, 0))
    x = 0
    for im in ims:
        s.paste(im, (x, 0))
        x += im.width
    s.save(out)


def seated():
    a = os.path.join(RUN, "a-seated.png")
    b = os.path.join(RUN, "sprint-seated.png")
    seated_sheet(REF, a, b, os.path.join(RUN, "pair-seated-ab.png"))
    seated_sheet(REF, b, a, os.path.join(RUN, "pair-seated-ba.png"))
    rows = []
    for order, image, middle_is in (("ab", "pair-seated-ab.png", "A"), ("ba", "pair-seated-ba.png", "sprint")):
        for draw in (1, 2, 3):
            text, status = s5_judge.agy(SEATED_PROMPT % os.path.join(RUN, image), "pair-seated-%s-%d" % (order, draw))
            says = "middle" if "middle" in text.lower() else ("right" if "right" in text.lower() else "?")
            other = "sprint" if middle_is == "A" else "A"
            winner = middle_is if says == "middle" else (other if says == "right" else "ungraded")
            rows.append({"order": order, "draw": draw, "reply": text, "status": status, "closer": winner})
            print(order, draw, text.replace("\n", " "), "->", winner, flush=True)
    wins = {k: sum(1 for r in rows if r["closer"] == k) for k in ("A", "sprint", "ungraded")}
    json.dump({"rows": rows, "wins": wins}, open(os.path.join(RUN, "pair-seated-summary.json"), "w"), indent=1)
    print(wins)


def seated_rubric():
    for look in ("a", "sprint"):
        left = Image.open(REF).convert("RGB").resize((912, 512), Image.LANCZOS)
        right = Image.open(os.path.join(RUN, look + "-seated.png")).convert("RGB").resize((912, 512), Image.LANCZOS)
        sheet = Image.new("RGB", (1824, 512))
        sheet.paste(left, (0, 0))
        sheet.paste(right, (912, 0))
        path = os.path.join(RUN, look + "-seated.sbs.png")
        sheet.save(path)
        rows = []
        for draw in (1, 2, 3):
            text, status = s5_judge.agy(s5_judge.RUBRIC % path, "score-%s-seated-%d" % (look, draw))
            rows.append({"draw": draw, "reply": text, "status": status})
            print(look, draw, text.replace(chr(10), " | "), flush=True)
        json.dump(rows, open(os.path.join(RUN, "score-%s-seated-summary.json" % look), "w"), indent=1)


if __name__ == "__main__":
    mode = sys.argv[1]
    if mode == "tally":
        s6_judge.tally(sys.argv[2])
    elif mode == "rubric":
        for look in sys.argv[2:]:
            s5_judge.rubric(look)
    elif mode == "seated":
        seated()
    elif mode == "seatedrubric":
        seated_rubric()
    else:
        raise SystemExit("tally <run.json> | rubric <look>... | seated | seatedrubric")
