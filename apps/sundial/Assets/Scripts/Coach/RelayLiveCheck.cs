#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using GardenVR.Core;
using GardenVR.Input;
using UnityEngine;
using UnityEngine.Networking;

namespace GardenVR.Sundial
{
    /// <summary>Explicit development-player probe. Synthetic speech, real relay, isolated save, no text logs.</summary>
    public sealed class RelayLiveCheck : MonoBehaviour
    {
        static string _output;
        SundialController _controller;
        readonly Hands _hands = new Hands();
        readonly Speech _speech = new Speech();
        RelayConfig _config;
        float _nextCall;
        bool _complete;
        readonly string[] _answers = { "A short walk", "After breakfast", "A page of reading", "A quiet evening" };
        const string Reflection = "I walked after breakfast and read a page this evening.";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--relay-live-check");
            if (index < 0 || index + 1 >= args.Length) return;
            _output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(_output);
            // Never touch the owner's save or reuse a previous probe's state.
            SundialController.SaveDirectoryOverride = Path.Combine(Application.temporaryCachePath, "relay-live-" + Guid.NewGuid().ToString("N"));
            SundialService.DevSeedOnFresh = false;
            SundialService.FreshReducedMotion = true;
            var go = new GameObject("RelayLiveCheck");
            DontDestroyOnLoad(go);
            go.AddComponent<RelayLiveCheck>();
        }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
            using (var health = UnityWebRequest.Get("http://localhost:8787/healthz"))
            {
                health.timeout = 5;
                yield return health.SendWebRequest();
                Require(health.result == UnityWebRequest.Result.Success && health.downloadHandler.text.Contains("\"backend\":\"cli\""), "health backend must be cli");
            }
            Debug.Log("[RelayLive] backend=cli budget_ms=15000 speech=synthetic transport=UnityWebRequest");
            yield return Bind();
            yield return Until(() => _controller.Wizard.Step == Onboarding.OptIn, 15, "consent");
            yield return Emit("interview.on");
            for (int i = 0; i < 4; i++)
            {
                Require(_controller.Wizard.QuestionIndex == i, "question index");
                yield return Say("interview.speak", _answers[i]);
                yield return Emit("interview.next");
            }
            _nextCall = Time.realtimeSinceStartup + 11;
            yield return Until(() => _controller.Wizard.Step == Onboarding.Review, 20, "onboarding review");
            Require(_controller.Wizard.InterviewFromModel, "onboarding fell back");
            var proposals = _controller.Service.Save.Interview.Proposals;
            Require(proposals.Count > 0, "empty proposals");
            for (int i = 0; i < proposals.Count; i++)
                Require(VisibleText("interview.packet." + i).Contains(proposals[i].Name), "model proposal not on packet");
            Debug.Log("[RelayLive] live_onboarding from_model=true packet_text_matches=true proposal_count=" + proposals.Count);
            yield return Capture("live-onboarding.png");
            yield return Emit("interview.packet.0");
            if (_controller.Service.Save.Habits.Count == 0) yield return Emit("interview.arc.morning");
            Require(_controller.Service.Save.Habits.Count > 0, "proposal not planted");
            yield return Emit("interview.done");
            string plant = "plant." + SundialArcs.Key(_controller.Service.Save.Habits[0].Group);
            yield return Emit(plant, HandIntentKind.Look);
            yield return new WaitForSecondsRealtime(.3f);
            yield return Emit(plant);
            yield return Until(() => !_controller.Wizard.Running, 5, "first tend");
            yield return Pace();
            var reflection = _controller.GetComponent<ReflectionController>();
            reflection.Offer();
            yield return null;
            yield return Say(ReflectionController.SpeakId, Reflection);
            yield return Until(() => reflection.Line != null && !reflection.Waiting, 20, "reflection card");
            Require(reflection.FromModel, "reflection fell back");
            Require(VisibleText("ReflectionLine").Contains(reflection.Line), "model line not on card");
            Require(_controller.Service.Save.Journal.On(_controller.Service.Today().Index) == reflection.Line, "journal mismatch");
            Debug.Log("[RelayLive] live_reflection from_model=true card_text_matches=true journal_matches=true");
            yield return Capture("live-reflection.png");
            reflection.Close();

