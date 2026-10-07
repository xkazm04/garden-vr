# Lessons to forge into the registry

Measured lessons from this project, waiting to be forged into `C:\Users\kazda\kiro\ai-registry` (game-production /
software-engineering). Each line: date, what happened (measured), the rule it suggests, target subject.

## Triage 2026-10-07

Every entry below was triaged against the ai-registry at commit `739f4a866d15783d699dc3b8c82796ce5eeffe8d` (read-only).
Counts: forged 0, declined 18 (prior art 14, project lesson 2, not rule-shaped or not earned 2), lead filed 4 (two
leads, each citing two entries), open 4 (lead filed 4 + untriaged 0). Total 22. A filed lead stays open until the
registry's intake rules on it; the leads are in `.ai/registry-leads.jsonl`, the consults in `.ai/consults.jsonl`.

Search commands used (all against the registry at that commit, never its working tree):

- `git -C <registry> log -1` for the commit; `git -C <registry> show <commit>:knowledge/<domain>/index.json` for
  game-production, software-engineering, llm-observability and agent-operations, flattened to
  domain / subject / technique / use_when rows and searched with `grep -i -E` per entry.
- Subject files resolved only through `subjects[slug].file` in those indexes; techniques read with
  `git -C <registry> show <commit>:<subject dir>/techniques/<slug>.md`.
- Forged check: `git -C <registry> grep -l -F '<phrase>' <commit> -- knowledge` for garden-vr, Terrarium, Sundial,
  CS0619, GetInstanceID, activeInputHandler, TRELLIS, keep-awake, GUI-subsystem, taskkill and others. None of this
  project's lessons is in a bundle (garden-vr appears only in a subject proposal under docs/), so forged is 0.
- Prior art per entry: `git -C <registry> grep -n -i -E "<terms>" <commit> -- knowledge/<domain>` (plateau,
  silhouette, pairwise, approved frame, typography, sleep, template, merge/dependency, ceiling/floor).

- 2026-10-02 - **A headless coding agent ends its run when it ends a turn, even "waiting for" a background process.**
  Grok 4.7 (T-SUN-001) launched a Unity import in the background, replied "I will read the test result when this import
  exits", and the run stopped at 37 min with the whole package uncommitted; the import then failed on a compile error
  nobody read. Rule: in agent instructions, every long process runs in the foreground and the agent reads its log
  before the turn ends; the runner treats "exit 0 without REPORT" as failed, which caught it. Target:
  agent-operations / unattended-build-loop, software-engineering / subprocess-lifecycle.
  Registry: lead filed - software-engineering / fleet-orchestration, nearest technique result-harvest (it classes "completion claimed, result missing" but names no recovery): bounded same-session continuation when a clean exit lacks its declared artifact. One lead with the 2026-10-03 runner entry (registry 739f4a86)
- 2026-10-02 - **PowerShell does not wait for GUI-subsystem executables** (Unity.exe), so a "finished" batchmode run
  can leave a partial log (seen in T-TER-003). Rule: `Start-Process -Wait` or a blocking wrapper for Unity batchmode.
  Target: game-production / engine tooling.
  Registry: declined (a) prior art - software-engineering / packaging / installed-tree-acceptance, whose process application already records it: a windowed-subsystem binary detaches from the console, so launch through Start-Process and assert a positive liveness signal (registry 739f4a86)
