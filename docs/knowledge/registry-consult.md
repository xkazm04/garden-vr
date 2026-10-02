# Registry consult - Garden VR plan (2026-10-02)

Registry: `C:\Users\kazda\kiro\ai-registry` (read `README.md`, `knowledge/README.md`). Every subject below was resolved
through `knowledge/<domain>/index.json` -> `subjects[slug].file`; digests and revisions are from those indexes on
2026-10-02 (all prefixed `sha256-b2:`). Domains: `game-production` (54 subjects), `software-engineering`,
`agent-operations` (all subjects there have status `draft`). `localization` was not consulted: localisation is post-MVP
(each app plan, section 9); consult `czech`, `german`, `japanese`, `translation-quality-measurement` when it is queued.

## 1. Subjects consulted and what each changes in the plan

### Art, assets, judgment (game-production)

| Subject (file) | Digest / rev | What changes the plan | Where applied |
|---|---|---|---|
| `reference-parity-gating` (`knowledge/game-production/content-pipeline/reference-parity-gating/reference-parity-gating.md`) | `a18e94679e7706ee` / 1 | `dual-anchor-scoring`: the AI reference frames own look and composition, a second authority owns measurable quantities -> each style bible has a **spec anchor** table. `register-once-from-the-invariant`: fixed camera from the jar base / dial centre, translation only. `no-average-hides-a-failure`: the **minimum** across frames and regions counts. `findings-carry-the-correction`: art tasks emit located corrections, not a score. `instrument-blindness-register`: list what a still capture cannot see (boil over time, stereo, other head poses) | PLAN gate A1-A3; art bibles 5-7; art tasks |
| `generative-artifact-gating` (`.../content-pipeline/generative-artifact-gating/generative-artifact-gating.md`) | `4544d39d7c46caf3` / 5 | `acquired-is-not-generated` + `placeholder-is-not-an-asset`: textures cropped from reference frames are stand-ins, never green -> **gate A4 (no borrowed pixels)**. `gate-before-every-credit-spend`: view a generated image at headset scale before it feeds Blender or a bake. `auto-picked-vs-human-chosen-provenance`: record who picked | PLAN 7 (image generation); A4 |
| `shader-budget-authoring` (`.../surface-and-imagery/shader-budget-authoring/shader-budget-authoring.md`) | `d1c4f6c93cb669e0` / 2 | Hard and soft caps handed to the author up front; cost each shader against a cheaper swap (fresnel glass vs grab pass; halo card vs bloom); profile feature **pairs** (glass + halo cards) | PLAN A5; app plans section 6 |
| `asset-class-poly-budgeting` (`.../geometry/asset-class-poly-budgeting/asset-class-poly-budgeting.md`) | `83d174ba84bd1c83` / 2 | Triangles are the unit (a Blender quad count is about half); divide the scene budget across parts from the whole | Blender tasks report triangles |
| `generated-mesh-acceptance` / `mesh-finishing-for-engine-readiness` | `2f6134a34d83a7ab` / 1, `3c732aa8444e83a9` / 3 | Scripted meshes record algorithm + parameters; `headless-dcc-capability-limits`: probe every `bpy` op in `-b` mode (ops can silently no-op); one normal authority per consumer (inverted-hull outline needs smoothed normals) | Blender steps in T-TER-006, T-SUN-005 |
| `sprite-and-atlas-production` / `tiling-texture-acceptance` | `7e555dae277291eb` / 1, `79554b00c93b3e3e` / 1 | Boil frames and halos are a **set** (palette, pivot, timing); ASTC on flat ink art grows haloes, measure against source; wrap-around edge diff on every "tileable" image_gen output | T-SUN-006/007, T-TER-006/007 |
| `regeneration-vs-repair-economics` (`.../sourcing-economics/regeneration-vs-repair-economics/regeneration-vs-repair-economics.md`) | `a1a7192b63247062` / 2 | Classify a defect before re-rolling (a method gap is not fixed by more draws); bounded refine loops with a recorded best | art tasks: "keep the best attempt, never just the last" |
| `quality-verdict-integrity` (`.../craft-judgment/quality-verdict-integrity/quality-verdict-integrity.md`) | `bea58008d7738651` / 3 | Bind each judge verdict to the render's content hash; payload = artifact + reference + rubric only (never the agent's intent); a judge stays provisional until calibrated against owner labels; an unprovable verdict may condemn, never elevate | PLAN A3 (judge is advisory; owner A1 decides) |
| `aaa-craft-rubric-authoring` / `judgeable-spec-authoring` | `4739d012b0a4422d` / 4, `93856c6f119f365d` / 2 | Narrow anchored levels instead of x/10; capped disqualifiers; different model family for producer and judge; median of several draws | art bibles section 7; A3 uses Gemini, never Grok |

