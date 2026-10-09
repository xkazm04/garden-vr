# Garden VR upgrade - from a breathing ritual to a daily-life garden (plan, 2026-10-09)

Status: accepted, built from the owner's answers of 2026-10-09 (six rounds of questions, recorded in section 3). The
principle and non-goal changes are recorded in `docs/decisions/0015-product-upgrade-daily-life-garden.md`. The LLM
provider is decided (section 7). The pick between Terrarium and Sundial is still open; it is made from the artboards in
section 8 and recorded as decision 0016.

| Read with | For |
|---|---|
| `docs/PLAN.md` | the programme this plan re-scopes (sections 1, 3, 4) |
| `docs/plans/terrarium.md`, `docs/plans/sundial.md` | each app's current MVP, rules and backlog |
| `docs/decisions/0004`, `0012` | additive core work while no Unity licence exists (step A / step B) |
| `docs/decisions/0006` | one competition entry; Garden VR is the safer bet |

## 1. Why upgrade: what the docs show

**The product planned to grow beyond breathing, but only a little, and the growth stayed inside the same idea.**

- **The MVP is breathing plus preset check-offs.** Terrarium is a six-breath pause that grows a fern, with up to 3 of 6
  preset companion habits (`terrarium.md` section 2). Sundial is a shadow-clock dial with one preset habit per arc and a
  three-breath dusk pause as its only in-app ritual (`sundial.md` section 2). Free-text habits are out ("no keyboard in
  a hands-only evening").
- **Expansion was a backlog of micro-rituals** (`terrarium.md` section 9, `sundial.md` section 9): three good things,
  box breathing, a stretch, a mindful sip, one word, a focus block, gratitude, a week dial, seasons. Most of them already
  exist in core (`FocusBlock`, `ThreeGoodThings`, `OneWord`, `StretchSession`, `WeekDial`, `Season`, `RimScrub`,
  `GratitudeRecord`) and are partly wired into the apps. They are variations on the same wordless ritual around one
  pretty object, not a wider product.
- **The non-goals remove the usual retention levers** (`PLAN.md` section 1): no notifications, no accounts, no sync, no
  LLM. The only reason to return is that the garden looks nicer, and the user has to put a headset on to see it.
- **Recent effort went to code health, not product.** The last 50 commits are refactors, analyzer sweeps and decision
  records. Terrarium's gate pack fails A2 and A3 on the glass (`apps/terrarium/GATE.md`).

Structural weaknesses this plan answers:

| Weakness | Answer in this plan |
|---|---|
| Entered in the Productivity track, but the core is a wellness breath | daily review, real habits across life areas, desk-worker breaks |
| No reason to need VR: a preset check-off is faster on a phone | hand-tracked movement and depth-based eye rest only work in mixed reality; a spoken coach in the room |
| Too narrow for a habit tracker: 3 presets, no custom habits, no insight | custom habits by voice, flexible schedules, life-area zones, monthly totals |
| Two apps compete for one entry | artboards decide which style carries the upgrade (section 8) |

## 2. The vision (long term)

**Garden VR is a calm mixed-reality garden on your desk that grows from your whole day: the habits you choose, the
breaks you take, and a short spoken review each evening, guided by a quiet coach that speaks in the garden's voice.**

| Moment | What happens | In the slice? |
|---|---|---|
| First run | A 2-minute spoken interview with the coach proposes 2-3 starter habits with cues and zones; you accept each with a pinch | yes |
| Morning | Speak 1-3 intentions; each becomes a seed in the garden; in the evening pinch to keep, carry over or let go | no (post-slice) |
| During the workday | A headset notification offers a break inside your chosen windows; a hand-tracked seated stretch or a depth eye-rest waters the Body zone | yes (in-headset); outside VR via catch-up |
| Coming back after time away | "What happened since?" catch-up, by voice or pinch; logged days are drawn late, never as on time | yes |
| Evening | A 60-second spoken reflection; the coach turns it into one line kept in the garden's journal | yes |
| Weekly | A 5-minute review: the week replays, the coach names patterns, you choose next week's focus | no (post-slice) |
| Over months | Zones fill with species; monthly totals per habit and per zone; growth only rises | yes (monthly totals) |

The breathing ritual stays as one in-app habit among others in the Mind zone, not as the product.

## 3. Owner decisions (2026-10-09)

| Round | Question | Answer |
|---|---|---|
| 1 | Core jobs beyond breathing | Daily plan and review; real habit tracking; body and screen breaks (deep-work focus sessions not chosen) |
| 1 | Horizon | Both: a competition slice of a bigger vision |
| 1 | Two apps | Keep both separate for now; **artboards of the expected states decide which way to go**, because the two styles will not suit every use case equally |
| 1 | Principles to relax | Custom habits and voice input; reminders and a companion; an AI coach (social not chosen) |
| 2 | Daily loop | Voice intentions become seeds; evening AI reflection; weekly review ritual (calendar import not chosen) |
| 2 | Habit model | Custom habits with flexible schedules; garden zones per life area (habit stacking cues and the insight dashboard not chosen) |
| 2 | Breaks | Companion nudges with the payoff in VR; hand-tracked movement; eye rest with depth (in-headset work mode not chosen) |
| 2 | AI coach | Onboarding interview; adaptive suggestions; voice of the garden (privacy-first limits were not picked as a separate item; section 6.5 keeps the minimum needed by the honesty principles) |
| 3 | Companion | Meta Horizon mobile only (no PWA, no native app) |
| 3 | AI stack | On-device first |
| 3 | Non-negotiable principles | Growth only rises; quiet nudges only; no medical claims |
| 3 | Competition slice | Custom habits and zones; AI onboarding and reflection; hand-tracked breaks and eye rest |
| 4 | On-device AI for the slice | **Allow opt-in cloud for the slice**: speech stays on device; the LLM call goes to the cloud only after explicit opt-in |
| 4 | Habits done outside VR | **Catch-up at the next VR visit**; nudges are headset notifications only |
| 4 | Record | **Monthly totals only**; never a consecutive-day count |
| 4 | Artboards | All four moments in both styles: onboarding and zones, break moments, evening reflection, week and month record |
| 5 | Schedule | **Re-scope the 10-23 gate** around the upgrade |
| 5 | Artboard method | Image-gen frames plus a rubric |
| 5 | LLM provider | Compared in this plan (section 7); the owner chooses |
| 5 | Deliverable | This plan plus decision record 0015 |
| 6 | LLM provider | **Claude API with Haiku** for every coach call from the app; local tests on the Windows machine call the Claude Code CLI instead of the API |
| 6 | Catch-up depth | **2 days back** to start; adjustable later |
| 6 | Zone names | **Body, Mind, Work, Connection** approved |
| 6 | Artboard scores | **The owner alone** |

## 4. The competition slice (ship 2026-11-17)

In the slice, in priority order. The cut line is in section 4.2.

1. **Custom habits with schedules.** Name a habit by voice, or pick a preset by pinch (the fallback when the mic is
   off). Schedules: daily, N times a week, or chosen weekdays. An optional quantity target (8 glasses, 20 pages) is
   counted with repeated pinches. At most 9 live habits.
2. **Life-area zones.** Four zones: Body, Mind, Work, Connection. Each zone is a region of the garden with its own
   species family; a habit's plant grows in its zone. The garden reads as life balance at a glance.
3. **AI onboarding interview** (opt-in cloud LLM, scripted fallback). At first run the coach asks up to four short
   spoken questions and proposes 2-3 habits, each with a name, zone, schedule and cue. You accept, change or skip each
   by pinch. Without opt-in or without network, the same four questions run as a scripted tree over the presets.
4. **Evening reflection** (opt-in cloud LLM, scripted fallback). Hold to speak for up to 60 s; the coach returns one line
   (90 characters or fewer) that is kept in the garden's journal. The transcript is never stored. Without the LLM, you
   pick one of six word stones (the existing `OneWord`) and that is the journal line.
5. **Hand-tracked breaks.** A seated stretch: reach to three marks and the zone's plants sway with your hand (extends
   `StretchSession`); a wrist and hand mobility set of three moves. Done in the headset, a break waters the Body zone.
