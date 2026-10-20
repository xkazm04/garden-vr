# tools/orchestrate/prompts - the versioned prompt and agent library

The text the host sends to an agent, kept as files so a change is a reviewed diff with a version, never an edit to a
string literal that nobody notices. Host-owned, like the rest of `tools/orchestrate/` (guard W1).

| File | Used by | When |
|---|---|---|
| `agent-task.md` | `tools/orchestrate/agent-loop.mjs` | first call of every task, all engines (grok, claude, agy) |
| `agent-continue.md` | `tools/orchestrate/agent-loop.mjs` | same-session resume when an engine exits 0 without `REPORT.md` (up to twice) |
| `task-template.md` | the host, by hand | the skeleton every `orchestration/queue/<app>/<id>-<slug>.md` task is cut from (PLAN section 5, Task lifecycle) |

Not here, and why:
- **The agent spec** is `AGENTS.md` (canonical, `.ai/manifest.yaml`); every dispatch prompt tells the agent to read it
  first.
- **The judge prompts** for the art-fidelity lab are the versioned packet `tools/fidelity/judge/packet-v1/`
  (instructions, rubrics, schemas, `manifest.json`); a new judge prompt is a new `packet-vN/`, never an edit in place.
  The older A3 judge scripts in `apps/*/Art/Scripts/*_judge.py` keep their prompt inline; each verdict that used one
  pins it by the script's git blob sha (`judge.blob` in `docs/knowledge/verifications.jsonl`).
- **The scan-sweep lenses** live in the ai-registry skill `scan-sweep` (`references/lenses.md`), shared by every repo
  that adopts it. This repo pins the version it runs against in `.ai/manifest.yaml` `skillVersions:`; bump the pin
  when a sweep runs on a newer skill.

## Rules

1. Each file opens with front matter naming itself and an integer version:
   ```
   ---
   prompt: agent-task
   prompt_version: 1
   ---
   ```
   `prompt` is the file name without `.md`. Other front-matter lines are documentation (`used-by`, `placeholders`), or,
   in `task-template.md`, the task fields themselves.
2. **Any change to a file bumps `prompt_version`**, and the commit message says why the agent needs the new text.
   Guard rule P1 (`tools/guard/check.mjs`) refuses a commit or a range that changes a prompt file without raising its
   version, and fails `--all` on a file whose header is missing or malformed.
3. Placeholders are `{{name}}`. The loop fills `app`, `wt`, `taskPath` and `id`, and it refuses to start a task when a
   placeholder is left unfilled.

## What a run records

At start the loop reads each prompt once and logs `prompts: agent-task@v<n> <blob> ...`. A relaunch
(`tools/orchestrate/relaunch.sh`) picks up a new version. For every task it then writes:
- `orchestration/runs/<app>/<id>/prompt.txt`, the exact first message the engine received;
- a `prompts` field on the task's `tasks.jsonl` line, `{"task": {"version": n, "blob": "<git blob sha>"}, "continue":
  {...}}`.

So "why did the agent behave differently this week" starts with comparing two `tasks.jsonl` lines. A different blob
means the prompt changed (`git log --find-object=<blob>` names the commit), and the same blob rules the prompt out.
The prompts that have changed so far all followed a measured runner failure: the continuation and foreground lines
after T-SUN-001 and T-TER-005, and the agy fallback after T-TER-042/043 (`docs/knowledge/lessons.md`).