### Audio, runtime evidence, teaching, production (game-production)

| Subject | Digest / rev | What changes the plan | Where applied |
|---|---|---|---|
| `spatial-audio-scene-authoring` (`.../motion-and-audio/spatial-audio-scene-authoring/spatial-audio-scene-authoring.md`) | `0d2b9b42f9ee07bb` / 1 | A cue catalog with trigger, priority band, 2D/3D, concurrency, cooldown; protect narration by **one** mechanism (duck, not also reserve); mix to a stated integrated loudness, not to taste | AUDIO-BIBLE 2, 3, 5 |
| `adaptive-music-authoring` | `00e2e60b733dd108` / 2 | Calm beds are declared unpulsed; loop seams fold the decay tail; acceptance beyond "it decoded"; music only via the music endpoint | AUDIO-BIBLE 6 |
| `game-dialogue-voice-pipeline` | `2b9fce8d0d8cbaed` / 1 | Baked lines with stable ids, a revision diff, a cast ledger; duration budget against the **measured** take, reword before speed-up | AUDIO-BIBLE 4 |
| `perf-regression-gating` | `68ba78f2a478ee89` / 1 | A PC frame time is not a Quest number: gate deterministic counts (draws, tris, overdraw) now, mark Quest perf "not measured" | PLAN A5 |
| `runtime-observation-evidence` | `a73d7f7ec0bfa440` / 3 | Fixed timestep stated beside every number; behavioural discriminators (sample the uncoil curve) over a bare `State == Complete`; "unverifiable" is not "fail" | PlayMode tests in the queues |
| `learning-curve-and-teaching-design` | `ad86d31cbebbbb39` / 1 | Skill atoms ("hold = inhale", "the fern answers my breath", "a glance selects", "yesterday can be logged") each need introduce / practise / test sites; scripted runs prove wiring, never learnability | PLAN U2 (a human first run is the witness) |
| `playtest-signal-to-defect` | `3ecfd50d6322e70f` / 1 | Instrument sessions before the first one; keep observation separate from interpretation | owner review notes template (R1, R2, G) |
| `engine-pitfall-corpus` | `85a1ed1dbfd42e73` / 3 | Probe the **effect** in the exact batchmode mode (APIs resolve but stay inert); give agents real asset paths | tasks: captures read pixels, audio checks via offline mixdown |
| `unattended-build-loop` | `9206627385bc870d` / 5 | Verified and self-reported pass rates never share a field; credit reservation with drain-not-kill; preflight the environment; rollback to last green | PLAN 5 (host verification), 7 (credits) |
| `design-canon-as-executable-law` | `398ce42cd0bdc279` / 5 | Thresholds (breaths, pace, arcs, budgets) live in one place that tests read | constants live in core config classes; tests assert them |
| `production-work-prioritization` | `05b4ac47dcf9f7f8` / 3 | Vertical slice first; report the slice by step name | M1 per app before art breadth |
| `acceptance-verdict-spine` | `cc46738c751b13c7` / 3 | A verdict is `{status incl. not-measured, tier, reason}`; report the first failing member; "Quest-ready" cannot pass while Quest perf is not measured | `GATE.md` row shape |

### UX and engineering (software-engineering)

| Subject | Digest / rev | What changes the plan |
|---|---|---|
| `guided-tours` | `6f44ad03ebd5bd19` / 4 | Tour steps advance on the real intent, never a timer; stable step ids, resumable; audio failure never fails the tour; anchors by declared id (first runs in both plans) |
| `motion` | `8d9b3eabe2f3b534` / 10 | Reduced motion designs each preset's reduced form; **breath timing survives reduced motion**; content-bearing motion (the uncoil) shows its end state; one merged pause signal (art bibles section 4) |
| `async-ui-states`, `session-resume` | `c71d22c8deba8577` / 4, `50cb543ed324e4fe` / 4 | A failed load is not a first run; a ritual cut off is offered back, never auto-restarted; silence after a gap is designed |
| `voice-io` | `b7d28920c42693f8` / 19 | Discoverable but quiet: the first ritual is voiceless and the guide is offered after the answer (PLAN D6); one voice at a time; everything spoken also exists as text |
| `design-tokens`, `accessibility` | `165125b7a54d0de3` / 7, `5e30485ee0c7c123` / 7 | Colours by role tokens; a contrast floor for etched text; every intent reachable by a second path (hold-or-toggle setting, Tab focus); hidden objects stop receiving Look and Poke |
| `undo-history` (+ `batch-undo-commit-window`) | `6b331b14c940f6a7` / 3 (+ `140b51387ec6d463` / 1) | Undo is a deferred write (cancel a timer, cannot fail), sits at the tile, flushes on pause / teardown |
| `test-harness` | `a9235b11f7a38d4d` / 24 | Drive through intents, not coordinates; fresh save path per first-run test; a negative control for "growth only rises"; PC vs Quest is an uncrossed axis reported as not covered |
| `settings`, `embedded-db`, `migrations` | `85c48ec5bdb5581f` / 13, `0722fec97cff5b66` / 15, `b7d5afcb859dfff9` / 10 | Closed settings keys, defaults never written; rotate a backup then replace atomically; schemaVersion with append-only steps, snapshot before migrating, a newer file opens read-only |
| `status-vocabulary`, `proactive-nudges` | `bea7b30fd48d528d` / 5, `5e13c88d70a4c50a` / 5 | Tile states are one closed enum with labels from a catalog; no nudges in v1 (any "dusk is near" glow is a nudge with a budget) |