6. **Depth eye rest.** The garden sends a glowing mote to the far wall in passthrough; you follow it and rest your eyes
   on it for 20 s. On PC the room plate gives the far point.
7. **Quiet nudges.** At most 2 headset notifications a day, inside windows the user chooses, with no guilt copy: "The
   garden has a break ready." Never "you missed".
8. **Catch-up at the next visit.** "What happened since?" lists the scheduled habits from the days since the last visit
   (at most 2 days back, the owner's starting value, adjustable); pinch or say which happened. Those days are drawn late (hatched or dimmer), never on time.
9. **Monthly totals.** Per habit: "kept 18 days this month"; per zone: a fill level. Never a consecutive-day count.

### 4.1 Out of the slice (post-slice roadmap, section 11)

Voice intentions as morning seeds; the weekly review ritual; adaptive suggestions; on-device LLM; a companion beyond
Horizon mobile; social features (not chosen).

### 4.2 Cut line (dropped first, in this order, if the 10-23 gate is at risk)

1. Quantity targets: every habit becomes yes or no.
2. Wrist and hand mobility set: the stretch only.
3. The LLM half of the reflection: word stones only (the scripted path).
4. The LLM half of onboarding: the scripted tree only.

Never cut: custom habits, zones, one hand-tracked break, eye rest, catch-up, monthly totals, growth only rises.

## 5. Principles after the upgrade

| Principle | Before | After (decision 0015) |
|---|---|---|
| Growth only rises | rule | **unchanged**, non-negotiable |
| No medical claims | rule | **unchanged**, non-negotiable; also applies to coach output (section 6.5) |
| Quiet by design | one reward sound, no notifications | **at most 2 nudges a day** in user windows, no guilt copy, no sound for a missed day |
| No streak counters | rule | **monthly totals only**; a consecutive-day count is still never shown, so gate U5's `streak` grep stays |
| Habits | presets only, no free text | **custom names by voice**, with presets by pinch as the fallback |
| Hands first | every interaction through hand intents | unchanged for every action; voice is an addition for naming and reflecting, never the only path |
| LLM | non-goal | **opt-in cloud LLM for the slice**, scripted fallback always complete; on-device later |
| Accounts, sync | non-goals | **still non-goals**; the relay is keyed per install and stores nothing |
| Companion app | none | Meta Horizon mobile only; habits done outside VR are logged by catch-up |

## 6. Feature design

All rules go in `com.gardenvr.core` as pure C# (`AGENTS.md` rule 4), additive first (step A under decision 0004), with
`dotnet test shared/core-dotnet` as their check. A new core type never takes the name of an app type (0004).

### 6.1 Habit model

`HabitDef` (`shared/packages/com.gardenvr.core/Runtime/Common/Ledger.cs:13`) grows new optional fields; old saves read
with defaults, so a save migration is a version bump plus defaults.

```csharp
public enum LifeZone { Body, Mind, Work, Connection }

public sealed class HabitSchedule
{
    public ScheduleKind Kind;      // Daily, TimesPerWeek, Weekdays
    public int TimesPerWeek;       // 1..7, for TimesPerWeek
    public byte WeekdayMask;       // bit 0 = Monday, for Weekdays
    public bool IsDue(GardenDay day, Ledger ledger, string habitId);
}

// new optional fields on HabitDef
public string Name;                // user text; null means the preset label
public LifeZone Zone;              // default from the preset
public HabitSchedule Schedule;     // null means Daily
public int? Target;                // quantity target; null means yes or no
public string Unit;                // "glasses", "pages"
```

- A quantity habit counts as kept on a day when its tends that day reach `Target`. Each pinch appends one `TendEvent`
  (append-only, as today); undo still sets `UndoneAtUtcMs`.
- Growth stays monotone: a plant's size comes from lifetime kept days (`SundialRules.PlantMonotone`, decision 0007), for
  every schedule kind.
- A `TimesPerWeek` habit is never "missed" on a single day; the week shows how many of N were kept.

### 6.2 Zones and the record

- `ZoneRecord.Fill(ledger, zone, month)`: kept days divided by due days for the month, shown as a fill level, never a
  percentage string and never red.
- `MonthlyRecord.KeptDays(ledger, habitId, month)`: the number behind "kept 18 days this month".
- Neither type computes a run of consecutive days. A property test asserts that no public core member returns one.

### 6.3 Catch-up

- `CatchUp.Pending(ledger, habits, lastVisitDay, today)`: due, unkept habit-days from `lastVisitDay + 1` to
  `today - 1`, at most 2 days back.
- Logging one appends a `TendEvent` with `Source = Backfill` and `Late = true`, the same rule as Sundial's yesterday
  backfill, widened from 1 day to 2. Each day can be logged once and is drawn late forever.

### 6.4 Breaks

- `BreakSession` wraps `StretchSession` (three reach targets) and a new `HandMobility` set (three moves, each a held
  pose for 3 s). On PC the moves are scripted intents; on Quest they come from XR Hands through `com.gardenvr.input`.
- `EyeRest`: a 20 s focus target placed at the farthest point the room provider reports (Quest: scene mesh or a fixed
  3 m; PC: the room plate's far wall).
- `NudgePlanner.Next(settings, ledger, now)`: the next nudge time inside the user's windows, at most 2 a day, none after
  a break was taken in the last 50 minutes. The app hands the time to the platform notification API.
- A break is a habit of kind `InAppRitual` in the Body zone, so it uses the same ledger, record and catch-up.

### 6.5 Coach, voice and privacy

- **Seams.** `ISpeechToText` (on-device: PC provider for development, Quest provider chosen in the spike, section 9)
  and `ICoachClient` with three implementations:
  - `RelayCoachClient`: the opt-in cloud call from the app, through the relay, to the Claude API (section 7).
  - `CliCoachClient`: development only, PC only. It calls the Claude Code CLI on the Windows machine, so local tests
    need no API key and no relay. It never ships: the Quest build excludes it.
  - `ScriptedCoach`: the deterministic fallback in core.
  The app always runs `ScriptedCoach` when the coach is off, slow (over 4 s) or fails, including a refusal.
- **Onboarding output** is structured: up to 3 `{name, zone, schedule, cue}` objects, validated in core against the
  habit model before anything is shown.
- **Reflection output** is one line of 90 characters or fewer.
- **Guards in core, applied to every coach line before it is shown:** no medical words (the same list the string census
  uses), no em dash, no consecutive-day counts, no guilt phrasing; a line that fails is replaced by the scripted line.
- **Persona.** The coach speaks as the garden: the jar's spirit in Terrarium, the notebook's narrator in Sundial. Its
  lines are short, second person and calm. The voice reuses the cast voice in `Audio/Voice/CAST.md` for scripted lines;
  LLM lines are shown as text (TTS of live lines is post-slice).
- **Privacy minimum** (needed by the honesty principles): the LLM is off until the user opts in; transcripts never
  leave the request and are never saved; the relay stores nothing and logs no content; the only thing saved is the
  one-line journal entry; a setting deletes the journal.
- **The API key never ships in the APK.** A thin relay holds it, rate-limits per install key and forwards only the
  prompt the core builds.

## 7. LLM provider: Claude API with Haiku (decided 2026-10-09)

| Path | Where | Calls | Model |
|---|---|---|---|
| App, PC and Quest builds | `RelayCoachClient` -> relay -> Claude API | onboarding interview, evening reflection; later the weekly review and suggestions | Claude Haiku 5.5, `claude-haiku-5-5` |
| Local tests on the Windows machine | `CliCoachClient` -> the Claude Code CLI in print mode, one process per call, JSON output | the same prompts the core builds | Haiku, selected through the CLI's model flag |
| No coach | `ScriptedCoach` in core | the same moments, scripted | none |

- **The prompt is built in core and is identical on both paths**, so a line that passes the guards through the CLI
  passes them through the relay. The CLI path exists only for development; the Quest build never contains it.
- **Cost at list price** ($0.10 input and $0.50 output per 1M tokens, Anthropic API prices cached 2026-10-06): about
  $0.0002 per daily reflection and about $0.002 per onboarding interview, so a daily user costs about one cent a month.
- **Refusals:** a Haiku request has no server-side fallback model. The relay returns the refusal as a failure, and the
  app shows the scripted line.
- **Spike S-AI-1 (10-10 to 10-13)** no longer compares providers. It runs Haiku through `CliCoachClient` on the
  evaluation set below, then sends the same set through the relay once, to confirm both paths agree:

| Check | Pass |
|---|---|
| Structured onboarding: 20 scripted interview transcripts | at least 19 of 20 produce valid `{name, zone, schedule, cue}` objects on the first try |
| Guards: the same 20 plus 20 reflections | at least 39 of 40 lines pass the 6.5 guards; a failing line falls back to the scripted line |
| Latency through the relay from the PC build | p95 to the full reply 4 s or less |
| Tone | the owner reads all 40 lines once and marks any that feel off |

If onboarding misses its pass mark on Haiku, the fix is the prompt and the validator first; a larger model for
onboarding alone needs a new owner decision. The relay is the only place that names the model, so a later move to an
on-device model (section 11) is a change of `ICoachClient`, not of the app.

## 8. Artboards: how the style pick is made

The owner keeps both apps until the artboards show which style carries the upgrade.

- **Frames:** 4 moments x 2 styles = 8 frames, each at the app's reference framing (`docs/art/*-style.md`, "Gate
  frames"):
  1. AI onboarding and zones: the interview, and the first 2-3 custom habits appearing in their zones.
  2. Break moments: the hand-tracked stretch and the depth eye-rest mote in passthrough.
  3. Evening reflection: the speaking moment and the one-line journal entry (jar glow versus ink notebook).
  4. Week and month record: 6 to 9 custom habits across 4 zones, read at a glance. This is the scaling stress test.
- **Production (revised 2026-10-09):** the cloud session has no image generation, so it builds composited concept
  frames: the project's own room plates, seed textures and plant art (all with provenance in the repo) plus drawn
  SVG overlays in each style, rendered with headless Chromium. They show layout, scale and reading, not final
  fidelity. No pixels from the owner's reference frames are used. Frames go to `docs/art/artboards/<style>/`, and one
  comparison board puts each moment's two frames side by side. If they are too rough to judge, the Windows machine
  re-renders them with image generation (task W2).
- **Rubric** (1 to 5 per frame; the owner alone scores):

| # | Criterion | Why it matters for the pick |
|---|---|---|
| R1 | 9 habits across 4 zones read in 2 seconds | the jar holds a few plants; the dial was sized for 3 |
| R2 | The break moment is clear in passthrough and uses the real room | the strongest "why VR" answer |
| R3 | The voice moment shows a visible listener and stays calm | the coach must feel part of the garden |
| R4 | The monthly record reads as growth, never as a score | honesty principles |
| R5 | Art cost to reach: draws, triangles, and the distance from today's fidelity | Terrarium fails A2 and A3 on glass; Sundial is closer |

- **Decision:** the higher total wins, unless a frame scores under 3 on R1 or R2, which disqualifies that style for the
  slice. The pick is recorded as decision 0016 by 2026-10-14. The losing app's art work stops; it is parked, not deleted.

## 9. Execution and schedule (revised 2026-10-09: no agents are running)

The Grok agents and the hourly host loop in `docs/PLAN.md` section 5 are not running. Two workers carry the upgrade:
this **cloud Claude Code session** (Linux, this repository) and the **owner's Windows machine** (Unity, later the
headset). The split follows what each one can actually run.

### 9.1 What each worker can run

| Capability | Cloud session | Windows machine |
|---|---|---|
| `dotnet test shared/core-dotnet` (all core rules) | **yes**: .NET SDK 10 with `DOTNET_ROLL_FORWARD=Major`; 335 of 335 tests passed on 2026-10-09 | yes (.NET 9) |
| Claude Code CLI calls to Haiku (`claude -p --model haiku`) | **yes**: one test call returned from `claude-haiku-5-5` | yes |
| Node 22 (the relay, its tests, tooling) | **yes** | yes |
| Headless Chromium, Python with Pillow (composited images, boards) | **yes** | yes |
| Image generation (`image_gen`, Antigravity) | no | yes (Grok or Antigravity, if run there) |
| Unity 6.6 compile, PlayMode tests, captures, builds | **no** | yes, needs a Unity licence |
| Speech-to-text on PC, the microphone | no | yes |
| A live Claude API call through the relay (needs an API key) | no key here | yes, with the owner's key |
| Quest headset | no | yes, Phase 2 |

Rule for the cloud session: it changes only what it can check. Core changes land as step A (additive, decision 0004)
with dotnet tests and the edited `PublicSurface.approved.txt` in the same commit. Nothing in `apps/` changes from the
cloud, because no Unity compile can check it there.

### 9.2 Cloud session tasks (in order; each ends with green dotnet tests or a stated check, one commit per task)

| # | Task | Output | Check |
|---|---|---|---|
| C1 | Habit model: `LifeZone`, `HabitSchedule`, the new optional `HabitDef` fields, save round-trip with old saves reading as before (6.1) | core + tests | dotnet tests; the row codec golden tests still pass for saves without the new fields |
| C2 | Record: `MonthlyRecord`, `ZoneRecord`, quantity targets, a property test that no public member returns a consecutive-day count (6.2) | core + tests | dotnet tests |
| C3 | Catch-up with depth as a setting, default 2 (6.3) | core + tests | dotnet tests over 28 and 90 simulated days |
| C4 | Artboards: 8 composited concept frames and the comparison board (8) | `docs/art/artboards/` | the owner scores them |
| C5 | Coach core: prompt builders, structured output parsing and validation, the 6.5 guards, `ScriptedCoach` (onboarding tree and word stones), the `ICoachClient` seam | core + tests | dotnet tests |
| C6 | `CliCoachClient` as a small .NET console tool in `shared/core-dotnet` that sends the core's prompts through the Claude Code CLI | tool + tests with a fake CLI | dotnet tests; one live call |
| C7 | Evaluation: 20 interview transcripts and 20 reflections, run on Haiku through C6, results recorded, the 40 lines listed for the owner's tone read (7) | `docs/research/coach-eval-2026-10.md` | the pass marks in section 7 |
| C8 | Breaks: `BreakSession`, `HandMobility`, `EyeRest`, `NudgePlanner` (6.4) | core + tests | dotnet tests |
| C9 | Relay: a Node service with the Anthropic SDK and Haiku, structured output, refusal returned as failure, per-install rate limit, no content logging | `tools/relay/` | unit tests with a mocked client; a live call waits for the owner's key |
| C10 | Step B task files for the Windows machine, one per scene, written once the app is picked: the core members each one uses, the PlayMode tests, the captures | `docs/plans/upgrade-step-b.md` | the owner can start each one cold |

### 9.3 Windows machine tasks (owner, or a Claude Code session the owner runs there)

| # | Task | When |
|---|---|---|
| W1 | Pay the Unity compile debt: compile both apps against core at the head of this branch, run EditMode and PlayMode (decisions 0004, 0012) | first Unity session, before step B |
| W2 | Optional: re-render the artboard frames with image generation, if the composited frames are too rough to judge | 10-12 to 10-13 |
| W3 | Step B in the chosen app, from the C10 task files: zones in the garden, onboarding scene, break and eye-rest scenes, reflection, catch-up, monthly view, nudge settings | 10-15 to 10-22 |
| W4 | PC speech-to-text provider behind `ISpeechToText` | with W3 |
| W5 | Relay: set the API key, run it locally, make one live call from the PC build | with W3 |
| W6 | Gate pack for the chosen app | 10-22 |

### 9.4 Schedule

| Dates | Cloud session | Owner and Windows | Exit |
|---|---|---|---|
| Fri 10-09 | this plan; C1 starts | R1 review as planned | - |
| Sat 10-10 to Tue 10-13 | C1 to C8 | W1 if Unity is available; W2 optional | dotnet tests green; the board published |
| Wed 10-14 | C9 | **score the board alone and pick the app**; read the 40 coach lines | decision 0016 |
| Thu 10-15 to Thu 10-22 | C10 first, then support: core fixes found by W3, string census, gate pack documents | W3 to W6 | gate pack Thu 10-22 |
| Fri 10-23 | - | **Gate G, re-scoped** (section 10) | Quest / extend / park |
| Sat 10-24 to Wed 11-11 | core and relay changes Phase 2 needs | Quest integration as `PLAN.md` Phase 2, plus XR Hands poses, scene-mesh far point, mic, headset notifications, the relay from the device | RC Wed 11-11 |
| Thu 11-12 to Tue 11-17 | submission texts (tagline, form), checked against the guards | demo video, upload, H4 install check | submitted Tue 11-17 |

If W1 has not run by Thu 10-15, the gate takes the extension rule at once: Gate G moves to Fri 10-30 and Phase 2 shrinks
to 10-31 to 11-11.

## 10. Gate changes (for the chosen app)

| Item | Change |
|---|---|
| U1 | The scripted first run includes the onboarding interview on the scripted coach path; first ritual or first break and the garden's answer in 180 s or less |
| U3 | Extends to schedules (daily, N a week, weekdays), quantity targets, catch-up across 2 days, and monthly totals over 3 simulated months |
| U5 | Unchanged grep, plus: coach guard tests pass on the Haiku evaluation set (section 7); no public core member returns a consecutive-day count |
| U6 (new) | Every coach moment completes with the network off (scripted path), checked by a PlayMode test with `RelayCoachClient` disabled |
| U7 (new) | Privacy: no transcript in the save file or the relay logs; the delete-journal setting empties the journal; the LLM is off on a fresh save |
| S3 | Adds: speech-to-text result shown within 2 s of release on PC; a coach reply within 4 s, or the scripted line |
| A1 to A7 | Unchanged, for the chosen app's frames; the artboard moments become gate frames |

## 11. Post-slice roadmap

1. Voice intentions as morning seeds (keep, carry over, let go in the evening).
2. Weekly review ritual (the week replays; the coach names patterns; you choose next week's focus).
3. Adaptive suggestions: the coach notices a rarely kept habit and offers to shrink it or move its cue; never nags.
4. On-device LLM behind `ICoachClient`, replacing the relay where the device can carry it.
5. Spoken coach lines (TTS of live lines) in the cast voice.
6. A companion beyond Horizon mobile, if outside-VR logging by catch-up proves too weak.
7. The parked app's style, if a use case suits it better.

## 12. Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| No Unity session for step B before 10-15 | high | high | every rule lands in core from the cloud first; gate extension rule applied at once (section 9.4) |
| The cloud session cannot see the apps run | certain | medium | it touches no app file; W1 compiles every core change before step B; C10 hands over exact task files |
| Composited artboards too rough to decide on | medium | medium | they judge layout and scale, which R1 and R2 need; W2 re-renders with image generation if needed |
| Quest speech-to-text on device is weak or heavy | medium | high | presets by pinch are a complete path; the spike measures the PC provider first, the device in H1 |
| LLM latency on headset Wi-Fi breaks the calm | medium | medium | 4 s budget then the scripted line; the reflection is text, so latency hides behind "the garden is listening" |
| Coach says something medical, shaming or off-tone | medium | high | core guards on every line; evaluation set; the scripted fallback replaces any failing line |
| Nine habits overload the jar or the dial | high | medium | artboard criterion R1 is a disqualifier; zones group plants |
| The scope is bigger than the time left | high | high | cut line in section 4.2; one app only after 10-14 |
| Catch-up logging feels like a chore | medium | medium | at most 2 days back, one pinch per item, skippable with no consequence |
| The one-entry choice moves to Mage Arena VR | unknown | high | the upgrade makes Garden VR the stronger, safer entry (decision 0006) |

## 13. Owner answers to the open questions (2026-10-09)

1. LLM provider: Claude API with Haiku in the app; the Claude Code CLI for local tests on Windows (section 7).
2. Catch-up depth: 2 days to start; adjustable later. `CatchUp` takes the depth as a setting with 2 as the default.
3. Zone names: Body, Mind, Work, Connection, approved.
4. Artboard scores: the owner alone.

Still open:

5. Where the relay runs for the competition build. Judges install the APK on their own headsets, so the relay must be
   reachable from the internet with the owner's key, rate-limited, and up from 11-17 through judging. Candidates: a
   small serverless function on a host the owner already uses, or the owner's own machine during testing only. Needed
   by Phase 2 (10-24).
