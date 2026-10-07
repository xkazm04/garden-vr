using System;
using System.Text;
using GardenVR.Core;
using Xunit;

namespace GardenVR.Core.Tests;

public class SaveRestoreTests
{
    // Restore brings a record back from prev1, prev2 or the snapshot when save.json cannot be read. It never
    // replaces a readable live file, never rotates the previous copies, and keeps the damaged bytes.

    static readonly string[] AllNames =
    {
        SaveStore<Note>.LiveName, SaveStore<Note>.TempName, SaveStore<Note>.Prev1Name, SaveStore<Note>.Prev2Name,
        SaveStore<Note>.SnapshotName, "save.damaged.json", "save.damaged.2.json", "save.damaged.3.json"
    };

    [Fact]
    public void A_damaged_live_file_with_a_good_prev1_restores_from_prev1()
    {
        var io = new SaveCrashWindowTests.MemoryIo();
        SaveTwo(io, "one", "two");
        Put(io, SaveStore<Note>.LiveName, "{torn");

        var result = Store(io).Restore();

        Assert.Equal(LoadOutcome.Loaded, result.Outcome);
        Assert.Equal("one", result.Doc.Name);
        Assert.Equal(SaveStore<Note>.Prev1Name, result.RestoredFrom);
        Assert.False(result.ReadOnly);
        Assert.Equal("one", Store(io).Load().Doc.Name);
    }

    [Fact]
    public void A_damaged_live_file_and_a_damaged_prev1_restore_from_prev2()
    {
        var io = new SaveCrashWindowTests.MemoryIo();
        var store = Store(io);
        store.Save(new Note { Name = "one" });
        store.Save(new Note { Name = "two" });
        store.Save(new Note { Name = "three" });
        Put(io, SaveStore<Note>.LiveName, "{torn");
        Put(io, SaveStore<Note>.Prev1Name, "not json");

        var result = Store(io).Restore();

        Assert.Equal(LoadOutcome.Loaded, result.Outcome);
        Assert.Equal("one", result.Doc.Name);
        Assert.Equal(SaveStore<Note>.Prev2Name, result.RestoredFrom);
    }

    [Fact]
    public void A_missing_live_file_with_a_good_prev1_restores()
    {
        var io = new SaveCrashWindowTests.MemoryIo();
        SaveTwo(io, "one", "two");
        io.Delete(SaveStore<Note>.LiveName);

        var result = Store(io).Restore();

        Assert.Equal(LoadOutcome.Loaded, result.Outcome);
        Assert.Equal("one", result.Doc.Name);
        Assert.False(io.Exists("save.damaged.json"));
        Assert.Equal("one", Store(io).Load().Doc.Name);
    }

    [Fact]
    public void When_every_candidate_is_unreadable_the_result_is_failed_and_no_file_changes()
    {
        var io = new SaveCrashWindowTests.MemoryIo();
        Put(io, SaveStore<Note>.LiveName, "{torn");
        Put(io, SaveStore<Note>.Prev1Name, "garbage");
        Put(io, SaveStore<Note>.Prev2Name, "{\"SchemaVersion\":");
        var before = Snapshot(io);

        var result = Store(io).Restore();

        Assert.Equal(LoadOutcome.Failed, result.Outcome);
        Assert.Equal("restore", result.FailedStep);
        Assert.Null(result.Doc);
        Assert.Equal(before, Snapshot(io));
    }

    [Fact]
    public void An_older_schema_snapshot_returns_migrated_at_the_current_schema()
    {
        var io = new SaveCrashWindowTests.MemoryIo();
        Put(io, SaveStore<Note>.LiveName, "{torn");
        Put(io, SaveStore<Note>.SnapshotName, "{\"SchemaVersion\":1,\"Name\":\"old\"}");

        var result = StoreV2(io).Restore();

        Assert.Equal(LoadOutcome.Migrated, result.Outcome);
        Assert.Equal("old!", result.Doc.Name);
        Assert.Equal(SaveStore<Note>.SnapshotName, result.RestoredFrom);
        Assert.Contains("\"SchemaVersion\":2", Text(io, SaveStore<Note>.LiveName).Replace(" ", ""));
        Assert.Equal("{\"SchemaVersion\":1,\"Name\":\"old\"}", Text(io, SaveStore<Note>.SnapshotName));
        var loaded = StoreV2(io).Load();
        Assert.Equal(LoadOutcome.Loaded, loaded.Outcome);
        Assert.Equal("old!", loaded.Doc.Name);
    }

    [Fact]
    public void A_newer_schema_prev1_is_skipped_and_prev2_is_used()
    {
        var io = new SaveCrashWindowTests.MemoryIo();
        Put(io, SaveStore<Note>.LiveName, "{torn");
        Put(io, SaveStore<Note>.Prev1Name, "{\"SchemaVersion\":9,\"Name\":\"future\"}");
        Put(io, SaveStore<Note>.Prev2Name, "{\"SchemaVersion\":1,\"Name\":\"past\"}");

        var result = Store(io).Restore();

        Assert.Equal(LoadOutcome.Loaded, result.Outcome);
        Assert.Equal("past", result.Doc.Name);
        Assert.Equal(SaveStore<Note>.Prev2Name, result.RestoredFrom);
    }

    [Fact]
    public void When_only_newer_candidates_exist_the_result_is_failed()
    {
        var io = new SaveCrashWindowTests.MemoryIo();
        Put(io, SaveStore<Note>.LiveName, "{torn");
        Put(io, SaveStore<Note>.Prev1Name, "{\"SchemaVersion\":9,\"Name\":\"future\"}");
        Put(io, SaveStore<Note>.SnapshotName, "{\"SchemaVersion\":3,\"Name\":\"future\"}");
        var before = Snapshot(io);

        var result = Store(io).Restore();

        Assert.Equal(LoadOutcome.Failed, result.Outcome);
        Assert.Equal("restore", result.FailedStep);
        Assert.Equal(before, Snapshot(io));
    }

