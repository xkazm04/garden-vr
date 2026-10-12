# 0016 - Sundial carries the upgrade; Terrarium is parked

Date: 2026-10-09. Status: accepted (owner's answer of 2026-10-09: "Lets pick Sundial as the winner to chase from now on").

Source: `docs/plans/upgrade-2026-10.md` section 8, where the artboards were the method of the pick, and decision 0015.
The eight artboards are in `docs/art/artboards/` and on the scoring page https://claude.ai/artifact/QMY6GVyVjtpFW811Z3Tmru.
The owner picked directly; the scoring sheet holds no scores.

**Constraint.** Decision 0006 allows one competition entry, and the owner left open which Garden app would carry it.
Decision 0015 keeps both apps until the artboards settle which style carries the upgrade. The schedule
(`docs/plans/upgrade-2026-10.md` section 9.4) needs one app for step B from 2026-10-15.

**Decision.**

- **Sundial (Field Notebook) is the app for the upgrade and for the Garden VR entry.** Every step B task, the re-scoped
  gate on 2026-10-23, and Phase 2 apply to `apps/sundial` only.
- **Terrarium is parked, not deleted.** Its art and feature work stop. Its code, art and tests stay in the repository.
  It still compiles against the shared packages, because `docs/PLAN.md:184` requires a shared-package change to
  compile in both apps. It gets no new features and no gate pack.
- **The shared core stays shared.** The habit, record, catch-up, coach and break rules are in `com.gardenvr.core`;
  Terrarium can adopt them later without a rewrite.
- **Why Sundial fits** (from the plan and the gate packs, not from scores):
  - Its three time arcs with three rows each already hold nine habits. That is `HabitProfiles.MaxLive`, and three a
    row is the existing `ArcCapacity`.
  - It already has the morning stretch, the week dial, voice for the dusk ritual and the yesterday backfill that
    catch-up widens.
  - Its round-3 render was judged closer to its references, in 15 draws and 10.4k triangles. Terrarium fails gate A2
    and A3 on the glass.

**Alternatives that lost.**

- Terrarium (Night Moss): the stronger single visual moment, but the jar holds few plants, so it reads nine habits
  worst, and its glass is the open art risk.
- Keeping both through 2026-10-23: step B cannot run twice in the time left.
- Merging the two into one garden: not chosen in 0015.

**Consequences.**

- `docs/plans/upgrade-2026-10.md`: section 3 gains this answer; C4 and C10 change state in section 9.5; the step B
  task files are `docs/plans/upgrade-step-b.md`.
- `docs/PLAN.md` gains row D12. `docs/decisions/0006`: the open question of which Garden app carries the entry is
  answered (Sundial). Whether Garden VR or Mage Arena VR is the entry stays open, as 0006 says.
- `apps/sundial/Assets/Scripts/Tend/SundialSave.cs`: `RowChoices.ProfileKnown` is on (upgrade C11, under 0012 (c)),
  so the habit profile reaches the Sundial save. Saves without a profile are unchanged byte for byte.
