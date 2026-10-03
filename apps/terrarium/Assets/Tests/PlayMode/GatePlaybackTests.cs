using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using GardenVR.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GardenVR.Terrarium.Tests
{
    /// <summary>
    /// Gate playback. Reduced motion shows end states, a pinch hold answers on the same
    /// frame it is raised, and the four canonical journeys settle to one JSON document.
    /// </summary>
    public class GatePlaybackTests
    {
        float _savedDelta;
        string _logPath;

        [SetUp]
        public void SaveStep()
        {
            _savedDelta = Time.captureDeltaTime;
            _logPath = Path.Combine(Path.GetTempPath(), "gvr-ter-log-" + Guid.NewGuid().ToString("N") + ".jsonl");
            JarRitualController.LogPathOverride = _logPath;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            Time.captureDeltaTime = _savedDelta;
            JarRitualController.LogPathOverride = null;
            GardenService.ReducedMotionOverride = null;
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("Main");
            if (scene.IsValid() && scene.isLoaded)
                yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
            RitualHarness.ReleaseOverrides();
            if (!string.IsNullOrEmpty(_logPath) && File.Exists(_logPath))
            {
                try { File.Delete(_logPath); }
                catch (IOException) { }
            }
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator ReducedMotion_ShowsEndStates()
        {
            GardenService.ReducedMotionOverride = true;
            RitualHarness.FixedStep();
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsTrue(controller.Service.Settings.ReducedMotion);
            Assert.IsTrue(controller.View.reducedMotion);

            Transform plume = controller.View.mist != null && controller.View.mist.Length > 0 ? controller.View.mist[0] : null;
            Assert.IsNotNull(plume, "the mist plume is missing");
            Vector3 mistAt = plume.localPosition;

            RitualHarness.Play(controller, RitualScripts.SixShort());
            bool sawShare = false;
            bool sawFogStep = false;
            bool sawStaticRing = false;
            float ringFill = -1f;
            bool sawAnswer = false;
            float app = 0f;
            while (app < 30f && !sawAnswer)
            {
                yield return null;
                app += Time.deltaTime;
                TerrariumState state = controller.Snapshot();
                if (!sawShare && state.Phase == BreathPhase.Inhaling && controller.Session.PhaseTime >= 1.35f && state.Breaths == 0)
                {
                    sawShare = true;
                    Assert.AreEqual(1f / 6f, controller.View.uncoil, 0.02f, "the earned uncoil did not appear at once");
                    Assert.Less(state.Uncoil, controller.View.uncoil - 0.03f, "the session clock was snapped too");
                }
                if (sawShare && !sawFogStep && state.Phase == BreathPhase.Exhaling && state.Fog < 0.75f && state.Fog > 0.05f)
                {
                    sawFogStep = true;
                    Assert.AreEqual(1f, controller.View.fog, 0.001f, "fog swept instead of stepping");
                }
                if (!sawStaticRing && state.Phase == BreathPhase.Inhaling && state.Breaths == 1)
                {
                    float fill = PaceFill(controller);
                    if (ringFill < 0f) ringFill = fill;
                    else if (controller.Session.PhaseTime > 0.25f)
                    {
                        sawStaticRing = true;
                        Assert.AreEqual(1f / 6f, fill, 0.02f);
                        Assert.AreEqual(ringFill, fill, 0.001f, "the pace ring kept sweeping");
                    }
                }
                if (controller.HasAnswer && controller.AnswerTime < 0.35f)
                {
                    sawAnswer = true;
                    Assert.GreaterOrEqual(controller.View.answerTime, 2.5f, "the answer was still in motion");
                    Assert.AreEqual(-1f, controller.View.recoveredTime, 0.001f);
                }
            }

            Assert.IsTrue(sawShare, "the first hold never passed the fidget window");
            Assert.IsTrue(sawFogStep, "fog never decayed while the view held a step");
            Assert.IsTrue(sawStaticRing, "the pace ring was not sampled on the second breath");
            Assert.IsTrue(sawAnswer, "the ritual did not answer: " + controller.Snapshot().ToJson());
            Assert.Less((plume.localPosition - mistAt).magnitude, 0.0001f, "reduced motion drifted the mist");
            Assert.AreEqual(6, controller.Session.Breaths);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator Response_WithinTwoFrames()
        {
            RitualHarness.FixedStep();
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsNotNull(controller.View);
            if (File.Exists(_logPath)) File.Delete(_logPath);

            RitualHarness.Play(controller, OneHold());
            for (int i = 0; i < 6; i++) yield return null;

            Assert.IsTrue(controller.HoldAcknowledged, "the pinch hold did not reach the jar");
            Assert.AreEqual(JarRitualController.HoldAckTint.r, controller.PaceTint.r, 0.01f);
            Assert.AreEqual(JarRitualController.HoldAckTint.g, controller.PaceTint.g, 0.01f);
            Assert.IsTrue(File.Exists(_logPath), "the ritual log was not written");
            string text = File.ReadAllText(_logPath);
            float intent = -1f;
            float changed = -1f;
            string[] lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                if (intent < 0f && lines[i].Contains("\"event\":\"intent\"")) intent = ReadT(lines[i]);
                if (changed < 0f && lines[i].Contains("\"event\":\"state-changed\"")) changed = ReadT(lines[i]);
            }
            Assert.GreaterOrEqual(intent, 0f, text);
            Assert.GreaterOrEqual(changed, 0f, text);
            float dt = changed - intent;
            Debug.Log("response-dt=" + dt.ToString("0.######", CultureInfo.InvariantCulture));
            Assert.LessOrEqual(dt, 2f / 60f + 0.0001f, "visible response lagged the pinch");
            Assert.GreaterOrEqual(dt, -0.0001f);
        }

        [UnityTest]
        [Timeout(360000)]
        public IEnumerator Canonical_EndState()
        {
            RitualHarness.FixedStep();
            var first = new Settled();
            var normal = new Settled();
            var missed = new Settled();
            var day7 = new Settled();
            yield return SettleFresh(first);
            yield return SettleNormal(normal);
            yield return SettleMissed(missed);
            yield return SettleDay7(day7);
            string json = "{\"firstRun\":" + first.Json
                + ",\"normalDay\":" + normal.Json
                + ",\"missedDay\":" + missed.Json
                + ",\"day7\":" + day7.Json + "}";
            string hash = Sha256(json);
            Debug.Log("endstate-sha256=" + hash);
            Debug.Log("endstate-json=" + json);
            string dir = Environment.GetEnvironmentVariable("GARDEN_ENDSTATE_DIR");
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "end-state.json"), json);
                File.WriteAllText(Path.Combine(dir, "end-state.sha256"), hash + "\n");
            }
            Assert.IsTrue(json.Contains("\"fronds\":1"));
            Assert.IsTrue(json.Contains("\"fronds\":2"));
            Assert.IsTrue(json.Contains("\"fronds\":6"));
            Assert.IsTrue(json.Contains("\"phase\":\"Complete\""));
            Assert.IsTrue(json.Contains("\"phase\":\"Waiting\""));
        }

        sealed class Settled
        {
            public string Json;
        }

        IEnumerator SettleFresh(Settled slot)
        {
            RitualHarness.ReleaseOverrides();
            string dir = NewDir();
            GardenService.DirectoryOverride = dir;
            GardenService.ClockOverride = new FixedClock(GardenJourney.Origin, TimeZoneInfo.Utc);
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            RitualHarness.Play(controller, RitualScripts.SixShort());
            float app = 0f;
            bool settled = false;
            while (app < 40f)
            {
                yield return null;
                app += Time.deltaTime;
                TerrariumState state = controller.Snapshot();
                string step = controller.Director != null ? controller.Director.Step : "";
                if (state.Phase == BreathPhase.Complete && state.Breaths == 6 && state.Fog == 0f
                    && state.Uncoil >= 1f && step == FirstRunSteps.Done)
                {
                    settled = true;
                    break;
                }
            }
            Assert.IsTrue(settled, "first run did not settle: " + controller.Snapshot().ToJson());
            yield return null;
            slot.Json = Capture(controller);
        }

        IEnumerator SettleNormal(Settled slot)
        {
            var clock = new FixedClock(GardenJourney.Origin, TimeZoneInfo.Utc);
            var garden = new Garden();
            garden.CompleteRitual(GardenDay.From(clock.Now, GardenDay.DefaultBoundary).Index);
            clock.Advance(TimeSpan.FromDays(1));
            TerrariumSave save = garden.ToSave();
            save.FirstRunStep = FirstRunSteps.Done;
            Seed(save, clock.Now);
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            Assert.AreEqual(1, controller.Garden.Fronds);
            Assert.AreEqual(1f, controller.Garden.Vitality(controller.Service.TodayIndex), 0.001f);
            RitualHarness.Play(controller, RitualScripts.SixShort());
            float app = 0f;
            bool settled = false;
            while (app < 40f)
            {
                yield return null;
                app += Time.deltaTime;
                TerrariumState state = controller.Snapshot();
                if (state.Phase == BreathPhase.Complete && state.Breaths == 6 && state.Fronds == 2
                    && state.Fog == 0f && state.Uncoil >= 1f)
                {
                    settled = true;
                    break;
                }
            }
            Assert.IsTrue(settled, "normal day did not settle: " + controller.Snapshot().ToJson());
            yield return null;
            slot.Json = Capture(controller);
        }

        IEnumerator SettleMissed(Settled slot)
        {
            var clock = new FixedClock(GardenJourney.Origin, TimeZoneInfo.Utc);
            var garden = new Garden();
            garden.CompleteRitual(GardenDay.From(clock.Now, GardenDay.DefaultBoundary).Index);
            clock.Advance(TimeSpan.FromDays(3));
            TerrariumSave save = garden.ToSave();
            save.FirstRunStep = FirstRunSteps.Done;
            Seed(save, clock.Now);
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            for (int i = 0; i < 5; i++) yield return null;
            TerrariumState state = controller.Snapshot();
            Assert.AreEqual(1, state.Fronds);
            Assert.AreEqual(0.7f, state.Vitality, 0.001f);
            Assert.AreEqual(BreathPhase.Waiting, state.Phase);
            Assert.AreEqual(0, state.Breaths);
            Assert.IsTrue(controller.View.FiddleQuiet);
            slot.Json = Capture(controller);
            yield return null;
        }

        IEnumerator SettleDay7(Settled slot)
        {
            DateTimeOffset when = GardenJourney.Origin.AddDays(6);
            int today = GardenDay.From(when, GardenDay.DefaultBoundary).Index;
            int first = today - 6;
            var save = new TerrariumSave();
            save.FirstRunStep = FirstRunSteps.Done;
            save.FrondDays = new[] { first, first + 1, first + 2, first + 3, first + 4, first + 6 };
            save.LastRitualDay = today;
            save.RitualsCompleted = 6;
            save.Returns = 1;
            Seed(save, when);
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(6, controller.Garden.Fronds);
            Assert.AreEqual(1, controller.Garden.Flowers);
            RitualHarness.Play(controller, JourneyScripts.CorkLookBack());
            float app = 0f;
            bool started = false;
            while (app < 16f && (!started || controller.LookBackPlaying))
            {
                yield return null;
                app += Time.deltaTime;
                if (controller.LookBackPlaying) started = true;
            }
            Assert.IsTrue(started, "the cork hold did not replay the week");
            Assert.IsFalse(controller.LookBackPlaying);
            for (int i = 0; i < 3; i++) yield return null;
            TerrariumState state = controller.Snapshot();
            Assert.AreEqual(6, state.Fronds);
            Assert.AreEqual(1, state.Flowers);
            Assert.AreEqual(BreathPhase.Waiting, state.Phase);
            Assert.AreEqual(0, state.Breaths);
            yield return null;
            slot.Json = Capture(controller);
        }

        static string Capture(JarRitualController controller)
        {
            string step = "";
            if (controller.Director != null && controller.Director.Running)
                step = controller.Director.Step ?? "";
            else if (controller.Service != null && controller.Service.Document != null)
                step = controller.Service.Document.FirstRunStep ?? "";
            return "{\"step\":\"" + step + "\",\"state\":" + controller.Snapshot().ToJson() + "}";
        }

        static void Seed(TerrariumSave save, DateTimeOffset when)
        {
            RitualHarness.ReleaseOverrides();
            string dir = NewDir();
            File.WriteAllText(Path.Combine(dir, SaveStore<TerrariumSave>.LiveName), save.ToJson());
            GardenService.DirectoryOverride = dir;
            GardenService.ClockOverride = new FixedClock(when, TimeZoneInfo.Utc);
        }

        static string NewDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "gvr-ter-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        static string OneHold()
        {
            return "{\"t\":0.05,\"intent\":\"PinchHold\",\"target\":\"jar\",\"dur\":1.2}\n"
                + "{\"t\":0.05,\"intent\":\"Look\",\"target\":\"jar\",\"dur\":2}\n";
        }

        static float PaceFill(JarRitualController controller)
        {
            Transform ring = null;
            Transform[] all = controller.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == "PaceRing") ring = all[i];
            }
            Assert.IsNotNull(ring, "the pace ring is missing");
            Renderer renderer = ring.GetComponent<Renderer>();
            Assert.IsNotNull(renderer);
            return renderer.sharedMaterial.GetFloat("_Fill");
        }

        static float ReadT(string line)
        {
            const string key = "\"t\":";
            int at = line.IndexOf(key, StringComparison.Ordinal);
            Assert.GreaterOrEqual(at, 0, line);
            int start = at + key.Length;
            int end = line.IndexOf(',', start);
            Assert.Greater(end, start, line);
            float value;
            Assert.IsTrue(float.TryParse(line.Substring(start, end - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value), line);
            return value;
        }

        static string Sha256(string text)
        {
            byte[] hash = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(text));
            var sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
}