            string csv = Path.Combine(_output, "timings.csv");
            File.WriteAllText(csv, "sample,backend,kind,elapsed_ms,from_model,outcome\n");
            for (int i = 0; i < 20; i++)
            {
                yield return Pace();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                bool model; CoachFailure failure;
                string kind = i % 2 == 0 ? "Onboarding" : "Reflection";
                if (i % 2 == 0)
                {
                    var call = _controller.Coach.OnboardAsync(_answers);
                    yield return Until(() => call.IsCompleted, 20, "timed onboarding");
                    model = call.Result.FromModel; failure = call.Result.Failure;
                }
                else
                {
                    var call = _controller.Coach.ReflectAsync(Reflection, _controller.Service.ReflectionToday(), null);
                    yield return Until(() => call.IsCompleted, 20, "timed reflection");
                    model = call.Result.FromModel; failure = call.Result.Failure;
                }
                watch.Stop();
                string ms = watch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture);
                File.AppendAllText(csv, (i + 1) + ",cli," + kind + "," + ms + "," + model.ToString().ToLowerInvariant() + "," + failure + "\n");
                Debug.Log("[RelayLive] sample=" + (i + 1) + " backend=cli kind=" + kind + " elapsed_ms=" + ms + " from_model=" + model + " outcome=" + failure);
            }
            Require(string.IsNullOrEmpty(_controller.GetComponent<SpeechController>().Transcript), "speech transcript retained");
            _complete = true;
            Debug.Log("[RelayLive] complete samples=20 backend=cli");
            Application.Quit(0);
        }

        IEnumerator Bind()
        {
            yield return null; yield return null;
            _controller = FindAnyObjectByType<SundialController>();
            Require(_controller != null, "controller missing");
            if (_config == null)
            {
                _config = ScriptableObject.CreateInstance<RelayConfig>();
                _config.RelayUrl = "http://localhost:8787"; _config.BudgetSeconds = 15;
            }
            _controller.CoachConfig = _config;
            _controller.SetSource(_hands);
            _controller.GetComponent<SpeechController>().SetSpeech(_speech);
            yield return null;
        }
        IEnumerator Say(string target, string words)
        {
            yield return Emit(target, HandIntentKind.PinchHold);
            Require(_controller.GetComponent<SpeechController>().Listening, "speech seam not listening");
            _speech.Say(words);
            yield return Emit(target, HandIntentKind.Release);
        }
        IEnumerator Emit(string target, HandIntentKind kind = HandIntentKind.Pinch)
        { _hands.Emit(target, kind); yield return null; yield return null; }
        IEnumerator Pace()
        {
            while (Time.realtimeSinceStartup < _nextCall) yield return null;
            _nextCall = Time.realtimeSinceStartup + 11; // At most six starts in any minute, one install key.
        }
        static IEnumerator Until(Func<bool> ready, float seconds, string label)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!ready() && Time.realtimeSinceStartup < end) yield return null;
            Require(ready(), label + " deadline");
            yield return null;
        }
        static string VisibleText(string name)
        {
            var go = GameObject.Find(name);
            Require(go != null && go.activeInHierarchy && go.GetComponent<Renderer>().enabled, "missing visible text: " + name);
            return string.Join(" ", go.GetComponent<TextMesh>().text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
        }
        static void Require(bool condition, string label)
        { if (!condition) throw new InvalidOperationException("[RelayLive] ERROR " + label); }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(_output, name), texture.EncodeToPNG());
            Destroy(texture);
        }
        void OnLog(string message, string stack, LogType type)
        {
            if (_complete || (type != LogType.Exception && !message.StartsWith("[RelayLive] ERROR"))) return;
            File.WriteAllText(Path.Combine(_output, "probe-error.txt"), "Development player exception; inspect local player.log.\n");
            Application.Quit(1);
        }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }

        sealed class Hands : IHandIntentSource
        {
            public event Action<HandIntent> Intent;
            public event Action SystemPause { add { } remove { } }
            public bool IsPinching { get; private set; }
            public bool IsTracked => true;
            public float PinchStrength => IsPinching ? 1 : 0;
            public Ray LookRay => new Ray(Vector3.zero, Vector3.forward);
            public string BindingHint(HandIntentKind kind) => HandBindings.PcHint(kind);
            public void Emit(string id, HandIntentKind kind)
            { IsPinching = kind == HandIntentKind.PinchHold; Intent?.Invoke(new HandIntent(kind, LookRay, targetId: id, held: .6f)); }
        }
        sealed class Speech : ISpeechToText
        {
            public event Action<string> Final;
            public event Action<string> Partial { add { } remove { } }
            public bool Available => true;
            public void Begin() { }
            public void End() { }
            public void Say(string text) => Final?.Invoke(text);
        }
    }
}
#endif
