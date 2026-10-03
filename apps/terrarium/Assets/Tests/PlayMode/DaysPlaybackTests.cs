using System;
using System.Collections;
using System.IO;
using GardenVR.Core;
using GardenVR.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GardenVR.Terrarium.Tests
{
    public class DaysPlaybackTests
    {
        float _savedDelta;

        [SetUp]
        public void SaveStep()
        {
            _savedDelta = Time.captureDeltaTime;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            Time.captureDeltaTime = _savedDelta;
            Scene scene = SceneManager.GetSceneByName("Main");
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
            RitualHarness.ReleaseOverrides();
        }

        [Test]
        public void Slots_InnerLayerAndLean()
        {
            Vector3 outer = JarView.FrondSlot(0);
            Vector3 inner = JarView.FrondSlot(Garden.FrondsBeforeInnerLayer);
            float outerRadius = new Vector2(outer.x, outer.z).magnitude;
            float innerRadius = new Vector2(inner.x, inner.z).magnitude;
            Assert.Less(innerRadius, outerRadius);
            Assert.AreEqual(0.031f, outer.y, 0.0001f);
            Assert.AreEqual(0f, JarView.LeanDegrees(1f), 0.0001f);
            Assert.AreEqual(6f, JarView.LeanDegrees(Garden.VitalityFloor), 0.0001f);
            Assert.LessOrEqual(JarView.LeanDegrees(0f), 6f);
            float lean = JarView.LeanDegrees(0.70f);
            Assert.Greater(lean, 0f);
            Assert.LessOrEqual(lean, 6f);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Days_MissedThenRecovered()
        {
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            DateTimeOffset start = controller.Service.Now;

            yield return PlayOne(controller);
            Assert.AreEqual(1, controller.Garden.Fronds);
            Assert.AreEqual(1f, controller.Garden.Vitality(controller.Service.TodayIndex), 0.001f);
            Assert.IsFalse(controller.LastAnswer.Recovered);

            Assert.IsFalse(controller.ShiftDay(-1), "a day before the last ritual must be refused");
            Assert.AreEqual(start, controller.Service.Now);
            Assert.IsTrue(controller.ShiftDay(1));
            Assert.IsTrue(controller.ShiftDay(-1));
            Assert.AreEqual(start, controller.Service.Now);

            Assert.IsTrue(controller.ShiftDay(3));
            int missed = controller.Service.TodayIndex;
            Assert.AreEqual(0.70f, controller.Garden.Vitality(missed), 0.001f);
            Assert.AreEqual(1, controller.Garden.Fronds);
            controller.BeginEvening();
            Assert.IsTrue(controller.View.FiddleQuiet);
            Assert.Greater(controller.View.ShownLean, 0f);
            Assert.LessOrEqual(controller.View.ShownLean, 6f);

            yield return PlayOne(controller);
            Assert.IsTrue(controller.LastAnswer.Recovered);
            Assert.IsTrue(controller.LastAnswer.NewFrond);
            Assert.AreEqual(2, controller.Garden.Fronds);
            Assert.AreEqual(1f, controller.Garden.Vitality(controller.Service.TodayIndex), 0.001f);

            float seen = -1f;
            for (int i = 0; i < 180; i++)
            {
                yield return null;
                float t = controller.AnswerTime;
                if (t > 0.3f && t < 2.2f)
                {
                    seen = controller.View.recoveredTime;
                    GameObject ripple = GameObject.Find("RecoveredRipple");
                    Assert.IsNotNull(ripple, "recovered moss ripple was not built");
                    Assert.IsTrue(ripple.activeInHierarchy);
                    break;
                }
            }
            Assert.Greater(seen, 0.2f);
            Assert.Less(seen, 2.4f);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Days_WeekFlowers()
        {
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            int start = controller.Service.TodayIndex;

            for (int d = 0; d < 7; d++)
            {
                if (d == 5)
                {
                    Assert.IsTrue(controller.ShiftDay(1));
                    continue;
                }
                controller.BeginEvening();
                yield return PlayOne(controller);
                Assert.AreEqual(d < 5 ? d + 1 : d, controller.Garden.Fronds);
                if (d < 6) Assert.IsTrue(controller.ShiftDay(1));
            }

            Assert.AreEqual(start + 6, controller.Service.TodayIndex);
            Assert.AreEqual(6, controller.Garden.Fronds);
            Assert.AreEqual(1, controller.Garden.Flowers);
            Assert.AreEqual(1f, controller.Garden.Vitality(controller.Service.TodayIndex), 0.001f);
            Assert.IsNotNull(GameObject.Find("Flower0"), "day 7 did not open a flower");
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Save_SurvivesRelaunch()
        {
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            yield return PlayOne(controller);
            controller.BeginEvening();
            yield return PlayOne(controller);
            Assert.AreEqual(1, controller.Garden.Fronds);
            Assert.AreEqual(1, controller.Garden.DewToday);
            Assert.AreEqual(1, controller.View.ShownDew);
            int fronds = controller.Garden.Fronds;
            int dew = controller.Garden.DewToday;
            int today = controller.Service.TodayIndex;

            yield return RitualHarness.OpenMain();
            controller = RitualHarness.Controller();
            yield return null;
            yield return null;
            Assert.AreEqual(fronds, controller.Garden.Fronds);
            Assert.AreEqual(dew, controller.Garden.DewToday);
            Assert.AreEqual(today, controller.Garden.LastRitualDay);
            Assert.AreEqual(dew, controller.View.ShownDew);

            string live = Path.Combine(GardenService.DirectoryOverride, SaveStore<TerrariumSave>.LiveName);
            string repo = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            string destDir = Path.Combine(repo, "orchestration", "runs", "terrarium", "T-TER-008");
            Directory.CreateDirectory(destDir);
            File.Copy(live, Path.Combine(destDir, "save.json"), true);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Save_CorruptOffersBackup()
        {
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            yield return PlayOne(controller);
            controller.Service.Save();
            string dir = GardenService.DirectoryOverride;
            string live = Path.Combine(dir, SaveStore<TerrariumSave>.LiveName);
            string prev = Path.Combine(dir, SaveStore<TerrariumSave>.Prev1Name);
            Assert.IsTrue(File.Exists(prev), "a rotated backup was not written");
            File.WriteAllBytes(live, new byte[0]);

            yield return RitualHarness.OpenMain();
            controller = RitualHarness.Controller();
            for (int i = 0; i < 8; i++) yield return null;

            Assert.AreEqual(LoadOutcome.Failed, controller.Service.Outcome);
            Assert.IsFalse(controller.FirstRunStarted);
            Assert.AreEqual(0, controller.Garden.Fronds);
            Assert.AreEqual(0, controller.Updates);
            Assert.AreEqual(0, new FileInfo(live).Length);
            Assert.IsTrue(controller.RestorePromptVisible);
            GameObject prompt = GameObject.Find("RestorePrompt");
            Assert.IsNotNull(prompt);
            TextMesh text = prompt.GetComponent<TextMesh>();
            Assert.IsNotNull(text);
            Assert.AreEqual("restore the last copy?", text.text);
            IntentTarget target = prompt.GetComponent<IntentTarget>();
            Assert.IsNotNull(target);
            Assert.AreEqual(JarRitualController.RestorePromptId, target.Id);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Day_BoundaryAt0300()
        {
            var clock = new FixedClock(new DateTimeOffset(2026, 10, 4, 2, 30, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
            GardenService.ClockOverride = clock;
            int gardenDay = GardenDay.From(clock.Now, GardenDay.DefaultBoundary).Index;
            int noon = GardenDay.From(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero), GardenDay.DefaultBoundary).Index;
            Assert.AreEqual(noon - 1, gardenDay);

            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            yield return PlayOne(controller);
            Assert.AreEqual(gardenDay, controller.Service.TodayIndex);
            Assert.AreEqual(gardenDay, controller.Garden.LastRitualDay);
            Assert.AreEqual(1, controller.Garden.Fronds);
        }

        static IEnumerator PlayOne(JarRitualController controller)
        {
            RitualHarness.FixedStep();
            RitualHarness.Play(controller, RitualScripts.SixShort());
            float app = 0f;
            while (app < 30f)
            {
                yield return null;
                app += Time.deltaTime;
                if (controller.HasAnswer && controller.Session.Phase == BreathPhase.Complete)
                    yield break;
            }
            Assert.Fail("ritual did not complete: " + controller.Snapshot().ToJson());
        }
    }
}
