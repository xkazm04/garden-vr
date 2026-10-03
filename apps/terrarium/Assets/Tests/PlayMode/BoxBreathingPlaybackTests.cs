using System;
using System.Collections;
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
    public class BoxBreathingPlaybackTests
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
        [Timeout(600000)]
        public IEnumerator BoxBreathing_FourBreaths_ByPlayback()
        {
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            for (int i = 0; i < 8 && controller.Pebbles == null; i++)
                yield return null;
            Assert.IsNotNull(controller.Pebbles, "settings pebbles were not built");
            RitualHarness.Play(controller, Script());
            RitualHarness.FixedStep();

            bool sawLabel = false;
            bool sawFour = false;
            bool sideIn = false;
            bool sideHold = false;
            bool sideOut = false;
            bool sideEmpty = false;
            bool heldThrough = false;
            int holds = 0;
            float previous = controller.Snapshot().Uncoil;
            float app = 0f;
            bool finished = false;
            while (app < 100f)
            {
                yield return null;
                app += Time.deltaTime;
                TerrariumState state = controller.Snapshot();
                Assert.GreaterOrEqual(state.Uncoil + 1e-5f, previous, "uncoil fell at t=" + app.ToString("0.00", CultureInfo.InvariantCulture));
                previous = state.Uncoil;

                if (controller.Pebbles.Open)
                {
                    string pace = controller.Pebbles.Label(SettingsPebbles.PaceId);
                    string breaths = controller.Pebbles.Label(SettingsPebbles.BreathsId);
                    if (pace == SettingsPebbles.BoxLabel()) sawLabel = true;
                    if (breaths == "4 breaths") sawFour = true;
                    if (sawLabel)
                        AssertQuiet(pace);
                }

                BreathPhase phase = controller.Session.Phase;
                float phaseTime = controller.Session.PhaseTime;
                float fill = PaceFill(controller);
                if (phase == BreathPhase.HoldingFull)
                {
                    holds++;
                    if (controller.Session.Pinch == PinchState.Held) heldThrough = true;
                }
                if (phaseTime >= 1f && phaseTime <= 3f)
                {
                    if (phase == BreathPhase.Inhaling && fill > 0.04f && fill < 0.24f) sideIn = true;
                    if (phase == BreathPhase.HoldingFull && fill > 0.30f && fill < 0.49f) sideHold = true;
                    if (phase == BreathPhase.Exhaling && fill > 0.55f && fill < 0.74f) sideOut = true;
                    if (phase == BreathPhase.HoldingEmpty && fill > 0.80f && fill < 0.99f) sideEmpty = true;
                }

                if (state.Phase == BreathPhase.Complete)
                {
                    finished = true;
                    break;
                }
            }

            for (int i = 0; i < 20; i++) yield return null;
            TerrariumState end = controller.Snapshot();
            Assert.IsTrue(sawLabel, "the pace pebble never showed the box preset");
            Assert.IsTrue(sawFour, "the breath pebble never showed 4 breaths");
            Assert.IsTrue(sideIn, "the ring did not light the inhale side");
            Assert.IsTrue(sideHold, "the ring did not light the held-full side");
            Assert.IsTrue(sideOut, "the ring did not light the exhale side");
            Assert.IsTrue(sideEmpty, "the ring did not light the open side");
            Assert.IsTrue(heldThrough, "the pinch was not held through the held-full beat");
            Assert.Greater(holds, 30, "the held-full beat was only a flicker");
            Assert.IsTrue(finished, "box ritual did not complete: " + end.ToJson());
            Assert.AreEqual(4, end.Breaths);
            Assert.AreEqual(BreathPhase.Complete, end.Phase);
            Assert.AreEqual(1, end.Fronds);
            Assert.AreEqual(1f, end.Uncoil, 0.02f);
            Assert.IsTrue(controller.Service.Settings.BoxPace);
            Assert.AreEqual(4, controller.Service.Settings.Breaths);
            Assert.AreEqual(4, Count(controller, BreathEventKind.HoldFullStarted));
            Assert.GreaterOrEqual(Count(controller, BreathEventKind.HoldEmptyStarted), 3);
            Assert.IsNotNull(GameObject.Find(BoxPaceMarks.RootName), "the four side marks were not on the ring");

            string json = File.ReadAllText(Path.Combine(GardenService.DirectoryOverride, SaveStore<TerrariumSave>.LiveName));
            Assert.That(json, Does.Contain("\"BoxPace\":true"));
            Assert.That(json, Does.Contain("\"Breaths\":4"));
            Assert.IsFalse(json.Contains("\u2014"));
            AssertQuiet(SettingsPebbles.BoxLabel());
            string[] lines = EtchedLettering.VisibleLines();
            if (lines != null)
            {
                for (int i = 0; i < lines.Length; i++) AssertQuiet(lines[i]);
            }
        }

        static int Count(JarRitualController controller, BreathEventKind kind)
        {
            int n = 0;
            for (int i = 0; i < controller.Session.Events.Count; i++)
            {
                if (controller.Session.Events[i].Kind == kind) n++;
            }
            return n;
        }

        static float PaceFill(JarRitualController controller)
        {
            Transform[] all = controller.GetComponentsInChildren<Transform>(true);
            Transform ring = null;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == "PaceRing") ring = all[i];
            }
            Assert.IsNotNull(ring, "the pace ring is missing");
            Renderer renderer = ring.GetComponent<Renderer>();
            Assert.IsNotNull(renderer, "the pace ring has no renderer");
            return renderer.sharedMaterial.GetFloat("_Fill");
        }

        static void AssertQuiet(string text)
        {
            string value = text ?? "";
            string lower = value.ToLowerInvariant();
            Assert.IsFalse(value.Contains("\u2014"), value);
            Assert.IsFalse(lower.Contains("therap"), value);
            Assert.IsFalse(lower.Contains("medical"), value);
            Assert.IsFalse(lower.Contains("anxiety"), value);
            Assert.IsFalse(lower.Contains("cure"), value);
            Assert.IsFalse(lower.Contains("diagnos"), value);
            Assert.IsFalse(lower.Contains("symptom"), value);
            Assert.IsFalse(lower.Contains("treatment"), value);
            Assert.IsFalse(lower.Contains("clinical"), value);
        }

        static string Script()
        {
            var sb = new StringBuilder();
            Line(sb, 0.10f, "Poke", -1f, SettingsPebbles.LeadId);
            Line(sb, 0.30f, "Poke", -1f, SettingsPebbles.PaceId);
            Line(sb, 0.50f, "Poke", -1f, SettingsPebbles.PaceId);
            Line(sb, 0.70f, "Poke", -1f, SettingsPebbles.BreathsId);
            Line(sb, 0.90f, "Poke", -1f, SettingsPebbles.BreathsId);
            Line(sb, 1.10f, "Poke", -1f, SettingsPebbles.BreathsId);
            Line(sb, 1.40f, "Poke", -1f, SettingsPebbles.LeadId);
            float t = 1.80f;
            const float hold = 8.2f;
            const float gap = 8.2f;
            for (int i = 0; i < 4; i++)
            {
                Line(sb, t, "PinchHold", hold, "jar");
                t += hold;
                Line(sb, t, "Release", -1f, "jar");
                t += gap;
            }
            Line(sb, t - gap, "Look", 6f, null);
            return sb.ToString();
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
