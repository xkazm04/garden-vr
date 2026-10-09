# Upgrade step B: Sundial task files for the Windows machine

Task C10 of `docs/plans/upgrade-2026-10.md`. Decision 0016 picked Sundial. Each task below can be started cold by the
owner, or by a Claude Code session the owner runs on the Windows machine, which has Unity 6000.6.4f1, .NET 9 and the
repository. The rules came from the cloud session as step A in `com.gardenvr.core` (C1 to C9, C11). Step B is the app
adopting them: scenes, views, controllers, PlayMode tests and captures.

Shared rules for every task (`AGENTS.md`):
- Every interaction goes through `com.gardenvr.input` intents.
- Rules stay in core; app code renders state. If a task finds a rule it needs that core lacks, add it to core first,
  additively, with a dotnet test, then use it.
- No em dash in user-facing strings. No medical claims. Nothing streak-like.
- Commit one finished sub-step at a time. Write `orchestration/runs/sundial/<task>/REPORT.md` with the real command
  output.

## Commands every task uses

```powershell
$unity = "C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"
# core rules (fast, run first)
$env:DOTNET_ROLL_FORWARD="Major"; dotnet test shared/core-dotnet/GardenVR.sln
# EditMode, then PlayMode (PlayMode renders, so no -nographics)
Start-Process -Wait -FilePath $unity -ArgumentList "-batchmode -nographics -projectPath apps/sundial -runTests -testPlatform EditMode -testResults orchestration/runs/sundial/<task>/editmode.xml -logFile orchestration/runs/sundial/<task>/editmode.log"
Start-Process -Wait -FilePath $unity -ArgumentList "-batchmode -projectPath apps/sundial -runTests -testPlatform PlayMode -testResults orchestration/runs/sundial/<task>/playmode.xml -logFile orchestration/runs/sundial/<task>/playmode.log"
# Windows player
Start-Process -Wait -FilePath $unity -ArgumentList "-batchmode -nographics -quit -projectPath apps/sundial -executeMethod GardenVR.Sundial.Editor.Build.Windows -logFile orchestration/runs/sundial/<task>/build.log"
```

Read the results XML (`total` above 0, `failed` 0), not the exit code alone.

## Order and dates

| Task | What | Needs | Target |
|---|---|---|---|
| B0 | Pay the compile debt (W1) | - | first Unity session, before 10-15 |
| B1 | The save holds the upgrade's state | B0 | 10-15 |
| B2 | Habits with names, zones and schedules on the dial | B1 | 10-16 |
| B3 | Speech on PC behind one seam (W4) | B0 | 10-16 |
| B4 | Coach clients in the app (relay, CLI in the editor) | B1 | 10-16 |
| B5 | Onboarding interview in the first run | B2, B3, B4 | 10-17 |
| B6 | Break: stretch, hand poses, eye rest | B1 | 10-18 |
| B7 | Evening reflection and the journal | B3, B4 | 10-19 |
| B8 | Catch-up card | B1 | 10-19 |
| B9 | Monthly record: zone tabs, totals, quantity | B2 | 10-20 |
| B10 | Nudges and the new settings | B6 | 10-20 |
| B11 | Relay live check and latency (W5) | B4 | 10-20 |
| B12 | Strings, gate rows, captures, gate pack (W6) | all | 10-22 |

If B0 has not run by 10-15, the gate moves to 10-30 (plan section 9.4), and this table shifts by one week.

---

## B0 - Pay the compile debt

**Goal.** Both apps compile against core at the head of the upgrade branch, and every existing suite passes. This
covers the debt of decision 0012 (a) item 1, which includes every cloud change since `89971d1`.

**Steps.**
1. Merge the upgrade branch into the working branch.
2. Open `apps/sundial` and `apps/terrarium` in batchmode, one at a time, never both at once. Compile, then run
   EditMode and PlayMode for each. Terrarium is parked, but it must still compile, because `docs/PLAN.md:184` requires a
   shared-package change to compile in both apps.
3. New core types sit in new files (`Runtime/Common/HabitProfile.cs`, `Record.cs`, `CatchUp.cs`, `Breaks.cs`,
   `Runtime/Coach/*.cs`). Unity generates nothing for them; their `.meta` files are committed. If Unity rewrites a
   `.meta`, commit what Unity wrote.
4. `CoachFlow` uses `System.Threading.Tasks`. Confirm it compiles under the Unity 6.6 API level of both projects.

**Acceptance.** Both projects compile with 0 errors. EditMode and PlayMode XML show 0 failed for Sundial, and for
Terrarium as it stood. `dotnet test shared/core-dotnet/GardenVR.sln` passes.

**Out of scope.** Any behaviour change.

---

## B1 - The save holds the upgrade's state

