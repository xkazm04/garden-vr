using System;
using System.Collections;
using System.IO;
using GardenVR.Core;
using GardenVR.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GardenVR.Sundial.Tests.PlayMode
{
    public class InterviewTests
    {
        sealed class NoSpeech : ISpeechToText
        {
            public event Action<string> Final { add { } remove { } }
            public event Action<string> Partial { add { } remove { } }
            public bool Available => false;
            public void Begin() { Assert.Fail("Unavailable speech must never start"); }
            public void End() { }
        }
        sealed class Hands : IHandIntentSource
        {
            public event Action<HandIntent> Intent;
            public event Action SystemPause;
            public bool IsPinching { get; private set; }
            public Ray LookRay => new Ray(Vector3.zero, Vector3.forward);
            public float PinchStrength => IsPinching ? 1 : 0;
            public bool IsTracked => true;
            public string BindingHint(HandIntentKind kind) => HandBindings.PcHint(kind);
            public void Emit(string target, HandIntentKind kind = HandIntentKind.Pinch)
            {
                IsPinching = kind == HandIntentKind.PinchHold;
                Intent?.Invoke(new HandIntent(kind, LookRay, targetId: target));
            }
            public void Pause() => SystemPause?.Invoke();
        }
        SundialController _c;
        Hands _hands;
        float _delta;
        string _dir;
        string Evidence => SundialEvidence.Dir("T-SUN-062");
        [SetUp] public void Setup() { _delta = Time.captureDeltaTime; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            yield return SundialPlay.Unload();
            SundialController.SaveDirectoryOverride = null; SundialController.ClockOverride = null;
            SundialService.DevSeedOnFresh = true; SundialService.FreshReducedMotion = null;
            Time.captureDeltaTime = _delta;
        }
        IEnumerator Boot(bool reload = false, bool reduced = false)
        {
            if (!reload) _dir = SundialPlay.FreshDir();
            SundialService.DevSeedOnFresh = false; SundialService.FreshReducedMotion = reduced;
            SundialController.SaveDirectoryOverride = _dir; SundialController.ClockOverride = SundialPlay.Clock1420();
            Time.captureDeltaTime = 1f / 60f;
            yield return SundialPlay.LoadMain(c => _c = c);
            _hands = new Hands(); _c.SetSource(_hands);
            _c.GetComponent<SpeechController>().SetSpeech(new NoSpeech());
            yield return null;
        }
        IEnumerator Pinch(string target)
        {
            _hands.Emit(target); yield return null; yield return null;
        }
        IEnumerator Until(string step)
        {
            float end = Time.realtimeSinceStartup + 15;
            while (_c.Wizard.Step != step && Time.realtimeSinceStartup < end) yield return null;
            Assert.AreEqual(step, _c.Wizard.Step);
            yield return null;
        }
        IEnumerator SkipQuestions()
        {
            yield return Until(Onboarding.OptIn);
            Assert.IsFalse(_c.Service.Save.Settings.CoachOptIn);
            yield return Pinch("interview.off");
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(i, _c.Wizard.QuestionIndex);
                Assert.AreEqual(CoachPrompts.OnboardingQuestions[i], _c.Wizard.Question);
                yield return Pinch("interview.skip");
            }
            yield return Until(Onboarding.Review);
        }

        void CheckTargets(Camera camera)
        {
            foreach (var target in _c.GetComponentsInChildren<IntentTarget>())
            {
                if (!target.gameObject.activeInHierarchy || !target.Id.StartsWith("interview.")) continue;
                var collider = target.GetComponent<Collider>();
                Vector3 point = camera.WorldToViewportPoint(collider.bounds.center);
                Assert.That(point.x, Is.InRange(.01f, .99f), target.name + " outside horizontal frame");
                Assert.That(point.y, Is.InRange(.01f, .99f), target.name + " outside vertical frame");
                var ray = new Ray(camera.transform.position, collider.bounds.center - camera.transform.position);
                Assert.IsTrue(Physics.Raycast(ray, out var hit), target.name + " has no hit target");
                var actual = hit.collider.GetComponent<IntentTarget>();
                Assert.IsNotNull(actual, target.name + " occluded by " + hit.collider.name);
                Assert.AreEqual(target.Id, actual.Id, target.name + " occluded by another target");
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator FirstRun_Interview_ScriptedCoach_FirstTendUnder60s()
        {
            var wall = System.Diagnostics.Stopwatch.StartNew();
            yield return Boot();
            yield return SkipQuestions();
            Assert.AreEqual(3, _c.Service.Save.Interview.Proposals.Count);
            Assert.IsFalse(_c.Wizard.ZoneTabsVisible);
            RecordTilesTests.DisableAsyncShaders();
            var cam = RecordTilesTests.PoseDial(); RecordTilesTests.AttachPlate(cam);
            yield return SundialPlay.Seconds(.2f);
            Directory.CreateDirectory(Evidence);
            RecordTilesTests.Shot(cam, Path.Combine(Evidence, "DialG1-onboarding-packets.png"));
            CheckTargets(cam);
            yield return Pinch("interview.packet.0");
            Assert.AreEqual("Walk after lunch", _c.Service.HabitForArc("midday").Name);
            Assert.IsTrue(_c.Wizard.ZoneTabsVisible);
            yield return SundialPlay.Seconds(1);
            RecordTilesTests.Shot(cam, Path.Combine(Evidence, "DialG1-onboarding.png"));
            CheckTargets(cam);
            yield return Pinch("interview.packet.1");
            yield return Pinch("interview.skip.2");
            Assert.AreEqual(2, _c.Service.Save.Habits.Count);
            Assert.IsTrue(_c.Service.Save.Interview.Complete);
            yield return Pinch("interview.done");
            yield return Pinch("plant.midday");
            Assert.Greater(_c.Wizard.FirstTendAt, 0);
            Assert.LessOrEqual(_c.Wizard.FirstTendAt, 60);
            Assert.AreEqual(FirstRunSteps.Done, _c.Wizard.Step);
            Assert.IsFalse(_c.Wizard.Running);
            Assert.IsFalse(_c.GetComponent<DuskRitualController>().OfferVisible, "custom Read must not offer a breath check-in");
            Assert.IsFalse(_c.Service.Save.Settings.CoachOptIn);
            _c.Service.Flush();
            Assert.AreEqual(1, _c.LiveCount(_c.Service.HabitForArc("midday").Id));
            File.WriteAllText(Path.Combine(Evidence, "first-tend.txt"), "CoachOptIn=False; speech unavailable; questions skipped=4\nFirstTendAt=" + _c.Wizard.FirstTendAt + " simulated seconds\nWallSeconds=" + wall.Elapsed.TotalSeconds + " (includes scene load and two captures)\n");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Interview_ConsentAndQuestion_AreVisible_OptInPersists()
        {
            yield return Boot(reduced: true);
            yield return Until(Onboarding.OptIn);
            RecordTilesTests.DisableAsyncShaders();
            var cam = RecordTilesTests.PoseDial(); RecordTilesTests.AttachPlate(cam);
            yield return SundialPlay.Seconds(.2f);
            Directory.CreateDirectory(Evidence);
            RecordTilesTests.Shot(cam, Path.Combine(Evidence, "DialG1-onboarding-consent.png"));
            CheckTargets(cam);
            Assert.IsFalse(_c.Service.Save.Settings.CoachOptIn);
            yield return Pinch("interview.on");
            Assert.IsTrue(_c.Service.Save.Settings.CoachOptIn);
            yield return null;
            RecordTilesTests.Shot(cam, Path.Combine(Evidence, "DialG1-onboarding-question.png"));
            CheckTargets(cam);
            yield return SundialPlay.Unload(); yield return Boot(true);
            Assert.IsTrue(_c.Service.Save.Settings.CoachOptIn);
            Assert.AreEqual(Onboarding.QuestionStep(0), _c.Wizard.Step);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator FirstRun_Interview_Skips_AllFourQuestions()
        {
            yield return Boot(reduced: true);
            yield return SkipQuestions();
            Assert.IsEmpty(_c.GetComponent<SpeechController>().Transcript);
            for (int i = 0; i < 3; i++) yield return Pinch("interview.skip." + i);
            Assert.AreEqual(0, _c.Service.Save.Habits.Count);
            yield return Pinch("interview.done");
            yield return Until(FirstRunSteps.PickMorning);
            foreach (string id in new[] { "seed.morning", "seed.morning.water", "seed.midday", "seed.midday.top3", "seed.winddown", "seed.winddown.breaths" }) yield return Pinch(id);
            yield return Until(FirstRunSteps.FirstTend);
            yield return Pinch("plant.midday");
            Assert.That(_c.Wizard.FirstTendAt, Is.GreaterThan(0).And.LessThanOrEqualTo(60));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator FirstRun_Interview_Resumes_AtSavedStep()
        {
            yield return Boot(reduced: true);
            yield return Pinch("interview.off");
            yield return Pinch("interview.skip");
            yield return Pinch("interview.skip");
            Assert.AreEqual(Onboarding.QuestionStep(2), _c.Wizard.Step);
            yield return SundialPlay.Unload();
            yield return Boot(true);
            Assert.AreEqual(Onboarding.QuestionStep(2), _c.Wizard.Step);
            yield return Pinch("interview.skip"); yield return Pinch("interview.skip");
            yield return Until(Onboarding.Review);
            yield return Pinch("interview.packet.0");
            yield return Pinch("interview.skip.1");
            string id = _c.Service.Save.Habits[0].Id;
            yield return SundialPlay.Unload();
            yield return Boot(true);
            Assert.AreEqual(Onboarding.Review, _c.Wizard.Step);
            Assert.AreEqual(id, _c.Service.Save.Habits[0].Id);
            Assert.IsFalse(_c.Service.Save.Interview.Pending(0));
            Assert.IsFalse(_c.Service.Save.Interview.Pending(1));
            yield return Pinch("interview.packet.0");
            Assert.AreEqual(1, _c.Service.Save.Habits.Count);
            yield return Pinch("interview.packet.2");
            yield return Pinch("interview.done");
            yield return Pinch("plant.midday");
            Assert.Greater(_c.Wizard.FirstTendAt, 0);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Interview_Speech_UnknownCue_ArcChoice_PauseAndPrivacy()
        {
            yield return Boot(reduced: true);
            var speech = _c.GetComponent<SpeechController>();
            var source = new ScriptedSpeechSource();
            source.Load("{\"utterances\":[{\"events\":[{\"t\":0.1,\"kind\":\"final\",\"text\":\"water private-marker-062\"}]}]}");
            speech.SetSpeech(source);
            yield return Pinch("interview.off");
            _hands.Emit("interview.speak", HandIntentKind.PinchHold);
            yield return new WaitForSecondsRealtime(.15f);
            _hands.Emit("interview.speak", HandIntentKind.Release);
            Assert.IsNotEmpty(speech.Transcript);
            yield return Pinch("interview.next");
            Assert.IsEmpty(speech.Transcript);
            for (int i = 1; i < 4; i++) yield return Pinch("interview.skip");
            yield return Until(Onboarding.Review);
            Assert.AreEqual("Water", _c.Service.Save.Interview.Proposals[0].Name);
            yield return Pinch("interview.packet.0");
            Assert.AreEqual(0, _c.Service.Save.Interview.Selected);
            Assert.AreEqual(0, _c.Service.Save.Habits.Count);
            _hands.Pause();
            Assert.IsFalse(speech.Armed); Assert.IsFalse(speech.Listening);
            yield return SundialPlay.Unload(); yield return Boot(true);
            Assert.AreEqual(0, _c.Service.Save.Interview.Selected);
            yield return Pinch("interview.arc.morning");
            Assert.AreEqual("Water", _c.Service.HabitForArc("morning").Name);
            yield return Pinch("interview.done");
            yield return Pinch("plant.morning"); // Current midday arc is empty; the planted arc must work.
            var water = _c.Service.HabitForArc("morning");
            Assert.AreEqual(8, water.Target);
            Assert.AreEqual(-1f, _c.Wizard.FirstTendAt, "a partial quantity is not yet a kept day");
            for (int count = 1; count < water.Target.Value; count++) yield return Pinch("tile.morning.6");
            Assert.Greater(_c.Wizard.FirstTendAt, 0);
            Assert.IsTrue(_c.Ledger.IsKept(water.Id, _c.Service.Today().Index));
            _c.Service.Flush();
            Assert.AreEqual(FirstRunSteps.Done, _c.Wizard.Step);
            yield return SundialPlay.Unload(); yield return Boot(true);
            Assert.IsFalse(_c.Wizard.Running, "a single accepted habit finishes onboarding after its first tend");
            foreach (string file in Directory.GetFiles(_dir)) StringAssert.DoesNotContain("private-marker-062", File.ReadAllText(file));
        }
    }
}
