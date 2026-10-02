# Lessons to forge into the registry

Measured lessons from this project, waiting to be forged into `C:\Users\kazda\kiro\ai-registry` (game-production /
software-engineering). Each line: date, what happened (measured), the rule it suggests, target subject.

- 2026-10-02 - **A headless coding agent ends its run when it ends a turn, even "waiting for" a background process.**
  Grok 4.7 (T-SUN-001) launched a Unity import in the background, replied "I will read the test result when this import
  exits", and the run stopped at 37 min with the whole package uncommitted; the import then failed on a compile error
  nobody read. Rule: in agent instructions, every long process runs in the foreground and the agent reads its log
  before the turn ends; the runner treats "exit 0 without REPORT" as failed, which caught it. Target:
  agent-operations / unattended-build-loop, software-engineering / subprocess-lifecycle.
- 2026-10-02 - **PowerShell does not wait for GUI-subsystem executables** (Unity.exe), so a "finished" batchmode run
  can leave a partial log (seen in T-TER-003). Rule: `Start-Process -Wait` or a blocking wrapper for Unity batchmode.
  Target: game-production / engine tooling.
- 2026-10-02 - **Unity 6.6 turns `Object.GetInstanceID()` into a compile error (CS0619)** and Input System 1.17 fails to
  compile on 6.6; 1.20.0 works. Rule: pin package versions to the editor's verified set and give agents the API notes.
  Target: game-production / engine upgrade hygiene.
- 2026-10-02 - **Cross-agent task dependencies must wait for the merge, not for the other agent's "done".** Two worktrees
  on separate branches: a dependency counts only once its code is on main and merged into the dependent branch. The
  queue now distinguishes `done/` (same agent) from `merged/` (cross-agent). Target: agent-operations / fleet
  orchestration.
- 2026-10-03 - **An instruction does not fix a runner-level failure mode.** After `AGENTS.md` forbade ending a turn while
  Unity ran, Grok did it again (T-TER-005, 64 turns, "Unity is compiling ... the prefab wire-up runs as soon as that
  compile succeeds"). The fix that holds is in the runner: a clean exit without the required artefact resumes the same
  session (`grok -r <sessionId> -p "continue..."`, verified to keep context) up to twice. Target: agent-operations /
  unattended-build-loop.
