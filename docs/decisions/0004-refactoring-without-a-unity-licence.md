# 0004 - Refactoring while no Unity licence exists

Date: 2026-10-07. Status: accepted (App Master, 2026-10-07) under the owner's brief; the owner may overrule.
`docs/PLAN.md:184` is not edited; its Unity compile is deferred, not dropped.

Source: `docs/architecture/review-2026-10.md` at `ce81b8f`, section 1 and section 6 (candidates 4, 5, 9; candidate 10
follows from them). The review measured `main` at `89971d1`.

**Constraint.** This machine has no Unity licence. The owner's brief to the App Master says: "Unity batchmode has no
licence on this machine: the gate is `dotnet test shared/core-dotnet`; never claim a Unity build or play-mode test
passed." `docs/PLAN.md:184` requires that a shared-package change merges only if both apps compile against it on
`main` in the same check, and nobody can run that check now. Every step B of an extraction edits a Unity-compiled file,
and only the Unity suites that name that file can check the edit (review section 1, point 2).

**Decision.** An extraction runs in two steps.

- Step A is purely additive in `com.gardenvr.core`: new files or members, no signature or behaviour change. It is
  tested by dotnet and merges on `dotnet test shared/core-dotnet` alone. The first session that can run Unity compiles
  both apps against every core change merged since `89971d1` and records the result.
- Step B (an app file adopts the core code) runs only in a session that can compile Unity and run the suites that name
  that file. Until then the app copy is frozen.
- A new core type never takes the name of an app type: an app namespace would win silently, and no Unity compile can
  catch it here. Today no core type shares a name with an app type (review section 1, point 2).

**Alternatives that lost.**

- Wait for a licence: loses because it stops all milestone-3 work for an unknown time, while step A is safe to land. An
  additive change cannot break the app compile except through a type-name collision, which the naming rule closes.
- Refactor the app files now with no net: loses because the net reaches only the rules inside the top three files
  (about 250 of 7,009 lines); the rest is rendering and wiring that only Unity tests see (review section 1).
- `git mv` the engine-free files into core now: loses because it changes what the Unity assemblies compile, with no
  licence to check it (`docs/PLAN.md:184`); this is the alternative review Proposal 4 rejects. Those files are compiled
  in place by a dotnet test project until a licence exists.

**Consequences.** The suspension of `docs/PLAN.md:184` is on paper, with its debt named: one Unity compile of both apps
over all core changes since `89971d1`. Milestone 3 reports code lines and duplicate rule sites beside the hotspot
score, because a step B commit adds one churn commit and so raises a file's score unless it removes more than
`codeLines / (churn + 2)` code lines (`docs/health/BASELINE.md:27`; review section 1, point 4). Core paths keep their
owners (`docs/PLAN.md:193-194`). Decisions 0001 to 0003 are unchanged.
