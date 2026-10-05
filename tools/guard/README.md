# tools/guard - the AGENTS.md rules, checked by a machine

`AGENTS.md` lists rules that an agent tends to break under pressure. `check.mjs` turns the ones a machine can check
into errors that block a commit. It is plain Node with no dependencies, in the same style as `tools/orchestrate/`.

```powershell
node tools/guard/check.mjs --staged      # what is about to be committed; the pre-commit hook runs this
node tools/guard/check.mjs --all         # every tracked file; step 4 of the host Definition of Done (docs/AI_HARNESS.md)
node tools/guard/check.mjs --self-test   # each rule against good and bad in-memory fixtures; needs no git
```

Each finding prints as `path[:line] RULE message`. Any error exits 1, and the hook then refuses the commit. Warnings
alone exit 0.

## Rules

| Rule | Level | Checks | Source |
|---|---|---|---|
| R1 | error | `*.cs` under `apps/` and `shared/packages/` reads no `Keyboard.current`, `Mouse.current`, `UnityEngine.Input.` or legacy `Input.Get*` / `Input.mousePosition` / `Input.anyKey`. The only allowed place is `shared/packages/com.gardenvr.input/Runtime/Providers/` | AGENTS rule 1, gate S1 |
| R4 | error | No `UnityEngine` / `UnityEditor` in `shared/packages/com.gardenvr.core/` or `shared/core-dotnet/`, and `GardenVR.Core.asmdef` keeps `"noEngineReferences": true` | AGENTS rule 4 |
| R7 | error | No em dash (U+2014) inside a string literal in `.cs` under `apps/*/Assets/` or `shared/packages/*/Runtime/`. `Tests/` and `TextMesh Pro/` are skipped | AGENTS rule 7 |
| A1 | error | No committed `apps/<app>/Library/`, `Temp/`, `Obj/`, `Logs/`, `Build(s)/`, `UserSettings/`, no `.env` / `.env.*` other than `.env.example`, and no `*.blend1` | AGENTS "How to work a task" |
| G1 | error | Every line in the `## Commands` block of `CLAUDE.md` also appears in the canonical `## Commands` block of `AGENTS.md` | `.ai/manifest.yaml` `guidance.canonical` |
| W1 | warning | On branch `agent/<app>`, a staged path that the other agent owns or the host owns (PLAN section 5 ownership table) | PLAN section 5 |

W1 is a warning because PLAN section 5 allows a non-owned path when the task's `touches:` names it. The agent must
flag it in `REPORT.md`, and the host checks it against `touches:` during verification. Cross-app edits are the slip
recorded most often (T-TER-014 edited Sundial's `SceneSetup.cs`; see `docs/knowledge/lessons.md`).

Full-line comments are ignored. If a rule fires on code that is legitimately correct, fix the rule and add a fixture
for that case to `--self-test`. Do not bypass the hook with `--no-verify`.

## Installing the hook (owner's call)

`core.hooksPath` is shared by the main checkout and every agent worktree, so the owner turns it on once:

```powershell
git config core.hooksPath tools/guard/hooks
```

Git for Windows runs `hooks/pre-commit` because the file starts with a shebang. On Linux or macOS, also run
`git update-index --chmod=+x tools/guard/hooks/pre-commit`. Until the hook is installed, the host's `--all` run in the
Definition of Done is the gate.
