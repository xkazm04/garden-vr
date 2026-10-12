using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GardenVR.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GardenVR.Sundial.Tests.PlayMode
{
    public class CoachTests
    {
        RelayConfig _config;
        SundialSave _save;

        [SetUp] public void Setup()
        {
            _config = ScriptableObject.CreateInstance<RelayConfig>();
            _save = SundialCodec.Read(Json.ParseObject("{}"));
        }

        [TearDown] public void Cleanup() { UnityEngine.Object.DestroyImmediate(_config); }

        [UnityTest] public IEnumerator Coach_OffByDefault_ScriptedPathCompletes()
        {
            int constructed = 0;
            var coach = new SundialCoach(() => _save, _config, key => { constructed++; throw new Exception("Must stay off"); });
            Assert.IsFalse(_save.Settings.CoachOptIn);
            var onboard = coach.OnboardAsync(new[] { "a little walk" });
            yield return Complete(onboard);
            Assert.IsFalse(onboard.Result.FromModel);
            Assert.AreEqual(CoachFailure.Off, onboard.Result.Failure);
            Assert.IsNotEmpty(onboard.Result.Proposals);
            var reflect = coach.ReflectAsync("private words", null, "quiet");
            yield return Complete(reflect);
            Assert.AreEqual(ScriptedCoach.Reflection("quiet", null), reflect.Result.Line);
            Assert.AreEqual(CoachFailure.Off, reflect.Result.Failure);
            Assert.AreEqual(0, constructed);
        }

        [UnityTest] public IEnumerator Coach_RelayDown_FallsBackWithinBudget()
        {
            var fake = new NeverAnswers();
            _config.BudgetSeconds = 0.2f;
            _save.Settings.CoachOptIn = true;
            var coach = new SundialCoach(() => _save, _config, key => fake);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var call = coach.ReflectAsync("private words", null, "quiet");
            yield return Complete(call);
            Assert.AreEqual(CoachFailure.Timeout, call.Result.Failure);
            Assert.AreEqual(ScriptedCoach.Reflection("quiet", null), call.Result.Line);
            Assert.IsTrue(fake.Cancel.IsCancellationRequested);
            Assert.That(watch.Elapsed.TotalSeconds, Is.LessThan(1.2), "budget plus scheduler allowance");
        }

        [UnityTest] public IEnumerator Coach_Refused_ShowsScriptedLine()
        {
            _save.Settings.CoachOptIn = true;
            var coach = new SundialCoach(() => _save, _config, key => new Refuses());
            var call = coach.ReflectAsync("private words", null, "quiet");
            yield return Complete(call);
            Assert.IsFalse(call.Result.FromModel);
            Assert.AreEqual(CoachFailure.Refused, call.Result.Failure);
            Assert.AreEqual(ScriptedCoach.Reflection("quiet", null), call.Result.Line);
            var onboard = coach.OnboardAsync(null);
            yield return Complete(onboard);
            Assert.AreEqual(CoachFailure.Refused, onboard.Result.Failure);
            Assert.IsNotEmpty(onboard.Result.Proposals);
        }

        [UnityTest] public IEnumerator Coach_OptInAndInstallKeyAreReadOnEveryCall()
        {
            int calls = 0;
            string seenKey = null;
            var coach = new SundialCoach(() => _save, _config, key => { calls++; seenKey = key; return new Refuses(); });
            _save.Settings.CoachOptIn = true;
            yield return Complete(coach.OnboardAsync(null));
            Assert.AreEqual(_save.InstallKey, seenKey);
            _save = SundialCodec.Read(Json.ParseObject("{}")); // reset/restore must close the gate too
            yield return Complete(coach.OnboardAsync(null));
            Assert.AreEqual(1, calls);
            _save.Settings.CoachOptIn = true;
            yield return Complete(coach.OnboardAsync(null));
            Assert.AreEqual(2, calls);
            Assert.AreEqual(_save.InstallKey, seenKey);
        }

        [UnityTest] public IEnumerator Relay_PostsCoreEnvelopeAndKey_DecodesReply()
        {
            using (var server = new LocalServer("{\"ok\":true,\"text\":\"a reply\"}"))
            {
                var request = CoachPrompts.Reflection(CoachVoice.Sundial, "a small café walk", null);
                var call = new RelayCoachClient(server.Url + "/", _save.InstallKey).AskAsync(request, CancellationToken.None);
                yield return Complete(call);
                yield return Complete(server.Work);
                Assert.IsTrue(call.Result.Ok);
                Assert.AreEqual("a reply", call.Result.Text);
                StringAssert.StartsWith("POST /v1/coach HTTP/1.1", server.Headers);
                StringAssert.Contains("x-install-key: " + _save.InstallKey, server.Headers.ToLowerInvariant());
                StringAssert.Contains("content-type: application/json", server.Headers.ToLowerInvariant());
                Assert.AreEqual(RelayProtocol.Encode(request), server.Body);
            }
        }

        [UnityTest] public IEnumerator Relay_NeverAnsweringSocket_IsAbortedAfterFlowBudget()
        {
            using (var server = new LocalServer(null))
            using (var relay = new InFlightRelay(server.Url, _save.InstallKey,
                CoachPrompts.Reflection(CoachVoice.Sundial, "private words", null)))
            {
                // Establish the unanswered request before starting the short flow budget. Otherwise
                // a busy editor can abort before connect and leave the server waiting in Accept forever.
                yield return Complete(server.RequestReceived, "loopback request received");
                Assert.IsFalse(relay.Work.IsCompleted, "server must leave the real request unanswered");
                _config.RelayUrl = server.Url;
                _config.BudgetSeconds = 0.5f;
                _save.Settings.CoachOptIn = true;
                var call = new SundialCoach(() => _save, _config, key => relay)
                    .ReflectAsync("private words", null, "quiet");
                yield return Complete(call, "flow timeout and fallback");
                Assert.AreEqual(CoachFailure.Timeout, call.Result.Failure);
                Assert.IsFalse(call.Result.FromModel);
                Assert.AreEqual(ScriptedCoach.Reflection("quiet", null), call.Result.Line);
                yield return Complete(relay.Work, "relay abort");
                Assert.AreEqual(CoachFailure.Timeout, relay.Work.Result.Failure);
                yield return Complete(server.Work, "socket disconnect");
                Assert.IsTrue(server.Disconnected, "UnityWebRequest must abort the socket, not just abandon its Task");
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest] public IEnumerator Relay_RefusalAndMalformedReply_MapToFailure()
        {
            foreach (string body in new[] { "{\"ok\":false,\"failure\":\"Refused\"}", "bad json" })
            using (var server = new LocalServer(body))
            {
                var call = new RelayCoachClient(server.Url, _save.InstallKey).AskAsync(
                    CoachPrompts.Onboarding(CoachVoice.Sundial, null), CancellationToken.None);
                yield return Complete(call);
                Assert.AreEqual(body == "bad json" ? CoachFailure.Error : CoachFailure.Refused, call.Result.Failure);
            }
        }

        [UnityTest] public IEnumerator Relay_PreCancelled_DoesNotConnect()
        {
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                var call = new RelayCoachClient("http://localhost:1", _save.InstallKey).AskAsync(null, cancel.Token);
                yield return Complete(call);
                Assert.AreEqual(CoachFailure.Timeout, call.Result.Failure);
            }
        }

        static IEnumerator Complete(Task task, string stage = "task")
        {
            float deadline = Time.realtimeSinceStartup + 5;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(task.IsCompleted, stage + " did not complete within 5 seconds");
            if (task.IsFaulted) throw task.Exception;
            Assert.IsFalse(task.IsCanceled);
        }

        sealed class NeverAnswers : ICoachClient
        {
            public CancellationToken Cancel;
            public Task<CoachReply> AskAsync(CoachRequest request, CancellationToken cancel)
            {
                Cancel = cancel;
                return new TaskCompletionSource<CoachReply>().Task;
            }
        }
        sealed class Refuses : ICoachClient
        {
            public Task<CoachReply> AskAsync(CoachRequest request, CancellationToken cancel) =>
                Task.FromResult(CoachReply.Failed(CoachFailure.Refused));
        }

        // Only the test's connection setup precedes the budget. CoachFlow still supplies cancellation,
        // and the real RelayCoachClient still owns Abort and disposal on Unity's main thread.
        sealed class InFlightRelay : ICoachClient, IDisposable
        {
            readonly CancellationTokenSource _cancel = new CancellationTokenSource();
            readonly string _envelope;
            CancellationTokenRegistration _registration;
            public readonly Task<CoachReply> Work;

            public InFlightRelay(string url, string key, CoachRequest request)
            {
                _envelope = RelayProtocol.Encode(request);
                Work = new RelayCoachClient(url, key).AskAsync(request, _cancel.Token);
            }

            public Task<CoachReply> AskAsync(CoachRequest request, CancellationToken cancel)
            {
                Assert.AreEqual(_envelope, RelayProtocol.Encode(request));
                _registration = cancel.Register(() => _cancel.Cancel());
                return Work;
            }

            public void Dispose()
            {
                _registration.Dispose();
                _cancel.Cancel();
                _cancel.Dispose();
            }
        }

        // Loopback only, ephemeral port, no relay/model dependency. Read bytes to respect UTF-8 Content-Length.
        sealed class LocalServer : IDisposable
        {
            readonly TcpListener _listener = new TcpListener(IPAddress.Loopback, 0);
            TcpClient _socket;
            public string Url, Headers, Body;
            public bool Disconnected;
            public Task Work;
            readonly TaskCompletionSource<bool> _requestReceived = new TaskCompletionSource<bool>();
            public Task RequestReceived => _requestReceived.Task;

            public LocalServer(string reply)
            {
                _listener.Start();
                Url = "http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port;
                Work = Serve(reply);
            }

            async Task Serve(string reply)
            {
                _socket = await _listener.AcceptTcpClientAsync();
                using (_socket)
                using (var stream = _socket.GetStream())
                using (var header = new MemoryStream())
                {
                    var one = new byte[1];
                    while (true)
                    {
                        if (await stream.ReadAsync(one, 0, 1) == 0) throw new IOException("Closed before headers");
                        header.WriteByte(one[0]);
                        Headers = Encoding.ASCII.GetString(header.ToArray());
                        if (Headers.EndsWith("\r\n\r\n")) break;
                    }
                    int size = 0;
                    foreach (string line in Headers.Split('\n'))
                        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) size = int.Parse(line.Substring(15).Trim());
                    var body = new byte[size];
                    for (int read = 0; read < size;)
                    {
                        int count = await stream.ReadAsync(body, read, size - read);
                        if (count == 0) throw new IOException("Closed before body");
                        read += count;
                    }
                    Body = Encoding.UTF8.GetString(body);
                    _requestReceived.SetResult(true);
                    if (reply == null)
                    {
                        try { Disconnected = await stream.ReadAsync(one, 0, 1) == 0; }
                        catch (IOException) { Disconnected = true; }
                        return;
                    }
                    var bytes = Encoding.UTF8.GetBytes(reply);
                    var response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + bytes.Length + "\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(response, 0, response.Length);
                    await stream.WriteAsync(bytes, 0, bytes.Length);
                }
            }
            public void Dispose() { _socket?.Close(); _listener.Stop(); }
        }
    }
}
