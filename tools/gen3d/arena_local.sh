#!/usr/bin/env bash
# Local TRELLIS.2 arm of the T-TER-046 arena: 3 props x seeds, 1024_cascade. ComfyUI must be running on :8188.
#   bash tools/gen3d/arena_local.sh "42 7" [props...]
set -u
seeds="${1:-42 7}"; shift; props="${*:-mushroom moss_clump pebble_set}"
out=orchestration/runs/terrarium/T-TER-046/local
# the vendored trees must equal pins.json + patches/; a hand edit or a git reset there is caught here, not mid-arena
python tools/gen3d/vendored.py check || { echo "vendored gen3d trees drifted; fix with: python tools/gen3d/vendored.py reset tree" >&2; exit 1; }
for p in $props; do for s in $seeds; do
  [ -s $out/${p}_s${s}_r1024c.glb ] && continue
  python tools/gen3d/gen.py apps/terrarium/Art/Source/gen3d/$p.png $out/${p}_s${s}_r1024c.glb --seed $s --res 1024_cascade
done; done
