# Capture the S5 A/B set. One Unity at a time. Look A is the default card halo.
# Look B is capture state variant=halo2.
#
#   python apps/sundial/Art/Scripts/halo_s5_shots.py                 # every frame
#   python apps/sundial/Art/Scripts/halo_s5_shots.py pinch-b         # one frame
#   python apps/sundial/Art/Scripts/halo_s5_shots.py --measure       # budgets
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
UNITY = os.environ.get(
    "UNITY",
    r"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe",
)
RUN = os.path.join(REPO, "orchestration", "runs", "sundial", "T-SUN-047")
PROJECT = os.path.join(REPO, "apps", "sundial")
SCENE = "Assets/Scenes/Main.unity"
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A2-05-field-notebook-1.png")
MATTE = os.path.join(REPO, "shared", "assets", "room-plates", "dial-hand-matte.png")

NOHALO = "halo=0,waiting=0,gnomonDeg=105,time=1"
PINCH = "halo=1,haloTarget=midday,gnomonDeg=105,time=1"
B = ",variant=halo2"

# name, framing, state, overlay, extra args
# Names that start with "dial" are the F1 ladder frames (halo off, so A and B are the same frame; one is enough).
# g1-*-halo are the same state as pinch-* without the hand matte, so the glow gate and the contour read clean pixels.
SHOTS = (
    ("dial-g1-a-nohalo", "DialG1", NOHALO, False, []),
    ("dial-g1-noplants", "DialG1", NOHALO + ",plants=0", False, []),
    ("morning-noplants", "DialG1", "halo=0,haloTarget=morning,plants=0,gnomonDeg=105,time=1", False, []),
    ("morning-nohalo", "DialG1", "halo=0,haloTarget=morning,gnomonDeg=105,time=1", False, []),
    # One plant alone (no halo), so the gap between that plant and its ring is read against its own pixels.
    ("only-midday", "DialG1", "halo=0,plants=midday,gnomonDeg=105,time=1", False, []),
    ("only-morning", "DialG1", "halo=0,haloTarget=morning,plants=morning,gnomonDeg=105,time=1", False, []),
    ("only-dusk", "DialG1", "halo=0,haloTarget=winddown,plants=winddown,gnomonDeg=105,time=1", False, []),
    ("g1-a-halo", "DialG1", PINCH, False, []),
    ("g1-b-halo", "DialG1", PINCH + B, False, []),
    # Ring alone (spill=0): the stroke read without the soft light on the soil.
    ("g1-b0-halo", "DialG1", PINCH + B + ",spill=0", False, []),
    ("morning-b0", "DialG1", "halo=1,haloTarget=morning,gnomonDeg=105,time=1" + B + ",spill=0", False, []),
    ("dusk-b0", "DialG1", "halo=1,haloTarget=winddown,gnomonDeg=105,time=1" + B + ",spill=0", False, []),
    # The halo alone on black (isolate=halo, no plate): the ring read without the scene, for the contour metrics.
    ("iso-midday-a", "DialG1Black", "halo=1,haloTarget=midday,gnomonDeg=105,time=1,isolate=halo", False, []),
    ("iso-midday-b", "DialG1Black", PINCH + B + ",isolate=halo", False, []),
    ("iso-morning-a", "DialG1Black", "halo=1,haloTarget=morning,gnomonDeg=105,time=1,isolate=halo", False, []),
    ("iso-morning-b", "DialG1Black", "halo=1,haloTarget=morning,gnomonDeg=105,time=1" + B + ",isolate=halo", False, []),
    ("iso-dusk-a", "DialG1Black", "halo=1,haloTarget=winddown,gnomonDeg=105,time=1,isolate=halo", False, []),
    ("iso-dusk-b", "DialG1Black", "halo=1,haloTarget=winddown,gnomonDeg=105,time=1" + B + ",isolate=halo", False, []),
    ("pinch-a", "DialG1", PINCH, True, []),
    ("pinch-b", "DialG1", PINCH + B, True, []),
    ("seated-a", "DialSeated", PINCH, False, []),
    ("seated-b", "DialSeated", PINCH + B, False, []),
    ("orbit-a", "DialG1", PINCH, False, ["-orbits=-15,15"]),
    ("orbit-b", "DialG1", PINCH + B, False, ["-orbits=-15,15"]),
    # Other plants and stages, to see the hull on a seed, a bloom bud and the morning and dusk hero plants.
    ("morning-a", "DialG1", "halo=1,haloTarget=morning,gnomonDeg=105,time=1", False, []),
    ("morning-b", "DialG1", "halo=1,haloTarget=morning,gnomonDeg=105,time=1" + B, False, []),
    ("dusk-a", "DialG1", "halo=1,haloTarget=winddown,gnomonDeg=105,time=1", False, []),
    ("dusk-b", "DialG1", "halo=1,haloTarget=winddown,gnomonDeg=105,time=1" + B, False, []),
    ("sprout-a", "DialG1", "halo=1,haloTarget=midday,stage.midday=1,bloom.midday=0,gnomonDeg=105,time=1", False, []),
    ("sprout-b", "DialG1", "halo=1,haloTarget=midday,stage.midday=1,bloom.midday=0,gnomonDeg=105,time=1" + B, False, []),
)


def unity(name, method, framing, state, extra, overlay=False, out_ext=".png"):
    out = os.path.join(RUN, name + out_ext)
    log = os.path.join(RUN, name + ".log")
    cmd = [
        UNITY, "-batchmode", "-quit", "-projectPath", PROJECT,
        "-executeMethod", method,
        "-scene", SCENE, "-framing", framing, "-target", "dial", "-state", state,
        "-out", out, "-logFile", log,
    ]
    cmd.extend(extra)
    if overlay:
        cmd.extend(["-overlay", MATTE, "-reference", REF])
    print(method.split(".")[-1], name)
    completed = subprocess.run(cmd, cwd=REPO)
    if completed.returncode != 0:
        raise SystemExit("%s exit %s (see %s)" % (name, completed.returncode, log))
    if not os.path.isfile(out):
        raise SystemExit("%s wrote no output" % name)
    print("ok", out)


def main(argv):
    os.makedirs(RUN, exist_ok=True)
    if argv and argv[0] == "--measure":
        unity("measure-a", "GardenVR.Capture.Editor.CaptureCli.Measure", "DialG1", PINCH, [], out_ext=".json")
        unity("measure-b", "GardenVR.Capture.Editor.CaptureCli.Measure", "DialG1", PINCH + B, [], out_ext=".json")
        return
    wanted = set(argv)
    for name, framing, state, overlay, extra in SHOTS:
        if wanted and name not in wanted:
            continue
        unity(name, "GardenVR.Capture.Editor.CaptureCli.Shot", framing, state, extra, overlay)


if __name__ == "__main__":
    main(sys.argv[1:])
