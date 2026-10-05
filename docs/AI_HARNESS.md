# AI process & harness: garden-vr

AI agents build almost every change in this repo. Two Grok agents work in their own worktrees on `agent/terrarium` and
`agent/sundial`. A Claude host session queues their tasks, verifies the work and merges it, and the owner reviews it.
This page collects the parts that make that loop trustworthy. It also states the Definition of Done that an AI change
must meet before it counts as finished. The detail lives in `docs/PLAN.md` sections 4 and 5. This page is the short
version and points there.

## Components

Each row names what a reviewer can open or re-run from a clone, and what is not shown yet. A component counts as
evidenced only by something in git that runs, not by this page.

| Component | State (2026-10-05) | Evidence in git | Not shown yet |
|---|---|---|---|
| Evals and golden tests for AI-generated output | **Runs** for code; **advisory** for art | `shared/core-dotnet/GardenVR.Core.Tests/` runs on every `dotnet test shared/core-dotnet` and on every push in `.github/workflows/guard.yml`; the art judge's packet `tools/fidelity/judge/packet-v1/` and its calibration record `tools/fidelity/judge/calibration-2026-10-04.json` | The art judge cannot be re-run from a clone: its implementation (tools/fidelity/lab) and the locked gate frames it scores live only on the host machine. Its calibration is advisory (0 of 30 owner labels). There is no golden-render regression check yet. Making it re-runnable means re-tracking tools/fidelity/lab (byte-identical at f08ff0e^ on backup/pre-rewrite-main) and the eight gate frames whose sha256 values the ladders in `tools/fidelity/ladders/` hold; `.gitignore` keeps both untracked by the owner's choice, so that is the owner's call |
| Versioned prompt and agent library | **Runs** | `tools/orchestrate/prompts/` with `prompt_version`, which guard rule P1 enforces on every commit and in CI | - |
| Agent runbooks for recurring tasks | **Written** | PLAN section 5, `docs/decisions/`, `docs/knowledge/lessons.md` | - |
| Review gate and Definition of Done | **Specified; enforced in shape; tooled** | the steps below; `tools/orchestrate/verdict.mjs`, which writes the verdict line (it re-runs the guard in the agent's clean worktree and records every check the host did not re-run as "not run"); guard rules V1 and V2 over `docs/knowledge/verifications.jsonl`; `.github/pull_request_template.md` | The ledger holds **0 verdicts** so far. No verification has been recorded since it was added on 2026-10-05, and no merge has a committed record of what the host re-ran. The first line comes from the host's next verified merge. V2 does not yet match the host's merge subjects (`merge agent/<app>: ...`), so it cannot flag a missing line until it is widened (`docs/knowledge/lessons.md`, 2026-10-05) |
| Guard for every commit, agent or human | **Runs** | `tools/guard/check.mjs` (hook, `--all`, `--range`) and the CI workflow above; `tools/guard/install.mjs` installs the hooks in one step; `.github/CODEOWNERS` asks the owner to review any pull request that touches a host-owned path or a gate file | Local human commits are hooked only after the owner runs `node tools/guard/install.mjs` once (git has no post-clone hook); CI catches them once pushed. No format check runs in CI yet (`tools/guard/README.md`, analyzer section) |
| Supply chain | **Partly wired** | `.github/dependabot.yml` (Actions, NuGet, npm, pip; weekly proposals routed to the owner), `SECURITY.md` (private reporting), a read-only CI token, guard rule A1 against a committed `.env` | The CI actions are pinned to tags, not commit SHAs. Secret scanning, push protection and private vulnerability reporting are GitHub settings that only the owner can turn on. Unity packages are not watched (Dependabot cannot read them) |

**Has a judge verdict ever changed a merge?** Not visibly. Judge findings have steered work: the repeated "neon /
harsh bloom" disqualifier and the 2/5 plateau became the next task targets (`docs/knowledge/lessons.md`, 2026-10-03),
and two merges quote F2 tallies in their subjects. But no verdict line records a judge result, and no merge is known
to have been held back by one. The A3 score stays advisory (below) until it is calibrated against owner labels.

## Where things live

**Evals and golden tests**
- `dotnet test shared/core-dotnet` covers the core rules: the habit record over 28 simulated days and the honesty
  property tests (gate U3, U5).
- Unity EditMode and PlayMode suites in `apps/*/Assets/Tests/` cover the canonical journey playback, `FirstRun_*`,
  `PauseAt_*` and reduced motion (gate U1, S2-S4, A7). The commands are in the `## Commands` block of `AGENTS.md`.
- `tools/guard/check.mjs` mechanically checks AGENTS.md rules 1, 4 and 7, committed artefacts and path ownership. It
  also checks that `CLAUDE.md` carries exactly the `AGENTS.md` command list (G1) and that `.ai/manifest.yaml`
  declares `AGENTS.md` canonical and lists the same commands (G2). It has a `--self-test`. Every agent commit runs it
  through `tools/guard/hooks/pre-commit`, which the agent loop wires for the agent's process tree. The loop also runs
  `--all` and `--range <base>..<head>` after every task and writes `orchestration/runs/<app>/<id>/guard.txt`.
- `.editorconfig` and `shared/core-dotnet/Directory.Build.props` hold the C# format and analyzer baseline, which runs on
  every `dotnet test shared/core-dotnet` build. The promoted rules (CA2242, CA2200, CA2241, CA2245, CA1304, CA1310)
  fail that build. The rest are warnings until a build shows 0 hits for them. Guard rule B1 stops a promoted rule from
  being demoted (`tools/guard/README.md`, analyzer section).
- On `main`, where host and human commits land, the guard's H1 is an error until `core.hooksPath` points at
  `tools/guard/hooks`. Those commits face the same rules as an agent commit. The checkout cannot report green while
  they skip the hook.
- `.github/workflows/guard.yml` re-runs the guard (`--self-test`, `--all`, `--range` over the pushed commits) and
  `dotnet test shared/core-dotnet` (with the analyzer ratchet) on every push and pull request. So a commit that skipped
  every local hook still turns a check red once it reaches GitHub. Guard rule C1 fails if a check is removed from it.
- Guard rule D1 fails when `AGENTS.md`, `README.md`, `CONTRIBUTING.md`, this page, `docs/PLAN.md` or a plan in
  `docs/plans/` cites a repo path that a fresh clone does not have. The contest facts the plan binds on are tracked in
  `docs/knowledge/contest-record.md`.
- `docs/knowledge/verifications.jsonl` is the committed record of every host verdict (below). The guard rules V1 and V2
  validate it.
- `tools/fidelity/` (`fid.cmd`, `fid.py`, `README.md`) is the art-fidelity lab: F1 metrics against per-frame ladders, the F2 pairwise judge, and the F3 sweep and contact sheet.
- The LLM-as-judge for art is the A3 blind second opinion. Its rubric is section 7 of `docs/art/terrarium-style.md`
  and `docs/art/sundial-style.md`. Sundial's judge runs are scripted in `apps/sundial/Art/Scripts/*_judge.py` and
  `sprint_t050_rubric.py`.

**Prompt and agent library**
- `AGENTS.md` is the agent spec every agent reads first, and it is the canonical guidance (`.ai/manifest.yaml`).
  `CLAUDE.md` is the Claude adapter.
- `tools/orchestrate/prompts/` holds the versioned prompts (`README.md` there):
  - `agent-task.md` is the dispatch prompt every engine gets;
  - `agent-continue.md` is the same-session resume;
  - `task-template.md` is the task-file skeleton (id, app, title, priority, depends, estimate_min, touches, plus Goal,
    Read first, Steps, Acceptance, Evidence and Out of scope; PLAN section 5, Task lifecycle).
- Each prompt file carries `prompt_version`.
  - Guard rule P1 refuses a commit that changes a prompt without raising it.
  - `tools/orchestrate/agent-loop.mjs` sends only these files. Per task, it writes the exact text to
    `orchestration/runs/<app>/<id>/prompt.txt` and the version and git blob sha of each prompt to the task's
    `tasks.jsonl` line (`prompts`). So a change in agent behaviour can be checked against a change in its prompt.
- `tools/orchestrate/agent-loop.mjs` runs each agent with a pinned model and effort.
- The per-app plans are in `docs/plans/`.
- The art-fidelity judge prompts are the versioned packet `tools/fidelity/judge/packet-v1/` (instructions, rubrics,
  schemas, `manifest.json`); a new judge prompt is a new packet version.
- The older A3 judge scripts keep their prompt inline (for example the `PROMPT` constant in `sprint_t050_rubric.py`).
  They are app-owned and are not lifted. Each verdict line that used one records the script's git blob sha
  (`judge.blob`, checked by V1), which pins the exact prompt that run used.
- The scan-sweep lenses come from the `scan-sweep` skill (`.ai/manifest.yaml` `skills:`).
  - Its prompts are versioned in the ai-registry, not here, because every adopting repo shares them.
  - `.ai/manifest.yaml` `skillVersions:` pins the release this repo runs against (4.4.0, the version in the skill's
    `SKILL.md`; the registry's `catalog.json` still lists 4.3.0, which is the registry's to fix).
  - Its bug-hunting lens found the most real defects so far: the `fix(core-*)` commits.

**Runbooks for recurring agent tasks**
- The hourly host check is in PLAN section 5: observe, verify, merge, queue, STATUS.
- Merge policy and shared-package ownership are in PLAN section 5.
- `docs/decisions/` holds the ADRs (0001-0003).
- `docs/knowledge/lessons.md` holds what broke and why. `docs/knowledge/registry-consult.md` lists the registry
  rules this repo follows.

**Issue and PR templates.** The host merges `agent/<app>` into `main` locally, so the task file stays the issue and
`REPORT.md` stays the PR description for agent work. Work that enters or leaves through GitHub uses the same shape:
- `.github/ISSUE_TEMPLATE/agent-task.md` asks for the task-template fields (`tools/orchestrate/prompts/task-template.md`
  stays canonical), including `touches:` and the Acceptance commands. So a task is fully specified before it reaches
  the queue.
- `.github/pull_request_template.md` is the Definition of Done below as a checklist, with the eight `checks` keys and
  the verdict line.

## Review gate for AI changes: Definition of Done

An agent's `REPORT.md` is a self-report, and it never certifies anything (registry `no-gate-self-certifies`). A task
counts as **done** only when the host has re-run these checks itself, on a clean checkout of `agent/<app>` at its
head, against the base commit recorded at dispatch (`git diff <base>..agent/<app>`). The host then appends
`verified: <sha> <date>` to the task file. Any failed step means `rejected: <reason>`.

1. **Oracles restored.** Restore the task's tests, playback scripts and acceptance scripts from the base commit before
   grading. Any agent edit to them is a separate finding.
2. **Acceptance re-run.** Re-run every Acceptance command in the task file with the same timeouts. Read the results
   XML: the test-case count must be above 0 and the failed count must be 0. An exit code alone does not count.
3. **Core tests.** `dotnet test shared/core-dotnet` passes.
4. **Guard.** `node tools/guard/check.mjs --all` and `node tools/guard/check.mjs --range <base>..agent/<app> --as
   agent/<app>` both report 0 errors. Each `W1` warning from the range run must match a path the task's `touches:`
   names, and the report must flag it (`--all` cannot report W1). The agent loop already ran both after the task. Its
   `orchestration/runs/<app>/<id>/guard.txt`, and the `guard`, `guardRange`, `base` and `head` fields in
   `tasks.jsonl`, are the first look, but only the host's re-run counts.
5. **Scope.** `git diff --stat <base>..HEAD` touches only `touches:` plus the agent's owned paths (PLAN section 5,
   ownership), and `git check-ignore` finds no committed `Library/`, `Temp/`, `Logs/`, builds or `.env`.
