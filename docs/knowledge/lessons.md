# Lessons to forge into the registry

Measured lessons from this project, waiting to be forged into `C:\Users\kazda\kiro\ai-registry` (game-production /
software-engineering). Each line: date, what happened (measured), the rule it suggests, target subject.

- 2026-10-02 - **A headless coding agent ends its run when it ends a turn, even "waiting for" a background process.**
  Grok 4.7 (T-SUN-001) launched a Unity import in the background, replied "I will read the test result when this import
  exits", and the run stopped at 37 min with the whole package uncommitted; the import then failed on a compile error
  nobody read. Rule: in agent instructions, every long process runs in the foreground and the agent reads its log
  before the turn ends; the runner treats "exit 0 without REPORT" as failed, which caught it. Target:
  agent-operations / unattended-build-loop, software-engineering / subprocess-lifecycle.
- 2026-10-02 - **PowerShell does not wait for GUI-subsystem executables** (Unity.exe), so a "finished" batchmode run
  can leave a partial log (seen in T-TER-003). Rule: `Start-Process -Wait` or a blocking wrapper for Unity batchmode.
  Target: game-production / engine tooling.
- 2026-10-02 - **Unity 6.6 turns `Object.GetInstanceID()` into a compile error (CS0619)** and Input System 1.17 fails to
  compile on 6.6; 1.20.0 works. Rule: pin package versions to the editor's verified set and give agents the API notes.
  Target: game-production / engine upgrade hygiene.
- 2026-10-02 - **Cross-agent task dependencies must wait for the merge, not for the other agent's "done".** Two worktrees
  on separate branches: a dependency counts only once its code is on main and merged into the dependent branch. The
  queue now distinguishes `done/` (same agent) from `merged/` (cross-agent). Target: agent-operations / fleet
  orchestration.
- 2026-10-03 - **An instruction does not fix a runner-level failure mode.** After `AGENTS.md` forbade ending a turn while
  Unity ran, Grok did it again (T-TER-005, 64 turns, "Unity is compiling ... the prefab wire-up runs as soon as that
  compile succeeds"). The fix that holds is in the runner: a clean exit without the required artefact resumes the same
  session (`grok -r <sessionId> -p "continue..."`, verified to keep context) up to twice. Target: agent-operations /
  unattended-build-loop.
- 2026-10-03 - **Letting one agent touch the other app's scene files produced the first real merge conflict** (T-TER-014
  edited `apps/sundial/Assets/Editor/SceneSetup.cs` while Sundial's T-SUN-006 fixed the same lines). The loop's
  pre-task merge aborted cleanly and the host resolved it on main (the owner app's version was the superset). Rule:
  a cross-app change in a task's `touches` should be a request to the owning agent, or serialised behind that agent's
  current task. Target: agent-operations / fleet orchestration (shared-file ownership).
- 2026-10-03 - **Iterative agent art passes plateau at the quality of their source textures.** Three successive Grok
  passes per app (T-TER-006/007/015, T-SUN-006/007/014) moved the frames but every gate frame stayed at a blind Gemini
  median of 2/5; both judge and host named materials (moss, soil, washes) as the gap, not geometry or the engine. A
  single Nano Banana macro generated in 30 s exceeded every moss texture the passes produced. Rule: when a visual
  metric stops moving across passes, change the input (asset source), not the number of passes. Target: game-production
  / art pipeline; llm-observability / judge-calibration (a blind judge as the plateau detector).
- 2026-10-03 - **A blind image judge comparing a real-time render to a photoreal generated reference has its own
  ceiling.** After the Nano Banana material pass the Sundial dial was visibly the closest to its reference so far (host
  read), yet Gemini still scored 2/5, citing "billboards" and photoreal glass the engine target never had. Rule: calibrate
  the judge before using it as a gate - score reference A against reference B (same style, different image) and read
  every frame's score against that ceiling. Target: llm-observability / judge-calibration-and-drift.
- 2026-10-03 - **Unlocked art iterations oscillate.** Over seven Terrarium art passes, each task fixed its named target
  and silently regressed a neighbour (glow lost in T-TER-017, glass fogged in T-TER-018, cleared in T-TER-019, fogged and
  neon again in T-TER-020). Rule: once a look is accepted, lock it (values + the approving frame in a LOCKED file) and
  make every later art task run a per-region regression diff against the approved frame. Target: game-production / art
  pipeline; software-engineering / quality-gates (golden-image regression).
- 2026-10-03 - **Typography is the art pass nobody queues.** Both apps' journey tasks shipped user-facing text in Unity's
  default sans font on grey/black boxes (and literal "?" placeholder glyphs) inside otherwise styled scenes; every art
  task had targeted objects and materials, none named text. Rule: the style bible and the art tasks must include
  typography and UI surfaces, with a "no default font" grep in the gate. Target: game-production / art direction;
  software-engineering / design-tokens.
- 2026-10-03 - **A rubric-driven blind judge turns "looks off" into a named fix.** After calibration, the Terrarium judge
  named the same disqualifier ("neon / harsh bloom") on every draw of every frame, and the Sundial spec measurement put
  one wash at CIEDE2000 16 while the others sat near 7. Those two numbers became the next two task targets directly.
  Rule: give the judge the style bible's rubric with explicit disqualifiers, and log which disqualifier fired; a
  repeated disqualifier is the highest-value art fix. Target: llm-observability / judge-contract-design.
- 2026-10-04 - **A calibrated metric ladder reports when a metric cannot rank the comparison.** F1's ladder (floor = old
  render, ceiling = reference vs reference) put the current Terrarium frame "past the ceiling" on palette EMD and DISTS:
  those metrics reward colour statistics the render happens to share with the reference and cannot rank fidelity here.
  Rule: never adopt a perceptual metric without a ladder; a metric whose current reading lies outside floor..ceiling is
  disqualified for that frame, not celebrated. Target: llm-observability / judge-calibration-and-drift; game-production
  / art evaluation.
- 2026-10-04 - **Pairwise A/B against the reference moves where an absolute rubric does not.** After 30 tasks of "every
  frame scores 2", the first research-driven glass spike won 6 of 6 pairwise draws on the glass region. Target:
  llm-observability / judge-contract-design.
- 2026-10-04 - **An unattended night needs its own keep-awake, and Windows timers do not kill process trees.** The
  machine slept from about 03:10 to 10:30; the hourly host checks did not fire, and a 150-minute task timeout fired 8.7 h
  late on wake, while `child.kill('SIGTERM')` left the agent's children alive. Rule: start a keep-awake that outlasts the
  run every time the operator leaves, and kill with `taskkill /T /F`. Target: agent-operations / unattended-build-loop.
- 2026-10-04 - **A fallback engine must be smoke-tested in the exact headless mode it will run in.** The Gemini fallback
  (`agy -p`) reported SUCCESS on three tasks while every `write_file` was auto-denied (headless cannot prompt), and it ended
  turns "waiting for Unity" like Grok had. Fix: `--dangerously-skip-permissions` and the same same-session continuation
  (`--conversation <id>`). Also: a 402 "balance exhausted" is not a rate limit; it will not clear by waiting. Target:
  agent-operations / unattended-build-loop.
- 2026-10-04 - **Parameter sweeps confirm the plateau; technique changes break it.** F3's first JarG1 sweep (11 variants:
  moss tint, rim power, texture swaps incl. the projection-baked repaint) moved every region bar within the noise band,
  while the S3 technique change (wall-to-wall soil + shell moss) won every pairwise draw. Rule: use sweeps to tune a chosen
  technique, not to search for quality. Target: game-production / art pipeline.

