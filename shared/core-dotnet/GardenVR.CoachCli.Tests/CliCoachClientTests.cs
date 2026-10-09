using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GardenVR.CoachCli;
using GardenVR.Core;
using Xunit;

namespace GardenVR.CoachCli.Tests;

// Upgrade plan C6: the development path to the model through the Claude Code CLI, with the process replaced.
public class CliCoachClientTests
{
    static CliCoachClient Returning(string stdout, int exit = 0)
    {
        return new CliCoachClient { Run = (psi, stdin, c) => Task.FromResult(new ProcessResult { ExitCode = exit, StdOut = stdout, StdErr = "" }) };
    }

    [Fact]
    public void The_request_runs_print_mode_with_its_own_system_prompt_and_no_tools()
    {
        var client = new CliCoachClient();
        CoachRequest request = CoachPrompts.Reflection(CoachVoice.Terrarium, "a quiet day --model opus", null);
        ProcessStartInfo psi = client.StartInfo(request);
        Assert.Equal("claude", psi.FileName);
        var args = psi.ArgumentList.ToList();
        Assert.Equal("-p", args[0]);
        Assert.Equal("haiku", args[args.IndexOf("--model") + 1]);
        Assert.Equal("json", args[args.IndexOf("--output-format") + 1]);
        Assert.Equal(request.System, args[args.IndexOf("--system-prompt") + 1]);
        Assert.Equal("", args[args.IndexOf("--tools") + 1]);
        Assert.Contains("--no-session-persistence", args);
        Assert.Equal("", args[args.IndexOf("--setting-sources") + 1]);
        Assert.DoesNotContain(args, a => a.Contains("quiet day"));
        Assert.True(psi.RedirectStandardInput);
    }

    [Fact]
    public async Task The_user_text_goes_in_on_stdin()
    {
        string seen = null;
        var client = new CliCoachClient
        {
            Run = (psi, stdin, c) => { seen = stdin; return Task.FromResult(new ProcessResult { StdOut = "{\"result\":\"ok\",\"is_error\":false}" }); }
        };
        CoachRequest request = CoachPrompts.Reflection(CoachVoice.Terrarium, "a quiet day", null);
        await client.AskAsync(request, CancellationToken.None);
        Assert.Equal(request.User, seen);
    }

    [Fact]
    public async Task A_result_is_read_with_its_model()
    {
        var client = Returning("{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false,\"stop_reason\":\"end_turn\",\"result\":\"A calm day.\",\"modelUsage\":{\"claude-haiku-5-5\":{}}}");
        CoachReply reply = await client.AskAsync(new CoachRequest { System = "s", User = "u" }, CancellationToken.None);
        Assert.True(reply.Ok);
        Assert.Equal("A calm day.", reply.Text);
        Assert.Equal("claude-haiku-5-5", client.LastModel);
    }

    [Theory]
    [InlineData("{\"stop_reason\":\"refusal\",\"result\":\"\",\"is_error\":false}", CoachFailure.Refused)]
    [InlineData("{\"is_error\":true,\"result\":\"rate limited\"}", CoachFailure.Error)]
    [InlineData("{\"is_error\":false}", CoachFailure.Error)]
    [InlineData("not json", CoachFailure.Error)]
    [InlineData("", CoachFailure.Error)]
    public async Task Refusals_errors_and_garbage_are_failures(string stdout, CoachFailure failure)
    {
        CoachReply reply = await Returning(stdout).AskAsync(new CoachRequest { System = "s", User = "u" }, CancellationToken.None);
        Assert.False(reply.Ok);
        Assert.Equal(failure, reply.Failure);
    }

    [Fact]
    public async Task A_cancelled_or_missing_process_is_a_failure_not_a_throw()
    {
        var cancelled = new CliCoachClient { Run = (psi, stdin, c) => Task.FromCanceled<ProcessResult>(new CancellationToken(true)) };
        Assert.Equal(CoachFailure.Timeout, (await cancelled.AskAsync(new CoachRequest(), CancellationToken.None)).Failure);
        var missing = new CliCoachClient { ClaudePath = "/nonexistent/claude-cli" };
        Assert.Equal(CoachFailure.Error, (await missing.AskAsync(new CoachRequest { System = "s", User = "u" }, CancellationToken.None)).Failure);
    }

    [Fact]
    public async Task The_evaluation_scores_cases_from_raw_replies_without_fallback()
    {
        string onboardingReply = "{\"result\":\"{\\\"habits\\\":[{\\\"name\\\":\\\"Read\\\",\\\"zone\\\":\\\"Mind\\\",\\\"schedule\\\":{\\\"kind\\\":\\\"Daily\\\"},\\\"cue\\\":\\\"In bed\\\"},{\\\"name\\\":\\\"Walk\\\",\\\"zone\\\":\\\"Body\\\",\\\"schedule\\\":{\\\"kind\\\":\\\"Daily\\\"},\\\"cue\\\":\\\"After lunch\\\"}]}\"}";
        var cases = EvalCases.Load("{\"onboarding\":[{\"id\":\"o1\",\"answers\":[\"a\",\"b\",\"c\",\"d\"]}],\"reflection\":[{\"id\":\"r1\",\"voice\":\"sundial\",\"kept\":[\"Walk\"],\"transcript\":\"fine\"}]}");
        var client = new CliCoachClient
        {
            Run = (psi, stdin, c) => Task.FromResult(new ProcessResult
            {
                StdOut = stdin.Contains("<answers>") ? onboardingReply : "{\"result\":\"Four days in a row!\"}"
            })
        };
        var results = await Evaluation.Run(client, cases, 2);
        Assert.True(results[0].Pass);
        Assert.Equal(2, results[0].Proposals.Count);
        Assert.False(results[1].Pass);
        Assert.Equal(GuardReason.Exclamation, results[1].Guard);
        Assert.Equal(CoachVoice.Sundial, results[1].Case.Voice);
        Assert.Contains("onboarding valid: 1 of 1", Evaluation.Summary(results));
        Assert.Contains("\"id\": \"r1\"", Evaluation.ToJson(results, "claude-haiku-5-5"));
    }
}