    [Fact]
    public void A_readable_live_file_gives_live_readable_and_changes_nothing()
    {
        var io = new SaveCrashWindowTests.MemoryIo();
        SaveTwo(io, "one", "two");
        var before = Snapshot(io);

        var result = Store(io).Restore();

        Assert.Equal(LoadOutcome.Failed, result.Outcome);
        Assert.Equal("live-readable", result.FailedStep);
        Assert.Equal(before, Snapshot(io));
    }

    [Fact]
    public void A_save_after_a_restore_rotates_normally_and_load_returns_the_same_document()
    {
        var io = new SaveCrashWindowTests.MemoryIo();
        SaveTwo(io, "one", "two");
        Put(io, SaveStore<Note>.LiveName, "{torn");
        var store = Store(io);
        var restored = store.Restore();
        Assert.Equal("one", Store(io).Load().Doc.Name);
        Assert.Equal(restored.Doc.Name, Store(io).Load().Doc.Name);

        store.Save(new Note { Name = "next" });

        Assert.Equal("next", Store(io).Load().Doc.Name);
        Assert.Contains("one", Text(io, SaveStore<Note>.Prev1Name));
        Assert.Contains("one", Text(io, SaveStore<Note>.Prev2Name));
    }

    [Fact]
    public void The_damaged_bytes_survive_and_a_second_restore_keeps_both_damaged_files()
    {
        var io = new SaveCrashWindowTests.MemoryIo();
        SaveTwo(io, "one", "two");
        Put(io, SaveStore<Note>.LiveName, "{torn-first");
        Store(io).Restore();
        Assert.Equal("{torn-first", Text(io, "save.damaged.json"));

        Put(io, SaveStore<Note>.TempName, "stale temp");
        io.Delete(SaveStore<Note>.LiveName);
        Put(io, SaveStore<Note>.LiveName, "{torn-second");
        var second = Store(io).Restore();

        Assert.Equal(LoadOutcome.Loaded, second.Outcome);
        Assert.Equal("{torn-first", Text(io, "save.damaged.json"));
        Assert.Equal("{torn-second", Text(io, "save.damaged.2.json"));
        Assert.False(io.Exists(SaveStore<Note>.TempName));
    }

    [Fact]
    public void Prev1_and_prev2_are_byte_identical_before_and_after_a_restore()
    {
        var io = new SaveCrashWindowTests.MemoryIo();
        var store = Store(io);
        store.Save(new Note { Name = "one" });
        store.Save(new Note { Name = "two" });
        store.Save(new Note { Name = "three" });
        Put(io, SaveStore<Note>.LiveName, "{torn");
        Put(io, SaveStore<Note>.SnapshotName, "{\"SchemaVersion\":1,\"Name\":\"snap\"}");
        var prev1 = io.ReadBytes(SaveStore<Note>.Prev1Name);
        var prev2 = io.ReadBytes(SaveStore<Note>.Prev2Name);
        var snapshot = io.ReadBytes(SaveStore<Note>.SnapshotName);

        Store(io).Restore();

        Assert.Equal(prev1, io.ReadBytes(SaveStore<Note>.Prev1Name));
        Assert.Equal(prev2, io.ReadBytes(SaveStore<Note>.Prev2Name));
        Assert.Equal(snapshot, io.ReadBytes(SaveStore<Note>.SnapshotName));
    }

    [Fact]
    public void RestoredFrom_is_null_after_a_plain_load()
    {
        var io = new SaveCrashWindowTests.MemoryIo();
        Assert.Null(Store(io).Load().RestoredFrom);
        SaveTwo(io, "one", "two");
        Assert.Null(Store(io).Load().RestoredFrom);
    }

    static void SaveTwo(ISaveIo io, string first, string second)
    {
        var store = Store(io);
        store.Save(new Note { Name = first });
        store.Save(new Note { Name = second });
    }

    static void Put(SaveCrashWindowTests.MemoryIo io, string name, string text)
    {
        if (io.Exists(name)) io.Delete(name);
        var bytes = Encoding.UTF8.GetBytes(text);
        using (var stream = io.Create(name)) { stream.Write(bytes, 0, bytes.Length); }
    }

    static string Text(SaveCrashWindowTests.MemoryIo io, string name) { return Encoding.UTF8.GetString(io.ReadBytes(name)); }

    static string Snapshot(SaveCrashWindowTests.MemoryIo io)
    {
        var sb = new StringBuilder();
        foreach (var name in AllNames)
            sb.Append(name).Append('=').Append(io.Exists(name) ? Convert.ToBase64String(io.ReadBytes(name)) : "-").Append(';');
        return sb.ToString();
    }

    static SaveStore<Note> Store(ISaveIo io) { return Build(io, 1, Array.Empty<MigrationStep>()); }

    static SaveStore<Note> StoreV2(ISaveIo io)
    {
        var step = new MigrationStep(1, "bang", obj => obj.Set("Name", JsonValue.String(obj.Get("Name").AsString() + "!")));
        return Build(io, 2, new[] { step });
    }

    static SaveStore<Note> Build(ISaveIo io, int schema, MigrationStep[] steps)
    {
        return new SaveStore<Note>(
            io,
            schema,
            () => new Note { Name = "" },
            obj => new Note { Name = obj.Get("Name").AsString() },
            doc =>
            {
                var obj = new JsonObject();
                obj.Set("SchemaVersion", JsonValue.Number(schema));
                obj.Set("Name", JsonValue.String(doc.Name));
                return obj;
            },
            steps);
    }

    sealed class Note { public string Name; }
}
