# Capture the S1 A/B set. One Unity at a time. Look A is the default material.
# Look B is capture state variant=watercolour.
#
#   python apps/sundial/Art/Scripts/watercolour_s1_shots.py
#   python apps/sundial/Art/Scripts/watercolour_s1_shots.py dial-g1-b-nohalo
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
UNITY = os.environ.get(
    "UNITY",
    r"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe",
)
RUN = os.path.join(REPO, "orchestration", "runs", "sundial", "T-SUN-041")
PROJECT = os.path.join(REPO, "apps", "sundial")
SCENE = "Assets/Scenes/Main.unity"
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A2-05-field-notebook-1.png")
MATTE = os.path.join(REPO, "shared", "assets", "room-plates", "dial-hand-matte.png")

NOHALO = "halo=0,waiting=0,gnomonDeg=105,time=1"
PINCH = "halo=1,haloTarget=midday,gnomonDeg=105,time=1"

# name, framing, state, overlay
# Pinch and seated names do not start with "dial", so fid metrics leaves them
# out. Only the two DialG1 nohalo frames are on the F1 ladder.
SHOTS = (
    ("dial-g1-a-nohalo", "DialG1", NOHALO, False),
    ("dial-g1-b-nohalo", "DialG1", NOHALO + ",variant=watercolour", False),
    ("pinch-a", "DialG1", PINCH, True),
    ("pinch-b", "DialG1", PINCH + ",variant=watercolour", True),
    ("seated-a", "DialSeated", NOHALO, False),
    ("seated-b", "DialSeated", NOHALO + ",variant=watercolour", False),
)


def shot(name, framing, state, overlay):
    out = os.path.join(RUN, name + ".png")
    log = os.path.join(RUN, name + ".log")
    cmd = [
        UNITY,
        "-batchmode",
        "-quit",
        "-projectPath",
        PROJECT,
        "-executeMethod",
        "GardenVR.Capture.Editor.CaptureCli.Shot",
        "-scene",
        SCENE,
        "-framing",
        framing,
        "-target",
        "dial",
        "-state",
        state,
        "-out",
        out,
        "-logFile",
        log,
    ]
    if overlay:
        cmd.extend(["-overlay", MATTE, "-reference", REF])
    print("shot", name)
    completed = subprocess.run(cmd, cwd=REPO)
    if completed.returncode != 0:
        raise SystemExit("shot %s exit %s (see %s)" % (name, completed.returncode, log))
    if not os.path.isfile(out):
        raise SystemExit("shot %s wrote no png" % name)
    print("ok", out)


def measure(name, state):
    out = os.path.join(RUN, name + ".json")
    log = os.path.join(RUN, name + ".log")
    cmd = [
        UNITY,
        "-batchmode",
        "-quit",
        "-projectPath",
        PROJECT,
        "-executeMethod",
        "GardenVR.Capture.Editor.CaptureCli.Measure",
        "-scene",
        SCENE,
        "-framing",
        "DialG1",
        "-target",
        "dial",
        "-state",
        state,
        "-out",
        out,
        "-logFile",
        log,
    ]
    print("measure", name)
    completed = subprocess.run(cmd, cwd=REPO)
    if completed.returncode != 0:
        raise SystemExit("measure %s exit %s (see %s)" % (name, completed.returncode, log))
    print("ok", out)


def main(argv):
    os.makedirs(RUN, exist_ok=True)
    if argv and argv[0] == "--measure":
        measure("measure-a", NOHALO)
        measure("measure-b", NOHALO + ",variant=watercolour")
        return
    wanted = set(argv)
    for name, framing, state, overlay in SHOTS:
        if wanted and name not in wanted:
            continue
        shot(name, framing, state, overlay)


if __name__ == "__main__":
    main(sys.argv[1:])
