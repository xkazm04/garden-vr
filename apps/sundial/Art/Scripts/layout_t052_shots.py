# Capture the T-SUN-052 layout set. One Unity at a time. Look A is the default; B is capture state variant=layout(+soilmound+leafplant).
#
#   python apps/sundial/Art/Scripts/layout_t052_shots.py                 # every frame
#   python apps/sundial/Art/Scripts/layout_t052_shots.py pinch-b         # one frame
#   python apps/sundial/Art/Scripts/layout_t052_shots.py --measure       # budgets
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
UNITY = os.environ.get(
    "UNITY",
    r"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe",
)
RUN = os.path.join(REPO, "orchestration", "runs", "sundial", "T-SUN-052")
PROJECT = os.path.join(REPO, "apps", "sundial")
SCENE = "Assets/Scenes/Main.unity"
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A2-05-field-notebook-1.png")
MATTE = os.path.join(REPO, "shared", "assets", "room-plates", "dial-hand-matte.png")

NOHALO = "halo=0,waiting=0,gnomonDeg=105,time=1"
PINCH = "halo=1,haloTarget=midday,gnomonDeg=105,time=1"
# A is the baseline the host compared (look A). B is the full stack: layout on top of the soil mound, the drawn leaves and halo v2.
# The pinch-b of T-SUN-047 was halo2 alone; the A of this task is the same look A.
A = ""
SOIL = ",variant=soilmound"
STACK = ",variant=layout+soilmound+leafplant"
LAYOUT_ONLY = ",variant=layout"

S = ",variant=soilmound+leafplant"
SHOTS = (
    # Look A must still be the locked T-SUN-031 frame (sha256 7fecea65...), byte for byte.
    ("nohalo-a", "DialG1", NOHALO, False, []),
    # Dial silhouette on black (no plants), for the IoU and the axes.
    ("black-a", "DialG1Black", NOHALO + ",plants=0,isolate=dial", False, []),
    ("black-s", "DialG1Black", NOHALO + ",plants=0,isolate=dial" + S, False, []),
    ("black-b", "DialG1Black", NOHALO + ",plants=0,isolate=dial" + STACK, False, []),
    # Soil alone on black, for the soil area and the bed shape.
    ("soil-a", "DialG1Black", NOHALO + ",plants=0,isolate=soil", False, []),
    ("soil-s", "DialG1Black", NOHALO + ",plants=0,isolate=soil" + S, False, []),
    ("soil-b", "DialG1Black", NOHALO + ",plants=0,isolate=soil" + STACK, False, []),
    # The pinch halo alone on black, and each arc's plants alone on black, for the halo hug and spill.
    ("halo-a", "DialG1Black", PINCH + ",isolate=halo", False, []),
    ("halo-b", "DialG1Black", PINCH + ",isolate=halo" + STACK, False, []),
    ("plant-a-morning", "DialG1Black", NOHALO + ",plants=morning,isolate=dial", False, []),
    ("plant-a-midday", "DialG1Black", NOHALO + ",plants=midday,isolate=dial", False, []),
    ("plant-a-winddown", "DialG1Black", NOHALO + ",plants=winddown,isolate=dial", False, []),
    ("plant-b-morning", "DialG1Black", NOHALO + ",plants=morning,isolate=dial" + STACK, False, []),
    ("plant-b-midday", "DialG1Black", NOHALO + ",plants=midday,isolate=dial" + STACK, False, []),
    ("plant-b-winddown", "DialG1Black", NOHALO + ",plants=winddown,isolate=dial" + STACK, False, []),
    # The pinch frames with the hand matte (sbs with ref-1), and the same without the hand.
    ("pinch-a", "DialG1", PINCH + A, True, []),
    ("pinch-s", "DialG1", PINCH + S, True, []),
    ("pinch-b", "DialG1", PINCH + STACK, True, []),
    ("pinch-layout", "DialG1", PINCH + LAYOUT_ONLY, True, []),
    ("g1-a", "DialG1", PINCH + A, False, []),
    ("g1-s", "DialG1", PINCH + S, False, []),
    ("g1-b", "DialG1", PINCH + STACK, False, []),
    ("seated-a", "DialSeated", PINCH + A, False, []),
    ("seated-b", "DialSeated", PINCH + STACK, False, []),
    ("morning-a", "DialG1", "halo=1,haloTarget=morning,gnomonDeg=105,time=1" + A, False, []),
    ("morning-b", "DialG1", "halo=1,haloTarget=morning,gnomonDeg=105,time=1" + STACK, False, []),
    ("dusk-a", "DialG1", "halo=1,haloTarget=winddown,gnomonDeg=105,time=1" + A, False, []),
    ("dusk-b", "DialG1", "halo=1,haloTarget=winddown,gnomonDeg=105,time=1" + STACK, False, []),
    ("orbit-b", "DialG1", PINCH + STACK, False, ["-orbits=-15,15"]),
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
        unity("measure-a", "GardenVR.Capture.Editor.CaptureCli.Measure", "DialG1", PINCH + A, [], out_ext=".json")
        unity("measure-s", "GardenVR.Capture.Editor.CaptureCli.Measure", "DialG1", PINCH + S, [], out_ext=".json")
        unity("measure-b", "GardenVR.Capture.Editor.CaptureCli.Measure", "DialG1", PINCH + STACK, [], out_ext=".json")
        return
    wanted = set(argv)
    for name, framing, state, overlay, extra in SHOTS:
        if wanted and name not in wanted:
            continue
        unity(name, "GardenVR.Capture.Editor.CaptureCli.Shot", framing, state, extra, overlay)


if __name__ == "__main__":
    main(sys.argv[1:])
