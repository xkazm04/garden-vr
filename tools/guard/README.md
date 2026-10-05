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
| V2 | error on `main`, warning elsewhere | Every first-parent `Merge branch 'agent/<app>'` on `main` since the ledger was added has a line in `main`'s ledger whose `merge` is that commit. Off `main` (the agent loop's runs) it is a warning, so a host lapse shows in every `guard.txt` without failing the agent. **Known gap:** the host's merges on `main` are titled `merge agent/<app>: ...`, which the pattern does not match yet, so V2 cannot see them until `checkMergeRecords` is widened (`docs/knowledge/lessons.md`, 2026-10-05). `tools/orchestrate/verdict.mjs` writes the line | `docs/AI_HARNESS.md`, Definition of Done |
| W1 | warning | On branch `agent/<app>` (or `--as agent/<app>`), a staged or changed path that the other agent owns or the host owns (PLAN section 5 ownership table) | PLAN section 5 |
| B1 | error | The analyzer ratchet never demotes. Every id in `PROMOTED` (CA2242, CA2200, CA2241, CA2245, CA1304, CA1310) stays in `<WarningsAsErrors>` of `shared/core-dotnet/Directory.Build.props`, stays out of `<NoWarn>`, and keeps a `dotnet_diagnostic.<id>.severity` of `warning` or `error` in `.editorconfig` (that line is what switches the rule on) | this file, "C# analyzer and format baseline" |
| P1 | error | Every prompt file in `tools/orchestrate/prompts/` (`*.md` except `README.md`) opens with front matter `prompt: <its file name>` and an integer `prompt_version`. A commit or range that changes a prompt's text must raise its `prompt_version` (compared with `HEAD` for `--staged`, with `<base>` for `--range`); a new file and a line-ending-only change need no bump. The agent loop records each prompt's version and blob sha per task in `tasks.jsonl`, so a change in what agents are told is never silent | `tools/orchestrate/prompts/README.md` |
| H1 | error on `main`, warning elsewhere, not reported in CI | `core.hooksPath` is not `tools/guard/hooks`, so commits and merges in this checkout skip the guard. On `main`, where host and human commits land, a checkout without the hook cannot report green. Elsewhere it is a warning, because the agent loop wires the hook for agent commits through the environment, which the worktree config never shows. Under GitHub Actions (`GITHUB_ACTIONS=true`) it reports nothing: the runner's clone makes no commits, and the workflow is the gate | this file, "Installing the hooks" |
| C1 | error | `.github/workflows/guard.yml` exists and, outside full-line comments, still runs `tools/guard/check.mjs --self-test`, `--all`, `--range` and `dotnet test shared/core-dotnet`. CI is where a commit that skipped the hook is still checked, so removing one of these loosens a gate, which is owner-reserved | this file, "Where it runs" |
| D1 | error | `AGENTS.md`, `CLAUDE.md`, `README.md`, `CONTRIBUTING.md`, `docs/PLAN.md`, `docs/AI_HARNESS.md`, `docs/plans/*.md` and `docs/knowledge/contest-record.md` cite only repo paths a fresh clone has. A backticked or linked path under `apps/`, `shared/`, `tools/`, `docs/`, `.ai/` or `.github/` must be a tracked file or folder. Placeholders (`<app>`, `*`, `{1,2}`, `0001..0003`), `orchestration/` (untracked by design) and absolute local paths (provenance) are not checked | `docs/knowledge/contest-record.md`; `AGENTS.md`, missing-input rule |

