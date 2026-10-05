# AI process & harness: garden-vr

AI agents build almost every change in this repo. Two Grok agents work in their own worktrees on `agent/terrarium` and
`agent/sundial`. A Claude host session queues their tasks, verifies the work and merges it, and the owner reviews it.
This page collects the parts that make that loop trustworthy. It also states the Definition of Done that an AI change
must meet before it counts as finished. The detail lives in `docs/PLAN.md` sections 4 and 5. This page is the short
version and points there.

## Components
- [x] Evals and golden tests for AI-generated output
- [x] A versioned prompt and agent library the team reuses (below; the older A3 judge scripts stay pinned by blob)
- [x] Agent runbooks for recurring tasks
- [x] A review gate and Definition of Done for AI changes (below)

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
- `docs/decisions/0001-0003` hold the ADRs.
- `docs/knowledge/lessons.md` holds what broke and why. `docs/knowledge/registry-consult.md` lists the registry
  rules this repo follows.

**No issue or PR templates.** Nothing is pushed, and the host merges `agent/<app>` into `main` locally. The task file
plays the role of the issue template, and `REPORT.md` plays the role of the PR description.

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
   For a rejection, append the line without merging. The task-file `verified:` line is the working note, and the
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
| Host or human commit on `main` | `node tools/guard/check.mjs --staged` through `pre-commit` / `pre-merge-commit` once the owner sets `core.hooksPath`. Until then `--all` on `main` fails with H1. Analyzer rules promoted in `Directory.Build.props` fail `dotnet test shared/core-dotnet` for everyone | the commit itself; the guard output |
| Host | Steps 1-9 above, plus the A3 judge on gate frames | committed line in `docs/knowledge/verifications.jsonl` (guard V1, V2), plus the `verified:` / `rejected:` note in the task file and `orchestration/STATUS.md` |
| Owner | Owner-eyes gate items: first-time use (U2), daily use (U4), feel (S6), A1 frame rating, audio listen (A6), motion watch (A7); approves reserved changes | witness and evidence columns of `apps/terrarium/GATE.md` and `apps/sundial/GATE.md` |
