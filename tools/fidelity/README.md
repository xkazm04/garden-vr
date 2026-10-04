# Fidelity lab (F1)

Offline metrics for the JarG1 and DialG1 gates. The tool refuses to print a
distance that has no ladder. Judge, sweep, and sheet are later tasks.

## Setup

From the repo root, with Python 3.12:

```
uv venv tools/fidelity/.venv --python 3.12
uv pip install --python tools/fidelity/.venv/Scripts/python.exe torch torchvision --index-url https://download.pytorch.org/whl/cu124
uv pip install --python tools/fidelity/.venv/Scripts/python.exe -r tools/fidelity/requirements.txt
uv pip install --python tools/fidelity/.venv/Scripts/python.exe torch torchvision --index-url https://download.pytorch.org/whl/cu124
tools/fidelity/.venv/Scripts/python.exe -c "import torch; assert torch.cuda.is_available()"
```

The second torch install puts the CUDA wheel back if the metric packages
replaced it with a CPU build. A CPU torch is a failed setup: DreamSim is about
50x slower and the tool exits.

`color-matcher` is GPL-3.0. It stays in this offline venv and is not shipped
in the app.

## Commands

`tools/fidelity/fid.cmd` runs the venv interpreter.

```
fid.cmd ladder JarG1|DialG1 [--force] [--evidence <dir>]
fid.cmd metrics <run-dir> --frame JarG1|DialG1
fid.cmd metrics --self-test
fid.cmd metrics --bare
```

`--bare` exits 2. `metrics` exits non-zero when that frame has no ladder file.
`--self-test` checks the Sharma CIEDE2000 pair, the locked dial wash numbers,
the jar spec-anchor windows, the inversion rule, and a GPU hue-shift / 2 px
shift pair.

## Ladder

Lower is closer. Position 0 is the floor and position 1 is the ceiling:

```
(floor - distance) / (floor - ceiling)
```

Rungs, each against reference 1:

| Rung | Pair |
| --- | --- |
| ceiling | reference 1 vs reference 2 |
| current | reference 1 vs the locked gate frame |
| level2 | reference 1 vs the round-3 render |
| floor | reference 1 vs the round-2 render |
| noise | 90th percentile of the three re-renders and of JPEG q75 + 2 px |

If the floor distance is below the ceiling distance, the row is `INVALID`
and has no position. That inversion is a finding. A current that only misses
the order by less than the noise band is `within_noise`.

Families are never summed:

* Look: DreamSim `dino_vitb16` (ensemble DreamSim on the full frame only) and DINOv2 ViT-B/14 CLS cosine, stored as `1 - cosine`. DINOv3 is not used.
* Palette: Lab sliced Wasserstein (POT, stride sample, 32 projections, seed 0) and CIEDE2000 between region medians. Bible-hex CIEDE and the jar glow window are absolute stamps, not pair rungs.
* Structure: DISTS on the masked crop. FLIP, MS-SSIM, and LPIPS are render-versus-render only (re-renders, and the self-test). LPIPS is not used as a reference distance.

`ladders/<frame>.json` stores image hashes, package versions, and ladder
version `f1-1`. An unchanged stamp is reprinted, not recomputed. `--force`
recomputes.

Region polygons live in `regions/<frame>.json` (1824x1024). Rebuild them
with `python tools/fidelity/lab/build_regions.py`.

## Catalog

`catalog.json` is the rung list. The round-2 jar still is letterboxed because
it is not the JarG1 camera. The round-2 dial still is 912x512 and is nearest
neighbour doubled. A per-region inversion on those rungs is a framing finding.
