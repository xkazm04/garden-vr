using System;
using System.Collections;
using System.IO;
using GardenVR.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GardenVR.Sundial.Tests.PlayMode
{
    public class SpeechTests
    {
        sealed class Hands : IHandIntentSource
        {
            public event Action<HandIntent> Intent;
            public event Action SystemPause;
            public bool IsPinching { get; private set; }
            public Ray LookRay => new Ray(Vector3.zero, Vector3.forward);
            public float PinchStrength => IsPinching ? 1 : 0;
            public bool IsTracked { get; set; } = true;
            public string BindingHint(HandIntentKind kind) => HandBindings.PcHint(kind);
            public void Emit(HandIntentKind kind, string target = "speech.answer")
            {
                if (kind == HandIntentKind.PinchHold) IsPinching = true;
                if (kind == HandIntentKind.Release) IsPinching = false;
                Intent?.Invoke(new HandIntent(kind, LookRay, targetId: target));
            }
            public void Pause() { SystemPause?.Invoke(); }
        }
        sealed class Device : ISpeechToText, ISpeechCaptureState
        {
            public event Action<string> Final;
            public event Action<string> Partial;
            public bool Available { get; set; } = true;
            public bool Listening { get; set; }
            public int Begins, Ends;
            public void Begin() { Begins++; Listening = Available; }
            public void End() { Ends++; Listening = false; }
            public void Result(string text) { Final?.Invoke(text); }
            public void Hypothesis(string text) { Partial?.Invoke(text); }
        }
        GameObject _go;
        SpeechController _speech;
        Hands _hands;
        Device _device;
        [SetUp] public void Setup()
        {
            _go = new GameObject("Speech test");
            _speech = _go.AddComponent<SpeechController>();
            _hands = new Hands(); _device = new Device();
            _speech.SetSource(_hands); _speech.SetSpeech(_device);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            UnityEngine.Object.Destroy(_go);
            yield return SundialPlay.Unload();
            SundialController.SaveDirectoryOverride = null;
            SundialController.ClockOverride = null;
            SundialService.DevSeedOnFresh = false;
        }
        [UnityTest] public IEnumerator Hold_IsOptInAndTargeted_ReleaseAllowsFinal_ClearRejectsLateText()
        {
            _hands.Emit(HandIntentKind.PinchHold);
            Assert.AreEqual(0, _device.Begins);
            _hands.Emit(HandIntentKind.Release);
            _speech.Arm("speech.answer");
            _hands.Emit(HandIntentKind.PinchHold, "plant.midday");
            Assert.AreEqual(0, _device.Begins);
            _hands.Emit(HandIntentKind.PinchHold);
            _hands.Emit(HandIntentKind.PinchHold);
            Assert.AreEqual(1, _device.Begins);
            Assert.IsTrue(_speech.RippleVisible);
            _device.Hypothesis("temporary");
            Assert.AreEqual("temporary", _speech.PartialText);
            _hands.Emit(HandIntentKind.Release, "elsewhere");
            Assert.IsFalse(_speech.Listening);
            Assert.IsFalse(_speech.RippleVisible);
            _device.Result("final after stop");
            Assert.AreEqual("final after stop", _speech.Transcript);
            _speech.CompleteCoachCall();
            _device.Result("late native callback");
            Assert.IsEmpty(_speech.Transcript);
            Assert.IsEmpty(_speech.PartialText);
            yield return null;
        }
        [UnityTest] public IEnumerator Pause_DisarmsAndClears_DoesNotRestartFromHeldInput()
        {
            _speech.Arm(); _hands.Emit(HandIntentKind.PinchHold);
            _device.Result("private answer"); _hands.Pause();
            Assert.IsFalse(_speech.Listening); Assert.IsEmpty(_speech.Transcript);
            _speech.Arm(); _hands.Emit(HandIntentKind.PinchHold);
            Assert.AreEqual(1, _device.Begins);
            _hands.Emit(HandIntentKind.Release); _hands.Emit(HandIntentKind.PinchHold);
            Assert.AreEqual(2, _device.Begins);
            yield return null;
        }
        [UnityTest] public IEnumerator TrackingLoss_AndDisable_StopCapture()
        {
            _speech.Arm(); _hands.Emit(HandIntentKind.PinchHold);
            _hands.IsTracked = false;
            yield return null;
            Assert.IsFalse(_device.Listening); Assert.IsFalse(_speech.Armed);
            _hands.IsTracked = true; _hands.Emit(HandIntentKind.Release);
            _speech.Arm(); _hands.Emit(HandIntentKind.PinchHold);
            _speech.enabled = false;
            Assert.IsFalse(_device.Listening); Assert.IsFalse(_speech.RippleVisible);
            _hands.Emit(HandIntentKind.Release); _hands.Emit(HandIntentKind.PinchHold);
            Assert.AreEqual(2, _device.Begins);
        }
        [UnityTest] public IEnumerator DeviceFailure_HidesListening_UnavailableDoesNotBegin()
        {
            _device.Available = false; _speech.Arm(); _hands.Emit(HandIntentKind.PinchHold);
            Assert.AreEqual(0, _device.Begins);
            _hands.Emit(HandIntentKind.Release); _device.Available = true;
            _hands.Emit(HandIntentKind.PinchHold);
            _device.Listening = false; _device.Available = false;
            yield return null;
            Assert.IsFalse(_speech.RippleVisible); Assert.IsFalse(_speech.Listening);
            Assert.IsTrue(_speech.UsingPresetFallback);
            Assert.IsFalse(_speech.Available);
            _hands.Emit(HandIntentKind.PinchHold);
            Assert.AreEqual(1, _device.Begins);
        }
        [UnityTest, Timeout(180000)] public IEnumerator ScriptedFile_Intents_NoTranscriptInSave_Captures()
        {
            UnityEngine.Object.Destroy(_go);
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            yield return null;
            _speech = controller.GetComponent<SpeechController>();
            Assert.IsNotNull(_speech);
            var scripted = new ScriptedSpeechSource();
            scripted.Load(File.ReadAllText(Path.Combine(Application.dataPath, "Tests/Playback/speech.json")));
            _speech.SetSpeech(scripted);
            var intents = SundialPlay.Play(controller, "{\"t\":0.1,\"intent\":\"PinchHold\",\"target\":\"speech.answer\",\"dur\":20}\n{\"t\":20.1,\"intent\":\"Release\"}\n");
            _speech.Arm("speech.answer");
            intents.enabled = false;
            intents.Tick(0.2f);
            scripted.Tick(1f);
            Assert.AreEqual("private copper lantern beside the window", _speech.Transcript);
            Assert.IsTrue(_speech.Listening);
            yield return SundialPlay.Seconds(2);
            RecordTilesTests.DisableAsyncShaders();
            Camera camera = RecordTilesTests.PoseDial();
            RecordTilesTests.AttachPlate(camera);
            string dir = SundialEvidence.Dir("T-SUN-060");
            Directory.CreateDirectory(dir);
            yield return null;
            Color32[] listening = RecordTilesTests.Shot(camera, Path.Combine(dir, "DialG1-listening.png"));
            intents.Tick(21);
            Assert.IsFalse(_speech.Listening); Assert.IsFalse(_speech.RippleVisible);
            Color32[] released = RecordTilesTests.Shot(camera, Path.Combine(dir, "DialG1-released.png"));
            int changed = 0;
            for (int i = 0; i < listening.Length; i++) if (!listening[i].Equals(released[i])) changed++;
            Assert.Greater(changed, 100, "The listening indicator must actually render in the capture.");
            controller.Service.Flush();
            string saveDir = SundialController.SaveDirectoryOverride;
            foreach (string path in Directory.GetFiles(saveDir, "*", SearchOption.AllDirectories))
                StringAssert.DoesNotContain("private copper", File.ReadAllText(path), path);
            _speech.CompleteCoachCall();
            Assert.IsEmpty(_speech.Transcript);
        }
    }
}
