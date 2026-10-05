# AI process & harness: garden-vr

AI agents build almost every change in this repo. Two Grok agents work in their own worktrees on `agent/terrarium` and
`agent/sundial`. A Claude host session queues their tasks, verifies the work and merges it, and the owner reviews it.
This page collects the parts that make that loop trustworthy. It also states the Definition of Done that an AI change
must meet before it counts as finished. The detail lives in `docs/PLAN.md` sections 4 and 5. This page is the short
version and points there.

## Components
- [x] Evals and golden tests for AI-generated output
- [ ] A versioned prompt and agent library the team reuses (partial: see below)
- [x] Agent runbooks for recurring tasks
- [x] A review gate and Definition of Done for AI changes (below)

## Where things live

**Evals and golden tests**
- `dotnet test shared/core-dotnet` covers the core rules: the habit record over 28 simulated days and the honesty
  property tests (gate U3, U5).
- Unity EditMode and PlayMode suites in `apps/*/Assets/Tests/` cover the canonical journey playback, `FirstRun_*`,
  `PauseAt_*` and reduced motion (gate U1, S2-S4, A7). The commands are in the `## Commands` block of `AGENTS.md`.
- `tools/guard/check.mjs` mechanically checks AGENTS.md rules 1, 4 and 7, committed artefacts and path ownership. It
  has a `--self-test`.
- `tools/fidelity/` (`fid.cmd`, `fid.py`, `README.md`) is the art-fidelity lab: F1 metrics against per-frame ladders, the F2 pairwise judge, and the F3 sweep and contact sheet.
- The LLM-as-judge for art is the A3 blind second opinion. Its rubric is section 7 of `docs/art/terrarium-style.md`
  and `docs/art/sundial-style.md`. Sundial's judge runs are scripted in `apps/sundial/Art/Scripts/*_judge.py` and
  `sprint_t050_rubric.py`.

**Prompt and agent library**
- `AGENTS.md` is the agent spec every agent reads first, and it is the canonical guidance (`.ai/manifest.yaml`).
  `CLAUDE.md` is the Claude adapter.
- The task-file schema (id, app, depends, touches, plus Goal, Steps, Acceptance, Evidence and Out of scope) is in
  PLAN section 5, Task lifecycle.
- `tools/orchestrate/agent-loop.mjs` runs each agent with a pinned model and effort.
- The per-app plans are in `docs/plans/`.
- Not yet versioned: the judge prompts sit inline in each judge script (for example the `PROMPT` constant in
  `sprint_t050_rubric.py`). There is no shared prompt file with a version, so two runs can drift. Lifting them into
  one versioned file is the open item behind the unticked box.

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
4. **Guard.** `node tools/guard/check.mjs --all` reports 0 errors. Each `W1` warning must match a path the task's
   `touches:` names, and the report must flag it.
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
| Agent | Runs its Acceptance commands and `node tools/guard/check.mjs --staged` before each commit, then leaves evidence | `orchestration/runs/<app>/<id>/REPORT.md` |
| Host | Steps 1-8 above, plus the A3 judge on gate frames | `verified:` / `rejected:` line in the task file, and `orchestration/STATUS.md` |
| Owner | Owner-eyes gate items: first-time use (U2), daily use (U4), feel (S6), A1 frame rating, audio listen (A6), motion watch (A7); approves reserved changes | witness and evidence columns of `apps/terrarium/GATE.md` and `apps/sundial/GATE.md` |
