# Budgets: milestone 4, Quest 3 and Quest 3S

`terrarium.json` and `sundial.json` hold each app's budget for the two target headsets (decision 0010). **No value here
was measured on a headset.** Every value carries `measuredOnDevice: false`. H2 (2026-11-03, `docs/PLAN.md:67`) is the
first headset reading, and it is where these numbers meet OVR Metrics.

## Rules

- Every number has a source in the file's `sources` map. A source is one of three kinds. `upstream` is a dated Meta
  developer page, with its URL, the page's own last-updated date and the date it was read (2026-10-07). `registry` is
  a subject of the ai-registry game-production index. `project` is a line of this repo. A number built from others
  shows the arithmetic in `derivation`.
- Where Quest 3 and Quest 3S differ, both values are given, and the lower one binds (`docs/PLAN.md` D10, decision 0010).
- A number with no source is `null`, and its reason is listed in `nulls`. None is invented.
- `MiB` is 1048576 bytes and `GiB` is 1073741824 bytes. Meta states its limits in GiB, so the brief's "MB" is kept
  binary here, and each ceiling also gives its exact `hardBytes`.
- Soft caps are 80% of the hard cap, which is the PLAN A5 rule (`docs/PLAN.md:112`, "hard caps; soft caps at 80%").

## Fields

| Field | What it holds | Source |
|---|---|---|
| `frameTime.perRate[]` | Frame time in ms at each refresh rate both headsets offer: 72, 80, 90, 100 and 120 Hz, as 1000 / Hz. Quest 3 also runs above 120 Hz (up to 240 Hz). Quest 3S cannot, so those rates are not targets. The default rate is 72 Hz. | `meta-refresh-rates` (page updated 2026-10-01), which gives 13.9 ms at 72 Hz and 8.3 ms at 120 Hz. The 80, 90 and 100 Hz values are derived the same way. |
| `gpuFrameTimeMs` | `null`. No source splits the frame between CPU and GPU for these headsets. | Needs the H2 reading. |
| `appMemory` | The app memory ceiling: the PSS kill limit, 5.75 GiB = 5888 MiB, the same on both headsets. Soft cap 4710.4 MiB. | `meta-memory-ram` (updated 2025-08-12) and `meta-quick-start` (updated 2026-09-09, "Target well below these limits"). Soft cap from `plan-a5`. |
| `textureMemory` | The GPU texture-memory ceiling: 25% of the app ceiling = 1472 MiB, soft cap 1177.6 MiB. | Derived. The justification is below. |
| `display` | Per-eye resolution on each headset (Quest 3 2064x2208, Quest 3S 1832x1920). Both use the same XR2 Gen 2 GPU. | `meta-compare-devices` (updated 2026-09-30) |
| `caps.draws`, `caps.tris` | Terrarium: 40 draws and 60k tris. Sundial: 30 draws and 30k tris. Soft caps at 80%. | `plan-a5` |
| `caps.transparentLayersOverJar` | Terrarium: a mean of 1.5 transparent layers over the jar. Sundial: `null`, because PLAN A5 sets no cap for it. | `plan-a5` |
| `caps.textureMaxSidePx` | 1024 px. The soft cap is `null`: 80% of 1024 px is 819.2 px, which is not an import size. | `plan-a5` |
| `caps.postProcessing` | `allowed: false` | `plan-a5` |

## Why texture memory gets a quarter

Textures share the PSS pool with everything else the process keeps. The registry subject `render-submission-economy`
puts it this way: residency is "a memory pool that the graphics processor shares with everything else the process
holds". So the texture ceiling is a share of the app ceiling, not a separate pool. The subject
`shader-budget-authoring` asks for a budget to state the share it may consume and the headroom it leaves.

These apps keep four resident classes:

1. the engine and managed heap;
2. render targets: two eye buffers and the passthrough composition;
3. meshes and audio;
4. textures.

No device reading exists for any of them, so each class gets an equal quarter. Textures may consume up to 25% of the
hard ceiling, which leaves 75% for the other three. The share was set in the commit before the first texture-memory
reading, so it is not fitted to that reading. H2 replaces the four quarters with measured PSS.

The PSS limit is the same on both headsets, so the texture ceiling is the same on both.


## Readings

`node tools/assets/census.mjs --only texmem [--rev <commit>]` writes `TEXTURE-MEMORY.md` and `texture-memory.json`
here. It compares each app's imported texture bytes (ASTC 6x6, mip chain, the rules in `tools/assets/texmem.mjs`) with
`textureMemory.hardBytes` in that app's file at the same commit, and prints pass or fail. It names the commit and the
tree ids it read. Two runs at one commit are byte-identical.
