using System.Linq;
using Xunit;

namespace GardenVR.Core.Tests;

public class OnboardingTests
{
    [Fact] public void Breath_ritual_never_keeps_a_custom_evening_habit()
    {
        var custom = new CoachProposal { Name = "Read", Zone = LifeZone.Mind, Schedule = HabitSchedule.Daily() }.ToHabit("read", 1);
        Assert.False(SundialRules.IsBreathHabit(custom));
        var breath = new HabitDef { PresetKey = "breaths", Kind = HabitKind.InAppRitual };
        Assert.True(SundialRules.IsBreathHabit(breath));
        breath.ArchivedDay = 2;
        Assert.False(SundialRules.IsBreathHabit(breath));
    }
    [Theory]
    [InlineData(TileState.Today, true)]
    [InlineData(TileState.Flexible, true)]
    [InlineData(TileState.NotPlanned, true)]
    [InlineData(TileState.Kept, false)]
    [InlineData(TileState.Late, false)]
    public void Deliberate_tend_accepts_unkept_current_tiles(TileState tile, bool allowed) => Assert.Equal(allowed, SundialRules.CanTendToday(tile));
    [Theory]
    [InlineData("After lunch", "midday")]
    [InlineData("After my first coffee", "morning")]
    [InlineData("In bed, before the light goes out", "winddown")]
    [InlineData("With each meal", null)]
    [InlineData("Morning or evening", null)]
    public void Cue_only_selects_an_unambiguous_arc(string cue, string arc) => Assert.Equal(arc, Onboarding.CueArc(cue));

    [Fact] public void Four_questions_finish_in_waiting_and_unknown_steps_do_not_advance()
    {
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(i, Onboarding.QuestionIndex(Onboarding.QuestionStep(i)));
            Assert.Equal(i == 3 ? Onboarding.Waiting : Onboarding.QuestionStep(i + 1), Onboarding.AfterQuestion(Onboarding.QuestionStep(i)));
        }
        Assert.Null(Onboarding.AfterQuestion("unknown"));
        Assert.Null(Onboarding.QuestionStep(4));
    }

    [Fact] public void Draft_roundtrips_proposals_decisions_selection_without_answers()
    {
        var draft = new OnboardingDraft { Proposals = ScriptedCoach.Onboarding(new[] { "water and call mum and walk" }) };
        draft.Decisions[0] = 1;
        draft.Decisions[1] = 2;
        draft.Selected = 2;
        string json = Json.Write(draft.Write());
        var back = OnboardingDraft.Read(Json.ParseObject(json));
        Assert.Equal(draft.Proposals.Select(p => p.Name), back.Proposals.Select(p => p.Name));
        Assert.Equal(draft.Proposals.Select(p => p.Schedule.WeekdayMask), back.Proposals.Select(p => p.Schedule.WeekdayMask));
        Assert.Equal(draft.Proposals.Select(p => p.Target), back.Proposals.Select(p => p.Target));
        Assert.False(back.Pending(0)); Assert.False(back.Pending(1)); Assert.True(back.Pending(2));
        Assert.Equal(2, back.Selected); Assert.False(back.Complete);
        back.Decisions[2] = 2; Assert.True(back.Complete);
        Assert.DoesNotContain("answers", json.ToLowerInvariant());
        Assert.DoesNotContain("water and call mum and walk", json);
    }

    [Fact] public void First_tend_uses_a_planted_arc_even_when_current_arc_is_empty()
    {
        var habits = new[] { new HabitDef { Group = "morning" }, new HabitDef { Group = "winddown" } };
        Assert.Equal("morning", Onboarding.FirstTendArc(habits, "midday"));
        Assert.Equal("winddown", Onboarding.FirstTendArc(habits, "winddown"));
        habits[0].ArchivedDay = 1;
        Assert.Equal("winddown", Onboarding.FirstTendArc(habits, "midday"));
    }
}
