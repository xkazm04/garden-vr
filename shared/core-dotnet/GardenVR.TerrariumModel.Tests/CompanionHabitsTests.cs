using System;
using GardenVR.Core;
using GardenVR.Terrarium;
using Xunit;

// Line numbers cite apps/terrarium/Assets/Scripts/Ritual/CompanionHabits.cs.
public class CompanionHabitsTests
{
    readonly FixedClock _clock = new FixedClock(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
    readonly TerrariumSave _save = new TerrariumSave();
    readonly CompanionHabits _habits;

    public CompanionHabitsTests() { _habits = new CompanionHabits(_save); }

    GardenDay Today { get { return GardenDay.From(_clock.Now, GardenDay.DefaultBoundary); } }

    [Fact]
    public void Arm_holds_the_tend_for_six_seconds_then_writes_it()
    {
        // :107-128 TryArm, :140-147 Tick, :158-168 Publish.
        Assert.True(_habits.TryPlant("walk", Today.Index));
        Assert.True(_habits.TryArm("walk", Today, Today, false, TendSource.Pinch, _clock, _clock));
        Assert.True(_habits.IsPending);
        Assert.Equal("walk", _habits.PendingId);
        Assert.False(_habits.TryArm("walk", Today, Today, false, TendSource.Pinch, _clock, _clock));
        Assert.Empty(_save.Tends);

        _clock.Advance(TimeSpan.FromSeconds(5));
        Assert.False(_habits.Tick(_clock));
        Assert.Empty(_save.Tends);

        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(_habits.Tick(_clock));
        Assert.False(_habits.IsPending);
        Assert.Single(_save.Tends);
        Assert.Equal("walk", _save.Tends[0].HabitId);
        Assert.Equal(1, _habits.ShownLeaves("walk"));
    }

    [Fact]
    public void Undo_before_the_write_always_works_and_leaves_no_row()
    {
        // :130-138 TryUndo.
        _habits.TryPlant("walk", Today.Index);
        Assert.False(_habits.TryUndo());
        Assert.True(_habits.TryArm("walk", Today, Today, false, TendSource.Pinch, _clock, _clock));
        Assert.True(_habits.TryUndo());
        Assert.False(_habits.IsPending);
        Assert.Null(_habits.PendingId);
        Assert.Empty(_save.Tends);
        Assert.Empty(_habits.Ledger.Events);

        _clock.Advance(TimeSpan.FromSeconds(30));
        Assert.False(_habits.Tick(_clock));
        Assert.False(_habits.Flush());
        Assert.Empty(_save.Tends);
        // The same day can be armed again after an undo.
        Assert.True(_habits.TryArm("walk", Today, Today, false, TendSource.Pinch, _clock, _clock));
    }

    [Fact]
    public void Shown_leaves_count_the_held_tend_before_it_lands()
    {
        // :82-87 ShownLeaves adds one while the armed habit is pending, :89-93 ShownVitality reads full.
        _habits.TryPlant("walk", Today.Index);
        Assert.Equal(0, _habits.ShownLeaves("walk"));
        _habits.TryArm("walk", Today, Today, false, TendSource.Pinch, _clock, _clock);
        Assert.Equal(1, _habits.ShownLeaves("walk"));
        Assert.Equal(0, _habits.ShownLeaves("water"));
        Assert.Equal(1f, _habits.ShownVitality("walk", Today));
        _habits.TryUndo();
        Assert.Equal(0, _habits.ShownLeaves("walk"));

        _habits.TryArm("walk", Today, Today, false, TendSource.Pinch, _clock, _clock);
        Assert.True(_habits.Flush());
        Assert.Equal(1, _habits.ShownLeaves("walk"));
    }

    [Fact]
    public void Yesterday_is_offered_once_and_lands_late_as_a_backfill()
    {
        // :95-105 YesterdayVisible, :158-161 the late mark.
        GardenDay yesterday = new GardenDay(Today.Index - 1);
        _habits.TryPlant("read", yesterday.Index);
        Assert.True(_habits.YesterdayVisible("read", Today, _clock));
        Assert.False(_habits.TryArm("read", Today, Today, true, TendSource.Pinch, _clock, _clock));
        Assert.True(_habits.TryArm("read", yesterday, Today, true, TendSource.Pinch, _clock, _clock));
        Assert.False(_habits.YesterdayVisible("read", Today, _clock));
        Assert.True(_habits.Flush());
        Assert.True(_save.Tends[0].Late);
        Assert.Equal(TendSource.Backfill, _save.Tends[0].Source);
        Assert.False(_habits.YesterdayVisible("read", Today, _clock));
    }

    [Fact]
    public void Planting_refuses_a_repeat_an_unknown_preset_and_a_full_garden()
    {
        // :57-80 TryPlant.
        Assert.False(_habits.TryPlant("not-a-preset", Today.Index));
        Assert.True(_habits.TryPlant("walk", Today.Index));
        Assert.False(_habits.TryPlant("walk", Today.Index));
        string[] more = { "water", "read", "stretch", "journal", "early-night" };
        int planted = 1;
        foreach (string key in more)
        {
            bool ok = _habits.TryPlant(key, Today.Index);
            Assert.Equal(planted < Companions.MaxHabits, ok);
            if (ok) planted++;
        }
        Assert.Equal(Companions.MaxHabits, _habits.Active().Count);
    }

    [Fact]
    public void Offer_is_true_once()
    {
        // :39-44.
        Assert.True(_habits.Offer());
        Assert.False(_habits.Offer());
        Assert.True(_habits.PacketsOffered);
    }
}