### Orchestration (agent-operations, software-engineering, game-production)

| Subject | Digest / rev | What changes the plan |
|---|---|---|
| `unattended-run-isolation` (`knowledge/agent-operations/run-safety/unattended-run-isolation/unattended-run-isolation.md`) | `3a58da20288a8c2a` / 3 | Diff from the recorded base commit; each worktree its own `Library/`; hermetic environment for the child process |
| `deterministic-run-verification` | `ac3b017c4d37ee49` / 2 | Restore oracles from base before grading; baseline-relative test results; read results XML counts; `git check-ignore` on committed paths |
| `harness-fault-attribution`, `agent-run-budgeting`, `model-and-effort-selection`, `engine-behaviour-profiles` | `e2d4c798566b4d95` / 2, `3a4d0d6d28c6b615` / 2, `d5dd3456b7c8cb93` / 2, `40f0d360f0af1a7a` / 2 | Environment first when both agents go red; one termination cause per run; a quota refusal pauses the service, not the task; pin model and effort on every launch; fix the instruction before escalating |
| `concurrent-vcs`, `machine-paced-delivery`, `quality-gates`, `agent-instruction-files`, `multi-project` | `c8742871bd3cb036` / 5, `7c45f5d81cc35bf6` / 9, `75f3f7dd88cf3601` / 52, `25b61236a62604b0` / 31, `07b32de44fd25fc6` / 3 | Worktrees beside the repo; confirm each merge by reading the log; reserved change classes go to the owner; oracles frozen during repair; gate liveness (collected count > 0); every hourly check ends observed-changes / observed-quiet / could-not-observe |

## 2. Deviations to record (the standard stands; this project falls short here)

| # | Standard | This project | Consequence | Mitigation / when it closes |
|---|---|---|---|---|
| DV1 | `production-work-prioritization`: vertical slice before breadth | Two apps built in parallel (owner decision) | Attention split; neither may reach "beautiful" | Each app's M1 is a vertical slice; the gate parks a failing app |
| DV2 | `unattended-run-isolation/os-enforced-run-boundary`: low-privilege user or VM, network allowlist | Grok runs as the operator on Windows; boundary is a worktree + prose | An agent can write outside its worktree | Host checks diffs against `touches:`; Phase 2 may move agents to a separate Windows user |
| DV3 | Paid-API keys behind a proxy that caps spend | `ELEVENLABS_API_KEY` in `.env`, read by `tools/audio/elevenlabs.mjs` in the agent's process | An agent could call the API directly | Guard reserve 8,000, per-app pots, ledger audit at every hourly check |
| DV4 | `machine-paced-delivery`: human review before landing on `main` | The host (a model) merges hourly | Machine check, not human review | Reserved change classes go to the owner (PLAN 5); owner reviews R1, R2, G |
| DV5 | `deterministic-run-verification`: tasks advance only on verified results | `agent-loop.mjs` moves a task to `done/` when `REPORT.md` exists, and dependants may start | Downstream work on an unverified base | Host marks `verified:` / `rejected:` and never merges unverified work; a rejected upstream re-queues its dependants. Suggested tool change: a `verified/` state the loop's dependency check reads |
| DV6 | `reference-parity-gating/dual-anchor-scoring`; `quality-verdict-integrity` | Round 3 scored with one uncalibrated Gemini draw and cropped reference pixels | Self-certifying parity | Spec anchor + owner anchor + calibrated judge (PLAN A1-A3); A4 bans borrowed pixels |
| DV7 | `perf-regression-gating`: device baselines | No headset until Phase 2 | Quest perf unknown at the gate | Gate only deterministic counts (A5); H2 measures with OVR Metrics |
| DV8 | `accessibility` covers screens, weakly XR | Seated hands-only XR | Coverage gap of the standard, not of the app | Recorded; intent-equivalence matrix proposed as a lesson (section 3) |

