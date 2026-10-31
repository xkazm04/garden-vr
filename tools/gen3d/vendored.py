"""The one reset authority for the vendored gen3d trees (ComfyUI + ComfyUI-TRELLIS2 under C:\\ai).

Both trees are git checkouts AND build workspaces, so `git checkout .`, `git reset --hard`,
`git stash` and `git clean -fdx` each have an opinion about what "reset" means and each is
wrong about part of the tree: the first three silently drop tools/gen3d/patches (the patch
rides as an uncommitted edit), and `git clean -fdx` at the ComfyUI root deletes /models/
(ignored, ~12 GB of weights). Do not use them in C:\\ai. Use this:

  python tools/gen3d/vendored.py check            # each tree == its pins.json commit + its patches; exit 1 names the drift
  python tools/gen3d/vendored.py reset bytecode   # discard __pycache__ only; keeps tree, venv, weights
  python tools/gen3d/vendored.py reset tree       # re-checkout the pin, re-apply patches; keeps venv and weights.
                                                  #   saves any drift as <tree>.drift-<ts>.diff beside the tree first
  python tools/gen3d/vendored.py reset reinstall  # venv + weights: by hand, from README "Install". Never automated.

--root overrides C:\\ai (or set GEN3D_ROOT). stdlib only.
"""
import argparse, json, os, shutil, subprocess, sys, tempfile, time
from pathlib import Path

HERE = Path(__file__).resolve().parent
# tree name -> (path under root, pins.json component key, patch files applied in this tree)
TREES = {
    "comfyui": ("ComfyUI", "comfyui", []),
    "trellis2_node": ("ComfyUI/custom_nodes/ComfyUI-TRELLIS2", "trellis2_node", ["trellis2-device-arg.patch"]),
}


def git(tree, *args, env=None, check=True):
    r = subprocess.run(["git", "-C", str(tree), *args], capture_output=True, text=True, env=env)
    if check and r.returncode != 0:
        raise SystemExit(f"git {' '.join(args)} failed in {tree}: {r.stderr.strip()}")
    return r


def pins():
    return json.loads((HERE / "pins.json").read_text())["components"]


def drift(tree, pin, patches):
    """Compare the tree with pin + patches through a throwaway index. Returns a list of reasons, [] = clean."""
    reasons = []
    head = git(tree, "rev-parse", "HEAD").stdout.strip()
    if head != pin:
        reasons.append(f"HEAD {head[:9]} != pin {pin[:9]}")
    with tempfile.TemporaryDirectory() as td:
        env = {**os.environ, "GIT_INDEX_FILE": str(Path(td) / "index")}
        if git(tree, "read-tree", pin, env=env, check=False).returncode != 0:
            return reasons + [f"pin {pin[:9]} not in this clone (fetch it: reset tree)"]
        for p in patches:
            r = git(tree, "apply", "--cached", str(HERE / "patches" / p), env=env, check=False)
            if r.returncode != 0:
                return reasons + [f"patch {p} does not apply to the pin: {r.stderr.strip()}"]
        changed = git(tree, "diff", "--name-status", env=env).stdout.split("\n")
        extra = git(tree, "ls-files", "--others", "--exclude-standard", env=env).stdout.split("\n")
        reasons += [f"differs from pin+patches: {c}" for c in changed if c.strip()]
        reasons += [f"untracked, not ignored: {u}" for u in extra if u.strip()]
    return reasons


def cmd_check(root):
    bad = 0
    for name, (rel, key, patches) in TREES.items():
        tree = root / rel
        if not (tree / ".git").exists():
            print(f"{name}: MISSING {tree}"); bad += 1; continue
        reasons = drift(tree, pins()[key], patches)
        print(f"{name}: {'ok' if not reasons else 'DRIFT'} ({tree})")
        for r in reasons[:20]:
            print(f"  - {r}")
        if len(reasons) > 20:
            print(f"  ... {len(reasons) - 20} more")
        bad += bool(reasons)
    return 1 if bad else 0


def cmd_bytecode(root):
    n = 0
    for rel, _, _ in TREES.values():
        for d in (root / rel).rglob("__pycache__"):
            if d.is_dir():
                shutil.rmtree(d); n += 1
    print(f"removed {n} __pycache__ dirs; tree, venv and weights untouched")
    return 0


def cmd_tree(root):
    for name, (rel, key, patches) in TREES.items():
        tree, pin = root / rel, pins()[key]
        diff = git(tree, "diff", "HEAD").stdout
        if diff.strip():
            save = tree.parent / f"{tree.name}.drift-{time.strftime('%Y%m%d-%H%M%S')}.diff"
            save.write_text(diff)
            print(f"{name}: saved current edits to {save}")
        if git(tree, "cat-file", "-e", f"{pin}^{{commit}}", check=False).returncode != 0:
            git(tree, "fetch", "--depth", "1", "origin", pin)
        # tracked files only: ignored /models/ and /custom_nodes/ are never touched here
        git(tree, "checkout", "--force", "--detach", pin)
        for p in patches:
            git(tree, "apply", str(HERE / "patches" / p))
        print(f"{name}: at {pin[:9]} + {len(patches)} patch(es)")
    return cmd_check(root)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("verb", choices=["check", "reset"])
    ap.add_argument("grade", nargs="?", choices=["bytecode", "tree", "reinstall"])
    ap.add_argument("--root", default=os.environ.get("GEN3D_ROOT", r"C:\ai"))
    a = ap.parse_args()
    root = Path(a.root)
    if a.verb == "check":
        return cmd_check(root)
    if a.grade is None:
        ap.error("reset needs a grade: bytecode | tree | reinstall")
    if a.grade == "bytecode":
        return cmd_bytecode(root)
    if a.grade == "tree":
        return cmd_tree(root)
    print("reinstall rebuilds the venv and re-downloads weights (GBs). Do it by hand from tools/gen3d/README.md 'Install'.")
    return 2


if __name__ == "__main__":
    sys.exit(main())
