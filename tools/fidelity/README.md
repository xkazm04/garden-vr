# Fidelity lab (F1 metrics, F2 judge, F3 sweep and sheet)

Offline metrics for the JarG1 and DialG1 gates. The tool refuses to print a
distance that has no ladder.

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

## Sweep and sheet (F3)

```
fid.cmd sweep sweeps/jar-moss.json --out <dir>
fid.cmd metrics <dir> --frame JarG1
fid.cmd judge --pairwise --frame JarG1 --player id=path ... --out <dir>/judge.json   (optional)
fid.cmd sheet <dir> --frame JarG1 [--judge <dir>/judge.json] [--region <name>] [--out <png>]
```

`sweep` runs `CaptureCli.Sweep` once (spec format in `shared/packages/com.gardenvr.capture/README.md`), then fails
unless the log has `[Capture] Sweep OK`, base-first vs base-last FLIP is under the ladder's frame noise band
(`alignedNoise.flip.frame.band`), and `git status` shows no new change under the app's `Assets/`. It writes
`sweep.check.json`. Unity rewrites a few `ProjectSettings/*.asset` files when it opens a project; that is not an
asset change and is not checked (`git checkout -- apps/<app>/ProjectSettings` afterwards).

`sheet` reads `sweep.json` and `metrics.json` and writes `sheet-<frame>.png` and `sheet.json` in the sweep folder.
Reference and the locked frame (the sweep base) on top; one tile per variant with its axis label, rank-average,
BT score and disqualifiers (when `--judge` is given), and three bars per look region (look, palette, structure
ladder position; white tick is the locked frame); then 1:1 crop strips of the weakest region (lowest look
position on the locked frame) and of the spec's `focusRegion`. Ordering is the average of the three family ranks
(never a weighted sum), or Bradley-Terry when judged. A variant is `pruned` when any region/family median
position is below the locked frame by more than that cell's noise band (floor 0.02). `sheet.json` leaves
`chosen_by` null for the owner and sets `auto_picked` only from a judge file whose top player beats `locked`.
Region crops larger than 456 x 320 are centre-cropped (marked on the sheet). No `sheet.html`.

## Judge

`judge/packet-v1/` is the frozen packet: instructions, the section 7 rubrics,
the closed defect vocabulary, the JSON schemas, and the pinned model
`gemini-3.8-flash-high`. A change of those files starts a new packet series.
The packet hash is sha256 over those files in manifest order.

```
fid.cmd judge --self-test
fid.cmd judge --calibrate [--frame JarG1|DialG1] [--draws 10] [--jobs N] [--evidence <dir>] [--fresh]
fid.cmd judge --pairwise --frame JarG1|DialG1 --player id=path [--player id=path ...] [--criterion overall] [--crops] [--skip-disqualifiers] --out pair.json
fid.cmd judge --regrade --ledger calls.jsonl [--check]
fid.cmd judge --probe [--frame JarG1|DialG1]
```

Each criterion is one call. Each pair runs in both image orders, staged under
neutral file names. It is a win only when both orders name the same image.
Disagreement is a tie. The inversion rate is those disagreements divided by
pairs where both orders answered A or B. A missing or unparseable reply is
`ungraded` and is omitted from Bradley-Terry (`choix.ilsr_pairwise`, alpha
0.01), so it is never a loss. The locked gate frame is always a player.
A player with no agreed game stays in the list with no fitted strength.

Agy wraps a valid object with `toolAction` and `toolSummary`. Those two keys
are stripped before the schema check. Any other extra key is `ungraded`.

Every verdict stores the packet hash and the sha256 of the reference and the
two candidates. `--regrade` reads those stored files. `--check` rebuilds the
ranking from the ledger and does not call the model. A changed or missing
file is `ungraded`. The judge does not generate images.

`--calibrate` asks whether reference 2 is closer to reference 1 than the
locked frame, and whether the locked frame is closer than the round-2 floor.
Each check is 10 draws in both orders. A check passes at 9 of 10 agreed wins.
The gate can fail. The summary prints the rates either way. Owner picks in
`sheet.json` (`chosen_by: owner` and `judge_winner`) are appended to
`judge/golden.jsonl`. Cohen's kappa is reported at 30 labels, floor 0.6.
Until the gate passes and kappa meets that floor, the judge is advisory.

`--crops` is used only when the criterion name is a region and every image
is already 1824x1024. Crops are padded to at least 256 px on the short side.

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
