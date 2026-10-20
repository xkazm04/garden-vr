<!--
The Definition of Done in docs/AI_HARNESS.md is the full list. This template is its checklist; a box is ticked only
for a check you re-ran yourself. "Not run" is never a pass: leave the box empty and say why.
-->

## What changes

<!-- One or two sentences, stated as an observable result. For an agent task, the task id and its `touches:`. -->

Task: <!-- T-TER-nnn / T-SUN-nnn, or "none" for a host or human change -->
Base: <!-- the base commit recorded at dispatch (tasks.jsonl `base`), or the commit this branch started from -->

## Definition of Done

- [ ] 1. **oracles**: the task's tests, playback and acceptance scripts were restored from the base before grading
- [ ] 2. **acceptance**: every Acceptance command re-run; results XML test-case count above 0, failed 0
- [ ] 3. **core**: `dotnet test shared/core-dotnet` passes
- [ ] 4. **guard**: `node tools/guard/check.mjs --all` and `--range <base>..HEAD` report 0 errors; every W1 path is in `touches:`
- [ ] 5. **scope**: `git diff --stat <base>..HEAD` stays inside `touches:` and the owned paths; nothing ignored is committed
- [ ] 6. **evidence**: every named PNG opened and MP4 watched; no black or magenta capture
- [ ] 7. **report**: REPORT.md carries the real output of each command and the termination cause
- [ ] 8. **postMerge**: after the merge, the core tests pass and both apps compile in batchmode

For a visual change: the side-by-side against the art reference is attached, and for a gate frame the A3 judge ran
(host-run, advisory; only the owner's A1 rating certifies).

## Verdict line

<!-- Step 9: the line appended to docs/knowledge/verifications.jsonl after the merge (format in docs/AI_HARNESS.md). -->

```json
```

## Owner-reserved?

- [ ] This change deletes, skips or loosens a test or gate, adds a package, touches `.env` or credentials, or breaks a
      shared-package API. If ticked, it is a proposal for the owner, not a merge.
