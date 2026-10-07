using System;
using System.IO;
using System.Linq;
using GardenVR.Core;
using GardenVR.Sundial;
using Xunit;

// SundialService.TryRestoreBackup hands the restore to SaveStore.Restore. These use real files in a temp directory.
public sealed class SundialRestoreTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "gardenvr-sundial-restore-" + Guid.NewGuid().ToString("N"));
    readonly FixedClock _clock = new FixedClock(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);

    public SundialRestoreTests() { Directory.CreateDirectory(_dir); }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    string P(string name) { return Path.Combine(_dir, name); }

    SundialService Open() { return new SundialService(_clock, _dir); }

    // Four real saves: live holds water+top3 and mute, prev1 holds water+top3, prev2 holds water with one tend.
    void WriteFourSaves()
    {
        SundialService svc = Open();
        HabitDef water = svc.PlantPreset(SeedCatalog.Find("morning", "water"));
        Assert.True(svc.TendRitual(water.Id).Ok);
        svc.PlantPreset(SeedCatalog.Find("midday", "top3"));
        svc.SetMute(true);
        Assert.True(File.Exists(P("save.prev1.json")));
        Assert.True(File.Exists(P("save.prev2.json")));
    }

    static void Damage(string path) { File.WriteAllText(path, "{ this is not json"); }

    [Fact]
    public void A_damaged_live_file_with_a_good_prev1_is_restored_and_a_reload_reads_it()
    {
        WriteFourSaves();
        byte[] prev1 = File.ReadAllBytes(P("save.prev1.json"));
        Damage(P("save.json"));
        byte[] damaged = File.ReadAllBytes(P("save.json"));

        SundialService svc = Open();
        Assert.Equal(LoadOutcome.Failed, svc.Outcome);
        Assert.True(svc.BackupAvailable);
        Assert.True(svc.TryRestoreBackup());

        Assert.Equal(damaged, File.ReadAllBytes(P("save.damaged.json")));
        Assert.Equal(prev1, File.ReadAllBytes(P("save.prev1.json")));

        SundialService reloaded = Open();
        Assert.Equal(LoadOutcome.Loaded, reloaded.Outcome);
        Assert.Equal(new[] { "top3", "water" }, reloaded.Save.Habits.Select(h => h.PresetKey).OrderBy(k => k).ToArray());
        Assert.Single(reloaded.Save.Tends);
        Assert.False(reloaded.Save.Settings.Mute);
    }

    [Fact]
    public void With_live_and_prev1_damaged_prev2_is_restored()
    {
        WriteFourSaves();
        Damage(P("save.json"));
        Damage(P("save.prev1.json"));
        byte[] prev1 = File.ReadAllBytes(P("save.prev1.json"));

        SundialService svc = Open();
        Assert.True(svc.TryRestoreBackup());
        Assert.Equal(prev1, File.ReadAllBytes(P("save.prev1.json")));

        SundialService reloaded = Open();
        Assert.Equal(LoadOutcome.Loaded, reloaded.Outcome);
        Assert.Equal("water", Assert.Single(reloaded.Save.Habits).PresetKey);
        Assert.Single(reloaded.Save.Tends);
    }

    [Fact]
    public void When_every_file_is_unreadable_it_returns_false_and_leaves_every_file_alone()
    {
        WriteFourSaves();
        foreach (string name in new[] { "save.json", "save.prev1.json", "save.prev2.json" }) Damage(P(name));
        File.WriteAllText(P("save.snapshot.json"), "also not json");
        var before = Directory.GetFiles(_dir).ToDictionary(f => f, f => File.ReadAllBytes(f));

        SundialService svc = Open();
        Assert.Equal(LoadOutcome.Failed, svc.Outcome);
        Assert.False(svc.TryRestoreBackup());

        var after = Directory.GetFiles(_dir).ToDictionary(f => f, f => File.ReadAllBytes(f));
        Assert.Equal(before.Keys.OrderBy(k => k), after.Keys.OrderBy(k => k));
        foreach (string f in before.Keys) Assert.Equal(before[f], after[f]);
    }

    [Fact]
    public void With_no_backup_it_returns_false_and_writes_nothing()
    {
        Damage(P("save.json"));
        byte[] live = File.ReadAllBytes(P("save.json"));

        SundialService svc = Open();
        Assert.False(svc.BackupAvailable);
        Assert.False(svc.TryRestoreBackup());

        Assert.Equal(new[] { P("save.json") }, Directory.GetFiles(_dir));
        Assert.Equal(live, File.ReadAllBytes(P("save.json")));
    }

    [Fact]
    public void After_a_true_return_the_same_service_still_reports_failed_and_refuses_writes()
    {
        WriteFourSaves();
        Damage(P("save.json"));

        SundialService svc = Open();
        Assert.True(svc.TryRestoreBackup());
        byte[] restored = File.ReadAllBytes(P("save.json"));

        Assert.Equal(LoadOutcome.Failed, svc.Outcome);
        Assert.NotNull(svc.FailedStep);
        svc.PlantPreset(SeedCatalog.Find("winddown", "breaths"));
        svc.SetMute(true);

        Assert.Equal(restored, File.ReadAllBytes(P("save.json")));
        Assert.Equal(LoadOutcome.Failed, svc.Outcome);
    }
}
