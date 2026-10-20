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
| H1 | error on `main`, warning elsewhere, not reported in CI | `core.hooksPath` is not `tools/guard/hooks`, so commits and merges in this checkout skip the guard. On `main`, where host and human commits land, a checkout without the hook cannot report green. Elsewhere it is a warning, because the agent loop wires the hook for agent commits through the environment, which the worktree config never shows. Under GitHub Actions (`GITHUB_ACTIONS=true`) it reports nothing: the runner's clone makes no commits, and the workflow re-checks the pushed commits. Whether that red check blocks a merge is a GitHub setting; H1 does not fail CI over it (see "Making CI block a merge") | this file, "Installing the hooks" |
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

## Architecture rules in the core test run

The guard reads text. Some of the rules are better held by what the compiler can see.
`shared/core-dotnet/GardenVR.Core.Tests/ArchitectureRuleTests.cs` checks them on the repo's own files in every
`dotnet test shared/core-dotnet`, so they run in Definition of Done step 3 and in the CI `core` job, whoever made the
commit:

| Test | Rule | Fails when |
|---|---|---|
| `Only_the_input_package_compiles_against_the_input_system` | AGENTS rule 1 | an `.asmdef` under `apps/<app>/Assets` or `shared/packages` other than `GardenVR.Input.asmdef` references `Unity.InputSystem` (by name, GUID or precompiled DLL). Test asmdefs are exempt. Without the reference, `Keyboard.current` in app code does not compile |
| `App_runtime_code_never_falls_into_the_default_assembly` | AGENTS rule 1 | a non-Editor `.cs` under `apps/<app>/Assets` has no `.asmdef`/`.asmref` above it, so it compiles into Assembly-CSharp, which references every package |
| `Core_assembly_stays_an_engine_free_leaf` | AGENTS rule 4 | `GardenVR.Core.asmdef` loses `noEngineReferences`, gains a reference or precompiled DLL, or `com.gardenvr.core` gains a package dependency |
| `No_device_read_outside_the_keyboard_mouse_provider` | AGENTS rule 1 | wider than R1: any Input System device `.current`, `InputSystem.GetDevice` / `devices` / `onEvent`, or a legacy touch, scroll or sensor read outside the provider |
| `No_medical_claim_in_user_facing_text` | AGENTS rule 6 | a string literal in app or package runtime code, or a line of an app's `Audio/**/*.json`, uses the medical vocabulary the narration tests ban (therapy, medical, anxiety, cure, diagnose, treatment, clinical, insomnia, heal) |

Each rule test first checks that its detector fires on a known positive (the provider's own reference and device
reads), and two theories pin the matchers on in-memory lines, so a check that silently matches nothing fails.
Imported `TextMesh Pro/` and `Samples/` folders are skipped. AGENTS rules 2, 3 and 5 have no file or token form:
"streak" is a legitimate shader and material word (`_Streak`), so rule 3 is held by the PlayMode narration and save
tests instead. A Roslyn banned-API analyzer would be a new package (owner-reserved) and could only run inside Unity,
which CI cannot license; the asmdef reference is the same constraint with no dependency.

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
  licence. C1 keeps the workflow's checks in place. Red is a report, not a block: the host merges locally and pushes
  `main`, so CI reads a commit that has already landed, and it holds a merge back only where the owner made its jobs
  required checks ("Making CI block a merge" below). `.github/workflows/merge-gate.yml` says in every run which of the
  two it is.

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

### Claude sessions, without core.hooksPath (not wired yet)

`claude-hook.mjs` is a Claude Code `PreToolUse` hook that gates a Claude session's commits in any clone without
touching any config. When a Bash or PowerShell command would run `git commit` or `git merge` here while
`core.hooksPath` is not `tools/guard/hooks` (and neither a `-c` on that call nor the agent loop's `GIT_CONFIG_*` env sets
it), it refuses the command and names the per-call override that makes git run the same two hooks:

```powershell
git -c core.hooksPath=tools/guard/hooks commit -m "..."
```