**Goal.** `SundialSave` carries what the new moments need, behind a schema step from 1 to 2 with defaults for old
saves.

**Core it uses.** `QuantityJson`, `CoachJournal.Read/Write`, `CatchUp.Depth`, `NudgeWindow`, `RelayProtocol.NewInstallKey`.

**Steps.**
1. New members on `SundialSave`: `Counts` (`List<CountEvent>`), `Journal` (`CoachJournal`), `LastVisitDay` (`int?`),
   `InstallKey` (string, made once with `RelayProtocol.NewInstallKey`), and in `SundialSettings`: `CoachOptIn` (bool,
   default false), `CatchUpDepth` (int, default `CatchUp.DefaultDepth`), `NudgeWindows` (list, default
   `NudgePlanner.DefaultWindows`), `Nudges` (bool, default true), `BreakPoses` (bool, default true).
2. `SundialCodec.Read/Write` handle the new members. A missing member reads as its default.
3. Raise `SundialService.SchemaVersion` to 2, with a migration that fills the defaults; follow the existing
   `SaveStore` migration path.
4. `LastVisitDay` is written on each launch, after the catch-up card has had its chance (B8).
5. Update `GardenVR.SundialModel.Tests`:
   - the approved public surface;
   - a golden test for a schema-1 save read into schema 2;
   - a round trip with every new member set.

**Acceptance.**
- The dotnet tests pass, including `SundialProfileSaveTests` (from C11) and `SundialCodecGoldenTests`.
- A schema-1 `save.json` from today's player loads with nothing lost.
- PlayMode passes.

---

## B2 - Habits with names, zones and schedules on the dial

**Goal.** A habit shows its own name and zone, and keeps its schedule. Up to three per arc, nine in all, as now.

**Core it uses.**
- `HabitProfiles` (`ZoneOf`, `ScheduleOf`, `CleanName`, `CanAdd`), `HabitSchedule.IsPlannedOn`.
- `CoachProposal.ToHabit`, `SundialRules.Admit`.

**Steps.**
1. `SundialService`:
   - Add `PlantProposal(CoachProposal p, string arcKey)`. It plants through the same path as `TryAdmit`: the arc the
     user chose, or the arc with the fewest habits when none was chosen. Name, zone, schedule and target come from the
     proposal.
   - `PlantPreset` keeps working for presets. A preset's zone comes from `HabitProfiles.TryPresetZone`.
2. `DialView`:
   - Draw the zone dot under each plant, in the zone colours of the artboard (`tools/artboards/frames.mjs`, `FN`).
   - Show the habit's own name where the label shows today (`TileLabels`, `InkLetter`).
   - A weekday habit's tile reads "not planned" on its off days, as a blank paper tile, never a pale "missed" tile.
3. A times-a-week habit's week row reads "2 of 3 this week" on look. A single day is never shown as missed.

**PlayMode tests to add.**
- `Profile_NameAndZoneShowOnLook`.
- `Weekdays_OffDayTileIsBlank`.
- `PerWeek_NeverMarksADayMissed`.
- `NinePlants_ThreeArcsFull_AdmitRefusesTenth`.

