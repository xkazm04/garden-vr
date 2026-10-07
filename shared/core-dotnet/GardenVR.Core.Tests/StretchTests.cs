using System;
using GardenVR.Core;
using Xunit;

namespace GardenVR.Core.Tests;

public class StretchTests
{
    [Fact]
    public void Three_reaches_in_order_authorise_one_morning_ritual_tend()
    {
        var session = new StretchSession();
        Assert.Equal(ArcId.Morning, session.TendsArc);
        Assert.Equal(TendSource.Ritual, session.TendsAs);
        Assert.False(session.ConsumeTend());

        session.Update(0.4f, new StretchSample(0, true));
        session.Update(0.4f, new StretchSample(1, true));
        Assert.Equal(StretchPhase.Reaching, session.Phase);
        Assert.False(session.TendAuthorised);
        Assert.False(session.ConsumeTend());

        session.Update(0.4f, new StretchSample(2, true));

        Assert.Equal(StretchPhase.Complete, session.Phase);
        Assert.Equal(3, session.Reaches);
        Assert.Equal(1f, session.Stretch);
        Assert.True(session.TendAuthorised);
        Assert.True(session.ConsumeTend());
        Assert.False(session.TendAuthorised);
        Assert.False(session.ConsumeTend());
        Assert.Equal(3, Count(session, StretchEventKind.ReachCounted));
        Assert.Equal(1, Count(session, StretchEventKind.RitualComplete));
    }

    [Fact]
    public void A_skip_or_a_repeat_is_ignored_and_stretch_does_not_fall()
    {
        var session = new StretchSession();
        session.Update(0.1f, new StretchSample(2, true));
        session.Update(0.1f, new StretchSample(1, true));
        Assert.Equal(0, session.Reaches);
        Assert.Equal(0f, session.Stretch);
        Assert.Equal(StretchPhase.Waiting, session.Phase);
        Assert.Equal(2, Count(session, StretchEventKind.AheadIgnored));
        Assert.False(session.ConsumeTend());

        session.Update(0.1f, new StretchSample(0, true));
        float earned = session.Stretch;
        session.Update(0.1f, new StretchSample(0, true));
        session.Update(0.1f, new StretchSample(-1, true));
        session.Update(0.1f, new StretchSample(0, false));

        Assert.Equal(1, session.Reaches);
        Assert.Equal(earned, session.Stretch);
        Assert.Equal(1, Count(session, StretchEventKind.RepeatIgnored));
        Assert.False(session.TendAuthorised);
    }

    [Fact]
    public void Out_of_order_then_the_sequence_still_completes()
    {
        var session = new StretchSession();
        int[] marks = { 2, 2, 0, 1, 0, 2 };
        bool[] palms = { true, true, true, true, true, true };
        for (int i = 0; i < marks.Length; i++)
            session.Update(0.2f, new StretchSample(marks[i], palms[i]));

        Assert.Equal(3, session.Reaches);
        Assert.Equal(StretchPhase.Complete, session.Phase);
        Assert.True(session.ConsumeTend());
        Assert.Equal(ArcId.Morning, session.TendsArc);
    }

    [Fact]
    public void Stretch_never_decreases_across_a_scrambled_sequence()
    {
        var session = new StretchSession();
        var rng = new Random(22);
        float previous = 0f;
        for (int i = 0; i < 40; i++)
        {
            int mark = rng.Next(-1, 4);
            bool palm = rng.Next(0, 2) == 0;
            session.Update(0.05f, new StretchSample(mark, palm));
            Assert.True(session.Stretch >= previous);
            Assert.True(session.Stretch <= 1f);
            previous = session.Stretch;
        }
    }

    [Fact]
    public void A_palm_after_completion_does_not_authorise_a_second_tend()
    {
        var session = Drive(new StretchSession());
        Assert.True(session.ConsumeTend());
        float stretch = session.Stretch;
        session.Update(1f, new StretchSample(0, true));
        session.Update(1f, new StretchSample(2, true));
        Assert.Equal(stretch, session.Stretch);
        Assert.Equal(3, session.Reaches);
        Assert.False(session.ConsumeTend());
    }

    static StretchSession Drive(StretchSession session)
    {
        session.Update(0.2f, new StretchSample(0, true));
        session.Update(0.2f, new StretchSample(1, true));
        session.Update(0.2f, new StretchSample(2, true));
        return session;
    }

    static int Count(StretchSession session, StretchEventKind kind)
    {
        int count = 0;
        for (int i = 0; i < session.Events.Count; i++)
        {
            if (session.Events[i].Kind == kind) count++;
        }
        return count;
    }
}
