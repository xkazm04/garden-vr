# tools/guard - the AGENTS.md rules, checked by a machine

`AGENTS.md` lists rules that an agent tends to break under pressure. `check.mjs` turns the ones a machine can check
into errors that block a commit. It is plain Node with no dependencies, in the same style as `tools/orchestrate/`.

```powershell
node tools/guard/check.mjs --staged      # what is about to be committed; the pre-commit and pre-merge-commit hooks run this
node tools/guard/check.mjs --all         # every tracked file; step 4 of the host Definition of Done (docs/AI_HARNESS.md)
node tools/guard/check.mjs --range <base>..<head> [--as agent/<app>]   # every file the task's commits changed, read at <head>
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
| G1 | error | The `## Commands` block of `CLAUDE.md` holds exactly the commands of the canonical `## Commands` block of `AGENTS.md`, no more and no fewer. The fence must sit inside the `## Commands` section | `.ai/manifest.yaml` `guidance.canonical` |
| G2 | error | `.ai/manifest.yaml` declares `guidance:` `canonical: AGENTS.md`, and every `AGENTS.md` command appears verbatim in it, so a tool reading the manifest gets the same answer an agent reads | `.ai/manifest.yaml` `guidance.canonical` |
| V1 | error | Every line of `docs/knowledge/verifications.jsonl` is one verdict object with a task id, a known app, `verified` or `rejected`, a date, base and head shas, and all eight Definition of Done checks as `pass`, `fail`, `not run` or `n/a`. A `rejected` line has a reason. A `verified` line has the merge sha, every check `pass` or `n/a` ("not run" is never a pass), and a `guardOut` of `guard: 0 error(s)`. A `judge` is null or names its script and git blob sha | `docs/AI_HARNESS.md`, Definition of Done |
| V2 | error on `main`, warning elsewhere | Every first-parent `Merge branch 'agent/<app>'` on `main` since the ledger was added has a line in `main`'s ledger whose `merge` is that commit. Off `main` (the agent loop's runs) it is a warning, so a host lapse shows in every `guard.txt` without failing the agent | `docs/AI_HARNESS.md`, Definition of Done |
| W1 | warning | On branch `agent/<app>` (or `--as agent/<app>`), a staged or changed path that the other agent owns or the host owns (PLAN section 5 ownership table) | PLAN section 5 |
| H1 | warning | `core.hooksPath` is not `tools/guard/hooks`, so commits and merges in this checkout skip the guard | this file, "Installing the hooks" |

W1 is a warning because PLAN section 5 allows a non-owned path when the task's `touches:` names it. The agent must
flag it in `REPORT.md`, and the host checks it against `touches:` during verification. Cross-app edits are the slip
recorded most often (T-TER-014 edited Sundial's `SceneSetup.cs`; see `docs/knowledge/lessons.md`). `--all` cannot
report W1, because it has no notion of "this task's change"; `--range` and `--staged` do.

Full-line comments are ignored. If a rule fires on code that is legitimately correct, fix the rule and add a fixture
for that case to `--self-test`. Do not bypass the hook with `--no-verify`.

`--staged` and `--range` run G1 and G2 only when `AGENTS.md`, `CLAUDE.md` or `.ai/manifest.yaml` changed, and V1 only
when the ledger changed. `--all` always runs G1, G2, V1 and V2.

## Where it runs

- **Agent, at each commit.** The agent loop sets `core.hooksPath` to the worktree's `tools/guard/hooks` for the agent's
  own process tree (git's `GIT_CONFIG_COUNT` / `GIT_CONFIG_KEY_n` / `GIT_CONFIG_VALUE_n` environment, git 2.31+). So
  every `git commit` the agent runs goes through `pre-commit`, and a commit that breaks a rule is refused. The shared
  config is not touched.
- **Agent loop, after each task.** `tools/orchestrate/agent-loop.mjs` records the dispatch base and the task's head. It
  then runs `--all` and `--range <base>..<head>` in the agent's worktree and writes both outputs to
  `orchestration/runs/<app>/<id>/guard.txt` in the main checkout. The exit codes go to `tasks.jsonl` as `guard` and
  `guardRange`, together with `base`, `head` and `hook`. The range run catches a commit made with `--no-verify`, and
  it is the run that reports W1. It is advisory: a red guard does not move the task to `failed/`, but the host rejects
  the task in Definition of Done step 4.
- **Host, when verifying.** `--all` and `--range <base>..agent/<app> --as agent/<app>` on the agent branch
  (`docs/AI_HARNESS.md`, step 4). The verdict then goes into `docs/knowledge/verifications.jsonl`, which V1 and V2
  check.
- **Host, at each merge.** Once the hooks are installed, `pre-merge-commit` runs `--staged` on `git merge --no-ff
  agent/<app>`.

## Installing the hooks (owner's call)

`core.hooksPath` is shared by the main checkout and every agent worktree, so the owner turns it on once. That gates
host commits and merges too (H1 warns on every `--all` run until then):

```powershell
git config core.hooksPath tools/guard/hooks
```

Git for Windows runs `hooks/pre-commit` and `hooks/pre-merge-commit` because each file starts with a shebang. On Linux
or macOS, also run `git update-index --chmod=+x tools/guard/hooks/pre-commit tools/guard/hooks/pre-merge-commit`.

## C# analyzer and format baseline

The repo-root `.editorconfig` holds the C# formatting rules (IDE0055) and a short list of bug-pattern analyzers:
CA2242 (NaN compared with ==), CA1304/CA1305/CA1310 (culture-dependent parsing), CA1806 (a dropped result), CA2241,
CA2200, CA1065 and CA2245. `shared/core-dotnet/Directory.Build.props` switches the SDK's analyzers on for the core
build, so `dotnet test shared/core-dotnet` reports them as warnings on every run. The analyzers ship with the .NET
SDK, so no package is added.

Ratchet: a rule moves to `WarningsAsErrors` in `Directory.Build.props` once a build shows 0 hits for it. Pending, and
not run yet: `dotnet format whitespace shared/core-dotnet/GardenVR.sln --verify-no-changes`. It joins the `## Commands`
block (AGENTS.md, CLAUDE.md and `.ai/manifest.yaml` together, or G1/G2 fail) only after it has run green once.
