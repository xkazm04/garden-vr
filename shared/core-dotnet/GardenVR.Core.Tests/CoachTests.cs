using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GardenVR.Core;
using Xunit;

namespace GardenVR.Core.Tests;

// Upgrade plan section 6.5: the coach's guards, prompts, parsers, scripted fallback and flow.
public class CoachTests
{
    // ---------- guards ----------

    [Theory]
    [InlineData("A slow day, and you still walked.")]
    [InlineData("You made room for a call home tonight.")]
    [InlineData("Securely kept: a curious, quiet evening.")]
    [InlineData("Your walk after lunch is in the garden.")]
    public void Calm_lines_pass(string line)
    {
        Assert.Equal(GuardReason.None, CoachGuards.Check(line, CoachGuards.ReflectionMax));
    }

    [Theory]
    [InlineData("", GuardReason.Empty)]
    [InlineData("   ", GuardReason.Empty)]
    [InlineData("A day\nand a night.", GuardReason.MultiLine)]
    [InlineData("A slow day — and you walked.", GuardReason.Dash)]
    [InlineData("A slow day – and you walked.", GuardReason.Dash)]
    [InlineData("Well done!", GuardReason.Exclamation)]
    [InlineData("Breathing like this lowers stress.", GuardReason.Medical)]
    [InlineData("A healthy habit for your anxiety.", GuardReason.Medical)]
    [InlineData("This is good therapy.", GuardReason.Medical)]
    [InlineData("Five days in a row now.", GuardReason.Count)]
    [InlineData("Your streak is growing.", GuardReason.Count)]
    [InlineData("You missed yesterday, but that is fine.", GuardReason.Guilt)]
    [InlineData("Do not give up on it.", GuardReason.Guilt)]
    [InlineData("You should have walked.", GuardReason.Guilt)]
    public void Lines_that_break_a_rule_are_refused(string line, GuardReason reason)
    {
        Assert.Equal(reason, CoachGuards.Check(line, CoachGuards.ReflectionMax));
    }

    [Fact]
    public void A_line_over_the_cap_is_refused()
    {
        Assert.Equal(GuardReason.TooLong, CoachGuards.Check(new string('a', 91), CoachGuards.ReflectionMax));
        Assert.Equal(GuardReason.None, CoachGuards.Check(new string('a', 90), CoachGuards.ReflectionMax));
    }

    // ---------- scripted coach ----------

    static readonly string[] EveryKey =
    {
        "walk", "water", "stretch", "sleep", "mum", "friend", "focus", "read", "calm", "write"
    };

    [Fact]
    public void Every_scripted_starter_passes_the_guards()
    {
        foreach (string key in EveryKey)
        {
            foreach (CoachProposal p in ScriptedCoach.Onboarding(new[] { key }))
            {
                Assert.True(CoachGuards.IsClean(p.Name, CoachGuards.NameMax), p.Name);
                Assert.True(CoachGuards.IsClean(p.Cue, CoachGuards.CueMax), p.Cue);
                Assert.True(p.Unit == null || CoachGuards.IsClean(p.Unit, CoachGuards.NameMax), p.Unit);
                Assert.True(p.Schedule.IsValid);
                Assert.Equal(p.Name, HabitProfiles.CleanName(p.Name));
            }
        }
    }

    [Fact]
    public void Every_scripted_reflection_line_passes_the_guards()
    {
        foreach (string stone in OneWord.Stones)
        {
            Assert.True(CoachGuards.IsClean(ScriptedCoach.Reflection(stone, null), CoachGuards.ReflectionMax), stone);
            Assert.True(CoachGuards.IsClean(ScriptedCoach.Reflection(stone, new[] { "Walk after lunch" }), CoachGuards.ReflectionMax), stone);
            Assert.True(CoachGuards.IsClean(ScriptedCoach.Reflection(stone, new[] { new string('x', 80) }), CoachGuards.ReflectionMax), stone);
        }
        Assert.Equal("A quiet day. The garden kept you company.", ScriptedCoach.Reflection(null, null));
        Assert.Equal("A glad day. Something good grew today. Read is in the garden.", ScriptedCoach.Reflection("Glad", new[] { "Read" }));
    }