- 2026-10-02 - **Unity 6.6 turns `Object.GetInstanceID()` into a compile error (CS0619)** and Input System 1.17 fails to
  compile on 6.6; 1.20.0 works. Rule: pin package versions to the editor's verified set and give agents the API notes.
  Target: game-production / engine upgrade hygiene.
  Registry: declined (b) project lesson - Unity 6.6 and Input System version facts fail the General bar. game-production / engine-pitfall-corpus (incident-entry-shape) governs the entry's shape; the entries themselves live in the consumer's own corpus (here AGENTS.md's API notes) (registry 739f4a86)
- 2026-10-02 - **Cross-agent task dependencies must wait for the merge, not for the other agent's "done".** Two worktrees
  on separate branches: a dependency counts only once its code is on main and merged into the dependent branch. The
  queue now distinguishes `done/` (same agent) from `merged/` (cross-agent). Target: agent-operations / fleet
  orchestration.
  Registry: declined (c) not earned - no measured incident: the entry records a queue design (done/ vs merged/), not a run that broke on an unmerged dependency. Nearest: game-production / unattended-build-loop / completed-with-gaps-excluded-from-the-numerator (registry 739f4a86)
- 2026-10-03 - **An instruction does not fix a runner-level failure mode.** After `AGENTS.md` forbade ending a turn while
  Unity ran, Grok did it again (T-TER-005, 64 turns, "Unity is compiling ... the prefab wire-up runs as soon as that
  compile succeeds"). The fix that holds is in the runner: a clean exit without the required artefact resumes the same
  session (`grok -r <sessionId> -p "continue..."`, verified to keep context) up to twice. Target: agent-operations /
  unattended-build-loop.
  Registry: lead filed - same lead as the 2026-10-02 headless-turn entry (software-engineering / fleet-orchestration, nearest result-harvest). Prose-versus-gate half is prior art in agent-instruction-files / enforcement-demotion; the same-session continuation is the novel half (registry 739f4a86)
- 2026-10-03 - **Letting one agent touch the other app's scene files produced the first real merge conflict** (T-TER-014
  edited `apps/sundial/Assets/Editor/SceneSetup.cs` while Sundial's T-SUN-006 fixed the same lines). The loop's
  pre-task merge aborted cleanly and the host resolved it on main (the owner app's version was the superset). Rule:
  a cross-app change in a task's `touches` should be a request to the owning agent, or serialised behind that agent's
  current task. Target: agent-operations / fleet orchestration (shared-file ownership).
  Registry: declined (a) prior art - software-engineering / fleet-orchestration / parallel-dispatch: each dispatch declares a write set, and admission proves it disjoint from every live session's set; overlap is rejected at dispatch (registry 739f4a86)
- 2026-10-03 - **Iterative agent art passes plateau at the quality of their source textures.** Three successive Grok
  passes per app (T-TER-006/007/015, T-SUN-006/007/014) moved the frames but every gate frame stayed at a blind Gemini
  median of 2/5; both judge and host named materials (moss, soil, washes) as the gap, not geometry or the engine. A
  single Nano Banana macro generated in 30 s exceeded every moss texture the passes produced. Rule: when a visual
  metric stops moving across passes, change the input (asset source), not the number of passes. Target: game-production
  / art pipeline; llm-observability / judge-calibration (a blind judge as the plateau detector).
  Registry: declined (a) prior art - game-production / regeneration-vs-repair-economics / defect-class-to-remedy-map: admit a remedy only for defect classes it measurably cures; an upstream input change is its own remedy (registry 739f4a86)
- 2026-10-03 - **A blind image judge comparing a real-time render to a photoreal generated reference has its own
  ceiling.** After the Nano Banana material pass the Sundial dial was visibly the closest to its reference so far (host
  read), yet Gemini still scored 2/5, citing "billboards" and photoreal glass the engine target never had. Rule: calibrate
  the judge before using it as a gate - score reference A against reference B (same style, different image) and read
  every frame's score against that ceiling. Target: llm-observability / judge-calibration-and-drift.
  Registry: lead filed - llm-observability / judge-calibration-and-drift, nearest golden-set-agreement-measurement (human-label agreement only, no reference-pair ceiling): measure a judge's or metric's floor and reference-vs-reference ceiling before it gates. One lead with the 2026-10-04 metric-ladder entry (registry 739f4a86)
- 2026-10-03 - **Unlocked art iterations oscillate.** Over seven Terrarium art passes, each task fixed its named target
  and silently regressed a neighbour (glow lost in T-TER-017, glass fogged in T-TER-018, cleared in T-TER-019, fogged and
  neon again in T-TER-020). Rule: once a look is accepted, lock it (values + the approving frame in a LOCKED file) and
  make every later art task run a per-region regression diff against the approved frame. Target: game-production / art
  pipeline; software-engineering / quality-gates (golden-image regression).
  Registry: declined (a) prior art - game-production / reference-parity-gating / no-average-hides-a-failure (the subject's scope includes re-renders against an approved frame): the minimum across regions is the headline; a strong region never pays for a broken one (registry 739f4a86)
- 2026-10-03 - **Typography is the art pass nobody queues.** Both apps' journey tasks shipped user-facing text in Unity's
  default sans font on grey/black boxes (and literal "?" placeholder glyphs) inside otherwise styled scenes; every art
  task had targeted objects and materials, none named text. Rule: the style bible and the art tasks must include
  typography and UI surfaces, with a "no default font" grep in the gate. Target: game-production / art direction;
  software-engineering / design-tokens.
  Registry: declined (a) prior art - game-production / aaa-craft-rubric-authoring / criterion-set-coverage-audit: enumerate the deliverable's dimensions independently of the rubric, then map criteria; an uncovered dimension renders as silence, never a pass (registry 739f4a86)
- 2026-10-03 - **A rubric-driven blind judge turns "looks off" into a named fix.** After calibration, the Terrarium judge
  named the same disqualifier ("neon / harsh bloom") on every draw of every frame, and the Sundial spec measurement put
  one wash at CIEDE2000 16 while the others sat near 7. Those two numbers became the next two task targets directly.
  Rule: give the judge the style bible's rubric with explicit disqualifiers, and log which disqualifier fired; a
  repeated disqualifier is the highest-value art fix. Target: llm-observability / judge-contract-design.
  Registry: declined (a) prior art - game-production / reference-parity-gating / findings-carry-the-correction: the gate's primary payload is located, signed findings that tell the producer what to change; the score is derived from them (registry 739f4a86)
- 2026-10-04 - **A calibrated metric ladder reports when a metric cannot rank the comparison.** F1's ladder (floor = old
  render, ceiling = reference vs reference) put the current Terrarium frame "past the ceiling" on palette EMD and DISTS:
  those metrics reward colour statistics the render happens to share with the reference and cannot rank fidelity here.
  Rule: never adopt a perceptual metric without a ladder; a metric whose current reading lies outside floor..ceiling is
  disqualified for that frame, not celebrated. Target: llm-observability / judge-calibration-and-drift; game-production
  / art evaluation.
  Registry: lead filed - same lead as the 2026-10-03 judge-ceiling entry (llm-observability / judge-calibration-and-drift, nearest golden-set-agreement-measurement); this entry supplies the floor..ceiling disqualification rule (registry 739f4a86)
- 2026-10-04 - **Pairwise A/B against the reference moves where an absolute rubric does not.** After 30 tasks of "every
  frame scores 2", the first research-driven glass spike won 6 of 6 pairwise draws on the glass region. Target:
  llm-observability / judge-contract-design.
  Registry: declined (a) prior art - llm-observability / cross-provider-benchmark-operations / target-matrix-runs: when the question is selecting between two candidates, prefer a pairwise design over pointwise scores; it is more sensitive (registry 739f4a86)
- 2026-10-04 - **An unattended night needs its own keep-awake, and Windows timers do not kill process trees.** The
  machine slept from about 03:10 to 10:30; the hourly host checks did not fire, and a 150-minute task timeout fired 8.7 h
  late on wake, while `child.kill('SIGTERM')` left the agent's children alive. Rule: start a keep-awake that outlasts the
  run every time the operator leaves, and kill with `taskkill /T /F`. Target: agent-operations / unattended-build-loop.
  Registry: declined (a) prior art - agent-operations / agent-run-budgeting / ceiling-as-measurement-boundary (host-clock hazards; whole-tree kill is software-engineering / subprocess-lifecycle / termination-and-reaping): a timer firing on wake is a host artefact, requeued, not scored (registry 739f4a86)
- 2026-10-04 - **A fallback engine must be smoke-tested in the exact headless mode it will run in.** The Gemini fallback
  (`agy -p`) reported SUCCESS on three tasks while every `write_file` was auto-denied (headless cannot prompt), and it ended
  turns "waiting for Unity" like Grok had. Fix: `--dangerously-skip-permissions` and the same same-session continuation
  (`--conversation <id>`). Also: a 402 "balance exhausted" is not a rate limit; it will not clear by waiting. Target:
  agent-operations / unattended-build-loop.
  Registry: declined (a) prior art - software-engineering / agent-cli-transport / child-observed-posture: prove the stance a spawned agent actually observed, since a headless wrapper can silently drop permissions; the 402 half is agent-run-budgeting / refusal-detection-and-requeue (registry 739f4a86)
- 2026-10-04 - **Parameter sweeps confirm the plateau; technique changes break it.** F3's first JarG1 sweep (11 variants:
  moss tint, rim power, texture swaps incl. the projection-baked repaint) moved every region bar within the noise band,
  while the S3 technique change (wall-to-wall soil + shell moss) won every pairwise draw. Rule: use sweeps to tune a chosen
  technique, not to search for quality. Target: game-production / art pipeline.
  Registry: declined (a) prior art - same rule as the 2026-10-03 source-texture plateau entry: game-production / regeneration-vs-repair-economics / defect-class-to-remedy-map: a remedy, a sweep included, earns a class only on a measured cure (registry 739f4a86)

- **2026-10-04 (T-SUN-045) A still-image judge cannot see the billboard defect.** Asked "flat cut-out or volume?", a VLM judge
  called both the camera-facing card plant and the 52-card 3D assembly "volume" in 6 of 6 draws each. The difference only shows
  in an orbit (+-15 deg montage) or a stereo pair. Score depth/volume claims with orbit and stereo evidence, not stills; and a
  frame-level region metric (DISTS/dinov2 over three plants) moves the wrong way when one of three changes - give the changed
  object its own region. Domain: game-production / asset evaluation.
  Registry: declined (a) prior art - game-production / reference-parity-gating / instrument-blindness-register: list the defect classes a rig cannot see at any threshold, and give each class a different witness (registry 739f4a86)

- **2026-10-04 (T-SUN-048) An LLM judge run with the repo as cwd goes exploring instead of judging.** The one unparseable F2
  verdict was the agent narrating while it browsed ladders and rubrics in the repo. Running each judge call in a temp folder
  holding only the neutrally named images, inlining the schema and a "last message is one JSON object" contract, and taking the
  last JSON object with a verdict key gave 0 unparseable in 193 calls. Also a leak risk closed. Domain: llm-observability /
  judge-contract-design.
  Registry: declined (a) prior art - agent-operations / blind-judging-of-agent-runs / sealed-judge-workspace: blinding an agent judge is a property of its filesystem; stage a workspace holding only the brief and entries (registry 739f4a86)

- **2026-10-04 (T-TER-040..045) Surface spikes do not fix shape.** Five fidelity spikes (glass, thick glass, moss, fronds,
  atmosphere) each won their pairwise vs A, yet the rubric stayed at level 2 throughout. Side by side with the reference, the
  remaining gap was silhouette and mass: jar proportions, cork shape, moss as a mound. Region-scoped spikes and region
  criteria never own the outline. Measure silhouette IoU against the reference first, before surface work.
  Domain: game-production / art direction pipeline.
  Registry: declined (a) prior art - game-production / reference-parity-gating / dual-anchor-scoring: score against two anchors from different authorities, the profile anchor owning shape, so no wrong artifact satisfies both (registry 739f4a86)

- **2026-10-04 (T-TER-046) Task-template boilerplate is a context channel - it was gated by slug.** The Tripo key location and
  credit rules lived in a "spike protocol" block appended only to tasks whose slug contained `spike`; the one task that needed
  them was a `tool-` task, so the agent searched the wrong `.env` files and reported the arm blocked. Put resource locations in
  the agent instructions file every task reads, not in conditional template blocks. Domain: software-engineering /
  agent-instruction-files.
  Registry: declined (a) prior art - software-engineering / fleet-orchestration / brief-carries-the-session: a fresh worker gets only the brief and maybe standing files, so briefs use a fixed-section template where an empty section shows (registry 739f4a86)
- **2026-10-04 (T-TER-046) TRELLIS.2 (MIT ComfyUI port) at 1024_cascade runs on a 24 GB 4090 for compact props.** Hard-surface
  and simple organic props (mushroom, stacked pebbles) come out clean; fuzzy mass (moss clump) faceted on decimation; colour can
  drift (one pebble turned orange). Domain: game-production / generated assets.
  Registry: declined (c) not rule-shaped - a capability observation about one reconstruction tool on one GPU, with no trigger or action. Nearest: game-production / image-to-3d-input-gating (registry 739f4a86)

- **2026-10-04 (T-TER-034) A Unity 6.6 Windows player built from a batchmode project can ship with the Input System backend
  off.** `Keyboard.current` and `Mouse.current` were null in the player (activeInputHandler 0) though every editor test passed;
  set Active Input Handling to the Input System (2) or Both before the first build and smoke-test one key in the player log.
  Domain: game-production / Unity build pipeline.
  Registry: declined (b) project lesson - a Unity player-settings pitfall fails the General bar; the general half (a liveness smoke does not prove input is handled) is already in game-production / ship-pipeline-gating / post-cook-process-liveness-smoke. The entry belongs in the consumer's own corpus (engine-pitfall-corpus shape) (registry 739f4a86)
