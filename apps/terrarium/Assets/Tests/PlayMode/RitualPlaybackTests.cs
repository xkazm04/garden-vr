using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GardenVR.Core;
using GardenVR.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GardenVR.Terrarium.Tests
{
    public class RitualPlaybackTests
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

        [UnityTest]
        [Timeout(360000)]
        public IEnumerator Ritual_SixBreaths_ByPlayback()
        {
            string path = Path.Combine(Application.dataPath, "Tests/Playback/six-breaths.jsonl");
            string jsonl = File.ReadAllText(path);
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            bool mix = Environment.GetEnvironmentVariable("GARDEN_RITUAL_MIX") == "1";
            if (mix) controller.UseVoiceAndBed();
            controller.Mark("SixBreaths");
            ScriptedIntentSource source = RitualHarness.Play(controller, jsonl);
            RitualHarness.FixedStep();

            var releases = new List<ReleaseWatch>();
            source.Intent += intent =>
            {
                if (intent.Kind == HandIntentKind.Release)
                    releases.Add(new ReleaseWatch { Time = controller.AppTime });
            };

            float previous = controller.Snapshot().Uncoil;
            float app = 0f;
            bool finished = false;
            while (app < 90f)
            {
                yield return null;
                app += Time.deltaTime;
                TerrariumState state = controller.Snapshot();
                Assert.GreaterOrEqual(state.Uncoil + 1e-5f, previous, "uncoil fell at t=" + app.ToString("0.00", CultureInfo.InvariantCulture));
                previous = state.Uncoil;
                for (int i = 0; i < releases.Count; i++)
                {
                    ReleaseWatch watch = releases[i];
                    if (watch.Rose) continue;
                    if (app - watch.Time <= 0.5f && state.Fog > 0.2f) watch.Rose = true;
                }
                if (state.Phase == BreathPhase.Complete)
                {
                    finished = true;
                    break;
                }
            }

            for (int i = 0; i < 30; i++) yield return null;
            TerrariumState end = controller.Snapshot();
            Assert.IsTrue(finished, "ritual did not complete: " + end.ToJson());
            Assert.LessOrEqual(app, 90f);
            Assert.AreEqual(6, end.Breaths);
            Assert.AreEqual(BreathPhase.Complete, end.Phase);
            Assert.AreEqual(1, end.Fronds);
            Assert.AreEqual(1f, end.Uncoil, 0.001f);
            Assert.AreEqual(6, releases.Count, "expected one release per breath");
            for (int i = 0; i < releases.Count; i++)
                Assert.IsTrue(releases[i].Rose, "fog did not rise within 0.5 s of release " + i + " at t=" + releases[i].Time.ToString("0.00", CultureInfo.InvariantCulture));
            Assert.AreEqual(6, controller.FilledDots);
            if (mix)
            {
                Assert.GreaterOrEqual(controller.CueCount("bed.night"), 1);
                int spoken = controller.CueCount("vo.ter.open.01") + controller.CueCount("vo.ter.open.02")
                    + controller.CueCount("vo.ter.in.01") + controller.CueCount("vo.ter.in.02") + controller.CueCount("vo.ter.in.03")
                    + controller.CueCount("vo.ter.out.01") + controller.CueCount("vo.ter.mid.01")
                    + controller.CueCount("vo.ter.last.01") + controller.CueCount("vo.ter.close.01");
                Assert.Greater(spoken, 0, "voice guide did not speak during the mix ritual");
            }
            else
            {
                Assert.AreEqual(0, controller.CueCount("bed.night"));
                Assert.AreEqual(0, controller.CueCount("vo.ter.open.01"));
                Assert.AreEqual(0, controller.CueCount("vo.ter.in.01"));
            }
        }

        [UnityTest]
        [Timeout(360000)]
        public IEnumerator Ritual_FidgetsIgnored()
        {
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            RitualHarness.Play(controller, RitualScripts.FidgetsThenSix());
            RitualHarness.FixedStep();
            float app = 0f;
            while (app < 90f && controller.Snapshot().Phase != BreathPhase.Complete)
            {
                yield return null;
                app += Time.deltaTime;
            }
            TerrariumState end = controller.Snapshot();
            int ignored = 0;
            for (int i = 0; i < controller.Session.Events.Count; i++)
            {
                if (controller.Session.Events[i].Kind == BreathEventKind.ShortInhaleIgnored) ignored++;
            }
            Assert.GreaterOrEqual(ignored, 3, "the three short taps should be ignored, not dropped on the floor");
            Assert.AreEqual(6, end.Breaths);
            Assert.AreEqual(BreathPhase.Complete, end.Phase);
            Assert.AreEqual(1, end.Fronds);
        }

        [UnityTest]
        [Timeout(360000)]
        public IEnumerator Ritual_PauseAtBreath3_Resumes()
        {
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            RitualHarness.Play(controller, RitualScripts.PauseAtBreath3());
            RitualHarness.FixedStep();

            bool sawPause = false;
            int breathsAtPause = -1;
            float uncoilAtPause = 0f;
            bool lost = false;
            float app = 0f;
            while (app < 90f && controller.Snapshot().Phase != BreathPhase.Complete)
            {
                yield return null;
                app += Time.deltaTime;
                TerrariumState state = controller.Snapshot();
                if (!sawPause && controller.HoldingBreath && state.Breaths == 2)
                {
                    sawPause = true;
                    breathsAtPause = state.Breaths;
                    uncoilAtPause = state.Uncoil;
                }
                if (sawPause && controller.HoldingBreath)
                {
                    if (state.Breaths != breathsAtPause) lost = true;
                    if (state.Uncoil + 1e-4f < uncoilAtPause) lost = true;
                }
            }

            TerrariumState end = controller.Snapshot();
            Assert.IsTrue(sawPause, "PalmOpen did not hold the jar");
            Assert.IsFalse(lost, "pause dropped a breath or uncoil");
            Assert.AreEqual(2, breathsAtPause);
            Assert.AreEqual(6, end.Breaths);
            Assert.AreEqual(BreathPhase.Complete, end.Phase);
            Assert.AreEqual(1, end.Fronds);
            Assert.GreaterOrEqual(end.Uncoil + 1e-4f, uncoilAtPause);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Ritual_TrackingLost2s_Bridged_Or_Paused()
        {
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            ScriptedIntentSource source = RitualHarness.Play(controller, RitualScripts.TrackingLosses());
            RitualHarness.FixedStep();

            bool inGap = false;
            float gapStart = 0f;
            bool pausedInGap = false;
            bool shortBridged = false;
            bool shortPaused = false;
            bool longPaused = false;
            float app = 0f;
            while (app < 20f)
            {
                yield return null;
                app += Time.deltaTime;
                if (!source.IsTracked)
                {
                    if (!inGap)
                    {
                        inGap = true;
                        gapStart = app;
                        pausedInGap = false;
                    }
                    if (controller.Snapshot().Phase == BreathPhase.Paused) pausedInGap = true;
                }
                else if (inGap)
                {
                    inGap = false;
                    float length = app - gapStart;
                    if (length < 0.6f)
                    {
                        if (pausedInGap) shortPaused = true;
                        else shortBridged = true;
                    }
                    else if (length > 1.5f && pausedInGap)
                    {
                        longPaused = true;
                    }
                }
            }

            Assert.IsTrue(shortBridged, "a 0.3 s loss should be bridged");
            Assert.IsFalse(shortPaused, "a 0.3 s loss paused the breath");
            Assert.IsTrue(longPaused, "a 2 s loss should pause");
        }

        [UnityTest]
        public IEnumerator Ritual_Response_TwoFrames()
        {
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            ScriptedIntentSource source = RitualHarness.Play(controller, RitualScripts.OneHold());
            RitualHarness.FixedStep();

            int pinchUpdate = -1;
            int uncoilUpdate = -1;
            float previous = controller.Snapshot().Uncoil;
            source.Intent += intent =>
            {
                if (intent.Kind == HandIntentKind.PinchHold && pinchUpdate < 0)
                    pinchUpdate = controller.Updates + 1;
            };

            float app = 0f;
            while (app < 2f && uncoilUpdate < 0)
            {
                yield return null;
                app += Time.deltaTime;
                float uncoil = controller.Snapshot().Uncoil;
                if (uncoil > previous + 1e-6f) uncoilUpdate = controller.Updates;
                previous = uncoil;
            }

            Assert.GreaterOrEqual(pinchUpdate, 0, "PinchHold never arrived");
            Assert.GreaterOrEqual(uncoilUpdate, 0, "uncoil never moved");
            Assert.LessOrEqual(uncoilUpdate - pinchUpdate, 2,
                "pinch update " + pinchUpdate + " uncoil update " + uncoilUpdate);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Ritual_AutoPace_Counts()
        {
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            RitualHarness.DisableKeyboard();
            controller.SetAutoPace(true);
            RitualHarness.FixedStep();
            float app = 0f;
            while (app < 12f && controller.Snapshot().Breaths < 1)
            {
                yield return null;
                app += Time.deltaTime;
            }
            TerrariumState end = controller.Snapshot();
            Assert.GreaterOrEqual(end.Breaths, 1, "auto-pace did not count: " + end.ToJson());
            Assert.Greater(end.Uncoil, 0.15f);
        }

        [UnityTest]
        [Timeout(360000)]
        public IEnumerator Ritual_PauseOver60_AsksToContinue()
        {
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            RitualHarness.Play(controller, RitualScripts.LongPause());
            RitualHarness.FixedStep();

            bool sawPrompt = false;
            bool ignored = false;
            int breathsWhileWaiting = -1;
            float uncoilWhileWaiting = 0f;
            bool continued = false;
            float uncoilAfter = 0f;
            float app = 0f;
            while (app < 82f)
            {
                yield return null;
                app += Time.deltaTime;
                TerrariumState state = controller.Snapshot();
                if (controller.AwaitingContinue)
                {
                    sawPrompt = true;
                    IntentTarget prompt;
                    Assert.IsTrue(IntentTargetRegistry.TryGet(JarRitualController.ContinuePromptId, out prompt));
                    Assert.AreEqual("Continue breathing?", EtchedLettering.Read(prompt.gameObject));
                }
                if (sawPrompt && controller.AwaitingContinue && app > 69f && app < 73f)
                {
                    ignored = true;
                    breathsWhileWaiting = state.Breaths;
                    uncoilWhileWaiting = state.Uncoil;
                    Assert.AreEqual(BreathPhase.Inhaling, state.Phase);
                }
                if (sawPrompt && !controller.AwaitingContinue && app > 75f)
                {
                    continued = true;
                    uncoilAfter = state.Uncoil;
                }
            }

            Assert.IsTrue(sawPrompt, "the continue prompt never appeared");
            Assert.IsTrue(ignored, "did not observe the prompt window");
            Assert.AreEqual(1, breathsWhileWaiting);
            Assert.IsTrue(continued, "pinching the prompt did not continue");
            Assert.Greater(uncoilAfter, uncoilWhileWaiting + 0.01f);
        }

        sealed class ReleaseWatch
        {
            public float Time;
            public bool Rose;
        }
    }

    public static class RitualHarness
    {
        public static IEnumerator OpenMain()
        {
            // A fresh garden per test. A second OpenMain in the same test keeps the same clock and file.
            if (string.IsNullOrEmpty(GardenService.DirectoryOverride))
            {
                string dir = Path.Combine(Path.GetTempPath(), "gvr-ter-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(dir);
                GardenService.DirectoryOverride = dir;
            }
            if (GardenService.ClockOverride == null)
                GardenService.ClockOverride = new FixedClock(new DateTimeOffset(2026, 10, 3, 21, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);

            Scene existing = SceneManager.GetSceneByName("Main");
            if (existing.IsValid() && existing.isLoaded)
                yield return SceneManager.UnloadSceneAsync(existing);
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByName("Main");
            Assert.IsTrue(scene.IsValid() && scene.isLoaded, "Main did not load");
            SceneManager.SetActiveScene(scene);
        }

        /// <summary>Drops the injected clock and deletes the temp save. Safe to call twice.</summary>
        public static void ReleaseOverrides()
        {
            string dir = GardenService.DirectoryOverride;
            GardenService.ResetOverrides();
            if (string.IsNullOrEmpty(dir)) return;
            if (dir.IndexOf("gvr-ter-", StringComparison.OrdinalIgnoreCase) < 0) return;
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        public static JarRitualController Controller()
        {
            JarRitualController controller = UnityEngine.Object.FindAnyObjectByType<JarRitualController>();
            Assert.IsNotNull(controller, "Main has no JarRitualController");
            return controller;
        }

        public static void FixedStep()
        {
            Time.captureDeltaTime = 1f / 60f;
        }

        public static void DisableKeyboard()
        {
            KeyboardMouseIntentSource[] all = UnityEngine.Object.FindObjectsByType<KeyboardMouseIntentSource>(FindObjectsInactive.Include);
            for (int i = 0; i < all.Length; i++) all[i].enabled = false;
        }

        public static ScriptedIntentSource Play(JarRitualController controller, string jsonl)
        {
            DisableKeyboard();
            ScriptedIntentSource[] old = UnityEngine.Object.FindObjectsByType<ScriptedIntentSource>(FindObjectsInactive.Include);
            for (int i = 0; i < old.Length; i++)
            {
                if (old[i] != null) UnityEngine.Object.DestroyImmediate(old[i].gameObject);
            }
            var go = new GameObject("ScriptedIntent");
            SceneManager.MoveGameObjectToScene(go, controller.gameObject.scene);
            var source = go.AddComponent<ScriptedIntentSource>();
            source.Load(jsonl);
            controller.SetSource(source);
            return source;
        }
    }

    /// <summary>
    /// A trailing Look keeps <see cref="ScriptedIntentSource"/> ticking after the last Release.
    /// Without it the source marks itself finished and the pinch ramp never falls through 0.5.
    /// </summary>
    public static class RitualScripts
    {
        public static string FidgetsThenSix()
        {
            var sb = new StringBuilder();
            float t = 0f;
            for (int i = 0; i < 3; i++)
            {
                Hold(sb, t, 0.4f);
                t += 0.4f;
                Release(sb, t);
                t += 0.8f;
            }
            AppendSix(sb, t);
            return sb.ToString();
        }

        public static string PauseAtBreath3()
        {
            var sb = new StringBuilder();
            float t = 0f;
            Hold(sb, t, 4.0f); t += 4.0f;
            Release(sb, t); t += 5.0f;
            Hold(sb, t, 4.2f); t += 4.2f;
            Release(sb, t); t += 5.5f;
            Hold(sb, t, 4.5f);
            float inhaleStart = t;
            Line(sb, inhaleStart + 1.8f, "PalmOpen", -1f);
            t = inhaleStart + 1.8f + 10f;
            Hold(sb, t, 4.0f); t += 4.0f;
            Release(sb, t); t += 5.0f;
            Hold(sb, t, 4.2f); t += 4.2f;
            Release(sb, t); t += 5.5f;
            Hold(sb, t, 4.1f); t += 4.1f;
            Release(sb, t); t += 5.2f;
            Hold(sb, t, 4.3f); t += 4.3f;
            Release(sb, t);
            Line(sb, t, "Look", 6f);
            return sb.ToString();
        }

        public static string TrackingLosses()
        {
            var sb = new StringBuilder();
            Hold(sb, 0.5f, 4f);
            Line(sb, 1.2f, "Lost", 0.3f);
            Release(sb, 4.5f);
            Hold(sb, 8f, 4f);
            Line(sb, 9f, "Lost", 2f);
            Release(sb, 12f);
            Line(sb, 12f, "Look", 3f);
            return sb.ToString();
        }

        /// <summary>Six holds just past the minimum inhale and exhale. About 17 seconds of app time.</summary>
        public static string SixShort()
        {
            var sb = new StringBuilder();
            float t = 0f;
            const float hold = 1.5f;
            const float gap = 1.3f;
            for (int i = 0; i < 6; i++)
            {
                Hold(sb, t, hold);
                t += hold;
                Release(sb, t);
                t += gap;
            }
            Line(sb, t - gap, "Look", 2f);
            return sb.ToString();
        }

        public static string OneHold()
        {
            var sb = new StringBuilder();
            Hold(sb, 0.25f, 4f);
            Release(sb, 4.25f);
            Line(sb, 4.25f, "Look", 1f);
            return sb.ToString();
        }

        public static string LongPause()
        {
            var sb = new StringBuilder();
            Hold(sb, 0f, 4f);
            Release(sb, 4f);
            Hold(sb, 6f, 2f);
            Line(sb, 7f, "PalmOpen", -1f);
            Hold(sb, 70f, 2f);
            Release(sb, 72.2f);
            Line(sb, 73f, "Pinch", -1f, JarRitualController.ContinuePromptId);
            Hold(sb, 74.5f, 4f);
            Release(sb, 78.5f);
            Line(sb, 78.5f, "Look", 2f);
            return sb.ToString();
        }

        static void AppendSix(StringBuilder sb, float start)
        {
            float[] holds = { 4f, 4.2f, 4.5f, 4.1f, 4.3f, 4.4f };
            float[] gaps = { 5f, 5.5f, 6f, 5.2f, 5.8f, 6f };
            float t = start;
            for (int i = 0; i < holds.Length; i++)
            {
                Hold(sb, t, holds[i]);
                t += holds[i];
                Release(sb, t);
                t += gaps[i];
            }
            Line(sb, t - gaps[gaps.Length - 1], "Look", 6f);
        }

        static void Hold(StringBuilder sb, float t, float dur)
        {
            Line(sb, t, "PinchHold", dur, "jar");
        }

        static void Release(StringBuilder sb, float t)
        {
            Line(sb, t, "Release", -1f, "jar");
        }

        static void Line(StringBuilder sb, float t, string intent, float dur)
        {
            Line(sb, t, intent, dur, null);
        }

        static void Line(StringBuilder sb, float t, string intent, float dur, string target)
        {
            sb.Append("{\"t\":").Append(t.ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append(",\"intent\":\"").Append(intent).Append("\"");
            if (!string.IsNullOrEmpty(target))
                sb.Append(",\"target\":\"").Append(target).Append("\"");
            if (dur >= 0f)
                sb.Append(",\"dur\":").Append(dur.ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append("}\n");
        }
    }
}