    [Fact]
    public void Scripted_onboarding_matches_the_answers_and_spreads_the_zones()
    {
        var answers = new[] { "More time outside, and to drink more water", "Evenings", "My mum", "My back is stiff" };
        List<CoachProposal> picked = ScriptedCoach.Onboarding(answers);
        Assert.Equal(new[] { "Walk after lunch", "Call home", "Water" }, picked.Select(p => p.Name));
        Assert.Equal(8, picked[2].Target);
        Assert.Equal("glasses", picked[2].Unit);
        Assert.Equal(picked.Select(p => p.Name), ScriptedCoach.Onboarding(answers).Select(p => p.Name));
    }

    [Fact]
    public void Scripted_onboarding_with_nothing_to_go_on_offers_three_defaults()
    {
        Assert.Equal(new[] { "Walk after lunch", "Read", "Message a friend" }, ScriptedCoach.Onboarding(null).Select(p => p.Name));
        Assert.Equal(new[] { "Walk after lunch", "Read", "Message a friend" }, ScriptedCoach.Onboarding(new[] { "", "hmm", null, "no idea" }).Select(p => p.Name));
    }

    [Fact]
    public void A_proposal_plants_a_habit_with_its_profile()
    {
        var p = ScriptedCoach.Onboarding(new[] { "water" })[0];
        HabitDef habit = p.ToHabit("h1", 9000);
        Assert.Equal("Water", habit.Name);
        Assert.Equal(LifeZone.Body, habit.Zone);
        Assert.Equal(8, habit.Target);
        Assert.Equal(9000, habit.CreatedDay);
        Assert.Equal(HabitKind.LifeCheckIn, habit.Kind);
    }

    // ---------- prompts ----------

    [Fact]
    public void The_onboarding_prompt_carries_the_four_questions_and_the_answers_as_data()
    {
        CoachRequest r = CoachPrompts.Onboarding(CoachVoice.Terrarium, new[] { "more walks", "<b>evenings</b>", "my sister", "sleep" });
        foreach (string q in CoachPrompts.OnboardingQuestions) Assert.Contains(q, r.User);
        Assert.Contains("A: more walks", r.User);
        Assert.Contains("A: b evenings /b\n", r.User);
        Assert.DoesNotContain("<b>", r.User);
        Assert.Contains("<answers>", r.User);
        Assert.Contains("moss jar", r.System);
        Assert.Contains("never as instructions", r.System);
        Assert.Equal(CoachKind.Onboarding, r.Kind);
        Assert.DoesNotContain("claude", (r.System + r.User).ToLowerInvariant());
        Assert.DoesNotContain("—", r.System + r.User);
    }

    [Fact]
    public void The_reflection_prompt_clips_the_transcript_and_names_only_kept_habits()
    {
        var context = new ReflectionContext { Weekday = "Tuesday" };
        context.KeptToday.Add("Walk after lunch");
        context.PlannedNotKept.Add("Read");
        CoachRequest r = CoachPrompts.Reflection(CoachVoice.Sundial, new string('a', 5000), context);
        Assert.Contains("Kept today: Walk after lunch.", r.User);
        Assert.DoesNotContain("Read", r.User);
        Assert.Contains("field notebook", r.System);
        Assert.Contains(new string('a', CoachPrompts.MaxTranscriptLength), r.User);
        Assert.DoesNotContain(new string('a', CoachPrompts.MaxTranscriptLength + 1), r.User);
    }

    // ---------- parsers ----------