## 3. Forging plan - lessons this project should give back

Each lesson names its target subject, the milestone that produces the evidence, and what the evidence must contain.
Evidence stays consumer-side (paths in this repo); the published lesson carries no repo paths.

| # | Lesson (proposed rule) | Domain / subject | Milestone | Evidence needed |
|---|---|---|---|---|
| L1 | **Pixels borrowed from the parity reference make parity self-certifying**: a texture cropped from the reference is scored as excluded or capped | game-production / `reference-parity-gating` | M2 (both apps, 10-13) | Same scene scored with round-3 crops vs authored textures under a fixed judge payload; owner scores for both; the acquisition records |
| L2 | **Judge calibration for stylised parity**: a judge model's agreement with owner labels on real-time renders vs image-model frames | game-production / `quality-verdict-integrity` | R2 (10-16) and G (10-23) | >= 30 owner labels across levels, chance-corrected agreement with a CI, median-of-3 scores, payload ablation (with vs without the agent's intent) |
| L3 | **Scripted keyboard input as a stand-in for hands has an evidence ceiling**: it proves wiring and state, never learnability or feel | game-production / `learning-curve-and-teaching-design` + software-engineering / `test-harness` | G (10-23) then H1 (10-27) | Per skill atom: scripted pass status vs the owner's first-run time-to-competence on PC and then on Quest hands (small n, labelled) |
| L4 | **Narration timed to a breath cadence**: windows bound to the user's hold, triggered by intents, reword before speed-up | game-production / `game-dialogue-voice-pipeline` | M4 (10-17) | Line catalog with windows vs measured takes; overruns fixed by rewording vs speed; voice and model ids; owner listening notes |
| L5 | **Credit-metered generative audio on a fixed plan** (estimate vs billed per kind, re-render churn, endpoint per kind) | game-production / `unattended-build-loop` (budget technique) | M4 + end of Phase 1 | `tools/audio/ledger.jsonl` summarised: estimated vs billed per kind, churn share, refusals and their shapes |
| L6 | **Desktop stand-in perf gating for standalone XR**: which deterministic counts predict device cost | game-production / `perf-regression-gating` | H2 (11-03) | PC counts (draws, tris, overdraw) for each build vs OVR Metrics GPU time on Quest 3, dated, n builds |
| L7 | **Single-writer ownership of shared packages, enforced at landing** | software-engineering / `concurrent-vcs` | end of Phase 1 (10-23) | Count of foreign-path diffs attempted vs refused over >= 30 tasks; incidents where the prose rule alone failed |
| L8 | **Unity per-worktree `Library/` and the single-instance lock as a resident shared resource** | software-engineering / `concurrent-vcs` (`shared-resource-arbitration`) | M0-M1 | Cold import minutes per worktree, lock collisions, environment-caused batchmode failures by signature |
| L9 | **A Grok CLI engine profile** (end-of-task defaults, authority conflicts, served-model reporting, effort on resume) | agent-operations / `engine-behaviour-profiles` | after 20 runs (~10-08) | Init records and transcripts over >= 20 runs; contrasted with a Claude run on the same task where available |
| L10 | **Angular motion budgets and intent equivalence for seated XR** (motion budgets in degrees; every intent reachable by a second path; breath timing survives reduced motion) | software-engineering / `motion`, `accessibility` | G (PC), H2 (Quest) | Measured travel in degrees at desk distance; a matrix test of alternate paths; reduced-motion cadence test; owner comfort notes |
| L11 | **Inverted-hull outlines need a second normal authority** (smoothed normals for the hull beside the shading normals) | game-production / `mesh-finishing-for-engine-readiness` | M2 Sundial (10-14) | Before/after renders of hull cracks at hard edges; the headless Blender probe of the bake op |
| L12 | **Temporal NPR (boiling line) is invisible to still-frame parity** | game-production / `reference-parity-gating` (`instrument-blindness-register`) | M2 Sundial + H2 | Per-frame palette and line-width drift across the 10 fps cycle; a case where still parity passed and the animation read as noise (or did not) |

Contribution path: after each milestone above, the host writes the evidence summary into
`docs/knowledge/lessons/<id>.md` (consumer-side) and opens the registry change through the registry's own process
(`/forge` or a pull request, `CONTRIBUTING.md`), never by editing the registry checkout directly from an agent task.
