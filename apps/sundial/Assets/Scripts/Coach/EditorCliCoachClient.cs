#if UNITY_EDITOR_WIN
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GardenVR.Core;

namespace GardenVR.Sundial
{
    /// <summary>Unity-compatible port of GardenVR.CoachCli. Entire implementation is excluded from players.</summary>
    public sealed class EditorCliCoachClient : ICoachClient
    {
        public sealed class ProcessResult
        {
            public int ExitCode;
            public string StdOut;
        }

        // Use the native Claude executable, not a shell or a .cmd wrapper.
        public string ClaudePath = "claude.exe";
        public string Model = "haiku";
        public Func<ProcessStartInfo, string, CancellationToken, Task<ProcessResult>> Run = RunProcess;

        public ProcessStartInfo StartInfo(CoachRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var args = new StringBuilder();
            foreach (string arg in new[] { "-p", "--model", Model, "--output-format", "json",
                "--system-prompt", request.System ?? "", "--tools", "", "--no-session-persistence",
                "--setting-sources", "" })
            {
                if (args.Length > 0) args.Append(' ');
                args.Append(QuoteArgument(arg));
            }
            var info = new ProcessStartInfo(ClaudePath, args.ToString())
            {
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                UseShellExecute = false, CreateNoWindow = true
            };
            info.EnvironmentVariables.Remove("ANTHROPIC_API_KEY");
            return info;
        }

        // Unity's Mono does not reliably pass ArgumentList. Windows argv quoting preserves
        // empty arguments, quotes and trailing backslashes without invoking a shell.
        public static string QuoteArgument(string value)
        {
            var text = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                text.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
                text.Append(c);
                slashes = 0;
            }
            text.Append('\\', slashes * 2);
            return text.Append('"').ToString();
        }

        public async Task<CoachReply> AskAsync(CoachRequest request, CancellationToken cancel)
        {
            try
            {
                cancel.ThrowIfCancellationRequested();
                var result = await Run(StartInfo(request), request.User ?? "", cancel).ConfigureAwait(false);
                cancel.ThrowIfCancellationRequested();
                return Read(result);
            }
            catch (OperationCanceledException) { return CoachReply.Failed(CoachFailure.Timeout); }
            catch (Exception) { return CoachReply.Failed(CoachFailure.Error); }
        }

        public static CoachReply Read(ProcessResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.StdOut)) return CoachReply.Failed(CoachFailure.Error);
            try
            {
                var root = Json.ParseObject(result.StdOut);
                if (LedgerJson.StringMember(root, "stop_reason") == "refusal") return CoachReply.Failed(CoachFailure.Refused);
                if (result.ExitCode != 0 || (root.Has("is_error") && root.Get("is_error").Kind == JsonKind.Bool &&
                    root.Get("is_error").AsBool())) return CoachReply.Failed(CoachFailure.Error);
                if (!root.Has("result") || root.Get("result").Kind != JsonKind.String) return CoachReply.Failed(CoachFailure.Error);
                return CoachReply.Success(root.Get("result").AsString());
            }
            catch (Exception) { return CoachReply.Failed(CoachFailure.Error); }
        }

        public static Task<ProcessResult> RunProcess(ProcessStartInfo info, string stdin, CancellationToken cancel)
        {
            return Task.Run(async () =>
            {
                cancel.ThrowIfCancellationRequested();
                using (var process = new Process { StartInfo = info })
                {
                    process.Start();
                    using (cancel.Register(() =>
                    {
                        try { if (!process.HasExited) process.Kill(); }
                        catch (InvalidOperationException) { }
                        catch (System.ComponentModel.Win32Exception) { }
                    }))
                    {
                        var stdout = process.StandardOutput.ReadToEndAsync();
                        var stderr = process.StandardError.ReadToEndAsync();
                        await process.StandardInput.WriteAsync(stdin).ConfigureAwait(false);
                        process.StandardInput.Close();
                        process.WaitForExit();
                        string output = await stdout.ConfigureAwait(false);
                        await stderr.ConfigureAwait(false); // Drain, never log model/user content.
                        cancel.ThrowIfCancellationRequested();
                        return new ProcessResult { ExitCode = process.ExitCode, StdOut = output };
                    }
                }
            }, cancel);
        }
    }
}
#endif
