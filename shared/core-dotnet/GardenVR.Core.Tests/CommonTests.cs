using System;
using System.Globalization;
using System.IO;
using System.Text;
using GardenVR.Core;
using Xunit;

namespace GardenVR.Core.Tests;

public class CommonTests
{
    static readonly TimeSpan Boundary = GardenDay.DefaultBoundary;
    static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
    static int _zoneIds;

    [Fact]
    public void One_thirty_counts_for_the_previous_garden_day()
    {
        var previous = GardenDay.From(Wall(2026, 10, 1, 12, 0), Boundary);
        var atOneThirty = GardenDay.From(Wall(2026, 10, 2, 1, 30), Boundary);
        Assert.Equal(previous, atOneThirty);
        Assert.Equal(previous.Index, atOneThirty.Index);
    }

    [Fact]
    public void Three_oclock_exactly_starts_the_new_garden_day()
    {
        var previous = GardenDay.From(Wall(2026, 10, 1, 12, 0), Boundary);
        var justBefore = GardenDay.From(new DateTimeOffset(2026, 10, 2, 2, 59, 59, TimeSpan.Zero), Boundary);
        var atBoundary = GardenDay.From(Wall(2026, 10, 2, 3, 0), Boundary);
        Assert.Equal(previous, justBefore);
        Assert.Equal(previous.Index + 1, atBoundary.Index);
        Assert.NotEqual(justBefore, atBoundary);
    }

    [Fact]
    public void Garden_day_index_is_days_since_2000_01_01()
    {
        Assert.Equal(0, GardenDay.From(Wall(2000, 1, 1, 3, 0), Boundary).Index);
        Assert.Equal(-1, GardenDay.From(Wall(2000, 1, 1, 1, 30), Boundary).Index);
        // Same anchor the terrarium garden tests document for 2026-10-01.
        Assert.Equal(9770, GardenDay.From(Wall(2026, 10, 1, 12, 0), Boundary).Index);
        var leap = GardenDay.From(Wall(2024, 2, 28, 12, 0), Boundary);
        var march = GardenDay.From(Wall(2024, 3, 1, 12, 0), Boundary);
        Assert.Equal(leap.Index + 2, march.Index);
    }

    [Fact]
    public void Dst_spring_week_has_one_index_per_civil_date_and_a_twenty_three_hour_day()
    {
        // Custom zone: clocks jump from 02:00 to 03:00 on 8 March. No IANA id.
        var tz = DstZone(3, 8, 2, 11, 1, 2, 1);
        int previous = int.MinValue;
        for (int day = 6; day <= 10; day++)
        {
            var noon = GardenDay.From(Wall(2026, 3, day, 12, 0), Boundary);
            if (previous != int.MinValue) Assert.Equal(previous + 1, noon.Index);
            previous = noon.Index;
        }
        var march7 = GardenDay.From(Wall(2026, 3, 7, 12, 0), Boundary);
        Assert.Equal(march7, GardenDay.From(Wall(2026, 3, 8, 1, 30), Boundary));
        Assert.Equal(march7.Index + 1, GardenDay.From(Wall(2026, 3, 8, 3, 0), Boundary).Index);
        var length = march7.EndsAt(tz, Boundary) - new GardenDay(march7.Index - 1).EndsAt(tz, Boundary);
        Assert.Equal(TimeSpan.FromHours(23), length);
    }

    [Fact]
    public void Dst_autumn_week_has_a_twenty_five_hour_day()
    {
        // Clocks fall back from 02:00 to 01:00 on 1 November. 03:00 itself is unambiguous.
        var tz = DstZone(3, 8, 2, 11, 1, 2, 1);
        var oct31 = GardenDay.From(Wall(2026, 10, 31, 12, 0), Boundary);
        var length = oct31.EndsAt(tz, Boundary) - new GardenDay(oct31.Index - 1).EndsAt(tz, Boundary);
        Assert.Equal(TimeSpan.FromHours(25), length);
        var next = new GardenDay(oct31.Index + 1);
        var normal = next.EndsAt(tz, Boundary) - oct31.EndsAt(tz, Boundary);
        Assert.Equal(TimeSpan.FromHours(24), normal);
    }