6. **Evidence opened.** Open every PNG and watch every MP4 the task names. A black or magenta capture is a failed
   capture.
7. **Honest report.** `REPORT.md` carries the real, trimmed output of each command. A command it did not run is
   reported as "not run", and "not run" or "not measured" is never a pass (registry `unmeasured-is-not-a-pass`). The
   report also records the termination cause: finished, ceiling, refused, crashed or unknown.
8. **After the merge.** On `main`, `dotnet test shared/core-dotnet` passes and **both** apps compile in batchmode. A
   shared-package change must not break the other app. `git log -1 --stat` confirms that the merge is the one
   intended.
9. **Record.** Append one line to `docs/knowledge/verifications.jsonl` and commit it on `main` right after the merge.
   For a rejection, append the line without merging. `node tools/orchestrate/verdict.mjs --app <app> --task <id>
   --merge <sha>` (or `--reject "<reason>"`) writes it. It reads `base` and `head` from `tasks.jsonl`. It runs step 4
   itself in the agent's worktree, which must be a clean checkout at `head`, and fills `guardOut` and `w1`. It passes
   step 5 when every W1 path is in `touches:`. The host passes `--oracles`, `--acceptance`, `--core`, `--evidence`,
   `--report` and `--postMerge` for the checks it re-ran, and any it leaves out are recorded as "not run". The tool
   refuses a `verified` line that V1 would reject, and it never commits. The task-file `verified:` line is the working note, and the
   ledger is the record that survives, because `orchestration/` is not tracked. `node tools/guard/check.mjs --all`
   fails on a malformed line (V1). On `main` it also fails on an agent merge that has no line (V2), and on a checkout
   whose commit hooks are not installed (H1).

