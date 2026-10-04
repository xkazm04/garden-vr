"""Spike S5 (T-TER-045) capture driver. One Unity at a time, graphics on, foreground.

Looks (short names used in file names):
  a      the locked look, no variant
  s1     variant=s1, the S1 structured pane with the dark S1 cork (the host reviewed this one)
  b      variant=s5, S1 pane + S5 (chord haze, steam puffs, stretched spores, desk streak, contact line, mid-brown cork)
  c      variant=s2b2+s5, the PC best: S2 thick shell with Opaque Texture refraction, plus S5
  c1     variant=s2b1+s5, the Quest candidate: baked strip, plus S5
Ablations of b (one S5 part off each): b-nohaze, b-nosteam, b-nospore, b-nodesk, b-nocork.

    python apps/terrarium/Art/Scripts/s5_shots.py setup             # JarSetup: rebuild the prefab and materials
    python apps/terrarium/Art/Scripts/s5_shots.py probe             # ring and jar plane numbers (planes.json)
    python apps/terrarium/Art/Scripts/s5_shots.py shots             # the full set
    python apps/terrarium/Art/Scripts/s5_shots.py shots b-jar-g1    # named shots only
    python apps/terrarium/Art/Scripts/s5_shots.py measure           # budgets (draws, tris, transparent layers)
    python apps/terrarium/Art/Scripts/s5_shots.py --out <dir> shots ...
    python apps/terrarium/Art/Scripts/s5_shots.py --extra "s5sigma=20" shots b-jar-g1
"""
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
UNITY = os.environ.get("UNITY", r"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe")
RUN = os.path.join(REPO, "orchestration", "runs", "terrarium", "T-TER-045")
PROJECT = os.path.join(REPO, "apps", "terrarium")
SCENE = "Assets/Scenes/Main.unity"
REF = os.path.join(REPO, "shared", "assets", "art-reference", "A1-03-night-moss-1.png")

G1 = "breath=0.5,uncoil=0.3,fog=0.45,time=3"
LOOKS = {"a": "", "s1": ",variant=s1", "b": ",variant=s5", "c": ",variant=s2b2+s5", "c1": ",variant=s2b1+s5",
         "b-nohaze": ",variant=s5,s5haze=0", "b-nosteam": ",variant=s5,s5steam=0", "b-nospore": ",variant=s5,s5spore=0",
         "b-nodesk": ",variant=s5,s5desk=0", "b-nocork": ",variant=s5,s5cork=0"}
MAIN = ("a", "s1", "b", "c", "c1")

# name, framing, target, look, msaa (None = capture default 8), reference side-by-side
SHOTS = []
for _look in LOOKS:
    SHOTS.append((_look + "-jar-g1", "JarG1", "jar", _look, None, True))
for _look in MAIN:
    SHOTS.append((_look + "-seated", "SeatedPOV", None, _look, None, False))
for _framing, _tag in (("GlassBase", "glass-base"), ("GlassRim", "glass-rim"), ("MossClose", "moss-close")):
    for _look in MAIN:
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
    elif mode == "setup":
        # Rebuild the prefab and materials (JarSetup). Needed after any change to JarView.Build, the library or a texture.
        code = unity(["-nographics", "-executeMethod", "GardenVR.Terrarium.Editor.JarSetup.Run"], os.path.join(out_dir, "jarsetup.log"))
        if code != 0:
            raise SystemExit("JarSetup exit %s" % code)
    elif mode == "probe":
        out = os.path.join(out_dir, "planes.json")
        code = unity(["-nographics", "-executeMethod", "GardenVR.Terrarium.Editor.S5Probe.Planes", "-out", out], os.path.join(out_dir, "planes.log"))
        if code != 0 or not os.path.isfile(out):
            raise SystemExit("probe exit %s" % code)
    elif mode == "measure":
        for look, variant in LOOKS.items():
            if look not in MAIN and not look.startswith("b-"):
                continue
            for framing, tag in (("JarG1", ""), ("SeatedPOV", "-seated")):
                name = "measure-" + look + tag
                if names and name not in names:
                    continue
                measure(out_dir, name, G1 + variant, framing)
    else:
        raise SystemExit("mode must be setup, probe, shots or measure")


if __name__ == "__main__":
    main(sys.argv[1:])
