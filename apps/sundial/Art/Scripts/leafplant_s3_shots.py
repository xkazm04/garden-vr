# Capture the S3 A/B set. One Unity at a time. Look A is the default (the camera-facing card).
# Look B is capture state variant=leafplant.
#
#   python apps/sundial/Art/Scripts/leafplant_s3_shots.py                 # every frame
#   python apps/sundial/Art/Scripts/leafplant_s3_shots.py pinch-b close-a  # named frames only
#   python apps/sundial/Art/Scripts/leafplant_s3_shots.py --measure       # budgets (CaptureCli.Measure)
#   python apps/sundial/Art/Scripts/leafplant_s3_shots.py --edit-tests    # EditMode tests
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
UNITY = os.environ.get(
    "UNITY",
    r"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe",
)
RUN = os.path.join(REPO, "orchestration", "runs", "sundial", "T-SUN-045")
PROJECT = os.path.join(REPO, "apps", "sundial")
SCENE = "Assets/Scenes/Main.unity"
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A2-05-field-notebook-1.png")
MATTE = os.path.join(REPO, "shared", "assets", "room-plates", "dial-hand-matte.png")

NOHALO = "halo=0,waiting=0,gnomonDeg=105,time=1"
PINCH = "halo=1,haloTarget=midday,gnomonDeg=105,time=1"
B = ",variant=leafplant"
ORBITS = "-15,-10,-5,0,5,10,15"
IPD = "0.064"

# name, framing, state, overlay, extra args
SHOTS = (
    ("pinch-a", "DialG1", PINCH, True, []),
    ("pinch-b", "DialG1", PINCH + B, True, []),
    ("dial-g1-a-nohalo", "DialG1", NOHALO, False, []),
    ("dial-g1-b-nohalo", "DialG1", NOHALO + B, False, []),
    ("seated-a", "DialSeated", NOHALO, False, []),
    ("seated-b", "DialSeated", NOHALO + B, False, []),
    ("close-a", "PlantClose", NOHALO, False, []),
    ("close-b", "PlantClose", NOHALO + B, False, []),
    ("orbit-seated-a", "DialSeated", NOHALO, False, ["-orbits=" + ORBITS]),
    ("orbit-seated-b", "DialSeated", NOHALO + B, False, ["-orbits=" + ORBITS]),
    ("orbit-close-a", "PlantClose", NOHALO, False, ["-orbits=" + ORBITS]),
    ("orbit-close-b", "PlantClose", NOHALO + B, False, ["-orbits=" + ORBITS]),
    ("stereo-close-a-L", "PlantClose", NOHALO, False, ["-eyeShift=-0.032"]),
    ("stereo-close-a-R", "PlantClose", NOHALO, False, ["-eyeShift=0.032"]),
    ("stereo-close-b-L", "PlantClose", NOHALO + B, False, ["-eyeShift=-0.032"]),
    ("stereo-close-b-R", "PlantClose", NOHALO + B, False, ["-eyeShift=0.032"]),
    ("stereo-seated-a-L", "DialSeated", NOHALO, False, ["-eyeShift=-0.032"]),
    ("stereo-seated-a-R", "DialSeated", NOHALO, False, ["-eyeShift=0.032"]),
    ("stereo-seated-b-L", "DialSeated", NOHALO + B, False, ["-eyeShift=-0.032"]),
    ("stereo-seated-b-R", "DialSeated", NOHALO + B, False, ["-eyeShift=0.032"]),
)


def unity(args, log):
    cmd = [UNITY] + args + ["-logFile", log]
    return subprocess.run(cmd, cwd=REPO).returncode


def shot(name, framing, state, overlay, extra):
    out = os.path.join(RUN, name + ".png")
    log = os.path.join(RUN, name + ".log")
    args = [
        "-batchmode", "-quit", "-projectPath", PROJECT,
        "-executeMethod", "GardenVR.Capture.Editor.CaptureCli.Shot",
        "-scene", SCENE, "-framing", framing, "-target", "dial",
        "-state", state, "-out", out,
    ] + extra
    if overlay:
        args += ["-overlay", MATTE, "-reference", REF]
    print("shot", name)
    code = unity(args, log)
    if code != 0:
        raise SystemExit("shot %s exit %s (see %s)" % (name, code, log))
    if not os.path.isfile(out):
        raise SystemExit("shot %s wrote no png" % name)
    print("ok", out)


def measure(name, framing, state):
    out = os.path.join(RUN, name + ".json")
    log = os.path.join(RUN, name + ".log")
    args = [
        "-batchmode", "-quit", "-projectPath", PROJECT,
        "-executeMethod", "GardenVR.Capture.Editor.CaptureCli.Measure",
        "-scene", SCENE, "-framing", framing, "-target", "dial",
        "-state", state, "-out", out,
    ]
    print("measure", name)
    code = unity(args, log)
    if code != 0:
        raise SystemExit("measure %s exit %s (see %s)" % (name, code, log))
    print("ok", out)


def edit_tests():
    xml = os.path.join(RUN, "editmode.xml")
    log = os.path.join(RUN, "editmode.log")
    args = [
        "-batchmode", "-nographics", "-projectPath", PROJECT,
        "-runTests", "-testPlatform", "EditMode", "-testResults", xml,
    ]
    code = unity(args, log)
    print("editmode exit", code, xml)
    return code


def main(argv):
    os.makedirs(RUN, exist_ok=True)
    if argv and argv[0] == "--measure":
        measure("measure-a", "DialG1", NOHALO)
        measure("measure-b", "DialG1", NOHALO + B)
        measure("measure-pinch-a", "DialG1", PINCH)
        measure("measure-pinch-b", "DialG1", PINCH + B)
        return
    if argv and argv[0] == "--edit-tests":
        raise SystemExit(edit_tests())
    wanted = set(argv)
    for name, framing, state, overlay, extra in SHOTS:
        if wanted and name not in wanted:
            continue
        shot(name, framing, state, overlay, extra)


if __name__ == "__main__":
    main(sys.argv[1:])
