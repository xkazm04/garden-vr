# Garden VR - the programme

Written 2026-10-02 (Friday) by the planning lead. Binding inputs: the owner's decisions of 2026-10-02 (two apps, both
Unity 6.6 URP, PC-first, Quest only after a gate), `docs/decisions/0001..0003`, and the contest record under
`C:\Users\kazda\kiro\personas\.contest\` (paths below are relative to it unless absolute).

| Read next | For |
|---|---|
| `docs/plans/terrarium.md`, `docs/plans/sundial.md` | each app's MVP, journey, rules, milestones, backlog |
| `docs/art/terrarium-style.md`, `docs/art/sundial-style.md` | style bibles and the reference framings |
| `docs/audio/AUDIO-BIBLE.md` | sound direction, cue lists, loudness, credits |
| `docs/knowledge/registry-consult.md` | which registry standards shaped this plan, deviations, lessons to forge |
| `orchestration/queue/<app>/` | the first task queues |

## 1. Goals

1. **Two entries worth judging** in the Meta VR Start Developer Competition 2026, Productivity track, New Experience
   division; entries close **2026-11-18** (`staging/habit-garden/data/COMPETITION.md`). The track names "habits tied to
   a recurring context (morning routine, commute, wind-down)" in its own words; Terrarium owns wind-down, Sundial owns
   the whole day.
2. **The competition scope is the MVP.** Each app's MVP is the "In" column of its round-1 report, re-cut for Unity and
   the chosen art direction (sections 2 of each app plan). Anything beyond it is post-MVP backlog (more habits, more
   in-app rituals).
3. **PC-first until the gate (2026-10-23).** Mechanics, art at the chased quality and audio are mastered on Windows
   with keyboard and mouse standing in for hands through `com.gardenvr.input` (decision 0002). No headset work, no ADB.
4. **Quest only for an app that passes the gate** in section 4. The Quest swap must be a provider change (XR hands
   provider, passthrough room provider, anchor provider), not a rewrite.
5. **Honest habits, everywhere** (`AGENTS.md` rule 3; round-1 reports; TOOLING.md section C): growth only rises, a missed
   day is quiet and recoverable, no streak counters, nothing turns red or dies.

Non-goals before 2026-11-18: accounts, cloud sync, notifications, LLM features, controllers, roomscale, Meta VR Glasses
beyond a simulator profile (the glasses ship spring 2027, `data/TOOLING.md` section B).

## 2. The two apps

| | **Terrarium** (`apps/terrarium`) | **Sundial** (`apps/sundial`) |
|---|---|---|
| Art direction | A/1-03 Night Moss: extend reality, add glow and magic (`staging/habit-garden/OWNER-CHOICE.md`) | A/2-05 Field Notebook pushed to hand-drawn / anime linework living in the real room (same file) |
| The bet (round 1) | The ritual IS the growth: pinch-hold to breathe and a fern uncoils with you (`arena/habit-garden/entries/claude-claude-opus-5-5_high/variant-1/index.html`) | Read your day in two seconds, tend it in twenty: a sundial whose shadow is the real clock (`.../variant-2/index.html`) |
| Daily moment | Wind-down (Night Moss ties the jar to evening; round 1 said morning, now changed) | Morning, commute/midday, wind-down |
| Hero object | Palm-sized cork-topped glass jar on the desk, 35-45 cm ahead | Plate-sized drawn dial on the table, ~55 cm ahead |
| Core gesture | PinchHold = inhale, Release = exhale (6 breaths) | Look + Pinch = tend; PinchHold for the 3-breath dusk ritual |
| In-app ritual (MVP) | Six-breath paced pause | Three-breath dusk pause |
| Life habits (MVP) | Up to 3 presets, checked in by Poke/Pinch (Today / Yesterday) | One habit per arc (3), tended by Look + Pinch, yesterday backfill once, marked late |
| Growth rule | One permanent frond per practice day; glow vitality floor 0.6; first flower at 6 fronds | Rolling 7-tile record per plant; plant size from lifetime kept days (only rises); bloom from the week (decision D2) |
| Round-3 state | "About halfway": jar, cork, fronds, coil, ring, spores read; moss, frond lace, steam fall short; 21 draws, 18.8k tris (`arena/habit-garden-r2-r3/entries/claude-claude-opus-5-5_high-v1/variant-1/index.html` sections 01, 04) | "Closer": paper, washes, ink, boil, halo land; plants are cropped from the owner's frame (art debt); 15 draws, 10.4k tris (same report, section 02) |

Shared foundation: `com.gardenvr.core` (pure C#, `dotnet test shared/core-dotnet`), `.input`, `.audio`, `.fx`, `.room`,
`.capture` (`README.md`).

## 3. Phases and dates

Today is Friday 2026-10-02. Weeks run Saturday to Friday so each ends with an owner review on a Friday.

| Phase | Dates | What happens | Exit |
|---|---|---|---|
| **0. Bootstrap** | Fri 10-02 to Sat 10-03 | Host: repo, Unity projects, packages, worktrees (`C:/Users/kazda/kiro/gvr-terrarium`, `gvr-sundial`), these plans, first queues; host voice audition and credit check (section 7) | Both agent loops running on T-TER-001 / T-SUN-001 |
| **1. PC-first** | Sat 10-03 to Fri 10-23 | Mechanics, art toward the references, audio, the journey. Milestones M0-M6 per app (each plan, section 8) | Gate pack per app delivered Thu 10-22 |
| 1a. Vertical slice | 10-03 to Fri 10-09 | Each app: input, rig, hero port, core wired, the hero ritual end to end with scripted playback (M0-M1) | **Review R1 Fri 10-09**: owner plays both PC builds for 10 min |
| 1b. Art and audio | 10-10 to Fri 10-16 | Art passes at reference framing with dual-anchor parity; cue lists generated; narration; music beds (M2-M4) | **Review R2 Fri 10-16**: owner scores side-by-sides and the mixdowns |
| 1c. Journey and polish | 10-17 to Fri 10-23 | First run, missed day, day 7, settings, pause/resume, budgets, gate pack (M5-M6) | **Gate G Fri 10-23** |
| **Gate decision** | **Fri 2026-10-23** | Owner reads each app's gate pack, plays each PC build cold, decides per app: Quest / extend one week / park | Decision recorded as `docs/decisions/0004-quest-gate.md` |
| **2. Quest integration** | Sat 10-24 to Wed 11-11 | Only apps that passed: XR hands provider, passthrough, table + anchor, Operator e2e in XR Simulator, device perf; headset sessions H1-H3 | RC build Wed 11-11 |
| **3. Submission** | Thu 11-12 to Tue 11-17 | Competition release channel, demo video (<3 min), 140-char tagline, 500-word form, H4 install check from the invite URL | **Submitted Tue 11-17**; Wed 11-18 is buffer (entries close) |
| Post-MVP | from 11-19 | Backlog in each app plan, section 9 | - |

Headset sessions (owner only, Phase 2): **H1 Tue 10-27** hands + passthrough + first ritual on device; **H2 Tue 11-03**
art in real passthrough, perf with OVR Metrics, comfort; **H3 Tue 11-10** release candidate + video capture; **H4 Mon
11-16** clean install from the Competition-channel invite URL. Four sessions per passing app; if both pass, H1-H3 cover
both apps in one sitting (about 60 min each). Round 2 / 3 headset checklists are the starting templates
(`arena/habit-garden-r2-r3/entries/claude-claude-opus-5-5_high-v1/variant-1/index.html` section 07).

Extension rule: an app that fails only one gate group on 10-23 may take one more PC week (to **Fri 10-30**); its Quest
phase then shrinks to 10-31 to 11-11 with H1 moved to 11-02. An app that fails two or more groups is parked for the
competition and continues post-MVP. If neither app passes, the better-scoring one takes the extension.

## 4. The gate: useful, seamless, beautiful

Each criterion names its **witness** (who or what may certify it) and its **command or protocol**. An agent's own report
never certifies a gate item (registry: `no-gate-self-certifies`; see `docs/knowledge/registry-consult.md`). Items marked
"owner" are owner's-eyes items; everything else the host re-runs on a clean checkout of `main`.

### U. The app proves useful

| # | Criterion (pass condition) | Witness and how |
|---|---|---|
| U1 | Scripted canonical first run reaches the first complete ritual and the garden's answer in **<= 180 s** of app time (Terrarium: 6 breaths; Sundial: first tend <= 60 s and dusk ritual done <= 180 s) | Host: PlayMode playback test `FirstRun_*` (`-runTests -testPlatform PlayMode`), timestamps from the run log |
| U2 | A first-time human finishes the first complete moment in **<= 10 min** with no instructions outside the app | Owner (or a person who has never seen it), stopwatch, fresh save path; observation notes separate from fixes |
| U3 | The habit record is right across **28 simulated days** (misses, backfill, undo, 03:00 roll-over, DST week, clock set back) | Host: `dotnet test shared/core-dotnet` (property tests included) |
| U4 | The owner used the PC build on **>= 4 of 7 days** between 10-15 and 10-21, by choice, and says he would open it tomorrow without a reminder | Owner + the app's own save file (day records), not a self-report |
| U5 | Honesty invariants hold: growth never decreases over 1,000 random lives; no "streak" string or counter anywhere; no red, wilt-to-death or shaming copy | Host: property tests + `grep -rniE "streak|failed|broke|lost your" apps/*/Assets` returns nothing user-facing; owner reads every string once |

### S. The mechanics are seamless

| # | Criterion | Witness and how |
|---|---|---|
| S1 | Every interaction goes through intents: no `UnityEngine.Input`, `Keyboard.`, `Mouse.` outside the keyboard/mouse provider | Host: `grep -rnE "Input\.Get|Keyboard\.current|Mouse\.current" apps/*/Assets shared/packages --include=*.cs` lists only `com.gardenvr.input/Runtime/Providers/` |
| S2 | The canonical journey playback (first run, normal day, missed day, day 7) passes **10 of 10** consecutive runs with identical end state | Host: PlayMode playback suite run 10x; end-state JSON hashes equal |
| S3 | Response: from a Pinch/PinchHold intent to the first visible state change **<= 2 frames at 60 fps** (33 ms) | Host: playback log carries intent timestamp and the first `state-changed` marker |
| S4 | Pause/resume: focus loss or PalmOpen at any point of a ritual loses nothing and resumes cleanly; a ritual cut off is offered back, never auto-restarted | Host: PlayMode tests `PauseAt_*` at 5 points per ritual |
| S5 | Cold start of the Windows player to the first interactive frame **<= 4 s** (warm disk) | Host: player log timestamps, 3 runs, median |
| S6 | The owner completes the canonical journey without being stuck > 5 s anywhere and rates the feel of the core gesture >= 4/5 | Owner, at R2 and G |

### A. The art is beautiful (and the audio belongs to it)

| # | Criterion | Witness and how |
|---|---|---|
| A1 | **Reference parity, owner anchor**: every gate frame at its reference framing (each art bible, section "Gate frames") rated **>= 4 of 5** by the owner on "would I open the video with this frame?"; the minimum across frames counts, never the mean | Owner, from side-by-sides in `orchestration/runs/<app>/gate/sbs/` |
| A2 | **Spec anchor**: palette, line weights, glow falloff and dimensions within the bible's tolerances | Host: `measure` capture step (palette delta-E per region, measured line width px, object size px at framing) |
| A3 | **Blind second opinion**: an outside model family (Gemini; never Grok, the producer) scores each gate frame on the bible's anchored 1-5 rubric, median of 3 draws, payload = render + reference + rubric only, bound to the render's content hash; pass = **every frame >= 3** and no capped disqualifier | Host runs the judge; scores are advisory until calibrated against owner labels (registry `quality-verdict-integrity`) |
| A4 | **No borrowed pixels**: no texture cropped or keyed from the owner's reference frames ships; every texture has a provenance sidecar (generated / painted by code / photographed) | Host: provenance census over `apps/*/Assets/Art/Textures` |
| A5 | **Budgets** (hard caps; soft caps at 80%): Terrarium scene <= 40 draws, <= 60k tris, mean transparent layers over the jar <= 1.5; Sundial scene <= 30 draws, <= 30k tris; textures <= 1024 px; no post-processing | Host: `Measure` capture step (draws, tris, overdraw histogram). PC frame time is reported, never gated (it is not a Quest number) |
| A6 | **Audio**: full-ritual mixdown at -20 LUFS integrated +/- 1, true peak <= -1.5 dBTP; narration >= 10 dB above the music bed; no cue repeats identically within 3 s; owner approves the mixdown | Host: `ffmpeg ebur128` on the mixdown; owner listens (AUDIO-BIBLE section 5) |
| A7 | **Motion**: a 60 s capture of the hero moment plays without pops, sorting flicker or boil drift; reduced-motion mode shows the end states | Owner watches the capture; host checks the reduced-motion PlayMode test |

**An app passes the gate when every U, S and A item passes.** "Not measured" is not a pass (registry
`unmeasured-is-not-a-pass`); an item the host could not run is reported as not measured and blocks the gate.

The gate pack (Thu 10-22, per app, in `orchestration/runs/<app>/gate/`): `GATE.md` with one row per item (status, witness,
evidence path), side-by-sides, the judge record, budgets, the mixdown, a 90 s first-run capture, the Windows player zip.

## 5. Orchestration model

### Actors

| Actor | Where | Does | Never |
|---|---|---|---|
| Owner | anywhere | reviews R1, R2, G; owner-eyes items; headset sessions; approves reserved changes | - |
| Host (Claude) | main checkout `C:\Users\kazda\kiro\garden-vr`, branch `main` | hourly checks, verification, merges, queues tasks, runs the judge, keeps `orchestration/STATUS.md` | writes app feature code; stashes or resets an agent worktree |
| Grok agent "terrarium" | worktree `C:/Users/kazda/kiro/gvr-terrarium`, branch `agent/terrarium` | tasks in `orchestration/queue/terrarium/` | pushes, rewrites history, touches paths it does not own |
| Grok agent "sundial" | worktree `C:/Users/kazda/kiro/gvr-sundial`, branch `agent/sundial` | tasks in `orchestration/queue/sundial/` | same |

Worktrees sit beside the repo, not under it, so no parent `AGENTS.md`/`CLAUDE.md` leaks in and each gets its own Unity
`Library/` (registry `workspace-ancestry-isolation`, `no-links-into-live-trees`). Each agent opens only its own app's
Unity project; two Unity instances never open the same project path.

### Task lifecycle

`queue/<app>/` -> `running/` -> `done/` or `failed/` (by `tools/orchestrate/agent-loop.mjs`), then **verified** by the
host. The loop moves a task to `done/` when the agent left `REPORT.md`; that is a self-report. The host's verification
is a separate state: the host appends `verified: <sha> <date>` or `rejected: <reason>` to the task file in `done/` and
records it in `STATUS.md`. A downstream task in the same queue may start on a self-reported upstream, but **the host
never merges an unverified task**, and a rejected upstream sends its dependants back to `queue/`.

Each task file carries `id, app, title, depends, estimate_min, touches` and the sections Goal, Read first, Steps,
Acceptance (Windows commands), Evidence, Out of scope. Cross-queue dependencies are allowed (`depends: [T-SUN-002]` in a
Terrarium task).

At dispatch the host records the base commit; verification reads the work as `git diff <base>..agent/<app>`, not
against the agent's own claims (registry `diff-from-the-recorded-base`).

### Hourly host check (every hour while agents run; faster when a task just finished)

1. **Observe**: for each agent, newest commit time on `agent/<app>`, files in `running/`, loop log tail, Grok/Unity
   process CPU. Verdict per agent: `observed-changes` / `observed-quiet` / `could-not-observe` (registry
   `passive-signal-ingestion`). Quiet for 90 min with a task running and no CPU: check the environment first (Unity
   licence, editor lock, sleep, ElevenLabs refusal, GPU) before blaming the model (registry `clear-the-environment-first`).
2. **Verify each newly done task** on a clean checkout of `agent/<app>` at its head:
   - restore the oracles from the base commit (the task's test files, playback scripts, acceptance scripts) and note any
     agent edit to them as a separate finding (registry `grade-with-checks-the-run-could-not-touch`);
   - re-run every Acceptance command in the task, with the same timeouts; read results XML (collected count > 0,
     failed = 0), not exit codes alone;
   - `git diff --stat <base>..HEAD` touches only the task's `touches:` plus the agent's owned paths (section 5, ownership);
     `git check-ignore` on committed paths finds no `Library/`, `Temp/`, `Logs/`, builds, `.env`;
   - read the evidence (open the PNGs, watch the MP4); a capture that is black or magenta is a failed capture;
   - termination cause recorded: finished / ceiling / refused / crashed / unknown.
3. **Merge** verified work to `main` (`git merge --no-ff agent/<app>`), run `dotnet test shared/core-dotnet` and a
   batchmode compile of BOTH apps on `main` (a shared-package change must not break the other app), then confirm with
   `git log -1 --stat` that the merge is the one intended.
4. **Queue**: keep 3-5 ready tasks per agent; write new tasks from the plan's milestones and the reports' "next" notes;
   requeue failed tasks with a sharpened task file (fix the instruction before changing model or effort, registry
   `instruction-defect-before-tier-escalation`).
5. **STATUS.md**: one row (time, each agent's task and verdict, merged SHAs, credits remaining, notes for the owner).

### Merge policy

- Only the host merges, only into `main`, only verified work. Agents never push and have no credential that can.
- Agents start every task with `git merge --no-edit main` in their worktree (no rebase, no history rewrite). A conflict
  stops the task as blocked; the host resolves it.
- **Reserved for the owner** (the host proposes, does not merge): changes to test or gate configuration (a test
  deleted, skipped or loosened; `testables`; acceptance scripts), a new package or version in `Packages/manifest.json`,
  anything touching `.env`, credentials or `tools/audio/elevenlabs.mjs` guards, and a breaking change to a shared
  package API (registry `proposal-not-push`).
- A shared-package change merges only if both apps compile against it on `main` in the same check.

### Shared-package ownership (per path; each path has exactly one owner)

| Path | Owner | Why |
|---|---|---|
| `shared/packages/com.gardenvr.input/**` | **terrarium** | PinchHold breathing is the most demanding consumer; T-TER-001 builds it first |
| `shared/packages/com.gardenvr.room/**` | **terrarium** | the seated rig and plate land in T-TER-002 |
| `shared/packages/com.gardenvr.audio/**` | **terrarium** | breath-synced narration is the hardest audio case |
| `shared/packages/com.gardenvr.core/Runtime/Breath/**`, `.../Runtime/Terrarium/**` | **terrarium** | the spike's breath and garden rules |
| `shared/packages/com.gardenvr.core/Runtime/Common/**`, `.../Runtime/Sundial/**` | **sundial** | day clock, ledger, habits, persistence, rolling window |
| `shared/packages/com.gardenvr.capture/**` | **sundial** | captures, side-by-side, measure, playback runner (T-SUN-001) |
| `shared/packages/com.gardenvr.fx/**` except the two below | **sundial** | shared shaders (FCard halo, FShadowCatcher, FPlate, FToon + ink hull, FOverdraw); new app-only shaders go in the app |
| `shared/packages/com.gardenvr.fx/Runtime/Shaders/FGlass.shader`, `FGlow.shader` | **terrarium** | seeded into fx by the host; only the jar uses them |
| `shared/core-dotnet/GardenVR.Core.Tests/Terrarium*.cs`, `.../Common*.cs`+`Sundial*.cs` | terrarium / sundial | tests sit with their rules; the `.csproj` files are host-owned |
| `tools/blender/terrarium_*.py`, `tools/blender/sundial_*.py` | terrarium / sundial | new scripts by prefix; the seeded scripts are read-only references |
| `tools/audio/mixdown.mjs` | terrarium | created in T-TER-010 |
| `tools/audio/elevenlabs.mjs`, `tools/orchestrate/**`, `docs/**`, `AGENTS.md`, `*.csproj`, `Packages/manifest.json` | host | |

The non-owner consumes. It may propose a change by writing `orchestration/runs/<app>/<id>/REQUEST-<package>.md`
(what, why, the smallest backward-compatible API); the host turns it into a task for the owner. An urgent one-line
fix in a non-owned path is allowed only when the task file names that path in `touches:` and the report flags it.

### Agent configuration

Model `grok-4.7`, effort `high` for build tasks, pinned on every launch path including retries (registry
`pin-the-resolved-configuration`); the loop's per-task ceiling is 150 min (`agent-loop.mjs --timeout-min`). A task that
hits the ceiling is reported as "ceiling", not as failed. Three failures of the same task: the host rewrites it.

## 6. Schedule of milestones (both apps)

| Date | Terrarium | Sundial |
|---|---|---|
| Sat 10-03 to Mon 10-05 | **M0** foundations: input provider, seated rig + room plate, core restructure, hero port | **M0** foundations: capture package, core common (day, ledger, persistence), Sundial rules |
| Tue 10-06 to Fri 10-09 | **M1** vertical slice: six breaths end to end by scripted playback | **M1** vertical slice: rig, dial hero, glance-and-tend loop by scripted playback |
| Fri 10-09 | R1 owner review (both PC builds) | |
| Sat 10-10 to Wed 10-14 | **M2** art pass to the references (moss, frond atlas, steam, cork, glass) | **M2** art pass (SVG dial face, inked plant cards, tiles, painted shadow) |
| Mon 10-12 to Thu 10-15 | **M3** garden over days + life habits | **M3** tiles, backfill, undo, dusk ritual |
| Wed 10-14 to Fri 10-16 | **M4** audio: cues, narration, night bed | **M4** audio: tock, chime, arc beds, dusk narration |
| Fri 10-16 | R2 owner review (side-by-sides, mixdowns) | |
| Sat 10-17 to Tue 10-20 | **M5** journey: first run, look-back, settings, pause/resume | **M5** journey: first run with seed packets, day 7, settings |
| Tue 10-20 to Thu 10-22 | **M6** polish, budgets, reduced motion, gate pack | **M6** same |
| Fri 10-23 | **Gate G** | |

## 7. Budgets

### ElevenLabs (Starter plan; ~52,000 credits on 2026-10-02; reset ~2026-10-04)

Rates are the guard's own estimates in `tools/audio/elevenlabs.mjs` (TTS 1 credit per character; SFX ~40 credits per
second, minimum 100; music ~60 credits per second); the ledger `tools/audio/ledger.jsonl` records what was actually
billed and is the authority. The tool refuses any call that would leave fewer than **8,000** credits (`RESERVE`).

| Pot | Credits | Spent on |
|---|---|---|
| Host: voice audition + credit probe (Sat 10-03) | 4,000 | 4 calm voices x the same 300-character Terrarium passage + the same Sundial passage; owner picks one voice per app |
| Terrarium | 18,000 | SFX ~5,000; narration ~3,500 (with 2 retakes); music bed ~6,500; slack 3,000 (AUDIO-BIBLE section 7) |
| Sundial | 14,000 | SFX ~4,500; narration ~1,500; three arc beds ~6,000; slack 2,000 |
| Rework reserve (host-held) | 8,000 | re-renders after R2; a pot is released by the host on request |
| Guard reserve (`RESERVE`) | 8,000 | never spent |
| **Total** | **52,000** | |

Rules: every generation goes through the tool (it writes the ledger and a `.json` sidecar). Before each batch the agent
writes the batch's estimate into its report and checks `node tools/audio/elevenlabs.mjs credits`. A refusal or quota
error is not a task failure: stop audio work, record the provider's reset time, finish the rest of the task, report it
(registry `refusal-detection-and-requeue`). Retakes count against the pot. Music uses the music endpoint only.

Unresolved: whether unspent credits roll over at the ~10-04 reset. The host checks `credits` on Sat 10-03; if they do not
roll over, the host spends the audition pot plus an SFX palette batch for both apps (up to 15,000) before the reset,
from the cue lists in AUDIO-BIBLE section 3, and the app pots are re-based on the new month's allowance.

### Image generation (Grok `image_gen` / `image_edit`)

- Purpose: concept frames for states the references do not show, textures (moss atlas, frond atlas, steam flipbook
  sources, cork, paper, plant drawings), room plates. Never in-app UI text, never people's faces, never logos.
- Cap per task: **24 generations** unless the task says otherwise; record the count in the report.
- Every kept image goes to `apps/<app>/Art/Source/<name>.png` with `<name>.prompt.txt` (prompt, negative, tool, date,
  seed if any, which candidates were rejected and why). Auto-picked versus owner-chosen is recorded and never back-filled.
- A generated image is gated before it feeds Blender or a bake: view it at the size it will be seen in the headset
  framing (registry `gate-before-every-credit-spend`). Tileable textures get a wrap-around edge diff.
- **No pixels from the owner's reference frames ship.** Round 3 keyed the dial plants and cropped the moss, soil and
  cork bands from those frames; they are stand-ins and count as art debt until replaced (gate item A4).

### Agent time

Each task 45-120 min estimate, 150 min ceiling. Roughly 6 tasks per agent per day when the loop runs 10 h; the queues
in `orchestration/queue/` are the first ~2.5 days each.

## 8. Decisions this plan takes (owner may overturn)

| # | Decision | Default taken | Why | Alternative |
|---|---|---|---|---|
| D1 | Terrarium daily moment | **Wind-down** (evening), usable any time | Night Moss is the evening look (`OWNER-CHOICE.md`, stack consequence column) | Round-1 morning; or round-2's A/C day-night look switch (dropped: one look to perfect) |
| D2 | Sundial growth vs "growth only rises" | Tiles = rolling 7-day record; **plant size = lifetime kept days (monotone)**; bloom open when >= 5 of the last 7 are kept, else a closed bud | Reconciles round 1's rolling window with `AGENTS.md` rule 3 | Round-1 rule (size = kept in the last 7, can shrink as days age out) |
| D3 | Day boundary | **03:00 local** for both apps, a setting | Sundial round 1 used 03:00, Terrarium 04:00, the spike midnight | 04:00 |
| D4 | Sundial arcs | Morning 06:00-11:00, Midday 11:00-18:00, Wind-down 18:00-03:00 | Round-1 report's ARCS table; the IWSDK spike moved dusk to 17:00 | 17:00 dusk |
| D5 | Terrarium flower rule | **First flower at 6 fronds, then every 6** | Round 1: "a week with one missed day still flowers on day 7"; the spike's every-7th rule loses that | Every 7th (spike) |
| D6 | Narration default | First ritual is voiceless (the jar teaches); the voice guide is offered after the first answer and remembered | Registry `voice-ux-integration` (discoverable but quiet) | Voice on by default |
| D7 | Sundial habits per arc (MVP) | **One per arc** (3 plants, 21 tiles) | Matches the references and the 15-draw dial; 2-3 per arc is post-MVP | 1-3 per arc (round 1) |
| D8 | Room on PC | Room plates from `shared/assets/room-plates/` for development and parity; never shipped on Quest (passthrough replaces them) | Passthrough cannot be captured on PC | Generated room plates (added for variety in art tasks) |
| D9 | Both apps entered | Two separate competition entries | Owner's two-app decision | Whether one team may enter twice is not in `COMPETITION.md`: verify; fallback is to enter the stronger app |

## 9. Host actions before and around the first queues

| When | Action | Unblocks |
|---|---|---|
| Fri 10-02 | Create worktrees `C:/Users/kazda/kiro/gvr-terrarium` (`agent/terrarium`) and `gvr-sundial` (`agent/sundial`) from `main` after these docs and queues are committed; start both loops | T-TER-001, T-SUN-001 |
| Sat 10-03 | `node tools/audio/elevenlabs.mjs credits`; settle the rollover question (section 7); run the voice audition (4 voices x 2 passages, ~2,400 credits) and send the owner the clips | owner voice pick |
| After the owner's pick | Write `apps/terrarium/Assets/Audio/Voice/CAST.md` and `apps/sundial/Assets/Audio/Voice/CAST.md` (voice_id, name, model `eleven_multilingual_v2`, terms) on `main` | T-TER-011, T-SUN-012 voice steps |
| Each check | Mark verified tasks `verified: <sha>` in `orchestration/done/`; a rejected upstream re-queues its dependants (DV5 in `docs/knowledge/registry-consult.md`) | safe merges |
| By Thu 10-08 | Queue the next tasks per app from the plans' M2-M6 rows (Terrarium: look-back, settings pebble, reduced motion, string census, gate pack; Sundial: settings tab, PalmOpen dismiss, reduced motion, string census, gate pack) | M5-M6 |
| Fri 10-09, 10-16, 10-23 | Prepare the owner review packs (R1, R2, G) and record the outcomes as decisions | gate |
| Before 10-23 | Verify in the official rules whether one team may submit two entries (D9) | Phase 3 plan |

## 10. Risks

| Risk | Likelihood | Impact | Mitigation | Owner of the check |
|---|---|---|---|---|
| Two apps halve attention; neither reaches beautiful | high | high | Each app does a vertical slice first (M1) before breadth; the gate parks a failing app; the extension rule moves scope, not the deadline | host at R1, R2 |
| Grok in Unity batchmode: silent failures (exit 0 with zero tests, magenta shaders, async shader compile poisoning captures) | high | medium | Acceptance reads results XML counts; captures disable async compile and warm up (round-2 limitation 8); host re-runs everything | host every check |
| Art stalls at "halfway" (round-3 jar 5/10) | medium | high | Art passes are bounded loops with a recorded best; findings carry located corrections; owner scores at R2 not at the gate only | host, owner R2 |
| Reference frames are image-model frames with impossible light (green-lit hands, refraction) | high | medium | Platform limits listed in each art bible are excluded from parity; spec anchor alongside the frame anchor | host |
| Shared-package edits by the non-owner, or a change that breaks the other app | medium | medium | Path ownership table; host refuses foreign diffs; both apps compile on `main` before a merge lands | host |
| ElevenLabs credits drain or reset loses them | medium | medium | Pots per app, guard reserve, ledger; host checks the reset rule 10-03 | host |
| Owner time (a parallel harder VR entry, `data/OWNER-INTENT.md`) | high | high | Exactly three PC reviews (R1, R2, G) with prepared packs; four headset sessions in Phase 2 | host |
| Quest phase too short (19 days) for passthrough, anchors, perf | medium | high | Quest-readiness notes keep every device seam behind a provider; round 2/3 already built APKs with hand tracking + passthrough (`habit-garden-r2-r3/.../variant-1/index.html` section 07) | host from 10-24 |
| Unity on Windows workarounds re-bite (MSI admin, EPERM package cache, Gamma colour, Operator layer twice, Vulkan dzn crash, JDK missing) | medium | medium | The 12 workarounds in `arena/habit-garden-r2/entries/claude-claude-opus-5-5_high-v1/variant-1/index.html` section 04 and its `RUNBOOK.md`; reuse, do not rediscover | Phase 2 tasks |
| Wellness copy drifts into claims | low | high | No medical words in any string or narration; host greps every string list at the gate | host, owner |
| Two entries not allowed | unknown | medium | Verify with the official rules before 10-23 (D9) | owner |
| Boiling line reads as flicker in stereo | medium | low | Boil only plants and rim at 10 fps; a setting turns it off; H2 check | owner H2 |
| Hand in front of the drawn dial is not occluded on Quest | high | medium | Depth API / hand-mesh occluder planned in Phase 2; PC captures composite the hand matte (dev only) | Phase 2 |
