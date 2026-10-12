using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using GardenVR.Core;
using GardenVR.Sundial;
using Xunit;

namespace GardenVR.SundialModel.Tests;

public sealed class SundialUpgradeSaveTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "sundial-upgrade-" + Guid.NewGuid().ToString("N"));
    readonly FixedClock _clock = new FixedClock(new DateTimeOffset(2026, 10, 12, 2, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
    public SundialUpgradeSaveTests() { Directory.CreateDirectory(_dir); }
    public void Dispose() { Directory.Delete(_dir, true); }
    string Live => Path.Combine(_dir, "save.json");
    SundialService Open() => new SundialService(_clock, _dir);

    [Fact]
    public void Every_upgrade_member_survives_codec_and_disk_round_trips()
    {
        var service = Open();
        SundialSave save = service.Save;
        string key = save.InstallKey;
        save.Counts.Add(new CountEvent { Id = "c1", HabitId = "water", Day = 9780, AtUtcMs = 1760000000000L });
        save.Counts.Add(new CountEvent { Id = "c2", HabitId = "water", Day = 9781, AtUtcMs = 1760086400000L, UndoneAtUtcMs = 1760086400100L });
        Assert.True(save.Journal.Keep(9780, "You made room for a small pause."));
        Assert.True(save.Journal.Keep(9781, "A little time in the garden."));
        save.LastVisitDay = 9781;
        save.Settings.CoachOptIn = true;
        save.Settings.CatchUpDepth = 3;
        save.Settings.Nudges = false;
        save.Settings.BreakPoses = false;
        save.Settings.NudgeWindows = new List<NudgeWindow>
        {
            new NudgeWindow(new TimeSpan(8, 15, 30), new TimeSpan(9, 45, 59)),
            new NudgeWindow(new TimeSpan(18, 0, 0), TimeSpan.FromDays(1))
        };
        string text = Json.Write(SundialCodec.Write(save));
        var back = SundialCodec.Read(Json.ParseObject(text));
        Assert.Equal(text, Json.Write(SundialCodec.Write(back)));
        Assert.Equal(2, back.SchemaVersion);
        Assert.Equal(key, back.InstallKey);
        Assert.Equal(9781, back.LastVisitDay);
        Assert.Equal(2, back.Counts.Count);
        Assert.Null(back.Counts[0].UndoneAtUtcMs);
        Assert.Equal(1760086400100L, back.Counts[1].UndoneAtUtcMs);
        Assert.Equal("A little time in the garden.", back.Journal.On(9781));
        Assert.Equal(2, back.Journal.Entries.Count);
        Assert.True(back.Settings.CoachOptIn);
        Assert.Equal(3, back.Settings.CatchUpDepth);
        Assert.False(back.Settings.Nudges);
        Assert.False(back.Settings.BreakPoses);
        Assert.Equal(save.Settings.NudgeWindows, back.Settings.NudgeWindows);
        service.SetMute(true); // Normal app save, with every upgrade field still present.
        var reloaded = Open();
        Assert.Equal(Json.Write(SundialCodec.Write(save)), Json.Write(SundialCodec.Write(reloaded.Save)));
        reloaded.Save.Journal.Clear();
        reloaded.Save.Settings.NudgeWindows.Clear();
        reloaded.SetMute(false);
        var cleared = Open();
        Assert.Empty(cleared.Save.Journal.Entries);
        Assert.Empty(cleared.Save.Settings.NudgeWindows);
        Assert.Equal(key, cleared.Save.InstallKey);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Missing_members_get_defaults_and_each_save_owns_its_windows(int version)
    {
        File.WriteAllText(Live, "{\"SchemaVersion\":" + version + "}");
        var service = Open();
        Assert.Equal(version == 1 ? LoadOutcome.Migrated : LoadOutcome.Loaded, service.Outcome);
        SundialSave save = service.Save;
        Assert.Empty(save.Counts);
        Assert.Empty(save.Journal.Entries);
        Assert.Null(save.LastVisitDay);
        Assert.Matches("^[0-9a-f]{32}$", save.InstallKey);
        Assert.False(save.Settings.CoachOptIn);
        Assert.Equal(CatchUp.DefaultDepth, save.Settings.CatchUpDepth);
        Assert.True(save.Settings.Nudges);
        Assert.True(save.Settings.BreakPoses);
        Assert.Equal(NudgePlanner.DefaultWindows, save.Settings.NudgeWindows);
        save.Settings.NudgeWindows.Clear();
        Assert.Equal(2, new SundialSettings().NudgeWindows.Count);
        Assert.Equal(2, NudgePlanner.DefaultWindows.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(8)]
    public void Invalid_depth_uses_the_core_default(int depth)
    {
        var save = SundialCodec.Read(Json.ParseObject("{\"Settings\":{\"CatchUpDepth\":" + depth + "}}"));
        Assert.Equal(CatchUp.DefaultDepth, save.Settings.CatchUpDepth);
    }

    [Fact]
    public void Launch_keeps_previous_visit_until_completion_then_saves_the_garden_day_once()
    {
        var first = Open();
        Assert.Null(first.Save.LastVisitDay);
        string key = first.Save.InstallKey;
        first.CompleteLaunch();
        int day = first.Today().Index;
        Assert.Equal(day, first.Save.LastVisitDay);
        // 02:00 belongs to yesterday with the default 03:00 boundary.
        Assert.Equal(GardenDay.From(_clock.Now.AddDays(-1), TimeSpan.Zero).Index, day);
        var laterClock = new FixedClock(_clock.Now.AddDays(3), TimeZoneInfo.Utc);
        var later = new SundialService(laterClock, _dir);
        Assert.Equal(day, later.Save.LastVisitDay);
        Assert.Equal(key, later.Save.InstallKey);
        later.CompleteLaunch();
        byte[] completed = File.ReadAllBytes(Live);
        later.JumpTo(laterClock.Now.AddDays(1));
        later.CompleteLaunch();
        Assert.Equal(completed, File.ReadAllBytes(Live));
        var reloaded = Open();
        Assert.Equal(day + 3, reloaded.Save.LastVisitDay);
        Assert.Equal(key, reloaded.Save.InstallKey);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("{\"SchemaVersion\":3,\"LastVisitDay\":123,\"InstallKey\":\"future-key\"}")]
    public void Launch_never_overwrites_a_failed_or_future_save(string original)
    {
        File.WriteAllText(Live, original);
        var service = Open();
        int? before = service.Save.LastVisitDay;
        service.CompleteLaunch();
        Assert.Equal(before, service.Save.LastVisitDay);
        Assert.Equal(original, File.ReadAllText(Live));
    }

    [Fact]
    public void Migration_preserves_existing_upgrade_values_and_unknown_settings()
    {
        const string original = "{\"SchemaVersion\":1,\"Counts\":[],\"Journal\":[],\"LastVisitDay\":123,"
            + "\"InstallKey\":\"existing-key\",\"Settings\":{\"CoachOptIn\":true,\"CatchUpDepth\":3,"
            + "\"NudgeWindows\":[],\"Nudges\":false,\"BreakPoses\":false,\"FutureSetting\":7},\"FutureRoot\":42}";
        File.WriteAllText(Live, original);
        var service = Open();
        Assert.Equal(LoadOutcome.Migrated, service.Outcome);
        Assert.Equal("existing-key", service.Save.InstallKey);
        Assert.Equal(123, service.Save.LastVisitDay);
        Assert.True(service.Save.Settings.CoachOptIn);
        Assert.Equal(3, service.Save.Settings.CatchUpDepth);
        Assert.False(service.Save.Settings.Nudges);
        Assert.False(service.Save.Settings.BreakPoses);
        Assert.Empty(service.Save.Settings.NudgeWindows);
        service.SetMute(true);
        Assert.Equal(7, Open().Save.Settings.Extra["FutureSetting"].AsInt());
        Assert.Equal(42, Open().Save.Extra["FutureRoot"].AsInt());
        Assert.Equal(original, File.ReadAllText(Path.Combine(_dir, "save.snapshot.json")));
    }

    static string Fixture([CallerFilePath] string source = "") => Path.Combine(Path.GetDirectoryName(source), "Fixtures", "player-schema1-save.json");

    [Fact]
    public void The_current_players_schema1_save_loads_with_nothing_lost()
    {
        byte[] original = File.ReadAllBytes(Fixture());
        File.WriteAllBytes(Live, original);
        var service = Open();
        Assert.Equal(LoadOutcome.Migrated, service.Outcome);
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(_dir, "save.snapshot.json")));
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(_dir, "save.prev1.json")));
        var old = Json.ParseObject(System.Text.Encoding.UTF8.GetString(original));
        var migrated = Json.ParseObject(File.ReadAllText(Live));
        foreach (var pair in old.Passthrough("SchemaVersion", "Settings"))
            Assert.Equal(Json.Write(pair.Value), Json.Write(migrated.Get(pair.Key)));
        foreach (var pair in old.Get("Settings").AsObject().Passthrough())
            Assert.Equal(Json.Write(pair.Value), Json.Write(migrated.Get("Settings").AsObject().Get(pair.Key)));
        Assert.Equal(2, migrated.Get("SchemaVersion").AsInt());
        Assert.Equal("fr.appear", service.Save.FirstRunStep);
        string key = service.Save.InstallKey;
        service.CompleteLaunch();
        var reloaded = Open();
        Assert.Equal(LoadOutcome.Loaded, reloaded.Outcome);
        Assert.Equal(key, reloaded.Save.InstallKey);
        Assert.Equal(service.Today().Index, reloaded.Save.LastVisitDay);
        string evidence = Environment.GetEnvironmentVariable("GARDENVR_B1_EVIDENCE");
        if (!string.IsNullOrEmpty(evidence)) File.Copy(Live, Path.Combine(evidence, "player-schema2-save.json"), true);
    }
}
