# Capture the T-SUN-050 sprint composite set. One Unity at a time. Look A is the default; S is capture state variant=sprint.
#
#   python apps/sundial/Art/Scripts/sprint_t050_shots.py                 # every frame
#   python apps/sundial/Art/Scripts/sprint_t050_shots.py pinch-s nohalo-a # named frames
#   python apps/sundial/Art/Scripts/sprint_t050_shots.py --list          # the names
#   python apps/sundial/Art/Scripts/sprint_t050_shots.py --measure       # budgets (CaptureCli.Measure)
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
UNITY = os.environ.get(
    "UNITY",
    r"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe",
)
RUN = os.path.join(REPO, "orchestration", "runs", "sundial", "T-SUN-050")
PROJECT = os.path.join(REPO, "apps", "sundial")
SCENE = "Assets/Scenes/Main.unity"
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A2-05-field-notebook-1.png")
MATTE = os.path.join(REPO, "shared", "assets", "room-plates", "dial-hand-matte.png")

NOHALO = "halo=0,waiting=0,gnomonDeg=105,time=1"
PINCH = "halo=1,haloTarget=midday,gnomonDeg=105,time=1"
MORNING = "halo=1,haloTarget=morning,gnomonDeg=105,time=1"
DUSK = "halo=1,haloTarget=winddown,gnomonDeg=105,time=1"
A = ""
S = ",variant=sprint"
# The alternative the owner can compare: the sprint with the morning plant drawn as an assembly too (it lost to its card, 2 of 6).
S3 = ",variant=sprint+leafplant"
# The sprint without one part, to read what that part does inside the stack.
NOROOM = ",variant=layout+watercolour+soilmound+sprintleaf"
NOLEAF = ",variant=layout+watercolour+roomlight+soilmound"
NOH2 = ",variant=layout+watercolour+roomlight+soilmound+sprintleaf"
ORBITS = ["-orbits=-15,-10,-5,0,5,10,15"]

