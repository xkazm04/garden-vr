---
prompt: task-template
prompt_version: 1
id: T-XXX-000
app: terrarium
title: One line, what the task delivers
priority: 5
depends: []
estimate_min: 60
touches: []
---
# T-XXX-000 - Title

<!-- Copy this file to orchestration/queue/<app>/<id>-<slug>.md and fill every section. Keep the `prompt:` and
`prompt_version:` lines: they record which template version the task was cut from. `id` is T-TER-nnn or T-SUN-nnn.
`depends` lists task ids; a cross-agent id is met only once it is in merged/ (PLAN section 5). `touches` lists every
path outside the agent's own app the task may change; anything else on the range run's W1 list is a scope finding.
Do not add blocks that appear only for some task slugs: resource locations and credit rules live in AGENTS.md, which
every task reads (docs/knowledge/lessons.md, T-TER-046). Delete this comment. -->

## Goal

What is true when the task is done, in one or two sentences, stated as an observable result.

## Read first

- `AGENTS.md` (always)
- `docs/plans/<app>.md`, the section this task advances
- the files and references the agent needs, by path

## Steps

1. Numbered, each one a finished sub-step that can be its own commit.

## Acceptance

Windows commands the host re-runs with the same timeouts (Definition of Done step 2, `docs/AI_HARNESS.md`). Unity test
runs use the `## Commands` lines in `AGENTS.md` (never `-quit`); a pass is a results XML with test-case count above 0
and failed 0, not an exit code.

```powershell
dotnet test shared/core-dotnet
node tools/guard/check.mjs --staged
```

## Evidence

Paths under `orchestration/runs/<app>/<id>/`: `REPORT.md` with the real trimmed output of each Acceptance command, plus
the screenshots, side-by-sides against the art reference at the same framing, or videos this task changes.

## Out of scope

What the agent must not change, including the other app and any owner-reserved change (`docs/AI_HARNESS.md`).
