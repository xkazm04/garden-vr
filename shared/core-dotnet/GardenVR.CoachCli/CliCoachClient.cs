using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GardenVR.Core;

namespace GardenVR.CoachCli
{
    /// <summary>What one CLI run printed.</summary>
    public sealed class ProcessResult
    {
        public int ExitCode;
        public string StdOut;
        public string StdErr;
    }

    /// <summary>
    /// The development path to the model (upgrade plan section 7): one Claude Code CLI process per request, in print
    /// mode, with the request's own system prompt replacing Claude Code's, no tools, no session kept, no settings files.
    /// The user text goes in on stdin, so nothing from the user is ever parsed as a command-line flag.
    /// </summary>
    public sealed class CliCoachClient : ICoachClient
    {
        public string ClaudePath = "claude";
        public string Model = "haiku";

        /// <summary>Runs the process. Replaced in tests; the default starts the real CLI.</summary>
        public Func<ProcessStartInfo, string, CancellationToken, Task<ProcessResult>> Run = RunProcess;

        /// <summary>The model id the last successful reply reported, for the evaluation record.</summary>
        public string LastModel { get; private set; }

        public ProcessStartInfo StartInfo(CoachRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var psi = new ProcessStartInfo(ClaudePath)
            {
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                UseShellExecute = false, CreateNoWindow = true
            };
            foreach (string arg in new[]
            {
                "-p", "--model", Model, "--output-format", "json", "--system-prompt", request.System ?? "",
                "--tools", "", "--no-session-persistence", "--setting-sources", ""
            })
                psi.ArgumentList.Add(arg);
            return psi;
        }

        public async Task<CoachReply> AskAsync(CoachRequest request, CancellationToken cancel)
        {
            ProcessResult result;
            try { result = await Run(StartInfo(request), request.User ?? "", cancel).ConfigureAwait(false); }
            catch (OperationCanceledException) { return CoachReply.Failed(CoachFailure.Timeout); }
            catch (Exception) { return CoachReply.Failed(CoachFailure.Error); }
            return Read(result);
        }

        /// <summary>Reads the CLI's JSON result: a refusal stop reason is Refused; an error or anything unreadable is Error.</summary>
        public CoachReply Read(ProcessResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.StdOut)) return CoachReply.Failed(CoachFailure.Error);
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(result.StdOut))
                {
                    JsonElement root = doc.RootElement;
                    if (Text(root, "stop_reason") == "refusal") return CoachReply.Failed(CoachFailure.Refused);
                    if (root.TryGetProperty("is_error", out JsonElement isError) && isError.ValueKind == JsonValueKind.True)
                        return CoachReply.Failed(CoachFailure.Error);
                    string text = Text(root, "result");
                    if (text == null) return CoachReply.Failed(CoachFailure.Error);
                    if (root.TryGetProperty("modelUsage", out JsonElement usage) && usage.ValueKind == JsonValueKind.Object)
                        foreach (JsonProperty model in usage.EnumerateObject()) { LastModel = model.Name; break; }
                    return CoachReply.Success(text);
                }
            }
            catch (JsonException) { return CoachReply.Failed(CoachFailure.Error); }
        }

        static string Text(JsonElement root, string name)
        {
            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.String
                ? e.GetString() : null;
        }

        static async Task<ProcessResult> RunProcess(ProcessStartInfo psi, string stdin, CancellationToken cancel)
        {
            using (var process = new Process { StartInfo = psi })
            {
                process.Start();
                using (cancel.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } }))
                {
                    Task<string> stdout = process.StandardOutput.ReadToEndAsync();
                    Task<string> stderr = process.StandardError.ReadToEndAsync();
                    await process.StandardInput.WriteAsync(stdin).ConfigureAwait(false);
                    process.StandardInput.Close();
                    await process.WaitForExitAsync(cancel).ConfigureAwait(false);
                    return new ProcessResult { ExitCode = process.ExitCode, StdOut = await stdout.ConfigureAwait(false), StdErr = await stderr.ConfigureAwait(false) };
                }
            }
        }
    }
}
