"""Spike S4 (T-TER-043) capture driver. One Unity at a time, graphics on, foreground.

Look A is the locked fronds and fiddlehead (no variant). Look B is capture state variant=s4 (backlit fronds and the
thick fuzzy fiddlehead). variant=s4f is the fronds alone and variant=s4h is the fiddlehead alone, for the ablation.

    python apps/terrarium/Art/Scripts/s4_shots.py shots              # the A/B set
    python apps/terrarium/Art/Scripts/s4_shots.py shots b-jar-g1     # named shots only
    python apps/terrarium/Art/Scripts/s4_shots.py measure            # budgets for A and B
    python apps/terrarium/Art/Scripts/s4_shots.py --out <dir> shots ...   # another output folder (iteration)
"""
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
UNITY = os.environ.get("UNITY", r"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe")
RUN = os.path.join(REPO, "orchestration", "runs", "terrarium", "T-TER-043")
PROJECT = os.path.join(REPO, "apps", "terrarium")
SCENE = "Assets/Scenes/Main.unity"
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A1-03-night-moss-1.png")

G1 = "breath=0.5,uncoil=0.3,fog=0.45,time=3"
B = ",variant=s4"

# name, framing, target, state, msaa (None = capture default 8), reference side-by-side
SHOTS = [
    ("a-jar-g1", "JarG1", "jar", G1, None, True),
    ("b-jar-g1", "JarG1", "jar", G1 + B, None, True),
    ("bf-jar-g1", "JarG1", "jar", G1 + ",variant=s4f", None, False),
    ("bh-jar-g1", "JarG1", "jar", G1 + ",variant=s4h", None, False),
    ("b-jar-g1-fuzz0", "JarG1", "jar", G1 + B + ",fuzz=0", None, False),
    ("a-seated", "SeatedPOV", None, G1, None, False),
    ("b-seated", "SeatedPOV", None, G1 + B, None, False),
    ("a-frond-close", "FrondClose", "jar", G1, None, False),
    ("b-frond-close", "FrondClose", "jar", G1 + B, None, False),
    ("bf-frond-close", "FrondClose", "jar", G1 + ",variant=s4f", None, False),
    ("bh-frond-close", "FrondClose", "jar", G1 + ",variant=s4h", None, False),
    ("b-frond-close-fuzz0", "FrondClose", "jar", G1 + B + ",fuzz=0", None, False),
    ("a-frond-close-open", "FrondClose", "jar", "breath=0.5,uncoil=0.75,fog=0.45,time=3", None, False),
    ("b-frond-close-open", "FrondClose", "jar", "breath=0.5,uncoil=0.75,fog=0.45,time=3" + B, None, False),
]


def unity(args, log):
    cmd = [UNITY, "-batchmode", "-quit", "-projectPath", PROJECT] + args + ["-logFile", log]
    return subprocess.run(cmd, cwd=REPO).returncode


def shot(out_dir, name, framing, target, state, msaa, sbs, size=None):
    out = os.path.join(out_dir, name + ".png")
    log = os.path.join(out_dir, name + ".log")
    args = ["-executeMethod", "GardenVR.Capture.Editor.CaptureCli.Shot", "-scene", SCENE, "-framing", framing,
            "-state", state, "-out", out]
    if target:
        args += ["-target", target]
    if msaa:
        args += ["-msaa", str(msaa)]
    if size:
        args += ["-w", str(size), "-h", str(size)]
    if sbs:
        args += ["-reference", REF]
    print("shot", name, flush=True)
    code = unity(args, log)
    if code != 0 or not os.path.isfile(out):
        raise SystemExit("shot %s exit %s (see %s)" % (name, code, log))


def measure(out_dir, name, state, framing="JarG1"):
    out = os.path.join(out_dir, name + ".json")
    log = os.path.join(out_dir, name + ".log")
    args = ["-executeMethod", "GardenVR.Capture.Editor.CaptureCli.Measure", "-scene", SCENE, "-framing", framing,
            "-target", "jar", "-state", state, "-out", out]
    print("measure", name, flush=True)
    code = unity(args, log)
    if code != 0:
        raise SystemExit("measure %s exit %s (see %s)" % (name, code, log))


def main(argv):
    out_dir = RUN
    if "--out" in argv:
        i = argv.index("--out")
        out_dir = os.path.abspath(argv[i + 1])
        argv = argv[:i] + argv[i + 2:]
    os.makedirs(out_dir, exist_ok=True)
    mode = argv[0] if argv else "shots"
    names = set(argv[1:])
    if mode == "shots":
        for name, framing, target, state, msaa, sbs in SHOTS:
            if names and name not in names:
                continue
            shot(out_dir, name, framing, target, state, msaa, sbs)
    elif mode == "measure":
        for name, framing, extra in (("measure-a", "JarG1", ""), ("measure-b", "JarG1", B),
                                     ("measure-b-fuzz0", "JarG1", B + ",fuzz=0"),
                                     ("measure-a-seated", "SeatedPOV", ""), ("measure-b-seated", "SeatedPOV", B)):
            if names and name not in names:
                continue
            measure(out_dir, name, G1 + extra, framing)
    else:
        raise SystemExit("mode must be shots or measure")


if __name__ == "__main__":
    main(sys.argv[1:])