- **2026-10-04 (T-SUN-045) A still-image judge cannot see the billboard defect.** Asked "flat cut-out or volume?", a VLM judge
  called both the camera-facing card plant and the 52-card 3D assembly "volume" in 6 of 6 draws each. The difference only shows
  in an orbit (+-15 deg montage) or a stereo pair. Score depth/volume claims with orbit and stereo evidence, not stills; and a
  frame-level region metric (DISTS/dinov2 over three plants) moves the wrong way when one of three changes - give the changed
  object its own region. Domain: game-production / asset evaluation.

- **2026-10-04 (T-SUN-048) An LLM judge run with the repo as cwd goes exploring instead of judging.** The one unparseable F2
  verdict was the agent narrating while it browsed ladders and rubrics in the repo. Running each judge call in a temp folder
  holding only the neutrally named images, inlining the schema and a "last message is one JSON object" contract, and taking the
  last JSON object with a verdict key gave 0 unparseable in 193 calls. Also a leak risk closed. Domain: llm-observability /
  judge-contract-design.

- **2026-10-04 (T-TER-040..045) Surface spikes do not fix shape.** Five fidelity spikes (glass, thick glass, moss, fronds,
  atmosphere) each won their pairwise vs A, yet the rubric stayed at level 2 throughout. Side by side with the reference, the
  remaining gap was silhouette and mass: jar proportions, cork shape, moss as a mound. Region-scoped spikes and region
  criteria never own the outline. Measure silhouette IoU against the reference first, before surface work.
  Domain: game-production / art direction pipeline.

- **2026-10-04 (T-TER-046) Task-template boilerplate is a context channel - it was gated by slug.** The Tripo key location and
  credit rules lived in a "spike protocol" block appended only to tasks whose slug contained `spike`; the one task that needed
  them was a `tool-` task, so the agent searched the wrong `.env` files and reported the arm blocked. Put resource locations in
  the agent instructions file every task reads, not in conditional template blocks. Domain: software-engineering /
  agent-instruction-files.
- **2026-10-04 (T-TER-046) TRELLIS.2 (MIT ComfyUI port) at 1024_cascade runs on a 24 GB 4090 for compact props.** Hard-surface
  and simple organic props (mushroom, stacked pebbles) come out clean; fuzzy mass (moss clump) faceted on decimation; colour can
  drift (one pebble turned orange). Domain: game-production / generated assets.

- **2026-10-04 (T-TER-034) A Unity 6.6 Windows player built from a batchmode project can ship with the Input System backend
  off.** `Keyboard.current` and `Mouse.current` were null in the player (activeInputHandler 0) though every editor test passed;
  set Active Input Handling to the Input System (2) or Both before the first build and smoke-test one key in the player log.
  Domain: game-production / Unity build pipeline.
