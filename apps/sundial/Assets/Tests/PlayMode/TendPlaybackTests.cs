using System;
using System.Collections;
using System.IO;
using GardenVR.Core;
using GardenVR.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GardenVR.Sundial.Tests.PlayMode
{
    public class TendPlaybackTests
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
            SundialController.SaveDirectoryOverride = null;
            SundialController.ClockOverride = null;
            Time.captureDeltaTime = _savedDelta;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Tend_LookPinch_ByPlayback()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            Assert.AreEqual(LoadOutcome.Fresh, controller.Outcome);
            string before = controller.StateJson;
            File.WriteAllText(Path.Combine(SundialPlay.EvidenceDir(), "state-before.json"), before);
            Assert.IsTrue(before.Contains("\"gnomonDeg\":125"), before);
            Assert.AreEqual(TileState.Today, SundialPlay.Midday(controller).Window[6]);

            SundialPlay.Play(controller, SundialPlay.LookThenPinch("plant.midday", 0.30f, 0.30f));
            SundialPlay.FixedStep();
            // Pinch lands at 0.30 s. 6.5 s later is 6.80 s, past the 6 s commit.
            yield return SundialPlay.Seconds(6.80f);

            Assert.AreEqual(1, controller.LiveCount("top3"));
            Assert.AreEqual(TileState.Kept, SundialPlay.Midday(controller).Window[6]);
            Assert.AreEqual(1, controller.CueCount(SundialController.CueTock));
            Assert.AreEqual(1, controller.CueCount(SundialController.CueInk));
            string after = controller.StateJson;
            File.WriteAllText(Path.Combine(SundialPlay.EvidenceDir(), "state-after.json"), after);
            Assert.IsTrue(after.Contains("\"Kept\""), after);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Tend_Undo_InsideWindow()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            string script =
                SundialPlay.LookThenPinch("plant.midday", 0.30f, 0.30f) +
                "{\"t\":3.00,\"intent\":\"Pinch\",\"target\":\"undo.midday\"}\n";
            SundialPlay.Play(controller, script);
            SundialPlay.FixedStep();
            yield return SundialPlay.Seconds(8.0f);

            Assert.AreEqual(0, controller.LiveCount("top3"));
            Assert.AreEqual(1, controller.CueCount(SundialController.CueUndo));
            Assert.AreEqual(TileState.Today, SundialPlay.Midday(controller).Window[6]);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Tend_FlushOnPause()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            string script =
                "{\"t\":0.00,\"intent\":\"Pinch\",\"target\":\"plant.midday\"}\n" +
                "{\"t\":1.00,\"intent\":\"PalmOpen\"}\n";
            SundialPlay.Play(controller, script);
            SundialPlay.FixedStep();
            yield return SundialPlay.Seconds(1.40f);

            Assert.AreEqual(1, controller.LiveCount("top3"));
            Assert.AreEqual(TileState.Kept, SundialPlay.Midday(controller).Window[6]);
            Assert.IsFalse(controller.ShiftDay(-1), "a live tend must refuse a day that would put it in the future");
            Assert.AreEqual(1, controller.LiveCount("top3"));
            Assert.IsTrue(controller.ShiftDay(1));
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Tend_SecondSameDay_NoOp()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            string script =
                SundialPlay.LookThenPinch("plant.midday", 0.30f, 0.30f) +
                "{\"t\":7.20,\"intent\":\"Pinch\",\"target\":\"plant.midday\"}\n";
            SundialPlay.Play(controller, script);
            SundialPlay.FixedStep();
            yield return SundialPlay.Seconds(7.60f);

            Assert.AreEqual(1, controller.LiveCount("top3"));
            Assert.AreEqual(1, controller.CueCount(SundialController.CueTock));
            Assert.AreEqual(0, controller.CueCount(SundialController.CueUndo));
            Assert.AreEqual(TileState.Kept, SundialPlay.Midday(controller).Window[6]);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Gnomon_At1420_Is125()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            Assert.AreEqual(125f, controller.State.GnomonDeg, 0.001f);
            Assert.IsTrue(controller.StateJson.Contains("\"gnomonDeg\":125"), controller.StateJson);

            controller.HandleDev(DevCommand.ClockFast);
            Assert.IsTrue(controller.FastClock);
            controller.HandleDev(DevCommand.ClockFast);
            Assert.IsFalse(controller.FastClock);
            controller.HandleDev(DevCommand.StateOverlay);
            Assert.IsTrue(controller.OverlayVisible);

            SundialPlay.FixedStep();
            yield return SundialPlay.Seconds(2.0f);
            Assert.AreEqual(125f, controller.View.gnomonDeg, 0.75f);
            Assert.AreEqual(125f, controller.State.GnomonDeg, 0.001f);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Halo_OnlyAfterDwell()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            Assert.IsFalse(controller.LookHaloOn);
            SundialPlay.Play(controller, "{\"t\":0.00,\"intent\":\"Look\",\"target\":\"plant.morning\",\"dur\":0.10}\n");
            SundialPlay.FixedStep();

            float maxHalo = controller.View.halo;
            float app = 0f;
            while (app < 0.45f)
            {
                yield return null;
                app += Time.deltaTime;
                if (controller.View.halo > maxHalo) maxHalo = controller.View.halo;
            }

            Assert.Less(maxHalo, 0.5f, "a 0.1 s glance must not light the halo");
            Assert.IsFalse(controller.LookHaloOn);
            Assert.AreEqual(0f, controller.View.halo, 0.001f);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Save_SurvivesRelaunch()
        {
            string dir = SundialPlay.FreshDir();
            SundialController.SaveDirectoryOverride = dir;
            SundialController.ClockOverride = SundialPlay.Clock1420();
            SundialController first = null;
            yield return SundialPlay.LoadMain(c => first = c);
            SundialPlay.Play(first, SundialPlay.LookThenPinch("plant.midday", 0.30f, 0.30f));
            SundialPlay.FixedStep();
            yield return SundialPlay.Seconds(6.80f);
            Assert.AreEqual(1, first.LiveCount("top3"));
            Assert.IsTrue(File.Exists(Path.Combine(dir, "save.json")));

            yield return SundialPlay.Unload();
            SundialController.ClockOverride = SundialPlay.Clock1420();
            SundialController.SaveDirectoryOverride = dir;
            SundialController second = null;
            yield return SundialPlay.LoadMain(c => second = c);

            Assert.AreEqual(LoadOutcome.Loaded, second.Outcome);
            Assert.AreEqual(SundialService.DevSeedStep, second.Service.Save.FirstRunStep);
            Assert.AreEqual(1, second.LiveCount("top3"));
            Assert.AreEqual(TileState.Kept, SundialPlay.Midday(second).Window[6]);
            Assert.IsTrue(File.Exists(Path.Combine(dir, "save.json")));
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Response_TwoFrames()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            ScriptedIntentSource source = SundialPlay.Play(controller, "{\"t\":0.00,\"intent\":\"Pinch\",\"target\":\"plant.midday\"}\n");
            SundialPlay.FixedStep();

            int pinchFrame = -1;
            source.Intent += intent =>
            {
                if (pinchFrame < 0 && intent.Kind == HandIntentKind.Pinch)
                    pinchFrame = Time.frameCount;
            };

            float app = 0f;
            while (pinchFrame < 0 && app < 2f)
            {
                yield return null;
                app += Time.deltaTime;
            }

            Assert.GreaterOrEqual(pinchFrame, 0, "pinch did not fire");
            Assert.LessOrEqual(Time.frameCount - pinchFrame, 2);
            Assert.IsTrue(controller.UndoVisible || controller.View.pulse > 0f, "no visual on the pinch frame");
        }
    }

    public static class SundialPlay
    {
        /// <summary>
        /// The test player boots Main before the first test. Pin the save away from the real
        /// persistent path so that boot controller cannot write the user's dial file.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void IsolateBootSave()
        {
            SundialController.SaveDirectoryOverride = FreshDir();
            SundialController.ClockOverride = Clock1420();
        }

        public static FixedClock Clock1420()
        {
            var now = new DateTimeOffset(2026, 10, 3, 14, 20, 0, TimeSpan.Zero);
            return new FixedClock(now, TimeZoneInfo.Utc);
        }

        public static string FreshDir()
        {
            string dir = Path.Combine(Application.temporaryCachePath, "tsun008", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string EvidenceDir()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            string dir = Path.Combine(root, "orchestration", "runs", "sundial", "T-SUN-008");
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string LookThenPinch(string target, float lookDur, float pinchAt)
        {
            return "{\"t\":0.00,\"intent\":\"Look\",\"target\":\"" + target + "\",\"dur\":" + lookDur.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "}\n" +
                   "{\"t\":" + pinchAt.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ",\"intent\":\"Pinch\",\"target\":\"" + target + "\"}\n";
        }

        public static IEnumerator Open(Action<SundialController> ready)
        {
            SundialController.SaveDirectoryOverride = FreshDir();
            SundialController.ClockOverride = Clock1420();
            yield return LoadMain(ready);
        }

        public static IEnumerator LoadMain(Action<SundialController> ready)
        {
            yield return Unload();
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByName("Main");
            Assert.IsTrue(scene.IsValid() && scene.isLoaded, "Main did not load");
            SceneManager.SetActiveScene(scene);
            SundialController controller = UnityEngine.Object.FindAnyObjectByType<SundialController>();
            if (controller == null)
            {
                DialView view = UnityEngine.Object.FindAnyObjectByType<DialView>();
                Assert.IsNotNull(view, "Main has no DialView");
                controller = view.gameObject.AddComponent<SundialController>();
            }
            Assert.IsNotNull(controller.Service, "controller has no service");
            Assert.IsNotNull(controller.View, "controller has no view");
            if (ready != null) ready(controller);
        }

        public static IEnumerator Unload()
        {
            Scene scene = SceneManager.GetSceneByName("Main");
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
        }

        public static void FixedStep()
        {
            Time.captureDeltaTime = 1f / 60f;
        }

        public static ScriptedIntentSource Play(SundialController controller, string jsonl)
        {
            KeyboardMouseIntentSource[] keys = UnityEngine.Object.FindObjectsByType<KeyboardMouseIntentSource>(FindObjectsInactive.Include);
            for (int i = 0; i < keys.Length; i++) keys[i].enabled = false;
            var go = new GameObject("ScriptedIntent");
            SceneManager.MoveGameObjectToScene(go, controller.gameObject.scene);
            var source = go.AddComponent<ScriptedIntentSource>();
            source.Load(jsonl);
            controller.SetSource(source);
            return source;
        }

        public static IEnumerator Seconds(float seconds)
        {
            float app = 0f;
            while (app < seconds)
            {
                yield return null;
                app += Time.deltaTime;
            }
        }

        public static PlantState Midday(SundialController controller)
        {
            HabitDef habit = controller.Service.HabitForArc("midday");
            Assert.IsNotNull(habit, "midday habit missing");
            Assert.AreEqual("top3", habit.Id);
            PlantState plant = controller.Service.PlantFor(habit);
            Assert.IsNotNull(plant, "midday plant missing");
            Assert.IsNotNull(plant.Window);
            Assert.GreaterOrEqual(plant.Window.Length, 7);
            return plant;
        }
    }
}
