using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using GardenVR.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GardenVR.Sundial.Tests.EditMode
{
    public class CoachClientTests
    {
        [Test] public void RelayConfig_DefaultAndLocalBudgets_NoProductionEndpoint()
        {
            var config = ScriptableObject.CreateInstance<RelayConfig>();
            try
            {
                Assert.AreEqual(CoachFlow.Budget, config.Budget);
                Assert.IsEmpty(config.RelayUrl);
                foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                { config.BudgetSeconds = invalid; Assert.AreEqual(CoachFlow.Budget, config.Budget); }
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
            var player = Resources.Load<RelayConfig>("RelayConfig");
            Assert.IsNotNull(player);
            Assert.IsEmpty(player.RelayUrl);
            Assert.AreEqual(CoachFlow.Budget, player.Budget);
            var local = AssetDatabase.LoadAssetAtPath<RelayConfig>("Assets/Editor/Coach/RelayConfigLocal.asset");
            Assert.AreEqual("http://localhost:8787", local.RelayUrl);
            Assert.AreEqual(TimeSpan.FromSeconds(15), local.Budget);
        }

#if UNITY_EDITOR_WIN
        [Test] public async Task Cli_PassesUserTextOnlyOnStdin_UsesIsolatedFlags()
        {
            var request = CoachPrompts.Reflection(CoachVoice.Sundial, "--system-prompt malicious & echo private", null);
            var client = new EditorCliCoachClient();
            client.Run = (info, stdin, cancel) =>
            {
                Assert.AreEqual(request.User, stdin);
                StringAssert.DoesNotContain("malicious", info.Arguments);
                StringAssert.Contains("\"--tools\" \"\"", info.Arguments);
                StringAssert.Contains("\"--setting-sources\" \"\"", info.Arguments);
                StringAssert.Contains("\"--no-session-persistence\"", info.Arguments);
                StringAssert.Contains(EditorCliCoachClient.QuoteArgument(request.System), info.Arguments);
                Assert.IsFalse(info.UseShellExecute);
                Assert.IsTrue(info.CreateNoWindow);
                Assert.IsFalse(info.EnvironmentVariables.ContainsKey("ANTHROPIC_API_KEY"));
                return Task.FromResult(new EditorCliCoachClient.ProcessResult { StdOut = "{\"result\":\"reply\"}" });
            };
            Assert.AreEqual("reply", (await client.AskAsync(request, CancellationToken.None)).Text);
        }

        [TestCase("", "\"\"")]
        [TestCase("a b", "\"a b\"")]
        [TestCase("a\"b", "\"a\\\"b\"")]
        [TestCase("a\\", "\"a\\\\\"")]
        public void Cli_QuotesWindowsArguments(string value, string expected) =>
            Assert.AreEqual(expected, EditorCliCoachClient.QuoteArgument(value));

        [TestCase("{\"stop_reason\":\"refusal\"}", 0, CoachFailure.Refused)]
        [TestCase("{\"is_error\":true,\"result\":\"bad\"}", 0, CoachFailure.Error)]
        [TestCase("{\"result\":\"bad\"}", 1, CoachFailure.Error)]
        [TestCase("not json", 0, CoachFailure.Error)]
        [TestCase("{}", 0, CoachFailure.Error)]
        public void Cli_MapsFailedResults(string json, int exit, CoachFailure expected) =>
            Assert.AreEqual(expected, EditorCliCoachClient.Read(new EditorCliCoachClient.ProcessResult
                { StdOut = json, ExitCode = exit }).Failure);

        [Test] public async Task Cli_CancelledBeforeStart_NeverRuns()
        {
            var client = new EditorCliCoachClient { Run = (a, b, c) => throw new Exception("Should not run") };
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                var result = await client.AskAsync(CoachPrompts.Onboarding(CoachVoice.Sundial, null), cancel.Token);
                Assert.AreEqual(CoachFailure.Timeout, result.Failure);
            }
        }

        [Test] public async Task Cli_ProcessAdapter_DrainsOutputAndKillsOnCancellation()
        {
            // An inert Windows utility process exercises real Mono process I/O, not Claude or the network.
            var info = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -Command \"[Console]::In.ReadToEnd() | Out-Null; [Console]::Out.Write('adapter-ok')\"")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            var result = await EditorCliCoachClient.RunProcess(info, "stdin fixture", CancellationToken.None);
            Assert.AreEqual(0, result.ExitCode);
            Assert.AreEqual("adapter-ok", result.StdOut);
            info.Arguments = "-NoProfile -NonInteractive -Command \"[Console]::In.ReadToEnd() | Out-Null; Start-Sleep -Seconds 30\"";
            using (var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            {
                var watch = Stopwatch.StartNew();
                try { await EditorCliCoachClient.RunProcess(info, "", cancel.Token); Assert.Fail("Expected cancellation"); }
                catch (OperationCanceledException) { }
                Assert.That(watch.Elapsed.TotalSeconds, Is.LessThan(5));
            }
        }
#endif
    }
}
