using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GardenVR.Audio;
using GardenVR.Core;
using GardenVR.Input;
using GardenVR.Terrarium;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GardenVR.Terrarium.Tests
{
    /// <summary>
    /// PalmOpen at five points in a ritual, a cut-off ritual offered back, a quiet missed day,
    /// the day-7 cork look-back, and the settings pebbles. Scripts stay on one source so a
    /// fresh pinch can resume the frozen breath instead of dropping an open frame into it.
    /// </summary>
    public class JourneyTests
    {
        float _savedDelta;
        int _storedMute;

        [SetUp]
        public void SaveStep()
        {
            _savedDelta = Time.captureDeltaTime;
            _storedMute = PlayerPrefs.GetInt(AudioCueService.MuteKey, 0);
            AudioCueService.TestingResetMute();
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            Time.captureDeltaTime = _savedDelta;
            AudioCueService.TestingRestoreMute(_storedMute);
            Scene scene = SceneManager.GetSceneByName("Main");
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
            RitualHarness.ReleaseOverrides();
        }

        [UnityTest]
        [Timeout(360000)]
        public IEnumerator PauseAt_Waiting()
        {
            yield return RitualHarness.OpenMain();
            yield return PlayFrozen(RitualHarness.Controller(), JourneyScripts.Waiting(), BreathPhase.Waiting, 0, 0f, 2f, false);
        }

        [UnityTest]
        [Timeout(360000)]
        public IEnumerator PauseAt_Inhale1()
        {
            yield return RitualHarness.OpenMain();
            yield return PlayFrozen(RitualHarness.Controller(), JourneyScripts.Inhale1(), BreathPhase.Inhaling, 0, 0.30f, 0.95f, false);
        }

        [UnityTest]
        [Timeout(360000)]
        public IEnumerator PauseAt_Exhale1()
        {
            yield return RitualHarness.OpenMain();
            yield return PlayFrozen(RitualHarness.Controller(), JourneyScripts.Exhale1(), BreathPhase.Exhaling, 1, 1.05f, 1.70f, false);
        }

        [UnityTest]
        [Timeout(360000)]
        public IEnumerator PauseAt_Inhale3()
        {
            yield return RitualHarness.OpenMain();
            yield return PlayFrozen(RitualHarness.Controller(), JourneyScripts.Inhale3(), BreathPhase.Inhaling, 2, 0f, 0.25f, true);
        }

        [UnityTest]
        [Timeout(360000)]
        public IEnumerator PauseAt_Inhale6()
        {
            yield return RitualHarness.OpenMain();
            yield return PlayFrozen(RitualHarness.Controller(), JourneyScripts.Inhale6(), BreathPhase.Inhaling, 5, 0.15f, 0.70f, false);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Resume_OffersCutOffRitual()
        {
            DateTimeOffset when = GardenJourney.Origin;
            int today = GardenDay.From(when, GardenDay.DefaultBoundary).Index;
            var save = new TerrariumSave();
            save.FirstRunStep = FirstRunSteps.Done;
            save.FrondDays = new[] { today };
            save.LastRitualDay = today;
            save.RitualsCompleted = 1;
            Seed(save, when);

            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            Assert.IsFalse(controller.AwaitingContinue, "a finished garden offered a ritual that was not open");
            Assert.IsFalse(controller.Service.RitualOpen);
            Assert.AreEqual(1, controller.Garden.Fronds);
            Assert.AreEqual(BreathPhase.Waiting, controller.Snapshot().Phase);

            RitualHarness.Play(controller, JourneyScripts.OneInhale());
            RitualHarness.FixedStep();
            float open = 0f;
            while (open < 8f && !controller.Service.RitualOpen)
            {
                yield return null;
                open += Time.deltaTime;
            }
            Assert.IsTrue(controller.Service.RitualOpen, "the inhale was not remembered as an open ritual");
            Assert.AreEqual(0, controller.Snapshot().Breaths, "the cut-off inhale must not count a breath");
            Assert.AreEqual(1, controller.Garden.Fronds);

            yield return RitualHarness.OpenMain();
            controller = RitualHarness.Controller();
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsTrue(controller.AwaitingContinue, "the open ritual did not come back");
            Assert.AreEqual(BreathPhase.Waiting, controller.Snapshot().Phase);
            Assert.AreEqual(0, controller.Snapshot().Breaths);
            Assert.AreEqual(1, controller.Garden.Fronds);
            IntentTarget prompt;
            Assert.IsTrue(IntentTargetRegistry.TryGet(JarRitualController.ContinuePromptId, out prompt));
            Assert.IsTrue(prompt.gameObject.activeInHierarchy);
            Assert.AreEqual("Continue breathing?", EtchedLettering.Read(prompt.gameObject));

            RitualHarness.Play(controller, JourneyScripts.OfferThenContinue());
            float app = 0f;
            float uncoilWhileOffered = 0f;
            bool offeredDuringHold = false;
            bool continued = false;
            float uncoilAfter = 0f;
            while (app < 8f)
            {
                yield return null;
                app += Time.deltaTime;
                TerrariumState state = controller.Snapshot();
                if (controller.AwaitingContinue)
                {
                    if (state.Uncoil > uncoilWhileOffered) uncoilWhileOffered = state.Uncoil;
                    if (app > 0.4f && app < 1.6f)
                    {
                        offeredDuringHold = true;
                        Assert.AreEqual(0, state.Breaths);
                        Assert.AreEqual(BreathPhase.Waiting, state.Phase);
                    }
                }
                else if (app > 2.2f)
                {
                    continued = true;
                    if (state.Uncoil > uncoilAfter) uncoilAfter = state.Uncoil;
                }
            }

            Assert.IsTrue(offeredDuringHold, "the offered ritual restarted on the jar pinch");
            Assert.Less(uncoilWhileOffered, 0.01f);
            Assert.IsTrue(continued, "pinching the prompt did not hand the breath back");
            Assert.Greater(uncoilAfter, 0.05f);
            Assert.AreEqual(1, controller.Garden.Fronds, "continuing the offered ritual grew a second frond");
            Assert.IsFalse(controller.AwaitingContinue);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator MissedDay_IsQuiet()
        {
            DateTimeOffset when = GardenJourney.Origin;
            int today = GardenDay.From(when, GardenDay.DefaultBoundary).Index;
            var save = new TerrariumSave();
            save.FirstRunStep = FirstRunSteps.Done;
            save.FrondDays = new[] { today };
            save.LastRitualDay = today;
            save.RitualsCompleted = 1;
            Seed(save, when);

            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            for (int i = 0; i < 3; i++) yield return null;
            int cues = controller.CueTotal;
            int fronds = controller.Garden.Fronds;
            Assert.AreEqual(1, fronds);
            Assert.IsTrue(controller.ShiftDay(3), "the clock did not move to the missed day");

            TerrariumState state = controller.Snapshot();
            Assert.AreEqual(0.70f, state.Vitality, 0.001f);
            Assert.AreEqual(fronds, controller.Garden.Fronds);
            Assert.IsTrue(controller.View.FiddleQuiet);
            Assert.AreEqual(4.5f, controller.View.ShownLean, 0.05f);
            Assert.Greater(controller.View.ShownLean, 0f);
            Assert.LessOrEqual(controller.View.ShownLean, 6f);

            RitualHarness.FixedStep();
            float glow = controller.View.FrondGlow(0);
            float fiddle = controller.View.FiddleGlow;
            Assert.Greater(glow, 0.20f, "the kept frond went dark");
            Assert.Less(glow, 0.55f, "the missed day still glowed like a full day");
            Assert.Greater(fiddle, 0.20f);
            Assert.Less(fiddle, 0.45f);

            float drifted = fiddle;
            float quiet = 0f;
            while (quiet < 0.5f)
            {
                yield return null;
                quiet += Time.deltaTime;
                float now = controller.View.FiddleGlow;
                if (Mathf.Abs(now - fiddle) > Mathf.Abs(drifted - fiddle)) drifted = now;
            }
            Assert.AreEqual(cues, controller.CueTotal, "a missed day played a cue");
            Assert.Less(Mathf.Abs(drifted - fiddle), 0.02f);
            Assert.IsFalse(controller.CueNamed("streak"));
            Assert.IsFalse(controller.CueNamed("shame"));
            Assert.IsFalse(controller.CueNamed("wilt"));
            Assert.IsFalse(controller.CueNamed("fail"));
            Assert.IsFalse(controller.CueNamed("miss"));
            AssertQuietCopy(EtchedLettering.VisibleLines());
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Day7_LookBack()
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
            Assert.AreEqual(1f, controller.Garden.Vitality(controller.Service.TodayIndex), 0.001f);
            Assert.IsNotNull(GameObject.Find("Flower0"), "the week did not open a flower");
            Assert.AreEqual(-1, controller.View.LookLit);

            RitualHarness.Play(controller, JourneyScripts.CorkLookBack());
            RitualHarness.FixedStep();
            var seen = new List<string>();
            int last = -2;
            bool started = false;
            bool sawHold = false;
            float app = 0f;
            while (app < 14f && (!started || controller.LookBackPlaying))
            {
                yield return null;
                app += Time.deltaTime;
                TerrariumState state = controller.Snapshot();
                Assert.AreEqual(0, state.Breaths, "the cork pinch counted as a breath");
                Assert.AreEqual(BreathPhase.Waiting, state.Phase);
                if (!controller.LookBackPlaying) continue;
                started = true;
                int index = controller.LookBackIndex;
                if (index != last)
                {
                    last = index;
                    seen.Add(index + ":" + controller.LookBackLit + ":" + (controller.LookBackHeld ? "hold" : "kept"));
                }
                if (controller.LookBackHeld && controller.LookBackLit == 5)
                {
                    sawHold = true;
                    Assert.Less(controller.View.FrondGlow(5), 0.40f, "the missed day lit up inside the glass");
                    Assert.Greater(controller.View.FrondGlow(4), 0.55f);
                    Assert.IsTrue(controller.View.LookHeld);
                }
            }

            Assert.IsTrue(started, "pinching the cork did not replay the week");
            Assert.IsTrue(sawHold, "the missed day was not a held frame");
            Assert.AreEqual("0:1:kept 1:2:kept 2:3:kept 3:4:kept 4:5:kept 5:5:hold 6:6:kept", string.Join(" ", seen));
            Assert.AreEqual(1, controller.CueCount("lookback.shimmer"));
            Assert.IsFalse(controller.LookBackPlaying);
            Assert.AreEqual(-1, controller.View.LookLit, "the look-back stayed on the glass");
            Assert.Greater(controller.View.FrondGlow(5), 0.50f, "the last frond stayed dim after the replay");
            Assert.AreEqual(0, controller.Snapshot().Breaths);
            Assert.AreEqual(BreathPhase.Waiting, controller.Snapshot().Phase);
            Assert.AreEqual(6, controller.Garden.Fronds);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator Settings_PebblesToggle()
        {
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            for (int i = 0; i < 5 && controller.Pebbles == null; i++)
                yield return null;
            Assert.IsNotNull(controller.Pebbles, "settings pebbles were not built");
            Assert.IsFalse(controller.Pebbles.Open);
            Assert.AreEqual("Sound on", controller.Pebbles.Label(SettingsPebbles.MuteId));
            RitualHarness.Play(controller, JourneyScripts.Pebbles());
            RitualHarness.FixedStep();

            bool sawAll = false;
            float app = 0f;
            while (app < 4f && !(sawAll && !controller.Pebbles.Open && app > 1.6f))
            {
                yield return null;
                app += Time.deltaTime;
                if (!controller.Pebbles.Open) continue;
                if (controller.Pebbles.Label(SettingsPebbles.MuteId) == "Muted"
                    && controller.Pebbles.Label(SettingsPebbles.BedId) == "Night bed on"
                    && controller.Pebbles.Label(SettingsPebbles.VoiceId) == "Voice on"
                    && controller.Pebbles.Label(SettingsPebbles.MotionId) == "Quieter motion")
                {
                    sawAll = true;
                    AssertQuietCopy(new[]
                    {
                        controller.Pebbles.Label(SettingsPebbles.MuteId),
                        controller.Pebbles.Label(SettingsPebbles.VoiceId),
                        controller.Pebbles.Label(SettingsPebbles.BedId),
                        controller.Pebbles.Label(SettingsPebbles.MotionId),
                        controller.Pebbles.Label(SettingsPebbles.PaceId),
                        controller.Pebbles.Label(SettingsPebbles.BreathsId),
                        controller.Pebbles.Label(SettingsPebbles.HoldId)
                    });
                }
            }

            Assert.IsTrue(sawAll, "the four pebbles were never on together");
            Assert.IsFalse(controller.Pebbles.Open, "the fan stayed open");
            Assert.AreEqual("Sound on", controller.Pebbles.Label(SettingsPebbles.MuteId));
            Assert.IsFalse(controller.Service.Settings.Mute);
            Assert.IsTrue(controller.Service.Settings.VoiceGuide);
            Assert.IsTrue(controller.Service.Settings.NightBed);
            Assert.IsTrue(controller.Service.Settings.ReducedMotion);
            Assert.IsTrue(controller.View.reducedMotion);
            AudioCueService audio = controller.GetComponent<AudioCueService>();
            Assert.IsNotNull(audio);
            Assert.IsFalse(audio.Mute);
            Assert.IsTrue(audio.VoiceGuide);
            Assert.IsTrue(audio.Beds);
            Assert.GreaterOrEqual(controller.CueCount("pebble.tap"), 7);
            Assert.AreEqual(0, controller.Garden.Fronds);

            string json = File.ReadAllText(Path.Combine(GardenService.DirectoryOverride, SaveStore<TerrariumSave>.LiveName));
            Assert.That(json, Does.Contain("\"VoiceGuide\":true"));
            Assert.That(json, Does.Contain("\"NightBed\":true"));
            Assert.That(json, Does.Contain("\"ReducedMotion\":true"));
            Assert.That(json, Does.Not.Contain("\"Mute\""));
            AssertQuietCopy(EtchedLettering.VisibleLines());
        }

        static IEnumerator PlayFrozen(
            JarRitualController controller,
            string script,
            BreathPhase frozenPhase,
            int frozenBreaths,
            float minPhase,
            float maxPhase,
            bool focusLoss)
        {
            RitualHarness.Play(controller, script);
            RitualHarness.FixedStep();
            bool armed = false;
            bool saw = false;
            float uncoil = 0f;
            float phaseTime = 0f;
            bool lost = false;
            bool finished = false;
            float app = 0f;
            while (app < 50f && !finished)
            {
                yield return null;
                app += Time.deltaTime;
                TerrariumState state = controller.Snapshot();
                if (focusLoss && !armed && state.Phase == frozenPhase && state.Breaths == frozenBreaths)
                {
                    controller.NotifyFocusLost();
                    armed = true;
                }
                if (!saw && controller.HoldingBreath && state.Phase == frozenPhase && state.Breaths == frozenBreaths)
                {
                    saw = true;
                    uncoil = state.Uncoil;
                    phaseTime = controller.Session.PhaseTime;
                }
                if (saw && controller.HoldingBreath)
                {
                    if (state.Breaths != frozenBreaths) lost = true;
                    if (state.Phase != frozenPhase) lost = true;
                    if (state.Fronds != 0) lost = true;
                    if (state.Uncoil + 1e-4f < uncoil) lost = true;
                    if (controller.Session.PhaseTime > phaseTime + 0.05f) lost = true;
                }
                if (state.Phase == BreathPhase.Complete && state.Breaths == 6)
                    finished = true;
            }

            TerrariumState end = controller.Snapshot();
            Assert.IsTrue(saw, "the jar did not hold its breath at " + frozenPhase + " breaths " + frozenBreaths);
            Assert.IsFalse(lost, "the pause dropped a breath, a frond, or uncoil");
            Assert.GreaterOrEqual(phaseTime, minPhase, "phase time " + phaseTime.ToString("0.00", CultureInfo.InvariantCulture));
            Assert.LessOrEqual(phaseTime, maxPhase, "phase time " + phaseTime.ToString("0.00", CultureInfo.InvariantCulture));
            Assert.IsTrue(finished, "the ritual did not finish: " + end.ToJson());
            Assert.AreEqual(6, end.Breaths);
            Assert.AreEqual(BreathPhase.Complete, end.Phase);
            Assert.AreEqual(1, end.Fronds);
            Assert.GreaterOrEqual(end.Uncoil + 1e-4f, uncoil);
        }

        static void Seed(TerrariumSave save, DateTimeOffset when)
        {
            string dir = Path.Combine(Path.GetTempPath(), "gvr-ter-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, SaveStore<TerrariumSave>.LiveName), save.ToJson());
            GardenService.DirectoryOverride = dir;
            GardenService.ClockOverride = new FixedClock(when, TimeZoneInfo.Utc);
        }

        static void AssertQuietCopy(string[] lines)
        {
            if (lines == null) return;
            for (int i = 0; i < lines.Length; i++)
            {
                string text = lines[i] ?? "";
                string lower = text.ToLowerInvariant();
                Assert.IsFalse(lower.Contains("streak"), text);
                Assert.IsFalse(lower.Contains("fail"), text);
                Assert.IsFalse(lower.Contains("shame"), text);
                Assert.IsFalse(lower.Contains("wilt"), text);
                Assert.IsFalse(lower.Contains("missed"), text);
                Assert.IsFalse(lower.Contains("behind"), text);
                Assert.IsFalse(text.Contains("\u2014"), text);
            }
        }
    }

    static class JourneyScripts
    {
        public static string Waiting()
        {
            var sb = new StringBuilder();
            Line(sb, 0.20f, "PalmOpen", -1f, null);
            Breaths(sb, 0.80f, 6);
            return sb.ToString();
        }

        public static string Inhale1()
        {
            var sb = new StringBuilder();
            Line(sb, 0.20f, "PinchHold", 0.70f, "jar");
            Line(sb, 0.80f, "PalmOpen", -1f, null);
            Line(sb, 1.05f, "Release", -1f, "jar");
            Line(sb, 1.40f, "PinchHold", 1.30f, "jar");
            Line(sb, 2.70f, "Release", -1f, "jar");
            Breaths(sb, 4.00f, 5);
            return sb.ToString();
        }

        public static string Exhale1()
        {
            var sb = new StringBuilder();
            Line(sb, 0.20f, "PinchHold", 1.50f, "jar");
            Line(sb, 1.70f, "Release", -1f, "jar");
            Line(sb, 2.95f, "PalmOpen", -1f, null);
            Breaths(sb, 3.80f, 5);
            return sb.ToString();
        }

        public static string Inhale3()
        {
            var sb = new StringBuilder();
            float t = Breaths(sb, 0.20f, 2);
            Line(sb, t, "PinchHold", 0.80f, "jar");
            Line(sb, t + 0.80f, "Release", -1f, "jar");
            Breaths(sb, t + 1.50f, 4);
            return sb.ToString();
        }

        public static string Inhale6()
        {
            var sb = new StringBuilder();
            float t = Breaths(sb, 0.20f, 5);
            Line(sb, t, "PinchHold", 0.70f, "jar");
            Line(sb, t + 0.35f, "PalmOpen", -1f, null);
            Line(sb, t + 0.80f, "Release", -1f, "jar");
            Line(sb, t + 1.20f, "PinchHold", 1.40f, "jar");
            Line(sb, t + 2.60f, "Release", -1f, "jar");
            Line(sb, t + 2.60f, "Look", 2.2f, null);
            return sb.ToString();
        }

        public static string OneInhale()
        {
            var sb = new StringBuilder();
            Line(sb, 0.20f, "PinchHold", 1.50f, "jar");
            Line(sb, 1.70f, "Release", -1f, "jar");
            Line(sb, 1.70f, "Look", 0.6f, null);
            return sb.ToString();
        }

        public static string OfferThenContinue()
        {
            var sb = new StringBuilder();
            Line(sb, 0.20f, "PinchHold", 1.50f, "jar");
            Line(sb, 1.70f, "Release", -1f, "jar");
            Line(sb, 2.00f, "Pinch", -1f, JarRitualController.ContinuePromptId);
            Line(sb, 2.50f, "PinchHold", 1.50f, "jar");
            Line(sb, 4.00f, "Release", -1f, "jar");
            Line(sb, 4.00f, "Look", 1.6f, null);
            return sb.ToString();
        }

        public static string CorkLookBack()
        {
            var sb = new StringBuilder();
            Line(sb, 0.20f, "PinchHold", 2.50f, JarRitualController.CorkId);
            Line(sb, 2.70f, "Release", -1f, JarRitualController.CorkId);
            Line(sb, 2.70f, "Look", 8f, null);
            return sb.ToString();
        }

        public static string Pebbles()
        {
            var sb = new StringBuilder();
            Line(sb, 0.10f, "Poke", -1f, SettingsPebbles.LeadId);
            Line(sb, 0.30f, "Poke", -1f, SettingsPebbles.MuteId);
            Line(sb, 0.50f, "Poke", -1f, SettingsPebbles.BedId);
            Line(sb, 0.70f, "Poke", -1f, SettingsPebbles.VoiceId);
            Line(sb, 0.90f, "Poke", -1f, SettingsPebbles.MotionId);
            Line(sb, 1.10f, "Poke", -1f, SettingsPebbles.MuteId);
            Line(sb, 1.30f, "Poke", -1f, SettingsPebbles.LeadId);
            Line(sb, 1.30f, "Look", 0.8f, null);
            return sb.ToString();
        }

        static float Breaths(StringBuilder sb, float start, int count)
        {
            float t = start;
            const float hold = 1.5f;
            const float gap = 1.3f;
            for (int i = 0; i < count; i++)
            {
                Line(sb, t, "PinchHold", hold, "jar");
                t += hold;
                Line(sb, t, "Release", -1f, "jar");
                t += gap;
            }
            Line(sb, t - gap, "Look", 2f, null);
            return t;
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
