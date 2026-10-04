# Capture the S4b set (T-SUN-051). One Unity at a time. Look A is the default; B2 is variant=soilmound after S4b.
#   python apps/sundial/Art/Scripts/soilmound_s4b_shots.py            # every frame
#   python apps/sundial/Art/Scripts/soilmound_s4b_shots.py dial-g1-b2-nohalo orbit-g1-b2
#   python apps/sundial/Art/Scripts/soilmound_s4b_shots.py --measure
# The T-SUN-046 frames (look A and the mound) are read from that task's folder, not recaptured.
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
UNITY = os.environ.get("UNITY", r"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe")
RUN = os.path.join(REPO, "orchestration", "runs", "sundial", "T-SUN-051")
PROJECT = os.path.join(REPO, "apps", "sundial")
SCENE = "Assets/Scenes/Main.unity"
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A2-05-field-notebook-1.png")
MATTE = os.path.join(REPO, "shared", "assets", "room-plates", "dial-hand-matte.png")

NOHALO = "halo=0,waiting=0,gnomonDeg=105,time=1"
PINCH = "halo=1,haloTarget=midday,gnomonDeg=105,time=1"
B2 = ",variant=soilmound"
ORBITS = "-orbits=-15,-7.5,0,7.5,15"
EYE = 0.032     # half of a 64 mm baseline

# name, framing, state, overlay, extra args
SHOTS = (
    ("dial-g1-b2-nohalo", "DialG1", NOHALO + B2, False, []),
    ("pinch-b2", "DialG1", PINCH + B2, True, []),
    ("seated-b2", "DialSeated", NOHALO + B2, False, []),
    ("sprint-b2", "DialG1", NOHALO + ",variant=sprint", False, []),
    ("orbit-g1-b2", "DialG1", NOHALO + B2, False, [ORBITS]),
    ("orbit-seated-b2", "DialSeated", NOHALO + B2, False, [ORBITS]),
    ("stereo-seated-b2-L", "DialSeated", NOHALO + B2, False, ["-eyeShift=-%s" % EYE]),
    ("stereo-seated-b2-R", "DialSeated", NOHALO + B2, False, ["-eyeShift=%s" % EYE]),
    ("stereo-g1-b2-L", "DialG1", NOHALO + B2, False, ["-eyeShift=-%s" % EYE]),
    ("stereo-g1-b2-R", "DialG1", NOHALO + B2, False, ["-eyeShift=%s" % EYE]),
    ("dial-g1-a-nohalo", "DialG1", NOHALO, False, []),
)
MEASURES = (("measure-b2", NOHALO + B2),)


def unity(name, method, framing, state, extra, overlay=False, out_ext=".png"):
    out = os.path.join(RUN, name + out_ext)
    log = os.path.join(RUN, name + ".log")
    cmd = [UNITY, "-batchmode", "-quit", "-projectPath", PROJECT, "-executeMethod", method,
           "-scene", SCENE, "-framing", framing, "-target", "dial", "-state", state, "-out", out, "-logFile", log]
    cmd.extend(extra)
    if overlay:
        cmd.extend(["-overlay", MATTE, "-reference", REF])
    print(method.split(".")[-1], name, flush=True)
    completed = subprocess.run(cmd, cwd=REPO)
    if completed.returncode != 0:
        raise SystemExit("%s exit %s (see %s)" % (name, completed.returncode, log))
    if not os.path.isfile(out):
        raise SystemExit("%s wrote no output" % name)
    print("ok", out, flush=True)


def main(argv):
    os.makedirs(RUN, exist_ok=True)
    if argv and argv[0] == "--measure":
        for name, state in MEASURES:
            unity(name, "GardenVR.Capture.Editor.CaptureCli.Measure", "DialG1", state, [], out_ext=".json")
        return
    wanted = set(argv)
    for name, framing, state, overlay, extra in SHOTS:
        if wanted and name not in wanted:
            continue
        unity(name, "GardenVR.Capture.Editor.CaptureCli.Shot", framing, state, extra, overlay)


if __name__ == "__main__":
    main(sys.argv[1:])