    [Fact]
    public void EndsAt_in_a_spring_gap_is_the_transition_instant()
    {
        // A two-hour jump at 02:00 skips 03:00. The day ends at the instant the clock jumps.
        var tz = DstZone(3, 8, 2, 11, 1, 2, 2);
        var march7 = GardenDay.From(Wall(2026, 3, 7, 12, 0), Boundary);
        var end = march7.EndsAt(tz, Boundary);
        Assert.Equal(new DateTime(2026, 3, 8, 2, 0, 0, DateTimeKind.Utc), end.UtcDateTime);
        Assert.Equal(TimeSpan.FromHours(2), end.Offset);
    }

    [Fact]
    public void Ambiguous_end_uses_the_earlier_offset()
    {
        // A two-hour fall back at 04:00 repeats 02:00-04:00, so 03:00 happens twice.
        var tz = DstZone(3, 8, 2, 11, 1, 4, 2);
        var oct31 = GardenDay.From(Wall(2026, 10, 31, 12, 0), Boundary);
        var end = oct31.EndsAt(tz, Boundary);
        Assert.Equal(TimeSpan.FromHours(2), end.Offset);
        Assert.Equal(new DateTime(2026, 11, 1, 1, 0, 0, DateTimeKind.Utc), end.UtcDateTime);
    }

    [Fact]
    public void Time_zone_change_mid_week_follows_the_wall_clock_and_does_not_skip_ahead()
    {
        var east = FixedZone(12);
        var west = FixedZone(-10);
        var wednesdayEvening = new DateTimeOffset(2026, 6, 10, 20, 0, 0, TimeSpan.FromHours(12));
        var wednesday = GardenDay.From(wednesdayEvening, Boundary);
        var westWall = TimeZoneInfo.ConvertTime(wednesdayEvening, west);
        var westDay = GardenDay.From(westWall, Boundary);
        Assert.True(westDay.Index < wednesday.Index);

        var ledger = new Ledger();
        var clock = new FixedClock(wednesdayEvening, east);
        Assert.True(ledger.Tend("water", wednesday, TendSource.Pinch, clock).Ok);
        clock.Now = westWall;
        clock.Zone = west;
        var skipped = ledger.Tend("water", wednesday, TendSource.Pinch, clock);
        Assert.False(skipped.Ok);
        Assert.Equal("future", skipped.Reason);
        Assert.Single(ledger.Events);
        var caughtUp = ledger.Tend("water", westDay, TendSource.Pinch, clock);
        Assert.True(caughtUp.Ok);
        Assert.True(caughtUp.Event.Day < wednesday.Index);
    }

    [Fact]
    public void Clock_set_back_never_creates_a_future_day()
    {
        var zone = FixedZone(0);
        var clock = new FixedClock(Wall(2026, 10, 5, 10, 0), zone);
        var today = GardenDay.From(clock.Now, Boundary);
        var ledger = new Ledger();
        Assert.True(ledger.Tend("water", today, TendSource.Pinch, clock).Ok);
        clock.Now = clock.Now.AddDays(-2);
        var rewound = GardenDay.From(clock.Now, Boundary);
        Assert.Equal(today.Index - 2, rewound.Index);
        var future = ledger.Tend("water", new GardenDay(today.Index + 1), TendSource.Poke, clock);
        Assert.False(future.Ok);
        Assert.Equal("future", future.Reason);
        Assert.False(ledger.IsKept("water", today.Index + 1));
        Assert.Single(ledger.Events);
    }

    [Fact]
    public void Tend_is_idempotent_per_habit_day()
    {
        var clock = ClockAt(2026, 10, 5, 10, 0);
        var day = GardenDay.From(clock.Now, Boundary);
        var ledger = new Ledger();
        var first = ledger.Tend("water", day, TendSource.Pinch, clock);
        var second = ledger.Tend("water", day, TendSource.Ritual, clock);
        Assert.True(first.Ok);
        Assert.True(second.Ok);
        Assert.True(second.AlreadyKept);
        Assert.Equal(first.Event.Id, second.Event.Id);
        Assert.Equal(TendSource.Pinch, second.Event.Source);
        Assert.False(second.Event.Late);
        Assert.Single(ledger.Events);
        Assert.True(ledger.Tend("other", day, TendSource.Poke, clock).Ok);
        Assert.Equal(2, ledger.Events.Count);
    }