W1 is a warning because PLAN section 5 allows a non-owned path when the task's `touches:` names it. The agent must
flag it in `REPORT.md`, and the host checks it against `touches:` during verification. Cross-app edits are the slip
recorded most often (T-TER-014 edited Sundial's `SceneSetup.cs`; see `docs/knowledge/lessons.md`). `--all` cannot
report W1, because it has no notion of "this task's change"; `--range` and `--staged` do.

Full-line comments are ignored. If a rule fires on code that is legitimately correct, fix the rule and add a fixture
for that case to `--self-test`. Do not bypass the hook with `--no-verify`.

`--staged` and `--range` run G1 and G2 only when `AGENTS.md`, `CLAUDE.md` or `.ai/manifest.yaml` changed, V1 only
when the ledger changed, B1 only when `Directory.Build.props` or `.editorconfig` changed, and P1 on each changed prompt
file (header and version bump). C1 runs there when the workflow changed, and D1 on each changed citing doc, against
the files tracked at the checked state. `--all` always runs G1, G2, V1, V2, B1, C1 and D1, and P1's header check on
every prompt file (it has no earlier state, so it cannot check a bump). H1 runs in `--all` and `--range`; its level
follows the branch (`--as` counts for `--range`). A deleted workflow is caught by `--all`, not by `--staged` or `--range`,
which read only added and modified files.

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
- **Host and human commits on `main`.** The rules check the repo's content, not who wrote it, so a human commit is held
  to the same rules as an agent commit (W1 aside, which is about agent ownership). The hook gates it once
  `core.hooksPath` is set. Until then, `--all` and `--range` on `main` fail with H1, so the checkout where those commits
  land cannot report green while its commits skip the guard.
- **CI, on every push and pull request.** `.github/workflows/guard.yml` runs `--self-test`, `--all`, and `--range`
  over the pushed commits (`<before>..<sha>` on a push, `<base>..<merge>` on a pull request, and the merge base with
  `origin/main` for a new branch or a force push). A second job runs `dotnet test shared/core-dotnet` on Windows, which
  enforces the analyzer ratchet. Any error turns the check red. This holds a commit that skipped every local hook,
  whoever made it, to the same rules once it reaches GitHub. Unity test runs stay out of CI because they need an editor
  licence. C1 keeps the workflow's checks in place.

## Installing the hooks (owner's call)

`core.hooksPath` is shared by the main checkout and every agent worktree, so the owner turns it on once. That gates
host and human commits and merges at commit time. Until then H1 fails every `--all` run on `main` and warns on every
other branch, and CI is the first place an unhooked commit is checked:

```powershell
node tools/guard/install.mjs           # sets core.hooksPath to tools/guard/hooks; refuses to replace another hooks path
node tools/guard/install.mjs --check   # changes nothing; exits 1 while the hooks are not installed
```

Git has no post-clone hook, so no file in the repo can do this on clone by itself; `install.mjs` is the one step per
clone. It checks for Node 20 or newer and the two hook files, refuses to overwrite a different `core.hooksPath` (a
leftover `.husky`, say) unless given `--force`, sets the path, and reads it back with the same comparison H1 uses. It is
the same as `git config core.hooksPath tools/guard/hooks`, with those checks around it.

Git for Windows runs `hooks/pre-commit` and `hooks/pre-merge-commit` because each file starts with a shebang. On Linux
or macOS a hook runs only when its file is executable: `install.mjs` sets the bit in the working tree, and
`git update-index --chmod=+x tools/guard/hooks/pre-commit tools/guard/hooks/pre-merge-commit` records it for every
clone (the hooks are still committed as 100644).

## Review routing on GitHub

`.github/CODEOWNERS` names the owner for every host-owned path in the ownership table (`HOST_OWNED` above) and for the
gate files (`.github/`, `.editorconfig`, `shared/core-dotnet/Directory.Build.props`). A pull request that touches one of
them, such as a Dependabot bump of a `.csproj`, requests the owner's review. Keep it in step with `HOST_OWNED`. It
becomes a hard gate only if the owner turns on "Require review from Code Owners" for `main`.

## C# analyzer and format baseline

The repo-root `.editorconfig` holds the C# formatting rules (IDE0055) and a short list of bug-pattern analyzers:
CA2242 (NaN compared with ==), CA1304/CA1305/CA1310 (culture-dependent parsing), CA1806 (a dropped result), CA2241,
CA2200, CA1065 and CA2245. `shared/core-dotnet/Directory.Build.props` switches the SDK's analyzers on for the core
build, so `dotnet test shared/core-dotnet` runs them on every build. The analyzers ship with the .NET SDK, so no
package is added.

Ratchet: a rule moves to `WarningsAsErrors` in `Directory.Build.props` once a build shows 0 hits for it, and from then
on a hit fails `dotnet test shared/core-dotnet`, which is Definition of Done steps 3 and 8. Guard rule B1 keeps it there.
To promote a rule, add it to `WarningsAsErrors` and to `PROMOTED` in `check.mjs` in the same commit.

| State | Rules | Why |
|---|---|---|
| Error | CA2242, CA2200, CA2241, CA2245, CA1304, CA1310 | A static read of the core and its tests found 0 hits (2026-10-05), and a hit is a real bug |
| Warning | CA1305 | Known hits: `Sundial/StretchSession.cs` `Time.ToString("0.00")`, `Common/SaveStore.cs` `int.ToString()`, the tests' `Guid.ToString("N")`. Fix them, then promote |
| Warning | CA1806, CA1065, IDE0055 | Not measured yet. Read the warning list of `dotnet build shared/core-dotnet` and promote each one that shows 0 hits |

Pending, and not run yet: `dotnet format whitespace shared/core-dotnet/GardenVR.sln --verify-no-changes`. It joins the `## Commands`
block (AGENTS.md, CLAUDE.md and `.ai/manifest.yaml` together, or G1/G2 fail) and the `core` job of
`.github/workflows/guard.yml` (with a matching `CI_REQUIRED` entry for C1) only after it has run green once. If it is
red, do not reformat the core in one sweep: its sources are agent-owned and a whitespace sweep collides with live
agent branches. Fix the hits as each owner touches a file, then promote IDE0055 through the ratchet. Until then IDE0055 shows as a build warning
on every `dotnet test shared/core-dotnet`.
