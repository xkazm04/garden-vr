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

`CanBackfillYesterday` is true when yesterday is on or after `CreatedDay` and has no live tend. `Backfill` delegates to the Common ledger: yesterday only, once, late forever, the clock's real timestamp, and only before today's boundary. A yesterday before `CreatedDay` is refused here (`before-created`) and is not written.

## Gnomon

`GnomonAngleDeg` is 15 degrees per hour, 0 at 06:00, wrapping at 360. 14:20 is 125.

## State oracle

`SundialState.Capture` then `ToJson()`:

```json
{"now":860,"arc":"Midday","gnomonDeg":125,"plants":[{"habit":"water","window":["Kept","Missed","Late","Kept","Kept","Missed","Today"],"windowKept":4,"lifetimeKept":4,"stage":"Young","bloom":"Bud","dueNow":true,"canBackfill":true}]}
```

`arc` is null when the minute is between 03:00 and 06:00. Enum values are the C# names.