**Capture.** `DialG1` with nine plants (the artboard's record moment). It becomes a gate frame.

---

## B3 - Speech on PC behind one seam

**Goal.** One interface for speech-to-text, a PC provider, and a scripted provider for tests.

**Steps.**
1. Add `ISpeechToText` to `com.gardenvr.input` (owner: Sundial now that Terrarium is parked). The shape is
   `Begin()`, `End()`, `event Action<string> Final`, `event Action<string> Partial`, `bool Available`. It is started and
   stopped by `PinchHold` and `Release`, never by a key read in app code.
2. PC provider: `UnityEngine.Windows.Speech.DictationRecognizer` (Windows only). Check that it runs in the
   standalone player on this machine, and record the result. If it does not, use the scripted provider and a
   preset-only first run, and say so in the report.
3. `ScriptedSpeechSource` replays lines from a playback file, for PlayMode tests.
4. The microphone is used only while a hold lasts; a small ink "listening" ripple shows while it is open. Nothing is
   saved: transcripts live in memory until the coach call ends.

**Acceptance.** EditMode tests for the scripted source. A manual check on the player: hold Space, speak, release, and
the final text appears in the log in a development build only.

---

## B4 - Coach clients in the app

**Goal.** The app talks to the relay, and the editor can use the Claude Code CLI.

**Core it uses.** `ICoachClient`, `RelayProtocol.Encode/Decode/Path/InstallKeyHeader`, `CoachFlow`.

**Steps.**
1. `RelayCoachClient : ICoachClient` in the app:
   - `UnityWebRequest` POST of `RelayProtocol.Encode(request)` to `<relay>/v1/coach`, with header `x-install-key` from
     the save.
   - The relay URL comes from a `RelayConfig` ScriptableObject that is not committed with a production URL.
   - Cancellation aborts the request.
2. In the editor on Windows only (`#if UNITY_EDITOR_WIN`), a client that runs `GardenVR.CoachCli`'s
   `CliCoachClient` logic (copy the process code; the player never contains it).
3. A coach call is made only when `CoachOptIn` is true. Otherwise `CoachFlow` gets a null client and the scripted
   coach runs.

**PlayMode tests.**
- `Coach_OffByDefault_ScriptedPathCompletes`: gate U6.
- `Coach_RelayDown_FallsBackWithinBudget`: a fake server that never answers.
- `Coach_Refused_ShowsScriptedLine`.

---

## B5 - Onboarding interview in the first run

**Goal.** The first run asks the four questions and plants what the user accepts (artboard moment 1).

**Core it uses.** `CoachPrompts.OnboardingQuestions`, `CoachFlow.OnboardAsync`, `ScriptedCoach.Onboarding`,
`CoachProposal`.

**Steps.**
1. `FirstRunWizard`: after the shadow sweep, a paper card shows the opt-in in one line. "The coach can use an online
   model to suggest habits. Your words are not kept." There are two inked choices, both pinchable; the default is off.
2. Four questions, one at a time, on the card. Hold to answer, or pinch "skip".
3. `CoachFlow.OnboardAsync` turns the answers into three seed packets, labelled with name, zone and schedule, as in
   the artboard.
   - Pinch a packet to accept it. It drops onto its arc: the cue's time of day, or the arc the user pinches next.
   - Pinch "not this one" to skip it.
   - The zone tabs on the rim appear with the first planted habit.
4. A preset-only path (no speech, coach off) still reaches the first tend in 60 s or less.

**PlayMode tests.**
- `FirstRun_Interview_ScriptedCoach_FirstTendUnder60s`: gate U1, with the coach off.
- `FirstRun_Interview_Skips_AllFourQuestions`.
- `FirstRun_Interview_Resumes_AtSavedStep`.

**Capture.** The onboarding moment at `DialG1` framing. It becomes a gate frame.

---

## B6 - Break: stretch, hand poses, eye rest

**Goal.** The break from artboard moment 2.

**Core it uses.** `BreakSession`, `HandMobility`, `EyeRest.TargetDistance`, `BreakSession.PresetKey` ("break", Body zone).

**Steps.**
1. Build `BreakController` from `StretchRitualController`. The stretch's three marks and its palm handling stay; the
   stretch is the first part of `BreakSession`.
2. The hand poses use the existing intents: an open palm held, and a pinch held. A small ink figure shows the
   current pose.
3. The eye-rest mote is drawn at the room plate's far point (the window) on PC: `EyeRest.TargetDistance(null)`
   gives 3 m. Look at it to count down; look away to pause. The "13 s" slip shows the time left.
4. On completion, `ConsumeTend` tends the break habit (`TendSource.Ritual`). Add the habit on first use if the user
   has none.
5. The morning stretch ritual keeps its current meaning: it tends the morning arc. A break started from a nudge
   tends the break habit.

**PlayMode tests.**
- `Break_FullRun_TendsBreakHabitOnce`.
- `Break_LookAway_PausesEyeRest`.
- `Break_PoseLetGo_KeepsDonePoses`.
- `Break_PauseResume_AtEachPart`: gate S4.

**Capture.** The break moment. It becomes a gate frame.

---

## B7 - Evening reflection and the journal

**Goal.** Artboard moment 3: hold to speak after the dusk ritual; one line is kept.

**Core it uses.** `CoachFlow.ReflectAsync`, `ReflectionContext`, `ScriptedCoach.Reflection`, `CoachJournal`,
`OneWord.Stones`.

**Steps.**
1. When the dusk ritual closes, offer "Hold to tell the garden about today" or six word stones (`OneWord.Stones`) to
   pinch instead.
2. `ReflectionContext.KeptToday` holds today's kept habit names, and nothing else.
3. The line appears on a paper card in ink and is kept with `CoachJournal.Keep(today, line)`. One line a day; a
   second reflection replaces it.
4. The week dial page shows the journal lines of that week.
5. Settings gains "Delete the journal" with an inline confirmation (no system dialog).

**PlayMode tests.**
- `Reflection_Stone_KeepsScriptedLine`.
- `Reflection_Speech_FakeClient_KeepsModelLine`.
- `Reflection_DirtyLine_FallsBack`.
- `Journal_Delete_Empties`: gate U7.
- `Save_HoldsNoTranscript`: gate U7. It searches `save.json` for the spoken test words.

**Capture.** The reflection moment. It becomes a gate frame.

---

## B8 - Catch-up card

**Goal.** "What happened since?" at launch, up to the depth setting (2 by default).

**Core it uses.** `CatchUp.Pending`, `CatchUp.Log`, `Ledger.BackfillWithin`.

**Steps.**
1. On launch, after the dial draws, list `CatchUp.Pending(ledger, habits, save.LastVisitDay, today, depth)` on an
   inked card: one line per habit and day, each pinchable. Pinching logs it with `CatchUp.Log`, and the tile draws
   hatched (late).
2. "Done" or PalmOpen closes the card; skipping changes nothing.
3. The single "?" on yesterday's tile stays for the depth-1 case. Both paths log through the same core rule.

**PlayMode tests.**
- `CatchUp_TwoDaysAway_OffersTwoDays`.
- `CatchUp_Logged_IsHatchedLate`.
- `CatchUp_FirstVisit_ShowsNothing`.

---

## B9 - Monthly record: zone tabs, totals, quantity

**Goal.** Artboard moment 4.

**Core it uses.** `ZoneRecord.Fill`, `MonthlyRecord.KeptDays`, `GardenMonth.Of`, `Quantity.Add/Undo`, `QuantityTally`.

**Steps.**
1. Four paper tabs hang from the front rim, as in the artboard. Each tab's pencil shading is `ZoneRecord.Fill` for
   this month. A zone with nothing planned shows no shading and no number.
2. Looking at a plant shows "kept N days this month" (`MonthlyRecord.KeptDays`) beside its name.
3. A quantity habit's tile fills in steps (3 of 8) with each pinch. The pinch that reaches the target tends it
   (`Quantity.Add`). Undo works inside 6 s, as for tends.

**PlayMode tests.**
- `ZoneTabs_FillFromLedger`.
- `MonthTotal_OnLook`.
- `Quantity_ReachTarget_TendsOnce`.
- `Quantity_Undo_ReversesTend`.
- `NoConsecutiveCountString`: gate U5 grep, in the test.

**Capture.** The record moment with nine plants. It becomes a gate frame.

---

## B10 - Nudges and the new settings

**Goal.** At most two quiet nudges a day, plus the settings the upgrade adds.

**Core it uses.** `NudgePlanner.Next`, `NudgePlanner.Text`, `NudgeWindow`.

**Steps.**
1. On PC, a nudge is an ink note at the dial's edge, with `NudgePlanner.Text`. It arrives only while the app is
   open, and pinching it starts a break. On Quest (Phase 2), it becomes a system notification.
2. Keep the times nudges were sent today in memory. A restart forgets them, which can only mean fewer nudges.
3. New rows on `SettingsTabs`:
   - Coach online (off by default).
   - Catch-up depth (1, 2 or 3 days).
   - Nudges on or off.
   - Nudge windows (two inked time ranges, dragged like arc edges).
   - Hand poses in breaks.
   - Delete the journal (from B7).

**PlayMode tests.**
- `Nudge_AtMostTwoADay`.
- `Nudge_OnlyInsideWindows`.
- `Settings_NewRows_Persist`.

---

## B11 - Relay live check and latency

**Goal.** One live call from the PC build, and the section 7 latency mark.

**Steps.**
1. `cd tools/relay`, run `npm ci`, then `$env:ANTHROPIC_API_KEY=...`, then `node server.mjs`.
2. Point `RelayConfig` at `http://localhost:8787`, turn the coach on, and run one onboarding interview and one
   reflection in the player.
3. Time 20 relay calls (10 of each kind) from the player, and record p50 and p95. The mark is p95 4 s or less.
4. Fill in the latency row of `docs/research/coach-eval/README.md`.

---

## B12 - Strings, gate rows, captures, gate pack

**Goal.** `apps/sundial/GATE.md` for the re-scoped gate on 10-23.

**Steps.**
1. String census:
   - Every new user-facing string goes in one list.
   - Run the gate U5 grep, and add `streak` and `in a row` to it.
   - Run the medical words from `CoachGuards` over the list.
   - Check there is no em dash.
2. New gate rows from plan section 10:
   - U6 (coach off completes).
   - U7 (privacy: no transcript in the save, journal deletion works, coach off on a fresh save).
   - The S3 additions: speech result within 2 s, coach reply within 4 s or the scripted line.
3. Gate frames:
   - Today's `DialG1`.
   - The four artboard moments at `DialG1` framing (onboarding, break, reflection, record with nine plants).
   - Side-by-sides against the artboards and the references.
   - A3 is still failing on the round-3 rubric (median 2), so the art passes continue on these frames.
4. Re-run the canonical journey ten times (S2). It now includes the interview with the scripted coach.

**Acceptance.** Every row is pass, fail or needs-owner, with evidence; "not measured" is not a pass.
