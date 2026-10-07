# 0007 - A Sundial plant's lifetime is monotone

Date: 2026-10-07. Status: accepted (App Master, 2026-10-07).

Source: `docs/architecture/review-2026-10.md` at `ce81b8f`, section 6, candidate 2 (line 449).

**Constraint.** `AGENTS.md` rule 3: honest habits, growth only rises. `SundialRules.LifetimeKept` counts through
`Ledger.KeptDaysInWindow` (`shared/packages/com.gardenvr.core/Runtime/Sundial/SundialRules.cs:172-178`), which skips days
after today (`shared/packages/com.gardenvr.core/Runtime/Common/Ledger.cs:170`, the `e.Day > today.Index` test). So
setting the clock back lowers the stage. The app hides that with a session floor
(`apps/sundial/Assets/Scripts/Tend/SundialController.cs:87` and `:630-634`), which forgets everything on restart.

**Decision.** A plant's lifetime counts every live kept day since the habit was created, whatever the clock says today,
and an undo still removes a day. The rule lives in core as `SundialRules.PlantMonotone`
(`shared/packages/com.gardenvr.core/Runtime/Sundial/SundialRules.cs:157-170`) and `Ledger.KeptDaysFrom`
(`shared/packages/com.gardenvr.core/Runtime/Common/Ledger.cs:180-190`). Both landed additively under 0004 at `e44e3d0`,
with `shared/core-dotnet/GardenVR.Core.Tests/PlantMonotoneTests.cs`; `dotnet test shared/core-dotnet` passes 239 tests.
The value stays derived from the ledger and is never stored.

**Alternatives that lost.**

- The review's in-place change to `LifetimeKept`: 0004 forbids it, because it changes behaviour of an existing member
  that Unity compiles and no licence can check.
- Keeping the session floor in the app: it resets on restart, and it is a rule living in app code against `AGENTS.md`
  rule 4.

**Consequences.** Step B (switch `SundialController` to `PlantMonotone` and delete the floor) runs only in a session that
can compile Unity and run the suites that name that file (0004). Until then `Plant` and the floor are unchanged and the
app copy is frozen.