One line per verdict, on a single line in the file (wrapped here for reading):

```json
{"task":"T-TER-045","app":"terrarium","verdict":"verified","reason":null,"base":"<sha>","head":"<sha>","merge":"<sha>",
 "date":"2026-10-05","checks":{"oracles":"pass","acceptance":"pass","core":"pass","guard":"pass","scope":"pass",
 "evidence":"pass","report":"pass","postMerge":"pass"},"guardOut":"guard: 0 error(s), 1 warning(s)",
 "w1":["apps/sundial/Assets/Editor/SceneSetup.cs"],"judge":null}
```

- The `checks` keys are steps 1-8, and each value is `pass`, `fail`, `not run` or `n/a`. A `verified` line needs
  every check at `pass` or `n/a`, because "not run" is never a pass.
- A `rejected` line names its `reason` and has `"merge": null`.
- `base` and `head` come from `tasks.jsonl`.
- `guardOut` is the summary line of the host's `--all` re-run.
- `w1` lists the paths that the range run warned about.
- When the A3 judge ran, `judge` is `{"script": "<path>", "blob": "<git hash-object of the script>", "model": "...",
  "median": <score>}`.

**Visual changes** (anything that changes a rendered frame) also need:
- a side-by-side against the art reference at the same framing, saved with the evidence (AGENTS rule 5);
- the A2 `measure` step (palette delta-E, line width, size at framing) where the style bible sets a tolerance;
- for a change that touches a gate frame, the A3 blind judge, which is **required to run**. The host runs it, never
  the agent. It uses an outside model family (Gemini, never the Grok producer), takes the median of 3 draws, and sends
  only the render, the reference and the rubric. The score is recorded and **advisory**: it can flag a regression, but
  it never certifies a frame, and it stays that way until it is calibrated against owner labels (registry
  `quality-verdict-integrity`). Only the owner's A1 rating certifies.