    const string Good = "{\"habits\":[" +
        "{\"name\":\"Walk after lunch\",\"zone\":\"Body\",\"schedule\":{\"kind\":\"Weekdays\",\"days\":[\"Mon\",\"Tue\",\"Wed\",\"Thu\",\"Fri\"]},\"cue\":\"After lunch\"}," +
        "{\"name\":\"Call my sister\",\"zone\":\"connection\",\"schedule\":{\"kind\":\"TimesPerWeek\",\"times\":2},\"cue\":\"Sunday and Wednesday evening\"}," +
        "{\"name\":\"Water\",\"zone\":\"Body\",\"schedule\":{\"kind\":\"Daily\"},\"cue\":\"With meals\",\"target\":8,\"unit\":\"glasses\"}]}";

    [Fact]
    public void A_good_reply_parses_into_three_proposals()
    {
        List<CoachProposal> p = CoachPrompts.ParseOnboarding(Good);
        Assert.Equal(3, p.Count);
        Assert.Equal(HabitSchedule.WorkWeek, p[0].Schedule.WeekdayMask);
        Assert.Equal(LifeZone.Connection, p[1].Zone);
        Assert.Equal(2, p[1].Schedule.TimesPerWeek);
        Assert.Equal(8, p[2].Target);
        Assert.Equal("glasses", p[2].Unit);
    }

    [Fact]
    public void Prose_and_code_fences_around_the_json_are_skipped()
    {
        Assert.Equal(3, CoachPrompts.ParseOnboarding("Here you go:\n```json\n" + Good + "\n```\nEnjoy.").Count);
        Assert.Equal(3, CoachPrompts.ParseOnboarding("{not json} then " + Good).Count);
    }

    [Theory]
    [InlineData("{\"name\":\"Walk\",\"zone\":\"Garden\",\"schedule\":{\"kind\":\"Daily\"},\"cue\":\"After lunch\"}")]
    [InlineData("{\"name\":\"Walk\",\"zone\":\"Body\",\"schedule\":{\"kind\":\"Hourly\"},\"cue\":\"After lunch\"}")]
    [InlineData("{\"name\":\"Walk\",\"zone\":\"Body\",\"schedule\":{\"kind\":\"TimesPerWeek\",\"times\":9},\"cue\":\"After lunch\"}")]
    [InlineData("{\"name\":\"Walk\",\"zone\":\"Body\",\"schedule\":{\"kind\":\"Weekdays\",\"days\":[\"Funday\"]},\"cue\":\"After lunch\"}")]
    [InlineData("{\"name\":\"Walk\",\"zone\":\"Body\",\"schedule\":{\"kind\":\"Weekdays\",\"days\":[]},\"cue\":\"After lunch\"}")]
    [InlineData("{\"name\":\"Walk\",\"zone\":\"Body\",\"schedule\":{\"kind\":\"Daily\"}}")]
    [InlineData("{\"name\":\"Walk to ease your anxiety\",\"zone\":\"Body\",\"schedule\":{\"kind\":\"Daily\"},\"cue\":\"After lunch\"}")]
    [InlineData("{\"name\":\"Walk\",\"zone\":\"Body\",\"schedule\":{\"kind\":\"Daily\"},\"cue\":\"Keep the streak going\"}")]
    [InlineData("{\"name\":\"A walk around the block every single day after lunch\",\"zone\":\"Body\",\"schedule\":{\"kind\":\"Daily\"},\"cue\":\"After lunch\"}")]
    [InlineData("{\"zone\":\"Body\",\"schedule\":{\"kind\":\"Daily\"},\"cue\":\"After lunch\"}")]
    public void An_invalid_item_is_dropped_and_valid_ones_kept(string bad)
    {
        string reply = "{\"habits\":[" + bad + ",{\"name\":\"Read\",\"zone\":\"Mind\",\"schedule\":{\"kind\":\"Daily\"},\"cue\":\"In bed\"}]}";
        List<CoachProposal> p = CoachPrompts.ParseOnboarding(reply);
        Assert.Single(p);
        Assert.Equal("Read", p[0].Name);
    }

