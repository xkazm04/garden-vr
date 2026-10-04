"""Spike S3 (T-TER-042) capture driver. One Unity at a time, graphics on, foreground.

Look A is the locked moss (no variant). Look B is capture state variant=s3.

    python apps/terrarium/Art/Scripts/s3_moss_shots.py shots            # the A/B set
    python apps/terrarium/Art/Scripts/s3_moss_shots.py shots b-jar-g1   # named shots only
    python apps/terrarium/Art/Scripts/s3_moss_shots.py measure          # budgets for A and B
    python apps/terrarium/Art/Scripts/s3_moss_shots.py msaa             # MSAA x alpha-to-coverage sweep
    python apps/terrarium/Art/Scripts/s3_moss_shots.py --out <dir> shots ...   # another output folder (iteration)
"""
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
UNITY = os.environ.get("UNITY", r"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe")
RUN = os.path.join(REPO, "orchestration", "runs", "terrarium", "T-TER-042")
PROJECT = os.path.join(REPO, "apps", "terrarium")
SCENE = "Assets/Scenes/Main.unity"
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A1-03-night-moss-1.png")

G1 = "breath=0.5,uncoil=0.3,fog=0.45,time=3"
B = ",variant=s3"

# name, framing, target, state, msaa (None = capture default 8), reference side-by-side
SHOTS = [
    ("a-jar-g1", "JarG1", "jar", G1, None, True),
    ("b-jar-g1", "JarG1", "jar", G1 + B, None, True),
    ("a-seated", "SeatedPOV", None, G1, None, False),
    ("b-seated", "SeatedPOV", None, G1 + B, None, False),
    ("a-moss-close", "MossClose", "jar", G1, None, False),
    ("b-moss-close", "MossClose", "jar", G1 + B, None, False),
    ("b-jar-g1-shells8", "JarG1", "jar", G1 + B + ",shells=8", None, False),
    ("b-moss-close-shells8", "MossClose", "jar", G1 + B + ",shells=8", None, False),
    ("a-glass-base", "GlassBase", "jar", G1, None, False),
    ("b-glass-base", "GlassBase", "jar", G1 + B, None, False),
]

# MSAA sweep: the same moss close-up at 1, 2, 4 and 8 samples, alpha to coverage on and off.
MSAA = [("msaa%d-a2c%d" % (m, a), m, a) for m in (1, 2, 4, 8) for a in (0, 1)]


def unity(args, log):
    cmd = [UNITY, "-batchmode", "-quit", "-projectPath", PROJECT] + args + ["-logFile", log]
    return subprocess.run(cmd, cwd=REPO).returncode


def shot(out_dir, name, framing, target, state, msaa, sbs):
    out = os.path.join(out_dir, name + ".png")
    log = os.path.join(out_dir, name + ".log")
    args = ["-executeMethod", "GardenVR.Capture.Editor.CaptureCli.Shot", "-scene", SCENE, "-framing", framing,
            "-state", state, "-out", out]
    if target:
        args += ["-target", target]
    if msaa:
        args += ["-msaa", str(msaa)]
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
        for name, framing in (("measure-a", "JarG1"), ("measure-b", "JarG1"), ("measure-a-seated", "SeatedPOV"),
                              ("measure-b-seated", "SeatedPOV"), ("measure-b8", "JarG1")):
            if names and name not in names:
                continue
            state = G1 + (B if name.startswith("measure-b") else "")
            if name == "measure-b8":
                state += ",shells=8"
            measure(out_dir, name, state, framing)
    elif mode == "msaa":
        for name, m, a in MSAA:
            if names and name not in names:
                continue
            shot(out_dir, "b-" + name, "MossClose", "jar", G1 + B + ",a2c=%d" % a, m, False)
    else:
        raise SystemExit("mode must be shots, measure or msaa")


if __name__ == "__main__":
    main(sys.argv[1:])
