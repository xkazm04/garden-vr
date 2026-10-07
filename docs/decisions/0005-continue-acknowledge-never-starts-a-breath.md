# 0005 - Acknowledging Continue? never starts a breath

Date: 2026-10-07. Status: accepted (App Master, 2026-10-07).

Source: `docs/architecture/review-2026-10.md` at `ce81b8f`, section 2.1 (first bullet under the drift heading),
Proposal 1 and section 6, candidate 1.

**Constraint.** The pause-and-continue machine exists twice, in `JarRitualController.cs` and
`DuskRitualController.cs`, and the copies have drifted. Decision 0002 says the PC mapping keeps each gesture's feel, and
the fresh-pinch rule is part of that feel. Gate S4 (`docs/PLAN.md:100`) says a ritual cut off is offered back, never
auto-restarted, and its only witness is the PlayMode `PauseAt_*` suite, which cannot run on this machine (no Unity
licence, see 0004).

**Decision.** In both apps, acknowledging Continue? re-arms the fresh-pinch rule from the live hand: the jar's rule
(`apps/terrarium/Assets/Scripts/Ritual/JarRitualController.cs:763-767`, `_needFreshPinch = true; _sawOpen = !LivePinch()`).
The hand releases and pinches again before a breath resumes.

**Alternative that lost.** The dusk behaviour. There the acknowledgement clears only `_awaitContinue`
(`apps/sundial/Assets/Scripts/Ritual/DuskRitualController.cs:231-235`). If the hand opened at any time during the pause,
`_sawOpen` is still true (`:168`), so the acknowledging pinch can meet the resume test on the next frame (`:169`). One
pinch then does two things, and whether it starts a breath depends on whether the provider still reports a pinch.
`DuskRitualTests.cs:170-174` checks only that no breath counts in a window after the pinch, so neither suite pins the
case. This is a reading of the code; no Unity run confirmed it.

**Consequences.** Core's `RitualPause` (review Proposal 1, `com.gardenvr.core/Runtime/Breath/RitualPause.cs`) carries the
rule, with a dotnet test that acknowledging Continue? while pinching needs a release; this also gives S4 a dotnet
witness beside the PlayMode one. `DuskRitualController` adopts it at its step B, which runs only where Unity can
compile and run `DuskRitualTests` (0004). Until then the dusk copy keeps the drift, and the jar copy is the reference.
