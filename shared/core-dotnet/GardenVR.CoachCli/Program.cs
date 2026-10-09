using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GardenVR.Core;

namespace GardenVR.CoachCli
{
    /// <summary>
    /// dotnet run --project shared/core-dotnet/GardenVR.CoachCli -- onboard [--voice sundial] "a1" "a2" "a3" "a4"
    /// dotnet run --project shared/core-dotnet/GardenVR.CoachCli -- reflect [--voice sundial] [--kept "Walk"] "transcript"
    /// dotnet run --project shared/core-dotnet/GardenVR.CoachCli -- eval cases.json results.json [--parallel 4]
    /// GARDENVR_CLAUDE names the CLI when "claude" is not on PATH (for example claude.cmd on Windows).
    /// </summary>
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            if (args.Length == 0) return Usage();
            var client = new CliCoachClient();
            string path = Environment.GetEnvironmentVariable("GARDENVR_CLAUDE");
            if (!string.IsNullOrEmpty(path)) client.ClaudePath = path;
            var rest = args.Skip(1).ToList();
            CoachVoice voice = Take(rest, "--voice") == "sundial" ? CoachVoice.Sundial : CoachVoice.Terrarium;
            switch (args[0])
            {
                case "onboard":
                {
                    OnboardingResult r = await CoachFlow.OnboardAsync(client, true, voice, rest, TimeSpan.FromSeconds(60), CancellationToken.None);
                    Console.WriteLine((r.FromModel ? "model" : "scripted (" + r.Failure + ")") + ":");
                    foreach (CoachProposal p in r.Proposals) Console.WriteLine("  " + Describe(p));
                    return 0;
                }
                case "reflect":
                {
                    var context = new ReflectionContext();
                    string kept;
                    while ((kept = Take(rest, "--kept")) != null) context.KeptToday.Add(kept);
                    ReflectionResult r = await CoachFlow.ReflectAsync(client, true, voice, string.Join(" ", rest), context, "quiet", TimeSpan.FromSeconds(60), CancellationToken.None);
                    Console.WriteLine((r.FromModel ? "model: " : "scripted (" + r.Failure + "): ") + r.Line);
                    return 0;
                }
                case "eval":
                {
                    if (rest.Count < 2) return Usage();
                    int parallel = int.TryParse(Take(rest, "--parallel"), out int n) && n > 0 ? n : 4;
                    var cases = EvalCases.Load(File.ReadAllText(rest[0]));
                    var results = await Evaluation.Run(client, cases, parallel);
                    File.WriteAllText(rest[1], Evaluation.ToJson(results, client.LastModel));
                    Console.WriteLine(Evaluation.Summary(results));
                    return 0;
                }
                default:
                    return Usage();
            }
        }

        static string Describe(CoachProposal p)
        {
            string schedule = p.Schedule.Kind == ScheduleKind.TimesPerWeek ? p.Schedule.TimesPerWeek + " a week"
                : p.Schedule.Kind == ScheduleKind.Weekdays ? "days " + p.Schedule.WeekdayMask : "daily";
            return p.Name + " | " + p.Zone + " | " + schedule + " | " + p.Cue + (p.Target.HasValue ? " | " + p.Target + " " + p.Unit : "");
        }

        static string Take(List<string> args, string flag)
        {
            int i = args.IndexOf(flag);
            if (i < 0 || i + 1 >= args.Count) return null;
            string value = args[i + 1];
            args.RemoveRange(i, 2);
            return value;
        }

        static int Usage()
        {
            Console.Error.WriteLine("usage: onboard|reflect|eval (see Program.cs)");
            return 2;
        }
    }

    public sealed class EvalCase
    {
        public string Id;
        public CoachKind Kind;
        public CoachVoice Voice;
        public List<string> Answers = new List<string>();
        public string Transcript;
        public string Weekday;
        public List<string> Kept = new List<string>();
    }

    public static class EvalCases
    {
        /// <summary>{"onboarding":[{"id","voice","answers":[4]}],"reflection":[{"id","voice","weekday","kept":[...],"transcript"}]}</summary>
        public static List<EvalCase> Load(string json)
        {
            var cases = new List<EvalCase>();
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                JsonElement root = doc.RootElement;
                if (root.TryGetProperty("onboarding", out JsonElement onboarding))
                    foreach (JsonElement e in onboarding.EnumerateArray())
                        cases.Add(new EvalCase
                        {
                            Id = e.GetProperty("id").GetString(), Kind = CoachKind.Onboarding, Voice = VoiceOf(e),
                            Answers = e.GetProperty("answers").EnumerateArray().Select(a => a.GetString()).ToList()
                        });
                if (root.TryGetProperty("reflection", out JsonElement reflection))
                    foreach (JsonElement e in reflection.EnumerateArray())
                        cases.Add(new EvalCase
                        {
                            Id = e.GetProperty("id").GetString(), Kind = CoachKind.Reflection, Voice = VoiceOf(e),
                            Transcript = e.GetProperty("transcript").GetString(),
                            Weekday = e.TryGetProperty("weekday", out JsonElement w) ? w.GetString() : null,
                            Kept = e.TryGetProperty("kept", out JsonElement k) ? k.EnumerateArray().Select(a => a.GetString()).ToList() : new List<string>()
                        });
            }
            return cases;
        }

        static CoachVoice VoiceOf(JsonElement e)
        {
            return e.TryGetProperty("voice", out JsonElement v) && v.GetString() == "sundial" ? CoachVoice.Sundial : CoachVoice.Terrarium;
        }
    }

    public sealed class EvalResult
    {
        public EvalCase Case;
        public CoachReply Reply;
        public long Millis;
        public List<CoachProposal> Proposals;
        public string Line;
        public GuardReason Guard;

        /// <summary>Onboarding: at least two valid proposals on the first try. Reflection: the line passes every guard.</summary>
        public bool Pass
        {
            get { return Case.Kind == CoachKind.Onboarding ? Proposals != null && Proposals.Count >= 2 : Line != null; }
        }
    }

    /// <summary>Runs every case through the client once, with no fallback, and records what the model did.</summary>
    public static class Evaluation
    {
        public static async Task<List<EvalResult>> Run(ICoachClient client, List<EvalCase> cases, int parallel)
        {
            var results = new EvalResult[cases.Count];
            using (var gate = new SemaphoreSlim(parallel))
            {
                await Task.WhenAll(cases.Select(async (c, i) =>
                {
                    await gate.WaitAsync();
                    try { results[i] = await One(client, c); }
                    finally { gate.Release(); }
                }));
            }
            return results.ToList();
        }

        public static async Task<EvalResult> One(ICoachClient client, EvalCase c)
        {
            CoachRequest request;
            if (c.Kind == CoachKind.Onboarding) request = CoachPrompts.Onboarding(c.Voice, c.Answers);
            else
            {
                var context = new ReflectionContext { Weekday = c.Weekday };
                context.KeptToday.AddRange(c.Kept);
                request = CoachPrompts.Reflection(c.Voice, c.Transcript, context);
            }
            var watch = Stopwatch.StartNew();
            CoachReply reply;
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90)))
                reply = await client.AskAsync(request, timeout.Token);
            var r = new EvalResult { Case = c, Reply = reply, Millis = watch.ElapsedMilliseconds };
            if (reply.Ok && c.Kind == CoachKind.Onboarding) r.Proposals = CoachPrompts.ParseOnboarding(reply.Text);
            if (reply.Ok && c.Kind == CoachKind.Reflection)
            {
                r.Line = CoachPrompts.ParseReflection(reply.Text);
                r.Guard = CoachGuards.Check((reply.Text ?? "").Trim().Trim('"', '“', '”', '\'').Trim(), CoachGuards.ReflectionMax);
            }
            return r;
        }

        public static string Summary(List<EvalResult> results)
        {
            var onboarding = results.Where(r => r.Case.Kind == CoachKind.Onboarding).ToList();
            var reflection = results.Where(r => r.Case.Kind == CoachKind.Reflection).ToList();
            var millis = results.Select(r => r.Millis).OrderBy(m => m).ToList();
            long P(double q) => millis.Count == 0 ? 0 : millis[Math.Min(millis.Count - 1, (int)Math.Ceiling(q * millis.Count) - 1)];
            return "onboarding valid: " + onboarding.Count(r => r.Pass) + " of " + onboarding.Count
                + "\nreflection lines passing the guards: " + reflection.Count(r => r.Pass) + " of " + reflection.Count
                + "\nCLI latency p50 " + P(0.5) + " ms, p95 " + P(0.95) + " ms (process start included; not the relay's number)";
        }

        public static string ToJson(List<EvalResult> results, string model)
        {
            using (var stream = new MemoryStream())
            {
                using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    w.WriteStartObject();
                    w.WriteString("model", model ?? "unknown");
                    w.WriteString("summary", Summary(results));
                    w.WriteStartArray("results");
                    foreach (EvalResult r in results)
                    {
                        w.WriteStartObject();
                        w.WriteString("id", r.Case.Id);
                        w.WriteString("kind", r.Case.Kind.ToString());
                        w.WriteString("voice", r.Case.Voice.ToString());
                        w.WriteBoolean("pass", r.Pass);
                        w.WriteNumber("ms", r.Millis);
                        w.WriteString("failure", r.Reply.Failure.ToString());
                        w.WriteString("raw", r.Reply.Text);
                        if (r.Case.Kind == CoachKind.Reflection)
                        {
                            w.WriteString("guard", r.Guard.ToString());
                            w.WriteString("line", r.Line);
                        }
                        else
                        {
                            w.WriteStartArray("proposals");
                            foreach (CoachProposal p in r.Proposals ?? new List<CoachProposal>())
                            {
                                w.WriteStartObject();
                                w.WriteString("name", p.Name);
                                w.WriteString("zone", p.Zone.ToString());
                                w.WriteString("schedule", p.Schedule.Kind == ScheduleKind.TimesPerWeek ? p.Schedule.TimesPerWeek + " a week"
                                    : p.Schedule.Kind == ScheduleKind.Weekdays ? "weekday mask " + p.Schedule.WeekdayMask : "daily");
                                w.WriteString("cue", p.Cue);
                                if (p.Target.HasValue) { w.WriteNumber("target", p.Target.Value); w.WriteString("unit", p.Unit); }
                                w.WriteEndObject();
                            }
                            w.WriteEndArray();
                        }
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                    w.WriteEndObject();
                }
                return System.Text.Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
