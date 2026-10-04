"""T-TER-049 (S6) capture driver. One Unity at a time, graphics on, foreground. Reuses the S5 driver's shot and measure.

Looks (short names used in file names):
  c      variant=s2b2+s5, the T-TER-045 PC best, the one the host compared against
  c1     variant=s2b1+s5, the Quest analogue before this task
  s6     variant=s2b2+s5+s6, this task on the PC path
  s61    variant=s2b1+s5+s6, this task on the Quest path
Ablations of s6 (one part put back to its C value): s6-nocork, s6-nomoss, s6-nofiddle are not knobs; the silhouette parts are
one switch. The haze, steam and cork knobs of S5 still apply (s5haze, s5steam, s5cork).

    python apps/terrarium/Art/Scripts/s6_shots.py shots                # the full set
    python apps/terrarium/Art/Scripts/s6_shots.py shots s6-jar-g1      # named shots only
    python apps/terrarium/Art/Scripts/s6_shots.py measure              # budgets (draws, tris, transparent layers)
    python apps/terrarium/Art/Scripts/s6_shots.py --extra "s5sigma=20" shots s6-jar-g1
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import s5_shots as s5  # noqa: E402

RUN = os.path.join(s5.REPO, "orchestration", "runs", "terrarium", "T-TER-049")
G1 = s5.G1
LOOKS = {"c": ",variant=s2b2+s5", "c1": ",variant=s2b1+s5", "s6": ",variant=s2b2+s5+s6", "s61": ",variant=s2b1+s5+s6"}

SHOTS = []
for _look in LOOKS:
    SHOTS.append((_look + "-jar-g1", "JarG1", "jar", _look, None, True))
for _look in LOOKS:
    SHOTS.append((_look + "-seated", "SeatedPOV", None, _look, None, False))
for _framing, _tag in (("GlassBase", "glass-base"), ("GlassRim", "glass-rim"), ("MossClose", "moss-close")):
    for _look in ("c", "s6"):
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
        for look, variant in LOOKS.items():
            for framing, tag in (("JarG1", ""), ("SeatedPOV", "-seated")):
                name = "measure-" + look + tag
                if names and name not in names:
                    continue
                s5.measure(out_dir, name, G1 + variant + extra, framing)
    else:
        raise SystemExit("mode must be shots or measure")


if __name__ == "__main__":
    main(sys.argv[1:])
