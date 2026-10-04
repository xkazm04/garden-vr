"""Fidelity lab command line.

    python tools/fidelity/fid.py ladder JarG1|DialG1 [--force] [--evidence <dir>]
    python tools/fidelity/fid.py metrics <run-dir> --frame JarG1|DialG1
    python tools/fidelity/fid.py metrics --self-test
    python tools/fidelity/fid.py metrics --bare
    python tools/fidelity/fid.py judge --self-test
    python tools/fidelity/fid.py judge --calibrate [--frame JarG1|DialG1]

`metrics --bare` always exits 2. A distance is never printed without its ladder.
"""
from __future__ import annotations

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)


USAGE = """\
usage:
  fid.py ladder <JarG1|DialG1> [--force] [--evidence <dir>]
  fid.py metrics <run-dir> --frame <JarG1|DialG1>
  fid.py metrics --self-test
  fid.py metrics --bare
  fid.py judge --self-test
  fid.py judge --calibrate [--frame <JarG1|DialG1>] [--draws N] [--jobs N] [--evidence <dir>] [--fresh]
  fid.py judge --pairwise --frame <JarG1|DialG1> --player <id=path> --out <file>
  fid.py judge --regrade --ledger <calls.jsonl> [--check]
  fid.py judge --probe [--frame <JarG1|DialG1>]
"""


def _flag(argv, name):
    if name not in argv:
        return None
    index = argv.index(name)
    if index + 1 >= len(argv):
        raise SystemExit("missing value for %s" % name)
    return argv[index + 1]


def main(argv):
    if not argv or argv[0] in ("-h", "--help"):
        sys.stderr.write(USAGE)
        return 2
    command = argv[0]
    if command == "metrics" and "--bare" in argv:
        print("refusing to print a distance without its ladder", file=sys.stderr)
        return 2
    if command == "metrics" and "--self-test" in argv:
        from lab.selftest import run

        return run()
    if command == "ladder":
        if len(argv) < 2:
            sys.stderr.write(USAGE)
            return 2
        from lab.ladder import run_ladder

        return run_ladder(argv[1], force="--force" in argv, evidence=_flag(argv, "--evidence"))
    if command == "metrics":
        frame = _flag(argv, "--frame")
        positional = [item for item in argv[1:] if not item.startswith("--") and item != frame]
        if not frame or not positional:
            sys.stderr.write(USAGE)
            return 2
        from lab.ladder import run_metrics

        return run_metrics(positional[0], frame)
    if command == "judge":
        from lab.judge import main as judge_main

        return judge_main(argv[1:])
    sys.stderr.write(USAGE)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
