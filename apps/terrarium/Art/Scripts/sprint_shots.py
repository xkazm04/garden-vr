"""T-TER-047 (sprint composite) capture driver. One Unity at a time, graphics on, foreground. Reuses the S5 driver.

Looks (short names used in file names):
  a        the locked look, no variant
  s6       variant=s2b2+s5+s6, the T-TER-049 stack before the interaction fixes (frozen: the same string as then)
  sprint   variant=sprint, the PC best (S1 pane and glow, S2 B2, S3 moss, S4 fiddlehead, S5, S6)
  sprintq  variant=sprint+s2b1 with shells=8,fuzz=0, the Quest fallback
Ablations of sprint (one part put back, to see which part fights which): sprint-<name>.

    python apps/terrarium/Art/Scripts/sprint_shots.py shots                  # the full set
    python apps/terrarium/Art/Scripts/sprint_shots.py shots sprint-jar-g1    # named shots only
    python apps/terrarium/Art/Scripts/sprint_shots.py measure                # budgets (draws, tris, transparent layers)
    python apps/terrarium/Art/Scripts/sprint_shots.py --out <dir> --extra "s5haze=0" shots sprint-jar-g1
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import s5_shots as s5  # noqa: E402

RUN = os.path.join(s5.REPO, "orchestration", "runs", "terrarium", "T-TER-047")
G1 = s5.G1
LOOKS = {
    "a": "",
    "s6": ",variant=s2b2+s5+s6",
    "sprint": ",variant=sprint",
    "sprintq": ",variant=sprint+s2b1,shells=8,fuzz=0",
    "sprint-nohaze": ",variant=sprint,s5haze=0",
    "sprint-b0": ",variant=sprint+s2b0",
    "sprint-b1": ",variant=sprint+s2b1",
    "sprint-nosteam": ",variant=sprint,s5steam=0",
    "sprint-nospore": ",variant=sprint,s5spore=0",
    "sprint-nodesk": ",variant=sprint,s5desk=0",
}
MAIN = ("a", "s6", "sprint", "sprintq")

SHOTS = []
for _look in LOOKS:
    SHOTS.append((_look + "-jar-g1", "JarG1", "jar", _look, None, True))
for _look in MAIN:
    SHOTS.append((_look + "-seated", "SeatedPOV", None, _look, None, False))
for _framing, _tag in (("GlassBase", "glass-base"), ("GlassRim", "glass-rim"), ("MossClose", "moss-close")):
    for _look in MAIN:
        SHOTS.append((_look + "-" + _tag, _framing, "jar", _look, None, False))


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
            s5.shot(out_dir, name, framing, target, G1 + LOOKS[look] + extra, msaa, sbs)
    elif mode == "measure":
        for look in MAIN:
            for framing, tag in (("JarG1", ""), ("SeatedPOV", "-seated")):
                name = "measure-" + look + tag
                if names and name not in names:
                    continue
                s5.measure(out_dir, name, G1 + LOOKS[look] + extra, framing)
    else:
        raise SystemExit("mode must be shots or measure")


if __name__ == "__main__":
    main(sys.argv[1:])
