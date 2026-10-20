---
prompt: agent-continue
prompt_version: 1
used-by: tools/orchestrate/agent-loop.mjs, the same-session resume when an engine exits 0 without REPORT.md (up to twice)
placeholders: none
---
Continue the task. Your previous turn ended while you were waiting on a process (likely Unity), so nothing after that ran. Check whether it finished (read its log; if still running, wait in the FOREGROUND), then finish the remaining steps, verify, commit, and write REPORT.md. Do not end your turn until REPORT.md is committed.
