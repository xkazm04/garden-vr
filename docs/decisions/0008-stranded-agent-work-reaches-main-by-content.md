# 0008 - Stranded agent work reaches main by content

Date: 2026-10-07. Status: accepted (App Master, 2026-10-07).

Source: the owner's answer of 2026-10-07 to the App Master's risk ask: "Design mitigation solution and execute". The ask
offered three options: port all six commits to a fresh branch, port only the input fix, or leave the re-anchor to the
owner.

**Constraint.** `main` was rewritten by filter-branch on 2026-10-05. `agent/terrarium` and `agent/sundial` now share only
merge-base 6c63f35 with it, and are 482 and 484 commits ahead (`git rev-list --count main..7efeff1` and
`main..e8e7e6c`, the old tips; `tools/orchestrate/REANCHOR.md:3-4`). A merge or rebase would bring the pre-rewrite
history back. Review R1 on 2026-10-09 needs working input (`docs/PLAN.md:59`, row 1a).

**Decision.** Agent work reaches `main` by content, never by merge.

- The two T-TER-034 commits were ported as 1969bbf (Terrarium `activeInputHandler` 0 to 2) and 1f9ba5c (sprint look, L
  toggle, Player.log). `git cherry -v main 7efeff1 8688f79` prints `-` for both, so `main` already holds them by content.
- Sundial got the same input fix in d171b34 (`activeInputHandler` 0 to 2 in `apps/sundial/ProjectSettings/ProjectSettings.asset`).
- 1a7e9ab added `tools/orchestrate/divergence.mjs` (`MAX_AHEAD = 100`, line 12), wired in `tools/orchestrate/agent-loop.mjs`
  (import at line 16, the check at line 127, before the main merge), and `tools/orchestrate/REANCHOR.md`.
- T-SUN-053 (agent shas 00b92d2, f3340bf, 146a176, e8e7e6c) stays off `main` as unverified art. It re-enters only through
  host verification.

**Alternatives that lost.**

- Merging or rebasing the agent branches: they would bring back the pre-rewrite history.
- Porting only the input fix: it leaves the sprint look stranded.
- Waiting for the owner to re-anchor: R1 on 2026-10-09 needed working input.

**Consequences.** `tools/orchestrate/REANCHOR.md` must run before either loop relaunches. Until it runs, the loop's
divergence guard refuses to start a task on a worktree more than 100 commits ahead.