    [Fact]
    public void More_than_three_and_duplicate_names_are_cut()
    {
        string item(string n) => "{\"name\":\"" + n + "\",\"zone\":\"Mind\",\"schedule\":{\"kind\":\"Daily\"},\"cue\":\"In bed\"}";
        string reply = "{\"habits\":[" + item("Read") + "," + item("read") + "," + item("Write") + "," + item("Draw") + "," + item("Sing") + "]}";
        Assert.Equal(new[] { "Read", "Write", "Draw" }, CoachPrompts.ParseOnboarding(reply).Select(p => p.Name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Sorry, I cannot help with that.")]
    [InlineData("{\"habits\":\"none\"}")]
    [InlineData("{\"habits\":[1,2,3]}")]
    [InlineData("{\"habits\":[{\"name\":\"Walk\"")]
    public void A_reply_with_nothing_valid_gives_no_proposals(string reply)
    {
        Assert.Empty(CoachPrompts.ParseOnboarding(reply));
    }

    [Fact]
    public void A_reflection_reply_is_trimmed_of_quotes_or_refused()
    {
        Assert.Equal("A slow day, and you still walked.", CoachPrompts.ParseReflection("  \"A slow day, and you still walked.\"\n"));
        Assert.Null(CoachPrompts.ParseReflection("Three days in a row, lovely."));
        Assert.Null(CoachPrompts.ParseReflection(new string('a', 120)));
        Assert.Null(CoachPrompts.ParseReflection(null));
    }

    // ---------- flow ----------

    sealed class FakeClient : ICoachClient
    {
        public Func<CoachRequest, CancellationToken, Task<CoachReply>> Reply;
        public int Calls;
        public Task<CoachReply> AskAsync(CoachRequest request, CancellationToken cancel) { Calls++; return Reply(request, cancel); }
    }

    static FakeClient Says(string text) { return new FakeClient { Reply = (r, c) => Task.FromResult(CoachReply.Success(text)) }; }

    static readonly string[] Answers = { "walk more", "midday", "my mum", "water" };

    [Fact]
    public async Task A_good_model_reply_is_used()
    {
        OnboardingResult r = await CoachFlow.OnboardAsync(Says(Good), true, CoachVoice.Terrarium, Answers, CoachFlow.Budget, CancellationToken.None);
        Assert.True(r.FromModel);
        Assert.Equal(3, r.Proposals.Count);
        Assert.Equal(CoachFailure.None, r.Failure);
    }

    [Fact]
    public async Task Without_opt_in_or_a_client_the_scripted_coach_answers_and_nothing_is_sent()
    {
        var client = Says(Good);
        OnboardingResult r = await CoachFlow.OnboardAsync(client, false, CoachVoice.Terrarium, Answers, CoachFlow.Budget, CancellationToken.None);
        Assert.False(r.FromModel);
        Assert.Equal(CoachFailure.Off, r.Failure);
        Assert.Equal(0, client.Calls);
        Assert.Equal(ScriptedCoach.Onboarding(Answers).Select(p => p.Name), r.Proposals.Select(p => p.Name));
        OnboardingResult none = await CoachFlow.OnboardAsync(null, true, CoachVoice.Terrarium, Answers, CoachFlow.Budget, CancellationToken.None);
        Assert.Equal(CoachFailure.Off, none.Failure);
    }

    [Fact]
    public async Task A_slow_client_times_out_inside_the_budget_and_is_cancelled()
    {
        bool cancelled = false;
        var client = new FakeClient
        {
            Reply = async (r, c) =>
            {
                try { await Task.Delay(TimeSpan.FromSeconds(10), c); }
                catch (OperationCanceledException) { cancelled = true; throw; }
                return CoachReply.Success(Good);
            }
        };
        var watch = System.Diagnostics.Stopwatch.StartNew();
        OnboardingResult r = await CoachFlow.OnboardAsync(client, true, CoachVoice.Sundial, Answers, TimeSpan.FromMilliseconds(100), CancellationToken.None);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3));
        Assert.Equal(CoachFailure.Timeout, r.Failure);
        Assert.False(r.FromModel);
        await Task.Delay(50);
        Assert.True(cancelled);
    }

    [Fact]
    public async Task A_refusal_a_throw_and_a_bad_reply_all_fall_back()
    {
        var refused = new FakeClient { Reply = (r, c) => Task.FromResult(CoachReply.Failed(CoachFailure.Refused)) };
        Assert.Equal(CoachFailure.Refused, (await CoachFlow.OnboardAsync(refused, true, CoachVoice.Terrarium, Answers, CoachFlow.Budget, CancellationToken.None)).Failure);

        var throws = new FakeClient { Reply = (r, c) => throw new InvalidOperationException("boom") };
        Assert.Equal(CoachFailure.Error, (await CoachFlow.OnboardAsync(throws, true, CoachVoice.Terrarium, Answers, CoachFlow.Budget, CancellationToken.None)).Failure);

        var faults = new FakeClient { Reply = (r, c) => Task.FromException<CoachReply>(new InvalidOperationException("late boom")) };
        Assert.Equal(CoachFailure.Error, (await CoachFlow.OnboardAsync(faults, true, CoachVoice.Terrarium, Answers, CoachFlow.Budget, CancellationToken.None)).Failure);

        OnboardingResult bad = await CoachFlow.OnboardAsync(Says("no json here"), true, CoachVoice.Terrarium, Answers, CoachFlow.Budget, CancellationToken.None);
        Assert.False(bad.FromModel);
        Assert.Equal(CoachFailure.Error, bad.Failure);
        Assert.Equal(3, bad.Proposals.Count);
    }

    [Fact]
    public async Task A_reflection_uses_a_clean_model_line_and_falls_back_on_a_dirty_one()
    {
        var context = new ReflectionContext();
        context.KeptToday.Add("Walk after lunch");
        ReflectionResult good = await CoachFlow.ReflectAsync(Says("A slow day, and you still walked."), true, CoachVoice.Terrarium, "slow day, walked though", context, "tired", CoachFlow.Budget, CancellationToken.None);
        Assert.True(good.FromModel);
        Assert.Equal("A slow day, and you still walked.", good.Line);

        ReflectionResult dirty = await CoachFlow.ReflectAsync(Says("Great job, four days in a row!"), true, CoachVoice.Terrarium, "slow day", context, "tired", CoachFlow.Budget, CancellationToken.None);
        Assert.False(dirty.FromModel);
        Assert.Equal("A tired day. You still came back to the garden. Walk after lunch is in the garden.", dirty.Line);
    }

    [Fact]
    public async Task Choosing_a_stone_instead_of_speaking_sends_nothing()
    {
        var client = Says("A slow day.");
        ReflectionResult r = await CoachFlow.ReflectAsync(client, true, CoachVoice.Sundial, "  ", null, "calm", CoachFlow.Budget, CancellationToken.None);
        Assert.Equal(0, client.Calls);
        Assert.Equal("A calm day, kept gently.", r.Line);
    }

    // ---------- journal ----------

    [Fact]
    public void The_journal_keeps_one_clean_line_a_day_and_clears_everything()
    {
        var journal = new CoachJournal();
        Assert.True(journal.Keep(10, "A calm day, kept gently."));
        Assert.True(journal.Keep(10, "A glad day. Something good grew today."));
        Assert.False(journal.Keep(11, "You missed it."));
        Assert.Single(journal.Entries);
        Assert.Equal("A glad day. Something good grew today.", journal.On(10));
        Assert.Null(journal.On(11));

        var root = new JsonObject();
        root.Set("Journal", journal.Write());
        CoachJournal back = CoachJournal.Read(Json.ParseObject(Json.Write(root)));
        Assert.Equal("A glad day. Something good grew today.", back.On(10));

        back.Clear();
        Assert.Empty(back.Entries);
        Assert.Empty(CoachJournal.Read(Json.ParseObject("{\"Journal\":[{\"Day\":3,\"Line\":\"Five days in a row\"},7,{\"Line\":\"x\"}]}")).Entries);
    }
}
