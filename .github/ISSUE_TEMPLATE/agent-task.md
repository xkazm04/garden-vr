---
name: Agent task
about: A task for the terrarium or sundial agent queue, with its Definition of Done
title: "T-XXX-000 - "
labels: []
---

<!--
The canonical task skeleton is tools/orchestrate/prompts/task-template.md. The host copies it into
orchestration/queue/<app>/ to dispatch the task; this issue form gathers the same fields so a task is
fully specified before it reaches the queue. Delete this comment.
-->

id: T-XXX-000
app: terrarium | sundial
priority: 5
depends: []
estimate_min: 60
touches: []   <!-- every path outside the agent's own app the task may change -->

## Goal

What is true when the task is done, in one or two sentences, stated as an observable result.

## Read first

- `AGENTS.md` (always)
- `docs/plans/<app>.md`, the section this task advances

## Steps

1. Numbered, each one a finished sub-step that can be its own commit.

## Acceptance

Windows commands the host re-runs with the same timeouts (Definition of Done step 2, `docs/AI_HARNESS.md`). Unity test
runs use the `## Commands` lines in `AGENTS.md`; a pass is a results XML with test-case count above 0 and failed 0.

## Evidence

The files the agent leaves under `orchestration/runs/<app>/<id>/`: REPORT.md, screenshots, side-by-sides, videos.

## Out of scope

What the task must not change.