It always refuses `--no-verify` and `-n`. It lets through anything it cannot read: bad input, no git, or a repo without
these hooks. `node tools/guard/claude-hook.mjs --self-test` checks each decision against command fixtures, and
`.github/workflows/claude-hook.yml` runs that on every push. It takes effect only once the host commits
`.claude/settings.json` with this entry (an automation lane could not write that file):

```json
{ "hooks": { "PreToolUse": [ { "matcher": "Bash|PowerShell", "hooks": [
  { "type": "command", "command": "node \"$CLAUDE_PROJECT_DIR/tools/guard/claude-hook.mjs\"", "timeout": 30 } ] } ] } }
```

That covers the host and lane sessions. A person committing by hand still needs `install.mjs`, and CI covers both.

## Making CI block a merge (owner's call)

A red CI check stops a merge only when `main` requires it. Until then `guard.yml` *detects* a bad commit after it is
on `main`; it does not keep one out. Requiring it is a repository setting, so no file in this repo can turn it on, and
it is reserved for the owner:

1. In a ruleset for `main` (Settings, Rules) or in classic branch protection, require the status checks
   `guard (AGENTS.md rules)` and `core tests and analyzer ratchet`. These are the job `name:` values in `guard.yml`;
   renaming a job breaks the match, and the probe below then reports it as not required.
2. Leave admins out of the bypass list (classic: "Do not allow bypassing the above settings"). Otherwise the owner's
   own pushes, which are most pushes to `main`, skip the check.
3. Change how `main` is published. A required check refuses a push whose commit has not already passed it, so the
   host's local merge goes to a branch first and reaches `main` through a pull request (or a push of the already-green
   commit). This changes the merge policy in PLAN section 5, which is why it is the owner's decision.

`tools/guard/protection.mjs` reports the current state. `.github/workflows/merge-gate.yml` runs it on every push and
pull request. It reads both places GitHub keeps required checks (`/rules/branches/main` and `/branches/main`) and
prints `required`, `not required` or `unknown` per gate job. If any gate job is not required everywhere, it raises a
`Merge gate` warning annotation and adds a line to the step summary. A setting it cannot read is `unknown`, never a
pass. Its `--self-test` checks the parsing and the verdict against fixtures and the real `guard.yml`.

The probe always passes, and H1 does not fail CI over this setting, for three reasons. A red check that is not
required blocks nothing either. Every push and Dependabot pull request would turn red over a setting no contributor
can change, which teaches everyone to ignore red. And H1 is about this checkout's `core.hooksPath`, while the guard
stays offline and deterministic. Do not make the probe's own job a required check.

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

### Layout, held by a test on every C# file

IDE0055 sees only the core build and is still a warning, and Unity reads none of `.editorconfig` when it compiles the
apps. So the part of the layout a single line can show is held by
`shared/core-dotnet/GardenVR.Core.Tests/FormatRuleTests.cs` instead. It runs in every `dotnet test shared/core-dotnet`:
Definition of Done steps 3 and 8, the CI `core` job, whoever made the commit. It reads every `.cs` under
`apps/<app>/Assets`, `shared/packages` and `shared/core-dotnet` (not `bin/`, `obj/`, `TextMesh Pro/` or `Samples/`)
and fails on:

| Finding | `.editorconfig` `[*.cs]` setting |
|---|---|
| `tab-indent` | `indent_style = space` |
| `trailing-whitespace` | `trim_trailing_whitespace = true` |
| `final-newline` | `insert_final_newline = true` |
| `brace-indent`: a line holding only `{` or `}` off the 4-space grid | `indent_size = 4` |
| `brace-same-line`: a line of code ending in `{` | `csharp_new_line_before_open_brace = all` |
| `close-brace-continues`: `} else`, `} catch`, `} finally` | `csharp_new_line_before_else`, `_catch`, `_finally` |
| `keyword-paren`: `if(`, `foreach(`, `using(` and the other control-flow keywords | `csharp_space_after_keywords_in_control_flow_statements = true` |

