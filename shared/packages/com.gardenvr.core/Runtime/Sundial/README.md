# Sundial rules

Engine-free rules for the dial. This folder does not reference the engine. `shared/core-dotnet` compiles it and `dotnet test shared/core-dotnet` runs the suite. The app draws `SundialState`. It does not decide it.

## Arcs (PLAN D4)

Minutes are after local midnight. Ranges are half-open.

| Arc | Minutes | Clock |
|---|---|---|
| Morning | 360-660 | 06:00-11:00 |
| Midday | 660-1080 | 11:00-18:00 |
| Wind-down | 1080-1620 | 18:00-03:00 the next calendar day |

`ArcAt` maps 00:00-02:59 onto that wind-down range (add 1440). 03:00 through 05:59 is a new garden day and no arc. 03:00 is the same boundary as `GardenDay`.

`HabitDef.Group` is the arc key: `morning`, `midday`, or `wind-down` (case, hyphens, underscores and spaces do not matter).

## Growth (PLAN D2)

Round 1 sized the plant from the last 7 days, so the plant could shrink when a kept day aged out. That rule is gone.

| | Source | Values | Can fall? |
|---|---|---|---|
| `Stage` | `LifetimeKept` | 0 seed, 1-2 sprout, 3-6 young, 7-13 leafy, 14+ full | No. A miss adds nothing. |
| `Bloom` | `WindowKept` | under 3 none, 3-4 bud, 5+ open | Yes. A kept day leaving the window can close it. |

`LifetimeKept` counts live tends on or after `CreatedDay` and through today. Late days count. Undone rows do not. A tend dated before the habit existed does not count.

## The 7 tiles

Oldest first, today last. `Before` for a day before `CreatedDay`. A live tend is `Kept`, or `Late` when the row is a backfill. Today with no tend is `Today`. Any earlier open day is `Missed`. `WindowKept` counts `Kept` and `Late` only.

`DueNow` is true only when the clock is inside the habit's arc and today's tile is still `Today`.
Each live habit has its own `DueNow`. Tending one habit in an arc leaves its siblings due.

## Habits per arc

An arc holds at most `MaxHabitsPerArc` (3) live habits. `Admit` puts the new habit on the lowest free row, 0, 1 or 2, and refuses a fourth (`arc-full`). An archived habit does not hold a row. Habit ids are unique across the dial. The ledger, the seven-day window, backfill and undo stay per habit id, so one row can be late or undone while the others are untouched. A lone habit is row 0. `MaxTiles` is 63: three arcs, three rows, seven days, one combined draw.

`CanBackfillYesterday` is true when yesterday is on or after `CreatedDay` and has no live tend. `Backfill` delegates to the Common ledger: yesterday only, once, late forever, the clock's real timestamp, and only before today's boundary. A yesterday before `CreatedDay` is refused here (`before-created`) and is not written.

## Gnomon

`GnomonAngleDeg` is 15 degrees per hour, 0 at 06:00, wrapping at 360. 14:20 is 125.

## State oracle

`SundialState.Capture` then `ToJson()`:

```json
{"now":860,"arc":"Midday","gnomonDeg":125,"plants":[{"habit":"water","window":["Kept","Missed","Late","Kept","Kept","Missed","Today"],"windowKept":4,"lifetimeKept":4,"stage":"Young","bloom":"Bud","dueNow":true,"canBackfill":true}]}
```

`arc` is null when the minute is between 03:00 and 06:00. Enum values are the C# names.

## Stretch reach

`StretchSession` is the morning ritual. Three marks, in order. A sample counts only when `PalmCommit` is true and `LookMark` is the next index. A repeat or a later mark is ignored and does not lower `Stretch`. `Stretch` is earned reaches / 3 and only rises. Completion sets `TendAuthorised` for `ArcId.Morning` with `TendSource.Ritual`. `ConsumeTend` returns true once. The session does not write the ledger. The app does, with `TendRitual`.

## Gratitude at midday

`GratitudeRecord` is the midday record. Five symbols, index 0..4: sun, leaf, cup, star, hearth. `TryInk(day, symbol)` writes one mark for that garden day. The mark is the day index and the symbol index. A second symbol the same day is refused and the first choice stays. Each new ink authorises one `TendSource.Ritual` on `ArcId.Midday`. `ConsumeTend` returns true once per unconsumed ink. A record built from saved marks does not authorise a tend. The record does not write the ledger. The app does, with `TendRitual`.

## Focus block

`FocusBlock` is the 25-minute shadow hour. `DurationSeconds` is 1500. `SweepDegrees` is 90: the ink shadow travels that far across the dial over a full hour. `TryStart(now, nowMin)` locks the arc from `ArcAt` and the gnomon angle at that minute. A start from 03:00 through 05:59 has no arc. `Observe` advances a running hour. Active time is the UTC gap from the start, minus every paused gap, so a spring-forward or a fall-back does not add or remove minutes, and a backward step does not unwind progress. `TryPause` / `TryResume` hold the ink. `TryEndEarly` still counts, including an end while paused, which keeps the earned minutes and drops the gap. There is no failed phase. A full hour or an early end sets `Counted` and, when an arc was locked, authorises one `TendSource.Ritual` on that arc. `ConsumeTend` returns true once. `Capture` / `Restore` round-trip a running, paused, or finished hour. Restored marks do not authorise a tend that was already consumed. The block does not write the ledger. The app does, with `TendRitual`.

## Week dial

`WeekDial.Read` is the second page. Four weeks, 28 tiles, oldest day first. Week 0 is the oldest. Week 3 ends on today. `TileOn` is the same day rule as the 7-tile window: before the habit existed the day is blank, a live tend is kept or late, today with no tend is still open, and any earlier open day is a quiet miss. A miss does not change any other day. An undone row is not live, so that day reads as a miss once it is no longer today. The JSON is `weeks`, `span`, and `plants[{habit, tiles}]`. There is no run count. The query does not write the ledger.

## Rim scrub

`RimScrub.FromDrag` maps a backward drag on the rim to an earlier minute. The gnomon moves 15 degrees per hour, so one degree of drag is four minutes and the shadow can follow the hand. A forward drag stays at now. The far end is 03:00 on the day six days before today, the start of the seven-day window. `Query` returns a `SundialState` whose window ends on the day being read. `DueNow` and `CanBackfillYesterday` are false on that picture, so it cannot be tended. The query does not write the ledger. `AllowsWrite` is false while the gesture or its settle is open. The caption is empty at the current minute, "Earlier today" on this garden day, "Yesterday" one day back, and "Earlier this week" after that. There is no run count.