SHOTS = (
    # Look A must still be the locked T-SUN-031 frame (sha256 7fecea65...), byte for byte.
    ("nohalo-a", "DialG1", NOHALO + A, False, []),
    ("nohalo-s", "DialG1", NOHALO + S, False, []),
    ("nohalo-s3", "DialG1", NOHALO + S3, False, []),
    ("nohalo-noroom", "DialG1", NOHALO + NOROOM, False, []),
    ("nohalo-noleaf", "DialG1", NOHALO + NOLEAF, False, []),
    # The pinch frames: with the hand matte and the reference (the capture composites), and the same without the hand.
    ("pinch-a", "DialG1", PINCH + A, True, []),
    ("pinch-s", "DialG1", PINCH + S, True, []),
    ("pinch-s3", "DialG1", PINCH + S3, True, []),
    ("g1-a", "DialG1", PINCH + A, False, []),
    ("g1-s", "DialG1", PINCH + S, False, []),
    ("pinch-morning-a", "DialG1", MORNING + A, False, []),
    ("pinch-morning-s", "DialG1", MORNING + S, False, []),
    ("pinch-evening-a", "DialG1", DUSK + A, False, []),
    ("pinch-evening-s", "DialG1", DUSK + S, False, []),
    # The seated view, without and with the halo.
    ("seated-a", "DialSeated", NOHALO + A, False, []),
    ("seated-s", "DialSeated", NOHALO + S, False, []),
    ("seatedp-a", "DialSeated", PINCH + A, False, []),
    ("seatedp-s", "DialSeated", PINCH + S, False, []),
    # On black: each arc's plants alone, and none, for the halo hug and the wedge under a drawn plant.
    ("plant-a-morning", "DialG1Black", NOHALO + ",plants=morning,isolate=dial" + A, False, []),
    ("plant-a-midday", "DialG1Black", NOHALO + ",plants=midday,isolate=dial" + A, False, []),
    ("plant-a-winddown", "DialG1Black", NOHALO + ",plants=winddown,isolate=dial" + A, False, []),
    ("plant-s-morning", "DialG1Black", NOHALO + ",plants=morning,isolate=dial" + S, False, []),
    ("plant-s-midday", "DialG1Black", NOHALO + ",plants=midday,isolate=dial" + S, False, []),
    ("plant-s-winddown", "DialG1Black", NOHALO + ",plants=winddown,isolate=dial" + S, False, []),
    ("plant-noroom-midday", "DialG1Black", NOHALO + ",plants=midday,isolate=dial" + NOROOM, False, []),
    ("plant-noroom-winddown", "DialG1Black", NOHALO + ",plants=winddown,isolate=dial" + NOROOM, False, []),
    ("empty-a", "DialG1Black", NOHALO + ",plants=0,isolate=dial" + A, False, []),
    ("empty-s", "DialG1Black", NOHALO + ",plants=0,isolate=dial" + S, False, []),
    ("halo-a", "DialG1Black", PINCH + ",isolate=halo" + A, False, []),
    ("halo-s", "DialG1Black", PINCH + ",isolate=halo" + S, False, []),
    # The sprint without halo v2 (the layout's halo on every plant), to read what halo v2 does for the card plant.
    ("halo-s-noh2", "DialG1Black", PINCH + ",isolate=halo" + NOH2, False, []),
    ("halo-morning-a", "DialG1Black", MORNING + ",isolate=halo" + A, False, []),
    ("halo-morning-s", "DialG1Black", MORNING + ",isolate=halo" + S, False, []),
    ("halo-morning-s-noh2", "DialG1Black", MORNING + ",isolate=halo" + NOH2, False, []),
    ("pinch-morning-s-noh2", "DialG1", MORNING + NOH2, False, []),
    # The plants close up, orbiting -15 to +15 degrees (frame .o0 is the straight one).
    ("orbit-close-a", "PlantClose", NOHALO + A, False, ORBITS),
    ("orbit-close-s", "PlantClose", NOHALO + S, False, ORBITS),
    ("orbit-close-morning-a", "PlantCloseMorning", NOHALO + A, False, ORBITS),
    ("orbit-close-morning-s", "PlantCloseMorning", NOHALO + S, False, ORBITS),
    ("orbit-close-morning-s3", "PlantCloseMorning", NOHALO + S3, False, ORBITS),
    ("orbit-close-evening-a", "PlantCloseEvening", NOHALO + A, False, ORBITS),
    ("orbit-close-evening-s", "PlantCloseEvening", NOHALO + S, False, ORBITS),
    ("orbit-seated-a", "DialSeated", NOHALO + A, False, ORBITS),
    ("orbit-seated-s", "DialSeated", NOHALO + S, False, ORBITS),
)

MEASURES = (
    ("measure-a", NOHALO + A),
    ("measure-s", NOHALO + S),
    ("measure-pinch-a", PINCH + A),
    ("measure-pinch-s", PINCH + S),
    ("measure-pinch-s3", PINCH + S3),
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
    print(method.split(".")[-1], name, flush=True)
    completed = subprocess.run(cmd, cwd=REPO)
    if completed.returncode != 0:
        raise SystemExit("%s exit %s (see %s)" % (name, completed.returncode, log))
    if not os.path.isfile(out):
        raise SystemExit("%s wrote no output" % name)
    print("ok", out, flush=True)


def main(argv):
    os.makedirs(RUN, exist_ok=True)
    if argv and argv[0] == "--list":
        print(" ".join(s[0] for s in SHOTS))
        return
    if argv and argv[0] == "--measure":
        wanted = set(argv[1:])
        for name, state in MEASURES:
            if wanted and name not in wanted:
                continue
            unity(name, "GardenVR.Capture.Editor.CaptureCli.Measure", "DialG1", state, [], out_ext=".json")
        return
    wanted = set(argv)
    for name, framing, state, overlay, extra in SHOTS:
        if wanted and name not in wanted:
            continue
        unity(name, "GardenVR.Capture.Editor.CaptureCli.Shot", framing, state, extra, overlay)


if __name__ == "__main__":
    main(sys.argv[1:])
