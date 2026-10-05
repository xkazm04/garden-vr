# Agent guide - Garden VR

Read this first, every task. It is short on purpose; the detail lives in `docs/`.

## What you are building

Two Unity 6.6 URP apps (`apps/terrarium`, `apps/sundial`) that share packages in `shared/packages/`. Phase 1 is
**PC-first**: everything runs on Windows with keyboard and mouse standing in for hands. There is **no Quest headset**;
never plan around one, never touch any ADB device (the one on the network, `10.0.0.139:5555`, is a Fire TV).

Your plan: `docs/plans/<your-app>.md`. The programme: `docs/PLAN.md`. Art target: `docs/art/` and
`shared/assets/art-reference/`. Audio target: `docs/audio/`.

## Rules that are not negotiable

1. **Hands-first design, keyboard/mouse stand-in.** Every interaction goes through `com.gardenvr.input` intents
   (Pinch, PinchHold, Release, Poke, Look, PalmOpen). Never read `Input`/`Keyboard`/`Mouse` directly in app code; the
   keyboard/mouse provider is the only place that does. This is what makes the later Quest swap a provider change.
2. **Seated, two-foot radius, under ten minutes** to a complete, satisfying moment. Fast start, clean pause/resume.
3. **Honest habits.** Growth only rises; a missed day is quiet and recoverable; nothing shames, wilts to death or
   turns red. No streak counters.
4. **Rules in `com.gardenvr.core`, pixels in the app.** Core is pure C# (no `UnityEngine`), tested with
   `dotnet test shared/core-dotnet`. App code renders state; it does not decide it.
5. **Authored-looking art is the bar.** Procedural primitives and sawtooth leaves are what the owner rejected. Model in
   Blender (scripted, `tools/blender/`), generate textures/concepts with your `image_gen` / `image_edit` tools, write
   real shaders. Compare every visual change against the art reference at the same framing and save the side-by-side.
6. **No medical claims** in any text or narration about breathing or meditation.
7. **No em dash in user-facing strings.**

## Commands

This is the canonical command list (`.ai/manifest.yaml` `guidance.canonical`). `CLAUDE.md` carries the same block and
`.ai/manifest.yaml` lists the same commands. `tools/guard/check.mjs` fails if any of the three drift (rules G1, G2). `<app>` is `terrarium` or `sundial`. `<out>` is the task's run folder.
Unity exits on its own after `-runTests`, so never add `-quit` to a test run. Read the results XML (test-case count
above 0, failed 0), not the exit code alone.

```powershell
dotnet test shared/core-dotnet
node tools/guard/check.mjs --staged
node tools/guard/check.mjs --all
node tools/guard/check.mjs --self-test
Start-Process -Wait -FilePath "C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe" -ArgumentList '-batchmode','-nographics','-projectPath','apps/<app>','-runTests','-testPlatform','EditMode','-testResults','<out>/editmode.xml','-logFile','<out>/editmode.log'
Start-Process -Wait -FilePath "C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe" -ArgumentList '-batchmode','-projectPath','apps/<app>','-runTests','-testPlatform','PlayMode','-testResults','<out>/playmode.xml','-logFile','<out>/playmode.log'
```

## How to work a task

- Your task file is in `orchestration/running/`. It names the goal, the acceptance checks and the evidence to leave.
- Work only inside your app folder and the shared paths the task names. If a shared package must change, keep the
  change backward compatible and say so in your report; the orchestrator merges shared changes.
- Unity editor: `C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe`. Use `-batchmode -nographics -quit
  -projectPath apps/<app> -executeMethod ...` for builds and editor scripts, `-runTests` without `-quit` for tests (see
  Commands), and `-batchmode` (with graphics) for captures. Never run two Unity instances on the same project at once.
- **Run Unity (and any long process) in the foreground and wait for it.** In headless mode your run ENDS the moment
  you finish a turn without a tool call, so never end a turn "waiting for" a background process. Use
  `Start-Process -Wait -FilePath <Unity.exe> -ArgumentList ...` (PowerShell does not wait on GUI-subsystem exes
  otherwise), then read the log.
- **Unity 6.6 API notes:** `Object.GetInstanceID()` is obsolete as an error (CS0619) - use `GetEntityId()`; package
  versions must be the 6.6-compatible ones already in `Packages/manifest.json` (Input System 1.20.0, URP 17.6.0).
- Verify before you claim: run the tests, capture the screenshot, read the log. A claim you did not run is "not run".
- **A cited path that does not exist on your machine is neither a blocker nor something to guess.** The contest folder
  (`C:\Users\kazda\kiro\personas\.contest\`, the `staging/...` and `arena/...` paths in the plans) and another
  machine's `orchestration/runs/` are provenance. Read `docs/knowledge/contest-record.md`, which holds every fact the
  plan binds on. If the fact you need is not there, write `input missing: <path> (<what you needed>)` in your report,
  continue with what the tracked docs say, and leave the decision that needed it to the host.
- **Commit atomically** on your own branch (`agent/<app>`), one commit per finished sub-step, with a clear message.
  Never push. Never rewrite history. Never commit `Library/`, `Temp/`, `Logs/`, builds or `.env`.
- Before each commit run `node tools/guard/check.mjs --staged`. It checks rules 1, 4 and 7, committed artefacts and
  path ownership, and it exits 1 on a break; fix the break, never bypass it. The agent loop also wires it as your
  pre-commit hook, so a commit that breaks a rule is refused. After the task, the loop re-checks every file your commits
  changed (`--range`), so `--no-verify` is caught. `dotnet test shared/core-dotnet` also runs
  `shared/core-dotnet/GardenVR.Core.Tests/ArchitectureRuleTests.cs`, which fails when an assembly other than
  GardenVR.Input references the Input System, when app runtime code sits outside an asmdef, when the core gains a
  reference or package dependency, or when a user-facing string makes a medical claim (rule 6). A `W1` warning means you touched a path
  you do not own; that is allowed only when the task's `touches:` names it, and your report must say so.
- Finish by writing `orchestration/runs/<app>/<task-id>/REPORT.md`: what changed, commands run with their real
  output (trimmed), evidence paths (screenshots, side-by-sides, videos), what failed, what you would do next.

## Secrets and paid services

- `.env` (git-ignored) holds `ELEVENLABS_API_KEY`. Generate audio only through `tools/audio/elevenlabs.mjs`, which
  enforces a credit reserve; never call the API another way. Keep prompts subtle: soft, quiet, natural.
- Image generation: your built-in `image_gen` / `image_edit`. Save every generated image you keep under the app's
  `Art/Source/` with a sidecar `.prompt.txt`.
- **When `image_gen` is rate-limited or out of quota (HTTP 429, quota, limit), do not stop and do not fake it:** use
  `bash tools/agy/image.sh "<prompt>" <out.png> [<image to edit>]` (Gemini / Nano Banana via the Antigravity CLI,
  ~30 s per image, high quality). It writes the provenance sidecar for you. Run it in the foreground.

## Keys and credits
- `ELEVENLABS_API_KEY` and `TRIPO_API_KEY` live in the git-ignored `.env` at the repo (and worktree) root. Never print or commit them. Tripo: log balance before/after every call; respect the task cap.
