using System;
using System.Collections;
using System.IO;
using System.Linq;
using GardenVR.Core;
using GardenVR.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GardenVR.Sundial.Tests.PlayMode
{
    // Real PC mapping, including its sticky dwell target. Only raw device sampling is replaced.
    [DefaultExecutionOrder(-100)]
    public sealed class BreakPcInput : MonoBehaviour, IHandIntentSource, IPalmStateSource
    {
        public readonly KbmIntentMapper Mapper = new KbmIntentMapper();
        public RawKbm Raw;
        public Ray Ray = new Ray(Vector3.zero, Vector3.forward);
        public bool Tracked = true;
        public string LastGesture;
        public event Action<HandIntent> Intent { add => Mapper.Intent += value; remove => Mapper.Intent -= value; }
        public event Action PalmReleased { add => Mapper.PalmReleased += value; remove => Mapper.PalmReleased -= value; }
        public event Action SystemPause;
        public bool PalmHeld => Tracked && Mapper.PalmHeld;
        public bool IsPinching => Tracked && Mapper.IsPinching;
        public float PinchStrength => Mapper.PinchStrength;
        public Ray LookRay => Ray;
        public bool IsTracked => Tracked;
        public string BindingHint(HandIntentKind kind) => Mapper.BindingHint(kind);
        public void LoseFocus() { Mapper.CancelPalmHold(); SystemPause?.Invoke(); }
        void Awake() { Mapper.Intent += i => { if (i.Kind != HandIntentKind.Look) LastGesture = i.Kind + ":" + i.TargetId; }; }
        void Update()
        {
            if (!Tracked) { Mapper.CancelPalmHold(); return; }
            Raw.Dt = Time.deltaTime;
            Mapper.Tick(Raw, () => Ray);
        }
    }

    public class BreakControllerTests
    {
        SundialController _dial;
        BreakController _break;
        BreakPcInput _input;
        Camera _camera;
        float _delta;
        static string RunDir => SundialEvidence.Dir("T-SUN-071");
        [SetUp] public void Setup() { _delta = Time.captureDeltaTime; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            yield return SundialPlay.Unload();
            SundialController.SaveDirectoryOverride = null; SundialController.ClockOverride = null;
            Time.captureDeltaTime = _delta;
        }

        IEnumerator Open(bool poses = true)
        {
            yield return SundialPlay.Open(c => _dial = c);
            SundialPlay.FixedStep();
            foreach (var source in UnityEngine.Object.FindObjectsByType<KeyboardMouseIntentSource>(FindObjectsInactive.Include)) source.enabled = false;
            _input = _dial.gameObject.AddComponent<BreakPcInput>();
            _dial.SetSource(_input);
            _dial.Service.Save.Settings.BreakPoses = poses;
            _break = _dial.GetComponent<BreakController>();
            Assert.IsNotNull(_break, "runtime bootstrap must install the controller");
            _camera = GameObject.Find("EyeCamera").GetComponent<Camera>();
            yield return null;
            yield return Click("break.start");
            Assert.IsTrue(_break.Active, "Take a break must be reachable through PC mapping");
            Assert.IsFalse(_dial.Dismissed);
        }

        void Aim(string id)
        {
            Physics.SyncTransforms();
            var target = _dial.GetComponentsInChildren<IntentTarget>().FirstOrDefault(t => t.Id == id);
            Assert.IsNotNull(target, "visible target " + id);
            _input.Ray = new Ray(_camera.transform.position, target.transform.position - _camera.transform.position);
            var hit = IntentTargetRegistry.Raycast(_input.Ray);
            Assert.IsNotNull(hit, "ray must hit " + id);
            Assert.AreEqual(id, hit.Id, "ray must reach " + id);
        }
        void Away() { _input.Ray = new Ray(_camera.transform.position, -_camera.transform.forward); }
        IEnumerator Frames(int frames) { for (int i = 0; i < frames; i++) yield return null; }
        IEnumerator Click(string id)
        {
            Aim(id); _input.Raw.LeftMouse = true; yield return null;
            _input.Raw.LeftMouse = false; yield return null;
        }
        IEnumerator Reach(int mark)
        {
            _input.Raw.P = false; yield return null;
            Aim("mark." + mark); _input.Raw.P = true;
            yield return Frames(40);
            _input.Raw.P = false; yield return null;
            Assert.AreEqual(mark + 1, _break.Session.Stretch.Reaches);
        }
        IEnumerator Stretch()
        {
            for (int i = 0; i < 3; i++) yield return Reach(i);
            Assert.AreEqual(0, _dial.LiveCount("water"), "a break must not tend the morning habit");
        }
        IEnumerator Pose()
        {
            int before = _break.Session.Mobility.Done;
            if (_break.Session.Mobility.Current == HandPose.PalmOpen) _input.Raw.P = true;
            else _input.Raw.Space = true;
            for (int i = 0; i < 240 && _break.Session.Mobility.Done == before; i++) yield return null;
            _input.Raw.P = _input.Raw.Space = false; yield return null;
            Assert.AreEqual(before + 1, _break.Session.Mobility.Done);
        }
        IEnumerator Eyes(float seconds)
        {
            yield return null; Aim("break.eye");
            float target = Mathf.Min(EyeRest.Seconds, _break.Session.Eyes.Rested + seconds);
            for (int i = 0; i < 1300 && _break.Session.Eyes.Rested < target - .001f; i++) yield return null;
            Assert.That(_break.Session.Eyes.Rested, Is.EqualTo(target).Within(.02f));
        }
        void AssertQuiet()
        {
            Assert.IsFalse(_dial.Dismissed);
            Assert.IsFalse(_dial.GetComponent<DuskRitualController>().Active);
            Assert.AreEqual(0, _dial.LiveCount("water"));
            foreach (var text in _dial.GetComponentsInChildren<TextMesh>())
                if (text.name.EndsWith("Prompt")) Assert.IsTrue(text.GetComponent<Renderer>().forceRenderingOff, text.name);
        }
        void Shot(string part)
        {
            Directory.CreateDirectory(RunDir);
            RecordTilesTests.Shot(_camera, Path.Combine(RunDir, "DialG1-break-" + part + ".png"));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Break_FullRun_TendsBreakHabitOnce()
        {
            yield return Open();
            RecordTilesTests.DisableAsyncShaders();
            _camera = RecordTilesTests.PoseDial(); RecordTilesTests.AttachPlate(_camera);
            yield return Frames(4);
            yield return Stretch();
            Assert.AreEqual(BreakPart.Mobility, _break.Session.Part);
            AssertQuiet(); Shot("palm");
            yield return Pose(); Shot("pinch");
            yield return Pose(); yield return Pose();
            Assert.AreEqual(BreakPart.EyeRest, _break.Session.Part);
            yield return Eyes(7); Away(); yield return null;
            Assert.AreEqual(13, _break.Session.Eyes.SecondsLeft);
            Assert.That(Vector3.Distance(_camera.transform.position, _break.View.EyeTarget.position), Is.EqualTo(3).Within(.001));
            AssertQuiet(); Shot("eye");
            yield return Eyes(13);
            Assert.IsTrue(_break.Session.Complete);
            var habit = _dial.Service.Save.Habits.Single(h => h.PresetKey == BreakSession.PresetKey);
            Assert.AreEqual(LifeZone.Body, habit.Zone);
            Assert.AreEqual(1, _dial.LiveCount(habit.Id));
            Assert.AreEqual(TendSource.Ritual, _dial.Ledger.Events.Single(e => e.HabitId == habit.Id).Source);
            Assert.IsFalse(_break.Session.ConsumeTend(), "controller must consume core authorization");
            Assert.IsFalse(_dial.UndoVisible);
            var reloaded = new SundialService(SundialPlay.Clock1420(), SundialController.SaveDirectoryOverride);

            Assert.AreEqual(1, reloaded.LiveCount(habit.Id), "completion must be persisted");
            // A second completed break reuses the habit and the idempotent ritual tend.
            yield return Click("break.start");
            Assert.IsTrue(_break.Active, "restart gesture=" + _input.LastGesture + ", settings=" + _dial.Settings.Open + ", dismissed=" + _dial.Dismissed);
            yield return Stretch();
            yield return Pose(); yield return Pose(); yield return Pose(); yield return Eyes(20);
            Assert.AreEqual(1, _dial.Service.Save.Habits.Count(h => h.PresetKey == BreakSession.PresetKey));
            Assert.AreEqual(1, _dial.LiveCount(habit.Id));
            File.WriteAllText(Path.Combine(RunDir, "controller-capture.txt"),
                "Main runtime BreakController, real KbmIntentMapper via raw-sample test provider.\n" +
                "DialG1 1824x1024; eye ray target at 3m; countdown 13s after 7s.\n" +
                "Two completed breaks; one Body break habit; one persisted Ritual tend; morning untouched.\n");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Break_LookAway_PausesEyeRest()
        {
            yield return Open(false); yield return Stretch();
            Assert.AreEqual(BreakPart.EyeRest, _break.Session.Part, "BreakPoses setting skips mobility");
            yield return Eyes(7); Away();
            float rested = _break.Session.Eyes.Rested;
            yield return Frames(180);
            Assert.AreEqual("break.eye", _input.Mapper.LookTargetId, "exercise sticky PC dwell target");
            Assert.AreEqual(rested, _break.Session.Eyes.Rested);
            Assert.AreEqual(13, _break.Session.Eyes.SecondsLeft);
            yield return Eyes(13); Assert.IsTrue(_break.Session.Complete);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Break_PoseLetGo_KeepsDonePoses()
        {
            yield return Open(); yield return Stretch(); yield return Pose();
            _input.Raw.Space = true; yield return Frames(80);
            _input.Raw.Space = false; yield return null;
            float progress = _break.Session.Mobility.Progress;
            Assert.AreEqual(1, _break.Session.Mobility.Done);
            yield return Frames(180);
            Assert.AreEqual(progress, _break.Session.Mobility.Progress);
            Assert.AreEqual(1, _break.Session.Mobility.Done);
            yield return Pose();
            _input.Raw.P = true; yield return Frames(75);
            _input.Raw.P = false; yield return null;
            progress = _break.Session.Mobility.Progress;
            yield return Frames(180);
            Assert.AreEqual(progress, _break.Session.Mobility.Progress, "released palm must stop counting");
            Assert.AreEqual(2, _break.Session.Mobility.Done);
            yield return Pose(); Assert.AreEqual(BreakPart.EyeRest, _break.Session.Part);
            AssertQuiet();
        }

        IEnumerator PauseAndResume(bool tracking = false)
        {
            if (tracking) _input.Tracked = false; else _input.LoseFocus();
            yield return null;
            Assert.IsTrue(_break.Paused);
            var session = _break.Session;
            int reaches = session.Stretch.Reaches, poses = session.Mobility.Done;
            float progress = session.Mobility.Progress, eyes = session.Eyes.Rested, stretchTime = session.Stretch.Time;
            yield return Frames(180);
            Assert.AreEqual(reaches, session.Stretch.Reaches); Assert.AreEqual(poses, session.Mobility.Done);
            Assert.AreEqual(progress, session.Mobility.Progress); Assert.AreEqual(eyes, session.Eyes.Rested);
            Assert.AreEqual(stretchTime, session.Stretch.Time);
            _input.Tracked = true; _input.Raw.P = _input.Raw.Space = false;
            yield return null; yield return Click("break.resume");
            Assert.IsFalse(_break.Paused); Assert.AreSame(session, _break.Session);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Break_PauseResume_AtEachPart()
        {
            yield return Open(); yield return Reach(0);
            yield return Click("break.pause"); Assert.IsTrue(_break.Paused);
            yield return ReachWhilePaused();
            yield return Click("break.resume"); Assert.IsFalse(_break.Paused);
            yield return PauseAndResume(); yield return Reach(1); yield return Reach(2);
            _input.Raw.P = true; yield return Frames(80);
            yield return PauseAndResume();
            yield return Pose(); yield return Pose();
            _input.Raw.P = true; yield return Frames(80);
            yield return PauseAndResume(true); yield return Pose();
            yield return Eyes(7); yield return PauseAndResume();
            yield return Eyes(13);
            Assert.IsTrue(_break.Session.Complete);
            Assert.AreEqual(1, _dial.Ledger.Events.Count(e => e.Source == TendSource.Ritual));
        }

        IEnumerator ReachWhilePaused()
        {
            Aim("mark.1"); _input.Raw.P = true; yield return Frames(40);
            _input.Raw.P = false; yield return null;
            Assert.AreEqual(1, _break.Session.Stretch.Reaches, "pause slip must gate palm commits too");
        }
    }
}
