# T-SUN-050: the T-SUN-031 A3 whole-frame rubric prompt, three draws each on the pinch composites (reference | frame), look A and sprint.
#   python apps/sundial/Art/Scripts/sprint_t050_rubric.py
import concurrent.futures
import json
import os
import subprocess

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
RUN = os.path.join(REPO, "orchestration", "runs", "sundial", "T-SUN-050")
AGY = os.environ.get("AGY", os.path.join(os.environ.get("LOCALAPPDATA", ""), "agy", "bin", "agy.exe"))
PROMPT = ("Score 1-5 how closely the right image matches the left reference in hand-drawn quality, colour, line work and mood; "
          "list the 3 biggest differences. Image: %s")


def call(job):
    tag, draw = job
    out = os.path.join(RUN, "rubric-frame-%s-%d.json" % (tag, draw))
    if os.path.isfile(out) and json.load(open(out)).get("exit") == 0:
        return
    sbs = os.path.join(RUN, "pinch-%s.sbs.png" % tag)
    c = subprocess.run([AGY, "-p", PROMPT % sbs, "--model", "gemini-3.8-flash-high", "--output-format", "json"],
                       cwd=REPO, capture_output=True, text=True, timeout=1500)
    json.dump({"look": tag, "draw": draw, "exit": c.returncode, "stdout": c.stdout, "stderr": c.stderr[-2000:]}, open(out, "w"), indent=2)
    print("wrote", out, c.returncode, flush=True)


with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
    list(pool.map(call, [(t, d) for t in ("a", "s") for d in (1, 2, 3)]))