Single-line blocks and statements stay allowed, because `.editorconfig` preserves them (`try { Kill(); } catch (Exception) { }`).
So does an array initializer opened after `=` (the capture package's `BitmapFont.Data`), the one such line in the repo.
Literals and trailing comments are removed before the brace and keyword rules read a line. A theory pins each finding
on in-memory lines, and a third test fails when `.editorconfig` stops declaring a setting the test mirrors, so the two
change together. A static read of all 209 C# files (2026-10-05) found 0 hits, so the test went in as an error with no
reformat sweep of agent-owned code. A hit names `path:line finding`; fix it by hand or with `dotnet format whitespace`.

### The formatter in CI

A format workflow (not yet landed; the version harvested from ascent/runner failed CI and was left out, see the Harvest issue) would run `dotnet format whitespace --folder` on every push and pull request. It would cover what
the test does not, such as spacing inside a line, and it reaches the app code Unity compiles, which no build here
does. Folder mode needs no project file. Scope is the same as `FormatRuleTests` (`apps/<app>/Assets`, `shared/packages`,
`shared/core-dotnet`, without `TextMesh Pro/`, `Samples/`, `bin/` and `obj/`). It has two steps:

- **Changed files, a gate.** Every `.cs` in scope that the pushed commits added or modified (`<before>..<sha>` on a
  push, `<base>..<merge>` on a pull request) must be clean. Otherwise the job fails and names the files. Fix with
  `dotnet format whitespace . --folder --include <file>`. A new branch or a force push has no usable base, so the step
  skips and says so.
- **Whole tree, a report.** The same check over the whole scope writes the number of drifting files as a warning
  annotation and lists them in the step summary. It fails only if the formatter cannot run.

That split follows the rule above: do not reformat agent-owned code in one sweep, because a whitespace sweep collides
with live agent branches. Each file is fixed when its owner next touches it, and the gate stops new drift. When the
report shows 0 files, the owner can move the whole-tree check into the `core` job of `.github/workflows/guard.yml` with
a matching `CI_REQUIRED` entry for C1, add it to the `## Commands` block (AGENTS.md, CLAUDE.md and `.ai/manifest.yaml`
together, or G1/G2 fail), and promote IDE0055 through the ratchet. Until then IDE0055 shows as a build warning on every
`dotnet test shared/core-dotnet`.

The workflow uses no third-party action (its checkout is plain `git fetch`), so it adds nothing to pin.

`install.mjs --check` does not belong in CI. The runner checks its own fresh clone, never a contributor's, which is
why H1 reports nothing under GitHub Actions. A clone without the hook is caught at push time by `guard.yml` and this
workflow.

## Workflow and judge rules in the core test run

Two more checks in `shared/core-dotnet/GardenVR.Core.Tests/` read repo files rather than code. They run in every
`dotnet test shared/core-dotnet`, so the CI `core` job enforces them whoever made the commit:

| Test | Rule | Fails when |
|---|---|---|
| `WorkflowPinTests` | Actions run from a commit, not a movable tag | a `uses:` in `.github/workflows/*.yml` is not `owner/repo[/path]@<40-hex sha> # vX.Y.Z` (a local `./` action needs no pin; a `docker://` image needs an `@sha256:` digest). `TagPinned` lists the 7 tag refs that were there when the rule went in. It may only shrink: an entry that is pinned or gone fails as stale. Pin one with `git ls-remote https://github.com/<owner>/<repo> 'refs/tags/<tag>^{}'` and remove its entry in the same commit. The `guard.yml` refs are the owner's, because that file is a gate. Dependabot keeps SHA pins and their version comments current |
| `ArtJudgeGateTests` | The art judge stays advisory until owner agreement is shown (`docs/decisions/0017-art-judge-gate-criteria.md`) | a `tools/fidelity/judge/calibration-*.json` says `"advisory": false` without at least `kappa_min_labels` owner labels, at least 12 per frame, kappa at the packet floor with `ci95_low` of at least 0.4, raw `agreement` of at least 0.8, a per-frame inversion rate of at most 0.1 and a passing control gate; or an advisory record gives no reason; or a judge, ladder or control input is missing from the checkout and not on the `HostOnly` list of 8 host-side frames, which may only shrink |
