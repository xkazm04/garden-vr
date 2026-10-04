# Capture the S3 / S3b (T-SUN-049) A/B set. One Unity at a time. Look A is the default (the camera-facing card).
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
RUN = os.path.join(REPO, "orchestration", "runs", "sundial", "T-SUN-049")
PROJECT = os.path.join(REPO, "apps", "sundial")
SCENE = "Assets/Scenes/Main.unity"
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A2-05-field-notebook-1.png")
MATTE = os.path.join(REPO, "shared", "assets", "room-plates", "dial-hand-matte.png")

NOHALO = "halo=0,waiting=0,gnomonDeg=105,time=1"
PINCH = "halo=1,haloTarget=midday,gnomonDeg=105,time=1"
B = ",variant=leafplant"
ORBITS = "-15,-10,-5,0,5,10,15"
IPD = "0.064"

PINCH_MORNING = "halo=1,haloTarget=morning,gnomonDeg=105,time=1"
PINCH_EVENING = "halo=1,haloTarget=winddown,gnomonDeg=105,time=1"
CLOSES = (("", "PlantClose"), ("-morning", "PlantCloseMorning"), ("-evening", "PlantCloseEvening"))


def build_shots():
    out = []
    for tag, state in (("", PINCH), ("-morning", PINCH_MORNING), ("-evening", PINCH_EVENING)):
        out.append(("pinch%s-a" % tag, "DialG1", state, True, []))
        out.append(("pinch%s-b" % tag, "DialG1", state + B, True, []))
    out += [
        ("dial-g1-a-nohalo", "DialG1", NOHALO, False, []),
        ("dial-g1-b-nohalo", "DialG1", NOHALO + B, False, []),
        ("seated-a", "DialSeated", NOHALO, False, []),
        ("seated-b", "DialSeated", NOHALO + B, False, []),
    ]
    for tag, framing in CLOSES:
        out.append(("close%s-a" % tag, framing, NOHALO, False, []))
        out.append(("close%s-b" % tag, framing, NOHALO + B, False, []))
    # Every plant in bloom (open) and in bud, so the heads of each species show: bloom 2 is open, 1 is a bud.
    for tag, bloom in (("open", "bloom.morning=2,bloom.midday=2,bloom.winddown=2"), ("bud", "bloom.morning=1,bloom.midday=1,bloom.winddown=1")):
        out.append(("bloom-%s-a" % tag, "DialG1", NOHALO + "," + bloom, False, []))
        out.append(("bloom-%s-b" % tag, "DialG1", NOHALO + "," + bloom + B, False, []))
    for tag, framing in CLOSES[1:]:
        out.append(("close%s-open-a" % tag, framing, NOHALO + ",bloom.morning=2,bloom.winddown=2", False, []))
        out.append(("close%s-open-b" % tag, framing, NOHALO + ",bloom.morning=2,bloom.winddown=2" + B, False, []))
    out += [
        ("orbit-seated-a", "DialSeated", NOHALO, False, ["-orbits=" + ORBITS]),
        ("orbit-seated-b", "DialSeated", NOHALO + B, False, ["-orbits=" + ORBITS]),
    ]
    for tag, framing in CLOSES:
        out.append(("orbit-close%s-a" % tag, framing, NOHALO, False, ["-orbits=" + ORBITS]))
        out.append(("orbit-close%s-b" % tag, framing, NOHALO + B, False, ["-orbits=" + ORBITS]))
    for who, state in (("a", NOHALO), ("b", NOHALO + B)):
        out.append(("stereo-seated-%s-L" % who, "DialSeated", state, False, ["-eyeShift=-0.032"]))
        out.append(("stereo-seated-%s-R" % who, "DialSeated", state, False, ["-eyeShift=0.032"]))
    return tuple(out)


# name, framing, state, overlay, extra args
SHOTS = build_shots()


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
