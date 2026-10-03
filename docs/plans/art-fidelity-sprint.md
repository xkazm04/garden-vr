# Art fidelity sprint (from 2026-10-04)

Owner, 2026-10-03: "I don't see the graphical quality and fidelity. We need to work on this side to chase agreed art
quality and try to finetune ways to generate and improve assets, there are not many in this stage of the game and we need
to improve our skillset, research topics, extend and build tooling if needed."

Why the change: 63 tasks merged in a day, but every gate frame still scores 2 against a calibrated ceiling of 4. The
art passes plateaued at the quality of their inputs (Blender-scripted primitives + single generated textures + iterative
shader tweaks). More passes of the same kind will not close the gap.

| Phase | What | Output |
|---|---|---|
| 1. Research | Three dossiers: Terrarium rendering (glass, moss, glow, mist), Sundial NPR in reality (watercolour, ink, drawn volume), asset pipeline + evaluation tooling (image-to-3D, texture-to-material, perceptual metrics, registry consult) | `docs/research/*.md` |
| 2. Tooling | A fidelity lab: reference-matched look-dev capture, perceptual metrics + pairwise judge, parameter sweeps and variant grids, contact sheets for the owner; an asset-generation path (image to 3D or projection, bake, Unity) | `tools/fidelity/`, `tools/assetgen/` |
| 3. Technique spikes | 3-5 A/B experiments per app from the dossiers, each judged against the reference by metrics and the owner | `orchestration/runs/*/T-*-spike-*` |
| 4. Adopt | The owner picks the winning techniques; they become the art pipeline and the locked looks | `LOCKED.md`, style bibles |

Feature work is on hold (`orchestration/hold/`) until phase 4 lands.
