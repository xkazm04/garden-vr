# Garden VR - guidance for Claude sessions

`AGENTS.md` is the canonical guidance (declared in `.ai/manifest.yaml` as `guidance.canonical`); this file is the
Claude adapter, and where the two disagree `AGENTS.md` wins. Read `AGENTS.md` (shared rules for every agent) and
`docs/PLAN.md`. The host session orchestrates two Grok agents via `tools/orchestrate/`; see `orchestration/STATUS.md`
for the live state. Commit on the current branch; never push. A change is done only when it meets the Definition of
Done in `docs/AI_HARNESS.md`.

## Commands

The same list as `AGENTS.md`, so Claude and Grok sessions run the same commands; `node tools/guard/check.mjs` fails if
this block drifts from it. `<app>` is `terrarium` or `sundial`; `<out>` is the run folder. Never add `-quit` to a
Unity test run.

```powershell
dotnet test shared/core-dotnet
node tools/guard/check.mjs --staged
node tools/guard/check.mjs --all
node tools/guard/check.mjs --self-test
Start-Process -Wait -FilePath "C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe" -ArgumentList '-batchmode','-nographics','-projectPath','apps/<app>','-runTests','-testPlatform','EditMode','-testResults','<out>/editmode.xml','-logFile','<out>/editmode.log'
Start-Process -Wait -FilePath "C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe" -ArgumentList '-batchmode','-projectPath','apps/<app>','-runTests','-testPlatform','PlayMode','-testResults','<out>/playmode.xml','-logFile','<out>/playmode.log'
```