**Owner-reserved changes** are proposed and never merged by the host (PLAN section 5, merge policy). These are:
- a test or gate configuration that is deleted, skipped or loosened;
- a new package or a new package version;
- `.env`, credentials or the `elevenlabs.mjs` guards;
- a breaking change to a shared-package API.

## Who checks what

| Who | Checks | Record |
|---|---|---|
| Agent | Runs its Acceptance commands, leaves evidence. Every commit runs `node tools/guard/check.mjs --staged` through the pre-commit hook the loop wires | `orchestration/runs/<app>/<id>/REPORT.md` |
| Agent loop | `node tools/guard/check.mjs --all` and `--range <base>..<head>` in the worktree after every task (advisory, never moves a task) | `orchestration/runs/<app>/<id>/guard.txt`, `base`, `head`, `guard`, `guardRange` in `tasks.jsonl` |
| CI, every push and pull request | `.github/workflows/guard.yml`: `node tools/guard/check.mjs --self-test`, `--all`, `--range` over the pushed commits, and `dotnet test shared/core-dotnet` on Windows. Catches a commit that skipped every local hook | the check on GitHub, red or green |
| Host or human commit on `main` | `node tools/guard/check.mjs --staged` through `pre-commit` / `pre-merge-commit` once the owner runs `node tools/guard/install.mjs`. Until then `--all` on `main` fails with H1. Analyzer rules promoted in `Directory.Build.props` fail `dotnet test shared/core-dotnet` for everyone | the commit itself; the guard output |
| Host | Steps 1-9 above, plus the A3 judge on gate frames | committed line in `docs/knowledge/verifications.jsonl` (guard V1, V2), written by `tools/orchestrate/verdict.mjs`, plus the `verified:` / `rejected:` note in the task file and `orchestration/STATUS.md` |
| Owner | Owner-eyes gate items: first-time use (U2), daily use (U4), feel (S6), A1 frame rating, audio listen (A6), motion watch (A7); approves reserved changes | witness and evidence columns of `apps/terrarium/GATE.md` and `apps/sundial/GATE.md` |
