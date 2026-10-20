# Contest record - the facts the plan binds on

`docs/PLAN.md` and the app plans were written from the Habit Garden design contest and the competition's official
page. Those sources live outside this repository, under the contest root `C:\Users\kazda\kiro\personas\.contest\`
on the owner's machine (git-ignored in the Personas repo). An in-repo copy under docs/contest/ was untracked on
2026-10-05. This page keeps every fact the plan **depends on**, so an agent in a fresh clone or on another machine
plans from the same picture.

**This page is binding. The contest folder is provenance.** A fact is quoted here with its source path, which is
relative to the contest root unless it is a repo path. If you need a fact that is not here, follow the missing-input
rule in `AGENTS.md`: do not guess, and do not stop.

Transcribed 2026-10-05 from the last tracked copies (`docs/contest/{COMPETITION,TOOLING,OWNER-INTENT,README}.md`,
written 2026-10-02 from `staging/habit-garden/data/`). The round reports themselves (`arena/...`) were not re-read for
this page. Where a plan relies on them, it already restates the fact it uses, as noted below.

## 1. The competition (source: `staging/habit-garden/data/COMPETITION.md`, host transcription of the official page)

| Fact | Value |
|---|---|
| Competition | Meta VR Start Developer Competition 2026 |
| Window | opened 2026-09-24; **entries close 2026-11-18**; winners announced about 2026-12-11 |
| Prizes | $1M across about 20 prizes, top prizes $100,000; top awards per track plus special awards for craft across tracks. The Productivity track pays about $70K for first place (secondary source, TOOLING section C) |
| Track entered | **Productivity**: "apps that make you more effective anywhere or are designed around specific daily moments - multi-panel workspaces, task management, creative tools, or **habits tied to a recurring context (morning routine, commute, wind-down)**" |
| Other tracks | Entertainment (lean-back media); Gaming (hands-first or eyes-and-hands games, seated, no controllers) |
| Division | **New Experience**: conceived and built inside the window (from 2026-09-24); no pre-existing codebase |
| Hard requirement 1 | **Hands-first**: fully usable with hands end to end. Test: can someone complete the whole experience without ever pairing a controller? |
| Hard requirement 2 | **Policy compliance**: Meta VR / Start terms, Community Standards, Code of Conduct in VR, Developer App Policies, Content Guidelines |
| Judged guideline | **Seated-optimized**: seated, stationary, limit roomscale. Airplane-seat test: every interaction works in a two-foot radius |
| Judged guideline | **Easy in, easy out**: fast cold start, clean pause/resume, something satisfying in 10 minutes or less. One-bus-stop test: a complete moment in a short ride |
| Judged guideline | **Original, not a wrapper**: the core is entrant-built. Take-it-away test: remove the third-party service, is there still a project? Thin wrappers around YouTube, ChatGPT and similar are highly discouraged |
| Devices | Meta Quest and Meta VR Glasses. The store surfaces hand-tracked titles first to glasses owners without controllers |
| Suggested SDKs | Unity with Meta XR SDKs v81+ (v207) All-in-One; UE 5.8 with Oculus Integration v207+; Android (React Native via Expo, or Jetpack Compose); IWSDK (Node 20.19+). Organisers also promote AI tooling, including the Meta VR CLI |
| Submission 1 | The build: an APK on a "Competition" release channel plus its invite URL (Unity, Unreal, Android), or a hosted link (IWSDK/WebXR). Frozen at the deadline; it must stay reachable through judging |
| Submission 2 | A demo video **under 3 minutes** (headset capture, XR Simulator or emulator footage accepted), public on YouTube or Vimeo. Judges need not watch past 3 minutes |
| Submission 3 | A form: name, **140-character tagline**, track, division, a description of **at most 500 words** (inspiration, how it was built, future plans), how hand interactions are implemented, target launch date, team |
| 2025 precedent | Lifestyle winner Inkphony (the room becomes an instrument); Hand Interactions awards: Hand Survivor, Pocket Lands, Awesome Hand |

**Answered after this record was written:** the rules allow one entry per individual, so the owner has one entry in
total (PLAN decision D9, `docs/decisions/0006-one-competition-entry.md`).

## 2. Devices and tooling (source: `staging/habit-garden/data/TOOLING.md`, host-commissioned research 2026-10-02)

Marks: [V] official or primary source; [S] secondary; [U] could not be verified. A plan citing "TOOLING.md A", "B"
or "C" means this section: A is the Windows tooling, B the glasses and Horizon OS, C the habit research.

- **Meta VR Glasses** [V]: about 100 g, about 70x66 degree FOV (Quest 3: 110x96), colour passthrough and depth,
  $1,299.99, **ship spring 2027**. No one can test on real glasses before the deadline; only the Simulator and IWER
  glasses profiles exist. Input is eyes, hands and voice ("look to target, tap to select"). Same OS, SDKs and store as
  Quest.
- **Spatial anchors persist across sessions** [V]. This is what can keep the garden on the user's real table.
- Passthrough Camera API on Quest 3/3S [V]; on the glasses [U].
- No dedicated glanceable widget or ambient surface was found [U]. Notifications: Platform SDK user notifications [V];
  push "still under development" [S]. No health or wellness data API was found [U].
- **Meta XR Simulator v207** [V]: a PC OpenXR runtime with Quest and Glasses profiles, mouse/keyboard or webcam
  hands, synthetic passthrough, scene and anchors, and session capture to VRS. Headless CI is not documented [U].
- **Meta XR Operator** [V]: an MCP server for Unity 6 + Core SDK v207+ that drives head pose, hand gestures and gaze
  in the Simulator or on a device. It is the closest thing to scripted hand tests.
- **Meta VR CLI (`metavr`)** [V/S]: device management, install, logs, screenshots, traces and store publishing; no
  test assertions.
- Unreal: an open Meta bug, v207 plugins built for UE 5.7.0 fail to load in 5.8 [V].
- The researcher's conclusion (an opinion, not a mandate): Unity + Meta XR SDK v207 + XR Simulator, with Operator
  scripts for gestures, is the most testable-on-Windows loop. Only a headset can certify hand-tracking quality,
  comfort, cold start, performance and the true glasses FOV.

Habit science behind `AGENTS.md` rule 3:
- Seeing a broken streak lowers further engagement, even when the break was not the person's fault (Silverman &
  Barasch, J. Consumer Research 2023) [V, summary].
- One missed day had little effect on habit formation, with a median of 66 days to automaticity (Lally et al. 2010)
  [V].
- Popular "63% / 71% streak" statistics are probably fabricated blog numbers; do not cite them.
- VR meditation evidence is a pre/post pilot, not an RCT. **The entry may make no medical claims** (`AGENTS.md` rule 6).

## 3. The owner's intent (source: `staging/habit-garden/data/OWNER-INTENT.md`, 2026-10-02), summarised

- Habit Garden is the owner's **realistic** competition aim. The owner is building a second, experimental VR entry
  in parallel, so this one must stay achievable.
- Some habits can be practised inside the app, for example guided breathing.
- The design comes first, and a large part of it should be testable on a Windows machine with Meta's dev tools.
- The owner builds with autonomous coding agents that can run anything scriptable on Windows 11 but cannot wear a
  headset. **The owner's headset time is the scarcest resource; anything an agent can verify on Windows, it should.**

## 4. The contest rounds (source: the contest root's `arena/` and `staging/habit-garden/`)

| Round | What | Where the repo already carries what the plan uses |
|---|---|---|
| 1 | Three design reports; the owner shortlisted Breathing Terrarium and Sundial Bed | scope, journey, gestures and growth rules restated in `docs/plans/terrarium.md` and `docs/plans/sundial.md`, sections 1-4 |
| 2 | Spikes: Unity (Operator-driven hands) and IWSDK (IWER, glasses gaze) | the pure C# core became `shared/packages/com.gardenvr.core/`; the IWSDK SVG dial-face script is in `tools/blender/from-iwsdk/` |
| 3 | Fidelity gate: both stacks rendered both styles against the owner's frames | state figures restated in PLAN section 2; round-3 renders kept as rungs in `tools/fidelity/rungs/`; Blender scripts in `tools/blender/` |
| - | Owner's art choice and storyboard | copied to `docs/art/OWNER-CHOICE.md` and `docs/art/ART-STYLE-STORYBOARD.md`; reference frames in `shared/assets/art-reference/` |

**Windows workarounds from round 2** (source: `arena/habit-garden-r2/entries/claude-claude-opus-5-5_high-v1/variant-1/RUNBOOK.md`
and section 04 of its `index.html`). The runbook lists 12. Those named in the plan are: MSI install needs admin; EPERM
in the package cache; Gamma vs Linear colour space; the Operator layer registered twice; the Vulkan `dzn` crash; JDK
missing for Android builds. The other six were not transcribed into the repo before the copy was untracked. A
Phase 2 task that needs them reads the runbook on the owner's machine, or records "input missing" (`AGENTS.md`).

**Headset checklists:** the round 2 and round 3 checklists (round-3 report, section 07) are the starting templates for
headset sessions H1-H4 (PLAN section 3). They were not copied. They are needed only from Phase 2 (2026-10-24), and the
host transcribes them into the Phase 2 queue then.

## 5. Which plan decisions depend on which fact

| Plan item (`docs/PLAN.md`) | Depends on | Section above |
|---|---|---|
| Goal 1: two entries in the Productivity track, New Experience division, close 2026-11-18 | window, track wording, division | 1 |
| Goal 3/4: PC-first until the gate, Quest only after it; the swap is a provider change | no headset for agents; owner headset time scarce; Simulator and Operator limits | 2, 3 |
| Goal 5 and `AGENTS.md` rule 3: honest habits, no streaks | streak and missed-day research | 2 |
| Non-goals: no Meta VR Glasses beyond a simulator profile, no notifications, no LLM features | glasses ship spring 2027; push notifications not ready; "not a wrapper" guideline | 1, 2 |
| `AGENTS.md` rules 1 and 2: hands-first intents, seated, under ten minutes | hard requirement 1; seated and easy-in guidelines | 1 |
| `AGENTS.md` rule 6: no medical claims | VR meditation evidence is a pilot only; policy compliance | 1, 2 |
| Section 3 Phase 3: submission deliverables (video under 3 min, 140-character tagline, 500-word form, Competition channel invite URL) | submission 1-3 | 1 |
| Decision D1 (Terrarium is the wind-down moment) | the track names wind-down as a recurring context | 1 |
| Decision D9 (two separate entries) | **not in the record**; open | 1 |
| Section 10 risk "owner time" | parallel second entry | 3 |
| Section 10 risk "Unity on Windows workarounds re-bite" | round-2 runbook (6 of 12 named here) | 4 |
| Section 10 risk "Quest phase too short" | round 2/3 built APKs with hand tracking and passthrough; headset checklists | 4 |
