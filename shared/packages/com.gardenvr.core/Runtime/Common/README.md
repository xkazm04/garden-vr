# Common

Engine-free primitives shared by Terrarium and Sundial. This folder does not reference the engine. `shared/core-dotnet` compiles it and `dotnet test shared/core-dotnet` runs the suite.

## API

`GardenDay.Index` is the number of days since 2000-01-01 of the garden day. `From(local, boundary)` reads the wall clock on that `DateTimeOffset`. Before the boundary (default 03:00) the instant still belongs to the previous civil date. 03:00 exactly starts the new day. `EndsAt(zone, boundary)` is that next boundary in the given zone. A spring-forward gap resolves to the transition instant. An ambiguous autumn wall time resolves to the earlier instant.

`IClock` is the only time source the rules take. `FixedClock` is the test double. `SystemClock` reads the machine clock. Do not assume an IANA id resolves on this runtime. Tests build zones with `TimeZoneInfo.CreateCustomTimeZone` and explicit daylight rules.

`HabitDef` carries `Id`, `PresetKey`, `Group` (sundial), `Species` (terrarium), `Kind`, `Slot`, `CreatedDay` and `ArchivedDay`.

`Ledger.Tend` is idempotent per habit and garden day. A day after the clock's own garden day is refused, so setting the clock back cannot append a future day. `Undo` sets `UndoneAtUtcMs` and does not remove the row. It is refused once the event is older than the window (default 6 seconds, inclusive). `Backfill` accepts yesterday only, once, and only before today's boundary. The row is `Late` and its timestamp is the clock's real instant.

`DeferredTend` holds one tend for 6 seconds. `Tick` commits it when the window expires. `Flush` commits it immediately (pause and teardown). `Undo` cancels the pending write and always returns true, because the write has not happened yet.

`Json` reads and writes objects, arrays, strings, numbers, bools and null. Strings honour the usual escapes. Numbers are parsed and written with the invariant culture, so a comma decimal separator on the thread cannot change the file. `JsonObject.Passthrough` copies members the caller does not know into a dictionary. `Restore` writes that dictionary back without replacing keys the caller already set.

`SaveStore<T>` keeps one directory:

| File | Role |
|---|---|
| `save.json` | The live document |
| `save.json.tmp` | The fully written replacement, flushed before it is moved |
| `save.prev1.json` | The previous live file |
| `save.prev2.json` | The file before that |
| `save.snapshot.json` | Bytes copied before the first pending migration step |

`Load` returns `Fresh` (no file and no backup), `Loaded`, `Migrated` or `Failed`. `Failed` is never turned into `Fresh`, including a missing live file when a backup or a snapshot exists. A file whose `SchemaVersion` is newer than this build loads with `ReadOnly` set, and `Save` throws. `Save` writes the temp file, flushes, rotates `save.json` onto `save.prev1.json` and that file onto `save.prev2.json`, then moves the temp file onto `save.json`. A stream that throws during the temp write leaves the live file as it was.

`SettingsRegistry` is a closed list. `Define` adds a typed key. `Set` of a value equal to that key's default removes it. `ToJson` writes only the values that differ. `ReadJson` rejects a key that was not defined.

## Invariants

- 01:30 belongs to the previous garden day. 03:00 starts the new one.
- A DST week still has one index per civil date. The spring day that contains the gap is 23 hours for a one-hour jump. The autumn day that contains the repeated hour is 25 hours.
- A zone change follows the new wall clock. It does not invent a day ahead of that clock.
- Undo marks. It does not delete. Outside the window it is refused.
- Backfill is late, once, yesterday only, and only before the boundary.
- Unknown JSON members survive a rewrite.
- A truncated file is `Failed`. When a previous file exists, `BackupAvailable` is set.
- Defaults are not written.

## Adding a migration step

1. Append one `MigrationStep`. Its `FromVersion` is the last version that has shipped. Do not edit, reorder or remove a shipped step.
2. Pass `currentSchema` as that version plus one. The constructor rejects a gap or a reorder.
3. The step edits the `JsonObject` in place. Leave members you do not know. The store sets `SchemaVersion` to the next integer after the step returns, then writes the object.
4. The store copies `save.json` to `save.snapshot.json` after it has decided a step is pending and before the first step runs. It does not overwrite a snapshot that is already there.

A bad shipped step is fixed by appending another step. A file from a newer build stays read-only until this build understands it.
