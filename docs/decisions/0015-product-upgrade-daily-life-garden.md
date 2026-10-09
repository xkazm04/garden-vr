# 0015 - Garden VR grows from a breathing ritual into a daily-life garden

Date: 2026-10-09. Status: accepted (owner's answers of 2026-10-09). The pick between Terrarium and Sundial is left to
decision 0016, made from the artboards.

Source: `docs/plans/upgrade-2026-10.md`. Section 1 has the analysis, section 3 has the owner's answers round by round.

**Constraint.** The MVP has one in-app activity, a paced breath (`docs/plans/terrarium.md` section 2,
`docs/plans/sundial.md` section 2), and life habits are 3 presets checked off with no free text. The post-MVP backlogs
add more micro-rituals of the same kind. `docs/PLAN.md:31-32` rules out notifications, accounts, sync and LLM features.
For an entry in the Productivity track (decision 0006: one entry, Garden VR the safer bet), that leaves a weak use case
with no clear answer to why it needs VR.

**Decision.**

- The product becomes a daily-life garden: custom habits with schedules in four life-area zones, hand-tracked breaks
  with depth eye rest, an AI onboarding interview, an evening spoken reflection, catch-up for days away, and monthly
  totals. Breathing stays as one in-app habit. The competition slice is `docs/plans/upgrade-2026-10.md` section 4.
- Principles kept unchanged: growth only rises; no medical claims; hands first for every action.
- Principles changed:
  - Custom habit names by voice are allowed; presets by pinch stay as the full fallback.
  - At most 2 headset nudges a day, inside windows the user chooses, with no guilt copy.
  - The record shows monthly totals. A consecutive-day count is still never shown, so `AGENTS.md` rule 3 and gate U5's
    `streak` grep stand as written.
  - An opt-in cloud LLM is allowed for the slice. Speech-to-text stays on device, a scripted coach path is always
    complete, and the API key lives only in a relay that stores nothing. On-device first remains the direction.
  - Meta Horizon mobile is the only companion. Habits done outside VR are logged by catch-up at the next visit, at most
    2 days back, drawn late.
- Still non-goals: accounts, cloud sync, social features, controllers, roomscale.
- Both apps stay until the artboards (8 frames, 4 moments in 2 styles, scored on a rubric) settle which style carries
  the upgrade, by 2026-10-14. The 10-23 gate is re-scoped to the upgraded slice for the chosen app.

**Alternatives that lost.**

- Stay with breathing plus micro-rituals (the current backlogs): keeps a weak use case and no answer to why it needs VR.
- Deep-work focus sessions as the lead job: not chosen by the owner, though `FocusBlock` exists in core.
- A PWA or native companion: not chosen; Horizon mobile only.
- On-device LLM for the slice: too risky on Quest 3S budgets before 11-18. It stays on the roadmap.
- Forgiving streaks: not chosen; monthly totals only.
- Merging the two apps now: the owner wants the artboards to decide first.

**Consequences.**

- `docs/PLAN.md` section 1, non-goals: amended by this record (a pointer is added there). Section 3 dates and section 4
  gate items change as `docs/plans/upgrade-2026-10.md` sections 9 and 10 say, once decision 0016 names the app.
- New core work is additive step A under decision 0004. App adoption is step B and needs a Unity session. If none
  exists by 2026-10-15, the gate extension rule applies at once (gate on 2026-10-30).
- The LLM provider (owner's answer, 2026-10-09): the Claude API with Claude Haiku 5.5 for every coach call from the
  app, through the relay. Local tests on the Windows machine call the Claude Code CLI through a development-only
  `CliCoachClient` that the Quest build excludes. Details in `docs/plans/upgrade-2026-10.md` section 7.
- Catch-up starts at 2 days back and is a setting. The zones are Body, Mind, Work and Connection. The owner alone scores
  the artboards.
