using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GardenVR.Core;
using GardenVR.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GardenVR.Sundial.Tests.PlayMode
{
    public class ReflectionTests
    {
        const string Spoken = "violet bicycles beside the secret orchard";
        const string ModelLine = "A slow day, and you still walked.";
        sealed class Hands : IHandIntentSource
        {
            public event Action<HandIntent> Intent;
            public event Action SystemPause;
            public bool IsPinching { get; private set; }
            public bool IsTracked { get; set; } = true;
            public float PinchStrength => IsPinching ? 1 : 0;
            public Ray LookRay => new Ray(Vector3.zero, Vector3.forward);
            public string BindingHint(HandIntentKind kind) => HandBindings.PcHint(kind);
            public void Emit(string id, HandIntentKind kind = HandIntentKind.Pinch)
            {
                IsPinching = kind == HandIntentKind.PinchHold;
                Intent?.Invoke(new HandIntent(kind, LookRay, targetId: id, held: .6f));
            }
            public void Pause() => SystemPause?.Invoke();
        }
        sealed class Speech : ISpeechToText
        {
            public event Action<string> Final;
            public event Action<string> Partial { add { } remove { } }
            public bool Available { get; set; } = true;
            public void Begin() { }
            public void End() { }
            public void Say(string words) => Final?.Invoke(words);
        }
        sealed class Client : ICoachClient
        {
            public string Reply = ModelLine;
            public CoachRequest Request;
            public TaskCompletionSource<CoachReply> Pending;
            public CancellationToken Cancel;
            public Task<CoachReply> AskAsync(CoachRequest request, CancellationToken cancel)
            {
                Request = request; Cancel = cancel;
                return Pending == null ? Task.FromResult(CoachReply.Success(Reply)) : Pending.Task;
            }
        }
        SundialController _c;
        ReflectionController _reflection;
        Speech _device;
        Hands _hands;
        float _delta;
        string _dir;
        string Evidence => SundialEvidence.Dir("T-SUN-064");
        [SetUp] public void Setup() { _delta = Time.captureDeltaTime; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            yield return SundialPlay.Unload();
            SundialController.SaveDirectoryOverride = null; SundialController.ClockOverride = null;
            SundialService.DevSeedOnFresh = false; Time.captureDeltaTime = _delta;
        }
        IEnumerator Boot()
        {
            yield return SundialPlay.Open(c => _c = c);
            SundialPlay.FixedStep();
            _dir = SundialController.SaveDirectoryOverride;
            _hands = new Hands(); _device = new Speech(); _c.SetSource(_hands);
            _c.GetComponent<SpeechController>().SetSpeech(_device);
            _reflection = _c.GetComponent<ReflectionController>();
            yield return null;
        }
        IEnumerator Pinch(string id, HandIntentKind kind = HandIntentKind.Pinch)
        {
            _hands.Emit(id, kind); yield return null; yield return null;
        }
        IEnumerator Speak(Client client)
        {
            _c.Service.Save.Settings.CoachOptIn = true;
            _reflection.CoachOverride = new SundialCoach(() => _c.Service.Save, null, _ => client);
            _reflection.Offer(); yield return null;
            yield return Pinch(ReflectionController.SpeakId, HandIntentKind.PinchHold);
            _device.Say(Spoken);
            yield return Pinch(ReflectionController.SpeakId, HandIntentKind.Release);
        }
        IEnumerator Completed()
        {
            float end = Time.realtimeSinceStartup + 6;
            while (_reflection.Line == null && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsNotNull(_reflection.Line);
            Assert.IsEmpty(_c.GetComponent<SpeechController>().Transcript);
        }
        void Capture(string name)
        {
            Directory.CreateDirectory(Evidence);
            RecordTilesTests.Shot(GameObject.Find("EyeCamera").GetComponent<Camera>(), Path.Combine(Evidence, name + ".png"));
        }
        void CheckTargets(string prefix)
        {
            Physics.SyncTransforms();
            foreach (var target in _c.GetComponentsInChildren<IntentTarget>())
            {
                if (!target.Id.StartsWith(prefix) || !target.gameObject.activeInHierarchy) continue;
                var collider = target.GetComponent<Collider>();
                var p = GameObject.Find("EyeCamera").GetComponent<Camera>().WorldToViewportPoint(collider.bounds.center);
                Assert.That(p.x, Is.InRange(.01f, .99f), target.Id);
                Assert.That(p.y, Is.InRange(.01f, .99f), target.Id);
                Assert.IsTrue(Physics.Raycast(GameObject.Find("EyeCamera").GetComponent<Camera>().transform.position, collider.bounds.center - GameObject.Find("EyeCamera").GetComponent<Camera>().transform.position, out var hit), target.Id);
                Assert.AreEqual(target.Id, hit.collider.GetComponent<IntentTarget>()?.Id, target.Id + " occluded by " + hit.collider.name);
            }
        }
        [UnityTest, Timeout(180000)] public IEnumerator Reflection_Stone_KeepsScriptedLine()
        {
            yield return Boot();
            SundialPlay.Play(_c, DuskScripts.ThreeBreaths());
            float end = Time.realtimeSinceStartup + 40;
            while (!_c.GetComponent<DuskRitualController>().Answered && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(_reflection.Open, "dusk completion must offer reflection");
            _c.SetSource(_hands); yield return null;
            _device.Available = false; yield return null; yield return null;
            RecordTilesTests.DisableAsyncShaders(); var cam = RecordTilesTests.PoseDial(); RecordTilesTests.AttachPlate(cam);
            yield return SundialPlay.Seconds(.2f);
            Capture("DialG1-reflection-stones"); CheckTargets("reflection.");
            Assert.IsFalse(_c.Service.Save.Settings.CoachOptIn);
            yield return Pinch("reflection.stone.0"); yield return Completed();
            Assert.AreEqual(ScriptedCoach.Reflection("calm", _c.Service.ReflectionToday().KeptToday), _reflection.Line);
            Assert.IsFalse(_reflection.FromModel);
            Assert.AreEqual(_reflection.Line, _c.Service.Save.Journal.On(_c.Service.Today().Index));
            Capture("DialG1-reflection"); CheckTargets("reflection.");
            yield return Pinch("reflection.again"); yield return Pinch("reflection.stone.2"); yield return Completed();
            Assert.AreEqual(1, _c.Service.Save.Journal.Entries.Count);
            StringAssert.Contains("glad", _reflection.Line);
        }
        [UnityTest] public IEnumerator Reflection_Speech_FakeClient_KeepsModelLine()
        {
            yield return Boot();
            _c.Service.TendRitual("water");
            var client = new Client(); yield return Speak(client); yield return Completed();
            Assert.AreEqual(ModelLine, _reflection.Line); Assert.IsTrue(_reflection.FromModel);
            StringAssert.Contains("Water", client.Request.User); StringAssert.DoesNotContain("Planned", client.Request.User);
            Assert.AreEqual(ModelLine, new SundialService(SundialPlay.Clock1420(), _dir).Save.Journal.On(_c.Service.Today().Index));
            // Capture the listener with an existing saved line, then release to complete again.
            yield return Pinch("reflection.again");
            RecordTilesTests.DisableAsyncShaders(); var cam = RecordTilesTests.PoseDial(); RecordTilesTests.AttachPlate(cam);
            yield return Pinch(ReflectionController.SpeakId, HandIntentKind.PinchHold);
            Assert.IsTrue(_c.GetComponent<SpeechController>().RippleVisible);
            Capture("DialG1-reflection-listening");
            yield return Pinch(ReflectionController.SpeakId, HandIntentKind.Release);
        }
        [UnityTest] public IEnumerator Reflection_DirtyLine_FallsBack()
        {
            yield return Boot(); yield return Speak(new Client { Reply = "Your streak treats anxiety." }); yield return Completed();
            Assert.IsFalse(_reflection.FromModel);
            Assert.AreEqual(ScriptedCoach.Reflection(null, _c.Service.ReflectionToday().KeptToday), _reflection.Line);
        }
        [UnityTest] public IEnumerator Save_HoldsNoTranscript()
        {
            yield return Boot(); yield return Speak(new Client()); yield return Completed();
            string save = File.ReadAllText(Path.Combine(_dir, "save.json"));
            StringAssert.Contains(ModelLine, save); StringAssert.DoesNotContain(Spoken, save);
            foreach (string file in Directory.GetFiles(_dir)) StringAssert.DoesNotContain(Spoken, File.ReadAllText(file));
            _device.Say("late private words");
            Assert.IsEmpty(_c.GetComponent<SpeechController>().Transcript);
        }
        [UnityTest] public IEnumerator Journal_Delete_Empties()
        {
            yield return Boot(); yield return Speak(new Client()); yield return Completed();
            yield return Pinch(SettingsTabs.TabId, HandIntentKind.Poke);
            yield return Pinch(SettingsTabs.ConfirmJournalId, HandIntentKind.Poke);
            Assert.AreEqual(1, _c.Service.Save.Journal.Entries.Count, "confirmation must first be offered");
            yield return Pinch(SettingsTabs.DeleteJournalId, HandIntentKind.Poke);
            Assert.IsTrue(_c.Settings.ConfirmingJournal);
            yield return Pinch(SettingsTabs.CancelJournalId, HandIntentKind.Poke);
            Assert.AreEqual(1, _c.Service.Save.Journal.Entries.Count);
            yield return Pinch(SettingsTabs.DeleteJournalId, HandIntentKind.Poke);
            RecordTilesTests.DisableAsyncShaders(); var cam = RecordTilesTests.PoseDial(); RecordTilesTests.AttachPlate(cam);
            yield return null; Capture("DialG1-journal-delete"); CheckTargets("tab.settings.journal");
            yield return Pinch(SettingsTabs.ConfirmJournalId, HandIntentKind.Poke);
            Assert.IsEmpty(_c.Service.Save.Journal.Entries); Assert.IsFalse(_reflection.Open);
            Assert.IsEmpty(new SundialService(SundialPlay.Clock1420(), _dir).Save.Journal.Entries);
            foreach (string file in Directory.GetFiles(_dir)) StringAssert.DoesNotContain(ModelLine, File.ReadAllText(file));
        }
        [UnityTest] public IEnumerator Reflection_CancelAndDelete_RejectLateReply()
        {
            yield return Boot(); var client = new Client { Pending = new TaskCompletionSource<CoachReply>() };
            yield return Speak(client); Assert.IsTrue(_reflection.Waiting);
            _hands.Pause(); yield return null; Assert.IsTrue(client.Cancel.IsCancellationRequested);
            client.Pending.SetResult(CoachReply.Success(ModelLine)); yield return null; yield return null;
            Assert.IsEmpty(_c.Service.Save.Journal.Entries);
            client = new Client { Pending = new TaskCompletionSource<CoachReply>() };
            yield return Speak(client);
            yield return Pinch(SettingsTabs.TabId, HandIntentKind.Poke);
            yield return Pinch(SettingsTabs.DeleteJournalId, HandIntentKind.Poke);
            yield return Pinch(SettingsTabs.ConfirmJournalId, HandIntentKind.Poke);
            client.Pending.SetResult(CoachReply.Success(ModelLine)); yield return null; yield return null;
            Assert.IsEmpty(_c.Service.Save.Journal.Entries);
        }
        [UnityTest] public IEnumerator Journal_WeekPage_ShowsOnlySelectedWeek()
        {
            yield return Boot(); int today = _c.Service.Today().Index;
            _c.Service.KeepReflection(today, ModelLine);
            _c.Service.KeepReflection(today - 7, "A light day.");
            yield return Pinch(WeekDialController.CornerId, HandIntentKind.PinchHold);
            yield return Pinch(WeekDialController.CornerId, HandIntentKind.Release);
            yield return SundialPlay.Seconds(1);
            var week = _c.GetComponent<WeekDialController>();
            StringAssert.Contains(ModelLine, week.JournalText.Replace("\n", " "));
            StringAssert.DoesNotContain("A light day.", week.JournalText);
            RecordTilesTests.DisableAsyncShaders(); var cam = RecordTilesTests.PoseDial(); RecordTilesTests.AttachPlate(cam);
            yield return null; Capture("DialG1-journal-week"); CheckTargets("journal.");
            yield return Pinch("journal.older");
            StringAssert.Contains("A light day.", week.JournalText); StringAssert.DoesNotContain("slow", week.JournalText);
            yield return Pinch("journal.newer");
            StringAssert.Contains("slow", week.JournalText);
        }

        [UnityTest] public IEnumerator Journal_SevenLines_FitCard()
        {
            yield return Boot(); int today = _c.Service.Today().Index;
            const string longest = "You found a little room for the garden today, and carried a quiet moment into the evening.";
            Assert.AreEqual(CoachGuards.ReflectionMax, longest.Length);
            for (int i = 0; i < 7; i++) Assert.IsTrue(_c.Service.KeepReflection(today - i, longest));
            yield return Pinch(WeekDialController.CornerId, HandIntentKind.PinchHold);
            yield return Pinch(WeekDialController.CornerId, HandIntentKind.Release);
            yield return SundialPlay.Seconds(1);
            RecordTilesTests.DisableAsyncShaders(); var cam = RecordTilesTests.PoseDial(); RecordTilesTests.AttachPlate(cam);
            yield return null; Capture("DialG1-journal-seven-lines");
            var note = GameObject.Find("JournalWeek");
            var bounds = note.GetComponent<Renderer>().localBounds.size;
            Assert.LessOrEqual(bounds.x, .21f * .72f, "journal text exceeds the paper ink area");
            Assert.LessOrEqual(bounds.y, .17f * .72f, "journal text exceeds the paper ink area");
            float lineHeight = bounds.y / note.GetComponent<TextMesh>().text.Split('\n').Length;
            float pixels = Vector3.Distance(cam.WorldToViewportPoint(note.transform.position),
                cam.WorldToViewportPoint(note.transform.position + note.transform.up * lineHeight)) * 1024;
            Assert.GreaterOrEqual(pixels, 18, "seven journal lines must remain readable at DialG1");
            Assert.AreEqual(7, _c.Service.Save.Journal.WeekEnding(today).Count);
        }
    }
}
