using System;
using System.Collections;
using System.Globalization;
using System.IO;
using GardenVR.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GardenVR.Sundial.Tests.PlayMode
{
    public class FirstRunTests
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
            yield return SundialPlay.Unload();
            SundialService.DevSeedOnFresh = true;
            SundialService.FreshReducedMotion = null;
            SundialController.SaveDirectoryOverride = null;
            SundialController.ClockOverride = null;
            FirstRunWizard.SuppressBreathOffer = false;
            Time.captureDeltaTime = _savedDelta;
        }

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator FirstRun_FirstTendAndDusk()
        {
            SundialController controller = null;
            yield return Boot(false, c => controller = c);
            Assert.AreEqual(LoadOutcome.Fresh, controller.Outcome);
            Assert.IsTrue(controller.Wizard != null && controller.Wizard.Running, "fresh load should start the wizard");

            string script = File.ReadAllText(Path.Combine(Application.dataPath, "Tests/Playback/first-run.jsonl"));
            SundialPlay.Play(controller, script);

            float app = 0f;
            while (app < 175f)
            {
                yield return null;
                app += Time.deltaTime;
                if (controller.Wizard.Step == FirstRunSteps.Done && app > controller.Wizard.RitualDoneAt + 0.5f)
                    break;
            }

            Assert.AreEqual(FirstRunSteps.Done, controller.Service.Save.FirstRunStep);
            Assert.Greater(controller.Wizard.FirstTendAt, 0f, "first tend was not logged");
            Assert.LessOrEqual(controller.Wizard.FirstTendAt, 60f, "firstTendAt=" + controller.Wizard.FirstTendAt.ToString("0.00", CultureInfo.InvariantCulture));
            Assert.Greater(controller.Wizard.RitualDoneAt, controller.Wizard.FirstTendAt);
            Assert.LessOrEqual(controller.Wizard.RitualDoneAt, 180f, "ritualDoneAt=" + controller.Wizard.RitualDoneAt.ToString("0.00", CultureInfo.InvariantCulture));
            Assert.AreEqual(3, controller.Service.Save.Habits.Count);
            Assert.AreEqual("water", controller.Service.HabitForArc("morning").PresetKey);
            Assert.AreEqual("top3", controller.Service.HabitForArc("midday").PresetKey);
            Assert.AreEqual("breaths", controller.Service.HabitForArc("winddown").PresetKey);
            Assert.AreEqual(HabitKind.InAppRitual, controller.Service.HabitForArc("winddown").Kind);
            Assert.GreaterOrEqual(controller.LiveCount("top3"), 1);
            Assert.GreaterOrEqual(controller.LiveCount("breaths"), 1);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator FirstRun_ResumeAfterQuit()
        {
            string dir = SundialPlay.FreshDir();
            SundialController first = null;
            yield return Boot(false, c => first = c, dir);
            string script =
                "{\"t\":5.50,\"intent\":\"Pinch\",\"target\":\"seed.morning\"}\n" +
                "{\"t\":6.10,\"intent\":\"Pinch\",\"target\":\"seed.morning.water\"}\n" +
                "{\"t\":7.00,\"intent\":\"Pinch\",\"target\":\"seed.midday\"}\n" +
                "{\"t\":7.60,\"intent\":\"Pinch\",\"target\":\"seed.midday.top3\"}\n";
            SundialPlay.Play(first, script);
            float app = 0f;
            while (app < 12f)
            {
                yield return null;
                app += Time.deltaTime;
                if (first.Wizard.Step == FirstRunSteps.PickWinddown) break;
            }

            Assert.AreEqual(FirstRunSteps.PickWinddown, first.Service.Save.FirstRunStep);
            Assert.AreEqual("water", first.Service.HabitForArc("morning").PresetKey);
            Assert.AreEqual("top3", first.Service.HabitForArc("midday").PresetKey);
            Assert.IsNull(first.Service.HabitForArc("winddown"));
            Assert.IsTrue(File.Exists(Path.Combine(dir, "save.json")));

            yield return SundialPlay.Unload();
            SundialService.DevSeedOnFresh = false;
            SundialController.SaveDirectoryOverride = dir;
            SundialController.ClockOverride = SundialPlay.Clock1420();
            SundialController second = null;
            yield return SundialPlay.LoadMain(c => second = c);

            Assert.AreEqual(LoadOutcome.Loaded, second.Outcome);
            Assert.IsTrue(second.Wizard.Running, "a saved step should resume the wizard");
            Assert.AreEqual(FirstRunSteps.PickWinddown, second.Wizard.Step);
            Assert.AreEqual(FirstRunSteps.PickWinddown, second.Service.Save.FirstRunStep);
            Assert.AreEqual("water", second.Service.HabitForArc("morning").PresetKey);
            Assert.AreEqual("top3", second.Service.HabitForArc("midday").PresetKey);
            Assert.IsNull(second.Service.HabitForArc("winddown"));
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator FirstRun_NotOnFailedLoad()
        {
            string dir = SundialPlay.FreshDir();
            File.WriteAllText(Path.Combine(dir, "save.json"), "{not-json");
            SundialService.DevSeedOnFresh = false;
            SundialService.FreshReducedMotion = null;
            SundialController.SaveDirectoryOverride = dir;
            SundialController.ClockOverride = SundialPlay.Clock1420();
            Time.captureDeltaTime = 1f / 60f;
            SundialController controller = null;
            yield return SundialPlay.LoadMain(c => controller = c);
            float app = 0f;
            while (app < 0.4f)
            {
                yield return null;
                app += Time.deltaTime;
            }

            Assert.AreEqual(LoadOutcome.Failed, controller.Outcome);
            Assert.IsFalse(controller.Wizard.Running, "a failed load must not start the wizard");
            Assert.IsTrue(controller.Wizard.RestoreVisible, "corrupt save should show the restore prompt");
            Assert.AreNotEqual(FirstRunSteps.Appear, controller.Service.Save.FirstRunStep);
            Assert.AreEqual(0, controller.Service.Save.Habits.Count);
            Assert.IsFalse(FirstRunSteps.IsResume(controller.Service.Save.FirstRunStep));
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator FirstRun_ReducedMotion()
        {
            SundialController controller = null;
            yield return Boot(true, c => controller = c);
            float app = 0f;
            while (app < 0.35f)
            {
                yield return null;
                app += Time.deltaTime;
            }

            Assert.IsTrue(controller.Wizard.Running);
            Assert.GreaterOrEqual(controller.View.appear, 0.99f, "reduced motion draws the dial at once");
            Assert.AreEqual(125f, controller.View.gnomonDeg, 0.75f, "reduced motion leaves the shadow in place");
            Assert.AreEqual(FirstRunSteps.PickMorning, controller.Wizard.Step);
            Assert.IsFalse(controller.View.showPlants, "seeds have not dropped yet");
            Assert.IsNotNull(GameObject.Find("seed.morning"), "packets should already be up");
        }

        static IEnumerator Boot(bool reduced, Action<SundialController> ready, string dir = null)
        {
            SundialService.DevSeedOnFresh = false;
            SundialService.FreshReducedMotion = reduced ? (bool?)true : null;
            SundialController.SaveDirectoryOverride = string.IsNullOrEmpty(dir) ? SundialPlay.FreshDir() : dir;
            SundialController.ClockOverride = SundialPlay.Clock1420();
            Time.captureDeltaTime = 1f / 60f;
            yield return SundialPlay.LoadMain(ready);
        }
    }
}
