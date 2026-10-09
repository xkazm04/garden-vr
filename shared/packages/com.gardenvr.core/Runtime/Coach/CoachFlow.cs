using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GardenVR.Core
{
    public enum CoachFailure { None, Off, Timeout, Refused, Error }

    /// <summary>What a coach client returned. <see cref="Text"/> is set only when <see cref="Ok"/>.</summary>
    public sealed class CoachReply
    {
        public bool Ok;
        public string Text;
        public CoachFailure Failure;

        public static CoachReply Success(string text) { return new CoachReply { Ok = true, Text = text }; }
        public static CoachReply Failed(CoachFailure failure) { return new CoachReply { Ok = false, Failure = failure }; }
    }

    /// <summary>
    /// A path to a language model: the relay in the app, the Claude Code CLI in local tests. Implementations never throw
    /// for a failed call; they return <see cref="CoachReply.Failed"/>.
    /// </summary>
    public interface ICoachClient
    {
        Task<CoachReply> AskAsync(CoachRequest request, CancellationToken cancel);
    }

    public sealed class OnboardingResult
    {
        public List<CoachProposal> Proposals;
        /// <summary>True when the proposals came from the model; false when the scripted coach made them.</summary>
        public bool FromModel;
        public CoachFailure Failure;
    }

    public sealed class ReflectionResult
    {
        public string Line;
        public bool FromModel;
        public CoachFailure Failure;
    }

    /// <summary>
    /// Runs a coach moment: asks the client within <see cref="Budget"/>, checks the reply, and falls back to
    /// <see cref="ScriptedCoach"/> for anything else (no client, opted out, slow, refused, failed, or a reply that does
    /// not pass the guards). Every moment always completes.
    /// </summary>
    public static class CoachFlow
    {
        public static readonly TimeSpan Budget = TimeSpan.FromSeconds(4);

        public static async Task<OnboardingResult> OnboardAsync(ICoachClient client, bool optedIn, CoachVoice voice,
            IReadOnlyList<string> answers, TimeSpan budget, CancellationToken cancel)
        {
            CoachReply reply = await Ask(client, optedIn, CoachPrompts.Onboarding(voice, answers), budget, cancel).ConfigureAwait(false);
            if (reply.Ok)
            {
                List<CoachProposal> parsed = CoachPrompts.ParseOnboarding(reply.Text);
                if (parsed.Count > 0) return new OnboardingResult { Proposals = parsed, FromModel = true };
            }
            return new OnboardingResult
            {
                Proposals = ScriptedCoach.Onboarding(answers), FromModel = false,
                Failure = reply.Ok ? CoachFailure.Error : reply.Failure
            };
        }

        public static async Task<ReflectionResult> ReflectAsync(ICoachClient client, bool optedIn, CoachVoice voice,
            string transcript, ReflectionContext context, string stone, TimeSpan budget, CancellationToken cancel)
        {
            bool spoke = !string.IsNullOrWhiteSpace(transcript);
            CoachReply reply = spoke
                ? await Ask(client, optedIn, CoachPrompts.Reflection(voice, transcript, context), budget, cancel).ConfigureAwait(false)
                : CoachReply.Failed(CoachFailure.Off);
            if (reply.Ok)
            {
                string line = CoachPrompts.ParseReflection(reply.Text);
                if (line != null) return new ReflectionResult { Line = line, FromModel = true };
            }
            IReadOnlyList<string> kept = context == null ? null : context.KeptToday;
            return new ReflectionResult
            {
                Line = ScriptedCoach.Reflection(stone, kept), FromModel = false,
                Failure = reply.Ok ? CoachFailure.Error : reply.Failure
            };
        }

        static async Task<CoachReply> Ask(ICoachClient client, bool optedIn, CoachRequest request, TimeSpan budget, CancellationToken cancel)
        {
            if (client == null || !optedIn) return CoachReply.Failed(CoachFailure.Off);
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel))
            {
                Task<CoachReply> call;
                try { call = client.AskAsync(request, timeout.Token); }
                catch (Exception) { return CoachReply.Failed(CoachFailure.Error); }
                Task done = await Task.WhenAny(call, Task.Delay(budget, cancel)).ConfigureAwait(false);
                if (done != call)
                {
                    timeout.Cancel();
                    Observe(call);
                    return CoachReply.Failed(CoachFailure.Timeout);
                }
                try
                {
                    CoachReply reply = await call.ConfigureAwait(false);
                    return reply ?? CoachReply.Failed(CoachFailure.Error);
                }
                catch (Exception) { return CoachReply.Failed(CoachFailure.Error); }
            }
        }

        // A call left behind after the budget must not raise an unobserved task exception later.
        static void Observe(Task task)
        {
            task.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }
    }

    /// <summary>One journal line per day; the only thing a reflection keeps. Clear deletes every line.</summary>
    public sealed class CoachJournal
    {
        public sealed class Entry
        {
            public int Day;
            public string Line;
        }

        readonly List<Entry> _entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries { get { return _entries; } }

        /// <summary>Keeps <paramref name="line"/> for <paramref name="day"/>, replacing that day's line. False for a line that fails a guard.</summary>
        public bool Keep(int day, string line)
        {
            if (!CoachGuards.IsClean(line, CoachGuards.ReflectionMax)) return false;
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Day == day) { _entries[i].Line = line; return true; }
            _entries.Add(new Entry { Day = day, Line = line });
            return true;
        }

        public string On(int day)
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Day == day) return _entries[i].Line;
            return null;
        }

        public void Clear() { _entries.Clear(); }

        public JsonArray Write()
        {
            var array = new JsonArray();
            foreach (Entry e in _entries)
            {
                var obj = new JsonObject();
                obj.Set("Day", JsonValue.Number(e.Day));
                obj.Set("Line", JsonValue.String(e.Line));
                array.Add(obj);
            }
            return array;
        }

        /// <summary>Reads a "Journal" array. Rows that are not objects, or whose line fails a guard, are dropped.</summary>
        public static CoachJournal Read(JsonObject root)
        {
            var journal = new CoachJournal();
            if (root == null || !root.Has("Journal") || root.Get("Journal").Kind != JsonKind.Array) return journal;
            JsonArray rows = root.Get("Journal").AsArray();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Kind != JsonKind.Object) continue;
                JsonObject row = rows[i].AsObject();
                if (!row.Has("Day") || row.Get("Day").Kind != JsonKind.Number) continue;
                journal.Keep(row.Get("Day").AsInt(), LedgerJson.StringMember(row, "Line"));
            }
            return journal;
        }
    }
}
