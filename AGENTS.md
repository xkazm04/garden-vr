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

## How to work a task

- Your task file is in `orchestration/running/`. It names the goal, the acceptance checks and the evidence to leave.
- Work only inside your app folder and the shared paths the task names. If a shared package must change, keep the
  change backward compatible and say so in your report; the orchestrator merges shared changes.
- Unity editor: `C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe`. Use `-batchmode -nographics -quit
  -projectPath apps/<app> -executeMethod ...` for builds/tests and `-batchmode` (with graphics) for captures.
  Never run two Unity instances on the same project at once.
- **Run Unity (and any long process) in the foreground and wait for it.** In headless mode your run ENDS the moment
  you finish a turn without a tool call, so never end a turn "waiting for" a background process. Use
  `Start-Process -Wait -FilePath <Unity.exe> -ArgumentList ...` (PowerShell does not wait on GUI-subsystem exes
  otherwise), then read the log.
- **Unity 6.6 API notes:** `Object.GetInstanceID()` is obsolete as an error (CS0619) - use `GetEntityId()`; package
  versions must be the 6.6-compatible ones already in `Packages/manifest.json` (Input System 1.20.0, URP 17.6.0).
- Verify before you claim: run the tests, capture the screenshot, read the log. A claim you did not run is "not run".
- **Commit atomically** on your own branch (`agent/<app>`), one commit per finished sub-step, with a clear message.
  Never push. Never rewrite history. Never commit `Library/`, `Temp/`, `Logs/`, builds or `.env`.
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
