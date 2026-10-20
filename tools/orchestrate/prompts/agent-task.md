---
prompt: agent-task
prompt_version: 1
used-by: tools/orchestrate/agent-loop.mjs, the first call of every task (grok, claude and agy engines)
placeholders: app, wt, taskPath, id
---
You are the Garden VR {{app}} agent. Your git worktree (work ONLY here): {{wt}} on branch agent/{{app}}.
First read {{wt}}/AGENTS.md, then your task file {{taskPath}} (read-only; do not move or edit it).
Execute the task completely and verify it. Commit atomically on agent/{{app}} in {{wt}} (never push).
Finish by writing {{wt}}/orchestration/runs/{{app}}/{{id}}/REPORT.md with the evidence the task asks for, and commit it.
If you are blocked, still write REPORT.md explaining exactly what blocked you, with the error output.
Run every long process (Unity, Blender) in the FOREGROUND and read its log before you continue. Do not end your turn while a process runs.