    [Fact]
    public void Undo_at_5_9_seconds_marks_the_event_and_keeps_the_row()
    {
        var clock = ClockAt(2026, 10, 5, 12, 0);
        var day = GardenDay.From(clock.Now, Boundary);
        var ledger = new Ledger();
        var tend = ledger.Tend("water", day, TendSource.Pinch, clock);
        clock.Advance(TimeSpan.FromMilliseconds(5900));
        Assert.True(ledger.Undo(tend.Event.Id, clock));
        Assert.Single(ledger.Events);
        Assert.NotNull(ledger.Events[0].UndoneAtUtcMs);
        Assert.False(ledger.IsKept("water", day.Index));
        Assert.Equal(0, ledger.KeptDays("water"));
    }

    [Fact]
    public void Undo_at_exactly_6_seconds_is_inside_the_window()
    {
        var clock = ClockAt(2026, 10, 5, 12, 0);
        var day = GardenDay.From(clock.Now, Boundary);
        var ledger = new Ledger();
        var tend = ledger.Tend("water", day, TendSource.Pinch, clock);
        clock.Advance(TimeSpan.FromMilliseconds(6000));
        Assert.True(ledger.Undo(tend.Event.Id, clock));
        Assert.Single(ledger.Events);
    }

    [Fact]
    public void Undo_at_6_1_seconds_is_refused()
    {
        var clock = ClockAt(2026, 10, 5, 12, 0);
        var day = GardenDay.From(clock.Now, Boundary);
        var ledger = new Ledger();
        var tend = ledger.Tend("water", day, TendSource.Ritual, clock);
        clock.Advance(TimeSpan.FromMilliseconds(6100));
        Assert.False(ledger.Undo(tend.Event.Id, clock));
        Assert.Null(ledger.Events[0].UndoneAtUtcMs);
        Assert.True(ledger.IsKept("water", day.Index));
        Assert.Single(ledger.Events);
    }

    [Fact]
    public void DeferredTend_undo_cancels_the_pending_write_and_never_fails()
    {
        var clock = ClockAt(2026, 10, 5, 12, 0);
        var day = GardenDay.From(clock.Now, Boundary);
        var ledger = new Ledger();
        var held = new DeferredTend(ledger);
        Assert.True(held.Undo());
        held.Arm("water", day, TendSource.Pinch, clock);
        Assert.True(held.IsPending);
        Assert.True(held.Undo());
        Assert.False(held.IsPending);
        Assert.Empty(ledger.Events);
        Assert.True(held.Undo());
    }

    [Fact]
    public void DeferredTend_flush_commits_the_held_tend()
    {
        var clock = ClockAt(2026, 10, 5, 12, 0);
        var armedAt = clock.Now.ToUnixTimeMilliseconds();
        var day = GardenDay.From(clock.Now, Boundary);
        var ledger = new Ledger();
        var held = new DeferredTend(ledger);
        held.Arm("water", day, TendSource.Ritual, clock);
        clock.Advance(TimeSpan.FromSeconds(1));
        var ev = held.Flush();
        Assert.NotNull(ev);
        Assert.False(held.IsPending);
        Assert.Equal(armedAt, ev.AtUtcMs);
        Assert.Single(ledger.Events);
        Assert.True(ledger.IsKept("water", day.Index));
        Assert.True(held.Undo());
        Assert.True(ledger.IsKept("water", day.Index));
    }

    [Fact]
    public void DeferredTend_commits_when_the_six_second_window_expires()
    {
        var clock = ClockAt(2026, 10, 5, 12, 0);
        var day = GardenDay.From(clock.Now, Boundary);
        var ledger = new Ledger();
        var held = new DeferredTend(ledger);
        held.Arm("water", day, TendSource.Pinch, clock);
        clock.Advance(TimeSpan.FromMilliseconds(5900));
        Assert.Null(held.Tick(clock));
        Assert.Empty(ledger.Events);
        clock.Advance(TimeSpan.FromMilliseconds(100));
        Assert.NotNull(held.Tick(clock));
        Assert.Single(ledger.Events);
        Assert.False(held.IsPending);
    }

