# 0010 - Target headsets are Quest 3 and Quest 3S

Date: 2026-10-07. Status: accepted (owner, 2026-10-07).

Source: the owner's answer on the morning of 2026-10-07.

**Constraint.** Phase 2 Quest integration starts 2026-10-24 (`docs/PLAN.md:63`), and the milestone 4 budgets need a
device. Until now `docs/PLAN.md` named none, and the plans still name Pro and glasses.

**Decision.**

- Phase 2 targets Quest 3 and Quest 3S.
- A budget must hold on both, so where they differ, the lower one binds.
- Neither headset has eye tracking. On device, `Look` is a hand ray or a head reticle (`docs/plans/sundial.md:31`, `:123`,
  `:278`; `docs/plans/terrarium.md:128`).
- Quest Pro eye gaze and the glasses simulator are not targets. `docs/PLAN.md:31-32` already keeps the glasses out before
  2026-11-18.
- No hardware figure is stated here. The device numbers are to be sourced in the milestone 4 budgets file.

**Alternatives that lost.**

- Quest Pro eye gaze as a target: the owner named Quest 3 and Quest 3S.
- The glasses simulator profile: out of scope before 2026-11-18 (`docs/PLAN.md:31-32`).

**Consequences.** `docs/PLAN.md` gains row D10. The eye-gaze wording for Pro and glasses in the plans
(`docs/plans/sundial.md:31`, `:123`, `:278`; `docs/plans/terrarium.md:128`) is now out of target and reads as a later
option; this record does not edit those plans. The milestone 4 budgets file sources each device number and takes the
lower of the two headsets.
