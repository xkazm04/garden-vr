"""Spike S2 (T-TER-044) capture driver. One Unity at a time, graphics on, foreground.

Look A is the locked glass (no variant). S1 is variant=s1 (structured pane on the A mesh, the previous spike). The S2
variants all use the structured pane on the thick-glass mesh (s2_jar.fbx): s2b0 is the shell alone, s2b1 adds the baked
refraction strip (the Quest path), s2b2 adds Opaque Texture refraction with 3-tap dispersion (the PC path).
Short names used in file names: a, s1, b0, b1, b2.

    python apps/terrarium/Art/Scripts/s2_shots.py bake              # re-bake the refraction strip (Unity, graphics on)
    python apps/terrarium/Art/Scripts/s2_shots.py shots             # the full set
    python apps/terrarium/Art/Scripts/s2_shots.py shots b1-jar-g1   # named shots only
    python apps/terrarium/Art/Scripts/s2_shots.py measure           # budgets (draws, tris, transparent layers)
    python apps/terrarium/Art/Scripts/s2_shots.py time [scale]      # PC frame time per look, JSON (default scale 1)
    python apps/terrarium/Art/Scripts/s2_shots.py --out <dir> shots ...   # another output folder (iteration)
    python apps/terrarium/Art/Scripts/s2_shots.py --extra "s2off=9" shots b2-jar-g1   # extra capture-state keys
"""
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
UNITY = os.environ.get("UNITY", r"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe")
RUN = os.path.join(REPO, "orchestration", "runs", "terrarium", "T-TER-044")
PROJECT = os.path.join(REPO, "apps", "terrarium")
SCENE = "Assets/Scenes/Main.unity"
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A1-03-night-moss-1.png")
STRIP = os.path.join(PROJECT, "Assets", "Art", "Textures", "s2_refract_strip.png")

G1 = "breath=0.5,uncoil=0.3,fog=0.45,time=3"
LOOKS = {"a": "", "s1": ",variant=s1", "b0": ",variant=s2b0", "b1": ",variant=s2b1", "b2": ",variant=s2b2"}

# name, framing, target, look, msaa (None = capture default 8), reference side-by-side
SHOTS = []
for _look in LOOKS:
    SHOTS.append((_look + "-jar-g1", "JarG1", "jar", _look, None, True))
for _look in LOOKS:
    SHOTS.append((_look + "-seated", "SeatedPOV", None, _look, None, False))
for _framing, _tag in (("GlassBase", "glass-base"), ("GlassRim", "glass-rim"), ("MossClose", "moss-close")):
    for _look in LOOKS:
        SHOTS.append((_look + "-" + _tag, _framing, "jar", _look, None, False))


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
    extra = ""
    if "--out" in argv:
        i = argv.index("--out")
        out_dir = os.path.abspath(argv[i + 1])
        argv = argv[:i] + argv[i + 2:]
    if "--extra" in argv:
        i = argv.index("--extra")
        extra = "," + argv[i + 1]
        argv = argv[:i] + argv[i + 2:]
    os.makedirs(out_dir, exist_ok=True)
    mode = argv[0] if argv else "shots"
    names = set(argv[1:])
    if mode == "shots":
        for name, framing, target, look, msaa, sbs in SHOTS:
            if names and name not in names:
                continue
            shot(out_dir, name, framing, target, G1 + LOOKS[look] + extra, msaa, sbs)
    elif mode == "measure":
        for look, variant in LOOKS.items():
            for framing, tag in (("JarG1", ""), ("SeatedPOV", "-seated")):
                name = "measure-" + look + tag
                if names and name not in names:
                    continue
                measure(out_dir, name, G1 + variant, framing)
    elif mode == "bake":
        out = os.path.join(out_dir, "s2_refract_strip.png")
        code = unity(["-executeMethod", "GardenVR.Terrarium.Editor.S2Bake.Bake", "-out", out], os.path.join(out_dir, "bake.log"))
        if code != 0 or not os.path.isfile(out):
            raise SystemExit("bake exit %s" % code)
        shutil.copyfile(out, STRIP)
    elif mode == "time":
        scale = sorted(names)[0] if names else "1"
        out = os.path.join(out_dir, "time-x%s.json" % scale)
        code = unity(["-executeMethod", "GardenVR.Terrarium.Editor.S2Bake.Time", "-out", out, "-scale", scale],
                     os.path.join(out_dir, "time-x%s.log" % scale))
        if code != 0 or not os.path.isfile(out):
            raise SystemExit("time exit %s" % code)
    else:
        raise SystemExit("mode must be shots, measure, bake or time")


if __name__ == "__main__":
    main(sys.argv[1:])