    [Fact]
    public void Backfill_yesterday_once_is_late_and_uses_the_real_timestamp()
    {
        var clock = ClockAt(2026, 10, 5, 18, 42);
        clock.Now = new DateTimeOffset(2026, 10, 5, 18, 42, 7, TimeSpan.Zero);
        var today = GardenDay.From(clock.Now, Boundary);
        var yesterday = new GardenDay(today.Index - 1);
        var ledger = new Ledger();
        var result = ledger.Backfill("water", yesterday, today, clock);
        Assert.True(result.Ok);
        Assert.True(result.Event.Late);
        Assert.Equal(TendSource.Backfill, result.Event.Source);
        Assert.Equal(yesterday.Index, result.Event.Day);
        Assert.Equal(clock.Now.ToUnixTimeMilliseconds(), result.Event.AtUtcMs);
        Assert.NotEqual(0, result.Event.AtUtcMs);
        Assert.True(ledger.IsKept("water", yesterday.Index));
    }

    [Fact]
    public void Backfill_a_second_time_is_refused()
    {
        var clock = ClockAt(2026, 10, 5, 18, 0);
        var today = GardenDay.From(clock.Now, Boundary);
        var yesterday = new GardenDay(today.Index - 1);
        var ledger = new Ledger();
        Assert.True(ledger.Backfill("water", yesterday, today, clock).Ok);
        var second = ledger.Backfill("water", yesterday, today, clock);
        Assert.False(second.Ok);
        Assert.Equal("already-kept", second.Reason);
        Assert.Single(ledger.Events);
        Assert.True(ledger.Events[0].Late);
    }

    [Fact]
    public void Backfill_two_days_ago_is_refused()
    {
        var clock = ClockAt(2026, 10, 5, 18, 0);
        var today = GardenDay.From(clock.Now, Boundary);
        var ledger = new Ledger();
        var result = ledger.Backfill("water", new GardenDay(today.Index - 2), today, clock);
        Assert.False(result.Ok);
        Assert.Equal("not-yesterday", result.Reason);
        Assert.Empty(ledger.Events);
    }

    [Fact]
    public void Backfill_after_the_boundary_is_refused()
    {
        var zone = FixedZone(0);
        var clock = new FixedClock(Wall(2026, 10, 5, 18, 0), zone);
        var today = GardenDay.From(clock.Now, Boundary);
        var yesterday = new GardenDay(today.Index - 1);
        clock.Now = today.EndsAt(zone, Boundary);
        var refused = new Ledger().Backfill("water", yesterday, today, clock);
        Assert.False(refused.Ok);
        Assert.Equal("after-boundary", refused.Reason);
        clock.Now = today.EndsAt(zone, Boundary).AddMilliseconds(-1);
        var allowed = new Ledger().Backfill("water", yesterday, today, clock);
        Assert.True(allowed.Ok);
        Assert.True(allowed.Event.Late);
    }

    [Fact]
    public void Kept_days_in_a_seven_day_window_ignore_undone_and_older_days()
    {
        var clock = ClockAt(2026, 10, 8, 12, 0);
        var today = GardenDay.From(clock.Now, Boundary);
        var ledger = new Ledger();
        Assert.True(ledger.Tend("water", today, TendSource.Pinch, clock).Ok);
        var yesterday = ledger.Tend("water", new GardenDay(today.Index - 1), TendSource.Pinch, clock);
        Assert.True(ledger.Tend("water", new GardenDay(today.Index - 6), TendSource.Poke, clock).Ok);
        Assert.True(ledger.Tend("water", new GardenDay(today.Index - 8), TendSource.Ritual, clock).Ok);
        Assert.True(ledger.Undo(yesterday.Event.Id, clock));
        Assert.Equal(2, ledger.KeptDaysInWindow("water", today, 7));
        Assert.Equal(3, ledger.KeptDays("water"));
        Assert.False(ledger.IsKept("water", today.Index - 1));
        Assert.True(ledger.IsKept("water", today.Index - 8));
    }

    [Fact]
    public void Json_round_trip_keeps_escapes_and_unknown_members()
    {
        const string raw = "{\"SchemaVersion\":1,\"Name\":\"caf\\u00e9\",\"note\":\"line\\nquote\\\"slash\\\\\",\"extra\":{\"z\":true},\"n\":1.5,\"flag\":false,\"z\":null,\"list\":[1,\"a\"]}";
        var parsed = Json.Parse(raw).AsObject();
        Assert.Equal("caf\u00e9", parsed.Get("Name").AsString());
        Assert.Equal("line\nquote\"slash\\", parsed.Get("note").AsString());
        Assert.Equal(1.5, parsed.Get("n").AsDouble(), 10);
        Assert.False(parsed.Get("flag").AsBool());
        Assert.True(parsed.Get("z").IsNull);
        Assert.Equal(1, parsed.Get("list").AsArray()[0].AsInt());

        var again = Json.Parse(Json.Write(parsed)).AsObject();
        Assert.Equal("line\nquote\"slash\\", again.Get("note").AsString());
        Assert.True(again.Get("extra").AsObject().Get("z").AsBool());

        var extra = parsed.Passthrough("SchemaVersion", "Name");
        Assert.False(extra.ContainsKey("Name"));
        Assert.True(extra.ContainsKey("extra"));
        Assert.True(extra.ContainsKey("list"));
        var rewritten = new JsonObject();
        rewritten.Set("SchemaVersion", JsonValue.Number(2));
        rewritten.Set("Name", JsonValue.String("reed"));
        rewritten.Restore(extra);
        var restored = Json.Parse(Json.Write(rewritten)).AsObject();
        Assert.Equal(2, restored.Get("SchemaVersion").AsInt());
        Assert.Equal("reed", restored.Get("Name").AsString());
        Assert.Equal("line\nquote\"slash\\", restored.Get("note").AsString());
        Assert.True(restored.Get("extra").AsObject().Get("z").AsBool());

        var previous = CultureInfo.CurrentCulture;
        try
        {
            var comma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            comma.NumberFormat.NumberDecimalSeparator = ",";
            CultureInfo.CurrentCulture = comma;
            var number = Json.Parse("{\"n\":2.25}").AsObject();
            Assert.Equal(2.25, number.Get("n").AsDouble(), 10);
            number.Set("n", JsonValue.Number(2.25));
            var text = Json.Write(number);
            Assert.Contains("2.25", text);
            Assert.DoesNotContain(",", text);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        Assert.Throws<FormatException>(() => Json.Parse("{\"a\":"));
    }

    [Fact]
    public void Empty_directory_loads_fresh()
    {
        var dir = TempDir();
        try
        {
            var result = Store(dir, 1).Load();
            Assert.Equal(LoadOutcome.Fresh, result.Outcome);
            Assert.NotNull(result.Doc);
            Assert.Equal(1, result.Doc.SchemaVersion);
            Assert.Null(result.FailedStep);
            Assert.False(result.BackupAvailable);
            Assert.False(result.ReadOnly);
            Assert.False(File.Exists(Path.Combine(dir, SaveStore<NoteDoc>.LiveName)));
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void Truncated_save_is_failed_with_backup_available_and_is_not_fresh()
    {
        var dir = TempDir();
        try
        {
            var store = Store(dir, 1);
            store.Save(new NoteDoc { SchemaVersion = 1, Name = "moss" });
            store.Save(new NoteDoc { SchemaVersion = 1, Name = "fern" });
            WriteText(Path.Combine(dir, SaveStore<NoteDoc>.LiveName), "{\"SchemaVersion\":1,\"Name\":\"tru");
            var truncated = store.Load();
            Assert.Equal(LoadOutcome.Failed, truncated.Outcome);
            Assert.NotEqual(LoadOutcome.Fresh, truncated.Outcome);
            Assert.Equal("parse", truncated.FailedStep);
            Assert.True(truncated.BackupAvailable);
            Assert.Null(truncated.Doc);

            WriteText(Path.Combine(dir, SaveStore<NoteDoc>.LiveName), "{\"Name\":\"x\"}");
            var missing = store.Load();
            Assert.Equal(LoadOutcome.Failed, missing.Outcome);
            Assert.NotEqual(LoadOutcome.Fresh, missing.Outcome);
            Assert.Equal("schema-version", missing.FailedStep);
            Assert.True(missing.BackupAvailable);
        }
        finally { DeleteDir(dir); }

        var bare = TempDir();
        try
        {
            WriteText(Path.Combine(bare, SaveStore<NoteDoc>.LiveName), "{");
            var result = Store(bare, 1).Load();
            Assert.Equal(LoadOutcome.Failed, result.Outcome);
            Assert.NotEqual(LoadOutcome.Fresh, result.Outcome);
            Assert.False(result.BackupAvailable);
            Assert.Null(result.Doc);
        }
        finally { DeleteDir(bare); }
    }

    [Fact]
    public void Newer_schema_loads_read_only_and_save_refuses()
    {
        var dir = TempDir();
        try
        {
            const string future = "{\"SchemaVersion\":9,\"Name\":\"future\",\"extra\":1}";
            WriteText(Path.Combine(dir, SaveStore<NoteDoc>.LiveName), future);
            var store = Store(dir, 1);
            var result = store.Load();
            Assert.Equal(LoadOutcome.Loaded, result.Outcome);
            Assert.True(result.ReadOnly);
            Assert.False(result.BackupAvailable);
            Assert.Equal("future", result.Doc.Name);
            Assert.Equal(9, result.Doc.SchemaVersion);
            Assert.Throws<InvalidOperationException>(() => store.Save(new NoteDoc { SchemaVersion = 1, Name = "nope" }));
            Assert.Equal(future, ReadText(Path.Combine(dir, SaveStore<NoteDoc>.LiveName)));
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void Migration_writes_a_snapshot_before_the_first_step()
    {
        var dir = TempDir();
        try
        {
            var step = new MigrationStep(1, "add-kept", obj => obj.Set("Kept", JsonValue.Bool(true)));
            var store = Store(dir, 2, step);
            const string original = "{\"SchemaVersion\":1,\"Name\":\"fern\",\"extra\":{\"hue\":\"moss\"}}";
            WriteText(Path.Combine(dir, SaveStore<NoteDoc>.LiveName), original);
            var result = store.Load();
            Assert.Equal(LoadOutcome.Migrated, result.Outcome);
            Assert.Equal("fern", result.Doc.Name);
            Assert.Equal(2, result.Doc.SchemaVersion);
            Assert.Equal(original, ReadText(Path.Combine(dir, SaveStore<NoteDoc>.SnapshotName)));
            var live = ReadText(Path.Combine(dir, SaveStore<NoteDoc>.LiveName));
            Assert.Contains("\"SchemaVersion\":2", live);
            Assert.Contains("Kept", live);
            Assert.Contains("moss", live);
            Assert.Equal(original, ReadText(Path.Combine(dir, SaveStore<NoteDoc>.Prev1Name)));
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void A_failed_migration_is_not_a_fresh_garden()
    {
        var dir = TempDir();
        try
        {
            var step = new MigrationStep(1, "explode", obj => { throw new InvalidOperationException("nope"); });
            var store = Store(dir, 2, step);
            const string original = "{\"SchemaVersion\":1,\"Name\":\"fern\"}";
            WriteText(Path.Combine(dir, SaveStore<NoteDoc>.LiveName), original);
            var result = store.Load();
            Assert.Equal(LoadOutcome.Failed, result.Outcome);
            Assert.NotEqual(LoadOutcome.Fresh, result.Outcome);
            Assert.Equal("explode", result.FailedStep);
            Assert.True(result.BackupAvailable);
            Assert.Null(result.Doc);
            Assert.Equal(original, ReadText(Path.Combine(dir, SaveStore<NoteDoc>.LiveName)));
            Assert.Equal(original, ReadText(Path.Combine(dir, SaveStore<NoteDoc>.SnapshotName)));
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void Save_rotates_two_backups_then_replaces()
    {
        var dir = TempDir();
        try
        {
            var store = Store(dir, 1);
            store.Save(new NoteDoc { SchemaVersion = 1, Name = "first" });
            store.Save(new NoteDoc { SchemaVersion = 1, Name = "second" });
            store.Save(new NoteDoc { SchemaVersion = 1, Name = "third" });
            Assert.Contains("third", ReadText(Path.Combine(dir, SaveStore<NoteDoc>.LiveName)));
            Assert.Contains("second", ReadText(Path.Combine(dir, SaveStore<NoteDoc>.Prev1Name)));
            Assert.Contains("first", ReadText(Path.Combine(dir, SaveStore<NoteDoc>.Prev2Name)));
            Assert.False(File.Exists(Path.Combine(dir, SaveStore<NoteDoc>.TempName)));
            var loaded = Store(dir, 1).Load();
            Assert.Equal(LoadOutcome.Loaded, loaded.Outcome);
            Assert.Equal("third", loaded.Doc.Name);

            File.Delete(Path.Combine(dir, SaveStore<NoteDoc>.LiveName));
            var crashed = Store(dir, 1).Load();
            Assert.Equal(LoadOutcome.Failed, crashed.Outcome);
            Assert.NotEqual(LoadOutcome.Fresh, crashed.Outcome);
            Assert.Equal("missing-save", crashed.FailedStep);
            Assert.True(crashed.BackupAvailable);
            Assert.Null(crashed.Doc);
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void A_failed_write_never_leaves_a_half_written_save_but_an_in_place_writer_does()
    {
        var goodDir = TempDir();
        try
        {
            var io = new DiskSaveIo(goodDir);
            var store = Store(io, 1);
            store.Save(new NoteDoc { SchemaVersion = 1, Name = "original-complete-document" });
            var original = File.ReadAllBytes(Path.Combine(goodDir, SaveStore<NoteDoc>.LiveName));
            io.WrapWrite = stream => new FailAfter(stream, 12);
            Assert.Throws<IOException>(() => store.Save(new NoteDoc { SchemaVersion = 1, Name = "replacement-that-must-not-land" }));
            Assert.Equal(original, File.ReadAllBytes(Path.Combine(goodDir, SaveStore<NoteDoc>.LiveName)));
        }
        finally { DeleteDir(goodDir); }

        var badDir = TempDir();
        try
        {
            var io = new DiskSaveIo(badDir);
            var live = Path.Combine(badDir, SaveStore<NoteDoc>.LiveName);
            WriteText(live, "{\"SchemaVersion\":1,\"Name\":\"original-complete-document\"}");
            var original = File.ReadAllBytes(live);
            io.WrapWrite = stream => new FailAfter(stream, 12);
            var broken = new InPlaceWriter(io);
            Assert.Throws<IOException>(() => broken.Write("{\"SchemaVersion\":1,\"Name\":\"replacement-that-must-not-land\"}"));
            var torn = File.ReadAllBytes(live);
            Assert.NotEqual(original, torn);
            Assert.Throws<FormatException>(() => Json.Parse(Utf8.GetString(torn)));
        }
        finally { DeleteDir(badDir); }
    }

    [Fact]
    public void Defaults_equal_to_the_default_are_not_written()
    {
        var registry = new SettingsRegistry();
        var voice = registry.Define("voice", false);
        var boundary = registry.Define("boundaryMin", 180);
        registry.Set(voice, false);
        registry.Set(boundary, 180);
        Assert.Equal(0, registry.ToJson().Count);
        Assert.False(registry.IsStored("voice"));
        registry.Set(voice, true);
        registry.Set(boundary, 200);
        var written = registry.ToJson();
        Assert.True(written.Get("voice").AsBool());
        Assert.Equal(200, written.Get("boundaryMin").AsInt());
        Assert.False(written.Has("absent"));
        registry.Set(voice, false);
        Assert.False(registry.ToJson().Has("voice"));
        Assert.False(registry.Get(voice));
        Assert.Equal(200, registry.Get(boundary));
        Assert.Throws<ArgumentException>(() => registry.Set(new SettingKey<bool>("nope", false), true));
        Assert.Throws<ArgumentException>(() => registry.Define("voice", true));
        Assert.Throws<ArgumentException>(() => registry.ReadJson(Json.Parse("{\"voice\":true,\"extra\":1}").AsObject()));
        Assert.False(registry.Get(voice));
        Assert.Equal(200, registry.Get(boundary));
        registry.ReadJson(Json.Parse("{\"voice\":false,\"boundaryMin\":180}").AsObject());
        Assert.Equal(0, registry.ToJson().Count);
    }

    [Fact]
    public void System_clock_reports_utc_now()
    {
        var clock = new SystemClock(TimeZoneInfo.Utc);
        var delta = (clock.Now - DateTimeOffset.UtcNow).Duration();
        Assert.True(delta < TimeSpan.FromSeconds(5));
        Assert.Equal(TimeZoneInfo.Utc.Id, clock.Zone.Id);
    }

    static DateTimeOffset Wall(int y, int m, int d, int hh, int mm)
    {
        return new DateTimeOffset(y, m, d, hh, mm, 0, TimeSpan.Zero);
    }

    static FixedClock ClockAt(int y, int m, int d, int hh, int mm)
    {
        return new FixedClock(Wall(y, m, d, hh, mm), FixedZone(0));
    }

    static TimeZoneInfo FixedZone(int hours)
    {
        var id = NextZoneId("GvrF");
        return TimeZoneInfo.CreateCustomTimeZone(id, TimeSpan.FromHours(hours), id, id);
    }

    static TimeZoneInfo DstZone(int startMonth, int startDay, int startHour, int endMonth, int endDay, int endHour, int deltaHours)
    {
        var id = NextZoneId("GvrD");
        var start = TimeZoneInfo.TransitionTime.CreateFixedDateRule(
            new DateTime(1, 1, 1, startHour, 0, 0, DateTimeKind.Unspecified), startMonth, startDay);
        var end = TimeZoneInfo.TransitionTime.CreateFixedDateRule(
            new DateTime(1, 1, 1, endHour, 0, 0, DateTimeKind.Unspecified), endMonth, endDay);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
            new DateTime(2099, 12, 31, 0, 0, 0, DateTimeKind.Unspecified),
            TimeSpan.FromHours(deltaHours),
            start,
            end);
        return TimeZoneInfo.CreateCustomTimeZone(id, TimeSpan.Zero, id, id + "S", id + "D", new[] { rule });
    }

    static string NextZoneId(string prefix)
    {
        _zoneIds++;
        return prefix + _zoneIds.ToString(CultureInfo.InvariantCulture);
    }

    static SaveStore<NoteDoc> Store(string dir, int current, params MigrationStep[] steps)
    {
        return Store(new DiskSaveIo(dir), current, steps);
    }

    static SaveStore<NoteDoc> Store(ISaveIo io, int current, params MigrationStep[] steps)
    {
        return new SaveStore<NoteDoc>(
            io,
            current,
            () => new NoteDoc { SchemaVersion = current, Name = "" },
            ReadNote,
            WriteNote,
            steps);
    }

    static NoteDoc ReadNote(JsonObject obj)
    {
        return new NoteDoc
        {
            SchemaVersion = obj.Get("SchemaVersion").AsInt(),
            Name = obj.Has("Name") ? obj.Get("Name").AsString() : ""
        };
    }

    static JsonObject WriteNote(NoteDoc doc)
    {
        var obj = new JsonObject();
        obj.Set("SchemaVersion", JsonValue.Number(doc.SchemaVersion));
        obj.Set("Name", JsonValue.String(doc.Name ?? ""));
        return obj;
    }

    static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gvr-tsun002-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    static void DeleteDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        catch (IOException) { }
    }

    static void WriteText(string path, string text) { File.WriteAllBytes(path, Utf8.GetBytes(text)); }
    static string ReadText(string path) { return Utf8.GetString(File.ReadAllBytes(path)); }

    sealed class NoteDoc
    {
        public int SchemaVersion;
        public string Name;
    }

    /// <summary>The negative control: writes straight onto the live name, so a torn stream is the save.</summary>
    sealed class InPlaceWriter
    {
        readonly ISaveIo _io;
        public InPlaceWriter(ISaveIo io) { _io = io; }
        public void Write(string json)
        {
            var bytes = Utf8.GetBytes(json);
            using (var stream = _io.Create(SaveStore<NoteDoc>.LiveName))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }
        }
    }

    sealed class FailAfter : Stream
    {
        Stream _inner;
        readonly int _max;
        int _written;

        public FailAfter(Stream inner, int max)
        {
            _inner = inner;
            _max = max;
        }

        public override bool CanRead { get { return false; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return true; } }
        public override long Length { get { throw new NotSupportedException(); } }
        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override void Flush() { _inner.Flush(); }
        public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_written >= _max) throw new IOException("injected failure");
            int allow = Math.Min(count, _max - _written);
            if (allow > 0)
            {
                _inner.Write(buffer, offset, allow);
                _written += allow;
            }
            if (allow < count) throw new IOException("injected failure");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _inner != null)
            {
                try { _inner.Dispose(); }
                catch (IOException) { }
                _inner = null;
            }
            base.Dispose(disposing);
        }
    }
}
