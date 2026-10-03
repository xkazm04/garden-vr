using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using GardenVR.Audio;
using GardenVR.Capture;
using GardenVR.Core;
using GardenVR.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GardenVR.Sundial.Tests.PlayMode
{
    /// <summary>
    /// Settings tabs, palm dismiss, a quiet missed day, the day-7 look-back, and pause at five points
    /// of the dusk ritual. The clock is injected. Nothing here reads a keyboard or a mouse.
    /// </summary>
    public class JourneyTests
    {
        float _savedDelta;
        int _savedMute;

        [SetUp]
        public void SaveStep()
        {
            _savedDelta = Time.captureDeltaTime;
            _savedMute = PlayerPrefs.GetInt(AudioCueService.MuteKey, 0);
            AudioCueService.TestingResetMute();
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            yield return SundialPlay.Unload();
            SundialService.DevSeedOnFresh = true;
            SundialService.FreshReducedMotion = null;
            SundialController.SaveDirectoryOverride = null;
            SundialController.ClockOverride = null;
            AudioCueService.SuppressAutoLoad = false;
            AudioCueService.TestingRestoreMute(_savedMute);
            Time.captureDeltaTime = _savedDelta;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Settings_Tabs_Poke()
        {
            string dir = SundialPlay.FreshDir();
            SundialController controller = null;
            yield return Boot(dir, SundialPlay.Clock1420(), c => controller = c);
            AudioCueService.SuppressAutoLoad = true;
            AudioCueService audio = controller.gameObject.AddComponent<AudioCueService>();
            Assert.IsNotNull(controller.Settings);
            Assert.IsFalse(controller.Settings.Open);
            Assert.AreEqual(SettingsTabs.TabLabel, controller.Settings.Label(SettingsTabs.TabId));
            Assert.IsFalse(controller.Service.Mute);
            Assert.IsFalse(controller.Service.Voice);
            Assert.IsFalse(controller.Service.Beds);
            Assert.IsFalse(controller.Service.ReducedMotion);

            var sb = new StringBuilder();
            Line(sb, 0.10f, "Poke", -1f, SettingsTabs.TabId);
            Line(sb, 0.30f, "Poke", -1f, SettingsTabs.MuteId);
            Line(sb, 0.50f, "Poke", -1f, SettingsTabs.BedsId);
            Line(sb, 0.70f, "Poke", -1f, SettingsTabs.VoiceId);
            Line(sb, 0.90f, "Poke", -1f, SettingsTabs.MotionId);
            Line(sb, 1.10f, "Poke", -1f, SettingsTabs.MuteId);
            Line(sb, 1.30f, "Poke", -1f, SettingsTabs.TabId);
            SundialPlay.Play(controller, sb.ToString());

            bool sawOpen = false;
            bool sawWords = false;
            bool sawAllOn = false;
            float app = 0f;
            while (app < 1.70f)
            {
                yield return null;
                app += Time.deltaTime;
                SettingsTabs tabs = controller.Settings;
                if (!tabs.Open) continue;
                sawOpen = true;
                string mute = tabs.Label(SettingsTabs.MuteId);
                string beds = tabs.Label(SettingsTabs.BedsId);
                string voice = tabs.Label(SettingsTabs.VoiceId);
                string motion = tabs.Label(SettingsTabs.MotionId);
                if (mute == SettingsTabs.MuteLabel
                    && beds == SettingsTabs.BedsLabel
                    && voice == SettingsTabs.VoiceLabel
                    && motion == SettingsTabs.MotionLabel)
                {
                    sawWords = true;
                    Assert.IsFalse(mute.Contains("\u2014") || motion.Contains("\u2014"));
                }
                if (controller.Service.Mute && controller.Service.Beds && controller.Service.Voice && controller.Service.ReducedMotion)
                    sawAllOn = true;
            }

            Assert.IsTrue(sawOpen, "the settings sheet did not open");
            Assert.IsTrue(sawWords, "the four tabs did not show their words");
            Assert.IsTrue(sawAllOn, "the four pokes did not turn the settings on");
            Assert.IsFalse(controller.Settings.Open, "the second poke on the tab should close the sheet");
            Assert.IsFalse(controller.Service.Mute, "mute was poked twice and should be off");
            Assert.IsTrue(controller.Service.Beds);
            Assert.IsTrue(controller.Service.Voice);
            Assert.IsTrue(controller.Service.ReducedMotion);
            Assert.IsFalse(audio.Mute);
            Assert.IsTrue(audio.VoiceGuide);
            Assert.IsTrue(audio.Beds);
            Assert.IsTrue(controller.View.reducedMotion);
            Assert.IsFalse(controller.View.boil, "reduced motion stops the boil");
            Assert.GreaterOrEqual(controller.Settings.CueCount(SettingsTabs.CueTap), 7);
            Assert.AreEqual(0, controller.LiveCount("top3"), "a settings poke must not tend a plant");

            string json = File.ReadAllText(Path.Combine(dir, "save.json"));
            StringAssert.Contains("\"Mute\":false", json);
            StringAssert.Contains("\"Beds\":true", json);
            StringAssert.Contains("\"Voice\":true", json);
            StringAssert.Contains("\"ReducedMotion\":true", json);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Dismiss_PalmOpen_Returns()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.FixedStep();
            yield return null;
            string before = controller.StateJson;
            Vector3 home = controller.transform.localPosition;
            Assert.IsFalse(controller.Dismissed);

            SundialPlay.Play(controller,
                "{\"t\":0.10,\"intent\":\"PalmOpen\"}\n" +
                "{\"t\":0.50,\"intent\":\"PalmOpen\"}\n");
            bool sawAway = false;
            bool sawBack = false;
            float app = 0f;
            while (app < 0.90f)
            {
                yield return null;
                app += Time.deltaTime;
                if (controller.Dismissed)
                {
                    sawAway = true;
                    Assert.Less(controller.transform.localPosition.y, home.y - 1f);
                    Assert.AreEqual(before, controller.StateJson);
                    Assert.IsTrue(controller.Wizard.Held);
                }
                if (sawAway && !controller.Dismissed)
                {
                    sawBack = true;
                    Assert.AreEqual(home.y, controller.transform.localPosition.y, 0.001f);
                    Assert.AreEqual(before, controller.StateJson);
                    Assert.IsFalse(controller.Wizard.Held);
                }
            }

            Assert.IsTrue(sawAway, "the first palm did not put the dial away");
            Assert.IsTrue(sawBack, "the second palm did not bring the dial back");
            Assert.IsFalse(controller.Dismissed);
            Assert.AreEqual(0, controller.LiveCount("top3"));

            SundialPlay.Play(controller,
                "{\"t\":0.05,\"intent\":\"Pinch\",\"target\":\"plant.midday\"}\n" +
                "{\"t\":0.40,\"intent\":\"PalmOpen\"}\n" +
                "{\"t\":0.80,\"intent\":\"PalmOpen\"}\n");
            yield return SundialPlay.Seconds(1.05f);
            Assert.IsFalse(controller.Dismissed);
            Assert.AreEqual(1, controller.LiveCount("top3"), "the tend committed while the dial was away");
            Assert.AreEqual(TileState.Kept, SundialPlay.Midday(controller).Window[6]);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator MissedDay_IsQuiet()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.FixedStep();
            for (int i = 0; i < 5; i++)
            {
                Keep(controller, "top3");
                if (i < 4) Assert.IsTrue(controller.ShiftDay(1));
            }
            yield return null;
            PlantState open = SundialPlay.Midday(controller);
            Assert.AreEqual(5, open.LifetimeKept);
            Assert.AreEqual(Stage.Young, open.Stage);
            Assert.AreEqual(Bloom.Open, open.Bloom);
            int card = controller.View.stageMidday;
            int cuesBefore = controller.Cues.Count;

            Assert.IsTrue(controller.ShiftDay(1));
            Assert.IsTrue(controller.ShiftDay(1));
            Assert.IsTrue(controller.ShiftDay(1));
            yield return null;

            PlantState missed = SundialPlay.Midday(controller);
            Assert.AreEqual(5, missed.LifetimeKept);
            Assert.AreEqual(Stage.Young, missed.Stage);
            Assert.AreEqual(card, controller.View.stageMidday, "a miss does not shrink the drawing");
            Assert.AreEqual(Bloom.Bud, missed.Bloom, "under five kept days the flower is a bud");
            Assert.AreEqual(TileState.Missed, missed.Window[5]);
            Assert.AreEqual(TileState.Today, missed.Window[6]);
            Assert.AreEqual(SundialArcs.TileDigit(TileState.Missed), controller.View.tiles[7 + 5]);
            Assert.AreEqual(TileLabels.Missed, TileLabels.For(TileState.Missed));
            Assert.IsTrue(controller.AskVisible("midday"));
            TextMesh ask = AskMesh("midday");
            Assert.AreEqual("?", ask.text);
            Assert.Less(ask.color.r, 0.45f, "the ask mark is ink, not an alarm red");
            Assert.Greater(ask.color.g, 0.05f);
            Assert.AreEqual(cuesBefore, controller.Cues.Count, "a missed day plays nothing");
            AssertNoShame(controller);

            yield return SundialPlay.Seconds(1.45f);
            Assert.AreEqual(0, controller.View.ShownBloomCard(1), "the open flower has folded back to the bud");
            Assert.AreEqual(card, controller.View.stageMidday);
            Assert.AreEqual(5, SundialPlay.Midday(controller).LifetimeKept);
            Assert.AreEqual(Stage.Young, SundialPlay.Midday(controller).Stage);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Day7_LookBack()
        {
            SundialController controller = null;
            yield return Boot(SundialPlay.FreshDir(), Clock2130(), c => controller = c);
            GrowWeek(controller);
            yield return null;

            PlantState dusk = Plant(controller, "winddown");
            PlantState midday = Plant(controller, "midday");
            PlantState morning = Plant(controller, "morning");
            Assert.AreEqual(TileState.Kept, dusk.Window[0]);
            Assert.AreEqual(TileState.Missed, dusk.Window[5]);
            Assert.AreEqual(TileState.Today, dusk.Window[6]);
            Assert.AreEqual(1, Count(dusk, TileState.Missed));
            Assert.AreEqual(5, dusk.WindowKept);
            Assert.GreaterOrEqual(dusk.LifetimeKept, 14);
            Assert.AreEqual(Stage.Full, dusk.Stage);
            Assert.AreEqual(Bloom.Open, dusk.Bloom);
            Assert.AreEqual((int)Stage.Full, controller.View.stageWinddown);
            Assert.AreEqual(1, controller.View.ShownBloomCard(2), "the dusk plant kept the week and is in bloom");

            Assert.AreEqual(Stage.Sprout, morning.Stage);
            Assert.AreEqual(Bloom.None, morning.Bloom);
            Assert.AreEqual((int)Stage.Sprout, controller.View.stageMorning);
            Assert.AreEqual(-1, controller.View.ShownBloomCard(0));
            Assert.Less(controller.View.stageMorning, controller.View.stageWinddown);

            Assert.AreEqual(Bloom.Bud, midday.Bloom);
            Assert.AreEqual(Stage.Young, midday.Stage);
            Assert.AreEqual(0, controller.View.ShownBloomCard(1), "a shorter week is a bud, not a dead plant");
            Assert.Less((int)midday.Stage, (int)dusk.Stage);

            for (int i = 0; i < 7; i++)
            {
                Assert.AreEqual(SundialArcs.TileDigit(dusk.Window[i]), controller.View.tiles[14 + i], "dusk slot " + i);
                Assert.AreEqual(TileLabels.For(dusk.Window[i]), Label(dusk.Window[i]));
            }
            Assert.IsTrue(controller.AskVisible("winddown"), "yesterday's pale tile still carries the quiet ask");
            Assert.AreEqual("?", AskMesh("winddown").text);
            Assert.AreEqual(ArcId.WindDown, controller.State.Arc);
            AssertNoShame(controller);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator PauseAt_Offer()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.FixedStep();
            yield return null;
            DuskRitualController dusk = Dusk(controller);
            Assert.IsTrue(dusk.OfferVisible);

            var sb = new StringBuilder();
            Line(sb, 0.10f, "PalmOpen", -1f, null);
            Line(sb, 0.45f, "PalmOpen", -1f, null);
            Holds(sb, 0.80f, 3, 2.0f, 1.40f);
            Trail(sb, 0.80f + 3f * 2.0f + 2f * 1.40f, 2.2f);
            SundialPlay.Play(controller, sb.ToString());

            bool sawAway = false;
            bool sawOfferAfter = false;
            float app = 0f;
            while (app < 12f)
            {
                yield return null;
                app += Time.deltaTime;
                if (controller.Dismissed && !dusk.Active && dusk.Breaths == 0)
                    sawAway = true;
                if (sawAway && !controller.Dismissed && !dusk.Active && dusk.Breaths == 0 && dusk.OfferVisible)
                    sawOfferAfter = true;
                if (dusk.Answered && dusk.CircleAmount < 0.05f) break;
            }

            Assert.IsTrue(sawAway, "palm did not dismiss before the ritual");
            Assert.IsTrue(sawOfferAfter, "bringing the dial back did not offer the breaths again");
            Assert.IsTrue(dusk.Answered);
            Assert.AreEqual(3, dusk.Breaths);
            Assert.AreEqual(1, controller.LiveCount("breaths"));
            Assert.AreEqual(TendSource.Ritual, LiveTend(controller, "breaths").Source);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator PauseAt_Inhale1()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.FixedStep();
            DuskRitualController dusk = Dusk(controller);

            var sb = new StringBuilder();
            Line(sb, 0.10f, "PinchHold", 2.00f, DuskRitualController.PlantId);
            Line(sb, 0.90f, "PalmOpen", -1f, null);
            Line(sb, 1.40f, "PalmOpen", -1f, null);
            Line(sb, 2.10f, "Release", -1f, DuskRitualController.PlantId);
            float t = Holds(sb, 2.40f, 3, 2.0f, 1.40f);
            Trail(sb, t, 2.2f);
            SundialPlay.Play(controller, sb.ToString());

            bool sawFreeze = false;
            bool sawBack = false;
            bool dropped = false;
            float frozen = -1f;
            int seen = 0;
            float app = 0f;
            while (app < 14f)
            {
                yield return null;
                app += Time.deltaTime;
                if (dusk.Breaths < seen) dropped = true;
                if (dusk.Breaths > seen) seen = dusk.Breaths;
                if (!sawFreeze && controller.Dismissed && dusk.PausedFor > 0.15f && dusk.Breaths == 0)
                {
                    frozen = dusk.CircleAmount;
                    sawFreeze = true;
                    Assert.IsTrue(dusk.Active);
                    Assert.IsFalse(dusk.Answered);
                    Assert.Greater(frozen, 0.05f);
                    Assert.AreEqual(0, controller.LiveCount("breaths"));
                }
                if (sawFreeze && !sawBack && !controller.Dismissed && dusk.Breaths == 0 && dusk.PausedFor > 0.20f)
                {
                    sawBack = true;
                    Assert.AreEqual(frozen, dusk.CircleAmount, 0.0001f, "the circle moved while the dial was away");
                    Assert.IsFalse(dusk.Answered);
                }
                if (dusk.Answered && dusk.CircleAmount < 0.05f) break;
            }

            Assert.IsTrue(sawFreeze, "the first inhale did not pause");
            Assert.IsTrue(sawBack, "the dial did not return with the breath still held");
            Assert.IsFalse(dropped, "the breath count went backwards");
            Assert.IsTrue(dusk.Answered);
            Assert.AreEqual(3, dusk.Breaths);
            Assert.AreEqual(1, controller.LiveCount("breaths"));
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator PauseAt_Exhale1()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.FixedStep();
            DuskRitualController dusk = Dusk(controller);

            var sb = new StringBuilder();
            Line(sb, 0.10f, "PinchHold", 2.00f, DuskRitualController.PlantId);
            Line(sb, 2.10f, "Release", -1f, DuskRitualController.PlantId);
            Line(sb, 3.40f, "PalmOpen", -1f, null);
            Line(sb, 3.80f, "PalmOpen", -1f, null);
            float t = Holds(sb, 4.20f, 2, 2.0f, 1.40f);
            Trail(sb, t, 2.2f);
            SundialPlay.Play(controller, sb.ToString());

            bool sawFreeze = false;
            bool sawBack = false;
            bool dropped = false;
            int seen = 0;
            float app = 0f;
            while (app < 14f)
            {
                yield return null;
                app += Time.deltaTime;
                if (dusk.Breaths < seen) dropped = true;
                if (dusk.Breaths > seen) seen = dusk.Breaths;
                if (!sawFreeze && controller.Dismissed && dusk.PausedFor > 0.10f)
                {
                    sawFreeze = true;
                    Assert.AreEqual(1, dusk.Breaths, "the pause did not land between breaths");
                    Assert.IsFalse(dusk.Answered);
                    Assert.AreEqual(0, controller.LiveCount("breaths"));
                    Assert.AreEqual(BreathPhase.Exhaling, dusk.Session.Phase);
                }
                if (sawFreeze && !sawBack && !controller.Dismissed && dusk.PausedFor > 0.10f)
                {
                    sawBack = true;
                    Assert.AreEqual(1, dusk.Breaths, "returning the dial restarted the ritual");
                    Assert.IsFalse(dusk.Answered);
                }
                if (dusk.Answered && dusk.CircleAmount < 0.05f) break;
            }

            Assert.IsTrue(sawFreeze, "the exhale did not pause");
            Assert.IsTrue(sawBack, "the dial did not come back");
            Assert.IsFalse(dropped);
            Assert.IsTrue(dusk.Answered);
            Assert.AreEqual(3, dusk.Breaths);
            Assert.AreEqual(1, controller.LiveCount("breaths"));
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator PauseAt_Inhale2()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.FixedStep();
            DuskRitualController dusk = Dusk(controller);

            var sb = new StringBuilder();
            Line(sb, 0.10f, "PinchHold", 2.00f, DuskRitualController.PlantId);
            Line(sb, 2.10f, "Release", -1f, DuskRitualController.PlantId);
            Line(sb, 3.50f, "PinchHold", 3.00f, DuskRitualController.PlantId);
            SundialPlay.Play(controller, sb.ToString());

            float app = 0f;
            bool ready = false;
            while (app < 6f)
            {
                yield return null;
                app += Time.deltaTime;
                if (dusk.Breaths == 1 && dusk.Session != null && dusk.Session.Phase == BreathPhase.Inhaling && dusk.CircleAmount > 0.10f)
                {
                    ready = true;
                    break;
                }
            }
            Assert.IsTrue(ready, "the second inhale did not start");
            float frozen = dusk.CircleAmount;
            controller.NotifyFocusLost();
            yield return SundialPlay.Seconds(0.35f);
            Assert.IsFalse(controller.Dismissed, "focus loss must not put the dial away");
            Assert.AreEqual(1, dusk.Breaths);
            Assert.AreEqual(frozen, dusk.CircleAmount, 0.0001f);
            Assert.IsFalse(dusk.Answered);
            Assert.IsTrue(dusk.Active);
            Assert.AreEqual(0, controller.LiveCount("breaths"));
            Assert.IsFalse(controller.Wizard.Held);

            var resume = new StringBuilder();
            float t = Holds(resume, 0.20f, 2, 2.0f, 1.40f);
            Trail(resume, t, 2.2f);
            SundialPlay.Play(controller, resume.ToString());
            app = 0f;
            while (app < 10f)
            {
                yield return null;
                app += Time.deltaTime;
                if (dusk.Breaths < 1) Assert.Fail("focus loss restarted the ritual");
                if (dusk.Answered && dusk.CircleAmount < 0.05f) break;
            }

            Assert.IsTrue(dusk.Answered, "the ritual did not finish after the focus loss");
            Assert.AreEqual(3, dusk.Breaths);
            Assert.AreEqual(1, controller.LiveCount("breaths"));
            Assert.IsFalse(controller.Dismissed);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator PauseAt_Inhale3()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.FixedStep();
            DuskRitualController dusk = Dusk(controller);

            var sb = new StringBuilder();
            Line(sb, 0.20f, "PinchHold", 2.00f, DuskRitualController.PlantId);
            Line(sb, 2.20f, "Release", -1f, DuskRitualController.PlantId);
            Line(sb, 3.60f, "PinchHold", 2.00f, DuskRitualController.PlantId);
            Line(sb, 5.60f, "Release", -1f, DuskRitualController.PlantId);
            Line(sb, 7.00f, "PinchHold", 1.20f, DuskRitualController.PlantId);
            Line(sb, 7.60f, "PalmOpen", -1f, null);
            Line(sb, 8.00f, "PalmOpen", -1f, null);
            Line(sb, 8.20f, "Release", -1f, DuskRitualController.PlantId);
            Line(sb, 8.70f, "PinchHold", 2.00f, DuskRitualController.PlantId);
            Line(sb, 10.70f, "Release", -1f, DuskRitualController.PlantId);
            Trail(sb, 10.70f, 2.2f);
            SundialPlay.Play(controller, sb.ToString());

            bool sawFreeze = false;
            bool sawBack = false;
            bool dropped = false;
            int seen = 0;
            float app = 0f;
            while (app < 16f)
            {
                yield return null;
                app += Time.deltaTime;
                if (dusk.Breaths < seen) dropped = true;
                if (dusk.Breaths > seen) seen = dusk.Breaths;
                if (!sawFreeze && controller.Dismissed && dusk.PausedFor > 0.10f)
                {
                    sawFreeze = true;
                    Assert.AreEqual(2, dusk.Breaths);
                    Assert.IsFalse(dusk.Answered);
                    Assert.IsFalse(dusk.AwaitingContinue, "a short pause must not restart or offer a new ritual");
                    Assert.AreEqual(0, controller.LiveCount("breaths"));
                    Assert.Greater(dusk.CircleAmount, 0.02f);
                }
                if (sawFreeze && !sawBack && !controller.Dismissed && dusk.Breaths == 2 && dusk.PausedFor > 0.10f)
                {
                    sawBack = true;
                    Assert.IsFalse(dusk.Answered);
                }
                if (dusk.Answered && dusk.CircleAmount < 0.05f) break;
            }

            Assert.IsTrue(sawFreeze, "the third inhale did not pause");
            Assert.IsTrue(sawBack, "the dial did not return on the second palm");
            Assert.IsFalse(dropped, "the count restarted");
            Assert.IsTrue(dusk.Answered);
            Assert.AreEqual(3, dusk.Breaths);
            Assert.AreEqual(1, dusk.CueCount(DuskRitualController.CueChime));
            Assert.AreEqual(1, controller.LiveCount("breaths"));
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Record_JourneyFrames()
        {
            string dir = RunDir();
            Directory.CreateDirectory(dir);
            DisableAsyncShaders();

            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.FixedStep();
            for (int i = 0; i < 5; i++)
            {
                Keep(controller, "top3");
                if (i < 4) Assert.IsTrue(controller.ShiftDay(1));
            }
            Assert.IsTrue(controller.ShiftDay(1));
            Assert.IsTrue(controller.ShiftDay(1));
            Assert.IsTrue(controller.ShiftDay(1));
            yield return SundialPlay.Seconds(1.50f);
            PlantState missed = SundialPlay.Midday(controller);
            Assert.AreEqual(TileState.Missed, missed.Window[5]);
            Assert.AreEqual(Stage.Young, missed.Stage);
            Assert.AreEqual(Bloom.Bud, missed.Bloom);
            Camera cam = PoseDial();
            AttachPlate(cam);
            Shot(cam, Path.Combine(dir, "missed.png"));

            var sb = new StringBuilder();
            Line(sb, 0.05f, "Poke", -1f, SettingsTabs.TabId);
            Line(sb, 0.25f, "Poke", -1f, SettingsTabs.BedsId);
            Line(sb, 0.45f, "Poke", -1f, SettingsTabs.VoiceId);
            SundialPlay.Play(controller, sb.ToString());
            yield return SundialPlay.Seconds(0.70f);
            Assert.IsTrue(controller.Settings.Open);
            Assert.IsTrue(controller.Service.Beds);
            Assert.IsTrue(controller.Service.Voice);
            Assert.IsFalse(controller.Service.Mute);
            Assert.IsFalse(controller.Service.ReducedMotion);
            Shot(cam, Path.Combine(dir, "settings.png"));

            yield return SundialPlay.Unload();
            yield return Boot(SundialPlay.FreshDir(), Clock2130(), c => controller = c);
            GrowWeek(controller);
            yield return SundialPlay.Seconds(0.40f);
            PlantState dusk = Plant(controller, "winddown");
            Assert.AreEqual(Bloom.Open, dusk.Bloom);
            Assert.AreEqual(Stage.Full, dusk.Stage);
            Assert.AreEqual(1, Count(dusk, TileState.Missed));
            Assert.AreEqual(Stage.Sprout, Plant(controller, "morning").Stage);
            Assert.AreEqual(Bloom.Bud, Plant(controller, "midday").Bloom);
            cam = PoseDial();
            AttachPlate(cam);
            Shot(cam, Path.Combine(dir, "day7.png"));
        }

        static IEnumerator Boot(string dir, FixedClock clock, Action<SundialController> ready)
        {
            SundialService.DevSeedOnFresh = true;
            SundialService.FreshReducedMotion = null;
            SundialController.SaveDirectoryOverride = dir;
            SundialController.ClockOverride = clock;
            yield return SundialPlay.LoadMain(ready);
            SundialPlay.FixedStep();
            yield return null;
        }

        static void GrowWeek(SundialController controller)
        {
            for (int i = 0; i < 14; i++)
            {
                TendResult ritual = controller.Service.TendRitual("breaths");
                Assert.IsTrue(ritual.Ok, ritual.Reason);
                if (i == 0 || i == 1) Keep(controller, "water");
                if (i >= 10) Keep(controller, "top3");
                Assert.IsTrue(controller.ShiftDay(1));
            }
            Assert.IsTrue(controller.ShiftDay(1));
        }

        static void Keep(SundialController controller, string habitId)
        {
            controller.Service.Arm(habitId);
            TendEvent committed = controller.Service.Flush();
            Assert.IsNotNull(committed, habitId);
        }

        static PlantState Plant(SundialController controller, string arc)
        {
            HabitDef habit = controller.Service.HabitForArc(arc);
            Assert.IsNotNull(habit, arc);
            PlantState plant = controller.Service.PlantFor(habit);
            Assert.IsNotNull(plant, arc);
            Assert.AreEqual(7, plant.Window.Length);
            return plant;
        }

        static int Count(PlantState plant, TileState state)
        {
            int count = 0;
            for (int i = 0; i < plant.Window.Length; i++)
            {
                if (plant.Window[i] == state) count++;
            }
            return count;
        }

        static string Label(TileState state)
        {
            switch (state)
            {
                case TileState.Kept: return TileLabels.Kept;
                case TileState.Late: return TileLabels.Late;
                case TileState.Missed: return TileLabels.Missed;
                case TileState.Today: return TileLabels.Today;
                case TileState.Before: return TileLabels.Before;
                default: return TileLabels.Neutral;
            }
        }

        static TextMesh AskMesh(string arc)
        {
            GameObject go = GameObject.Find("tile." + arc + ".yesterday.ask");
            Assert.IsNotNull(go, arc);
            TextMesh mesh = go.GetComponent<TextMesh>();
            Assert.IsNotNull(mesh, arc);
            return mesh;
        }

        static void AssertNoShame(SundialController controller)
        {
            for (int i = 0; i < controller.Cues.Count; i++)
            {
                string cue = controller.Cues[i].ToLowerInvariant();
                Assert.IsFalse(cue.Contains("streak") || cue.Contains("fail") || cue.Contains("shame") || cue.Contains("wilt"), controller.Cues[i]);
            }
        }

        static TendEvent LiveTend(SundialController controller, string habitId)
        {
            var events = controller.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                TendEvent ev = events[i];
                if (ev != null && ev.HabitId == habitId && !ev.UndoneAtUtcMs.HasValue) return ev;
            }
            Assert.Fail("no live tend for " + habitId);
            return null;
        }

        static DuskRitualController Dusk(SundialController controller)
        {
            DuskRitualController dusk = controller.GetComponent<DuskRitualController>();
            Assert.IsNotNull(dusk);
            return dusk;
        }

        static FixedClock Clock2130()
        {
            return new FixedClock(new DateTimeOffset(2026, 10, 3, 21, 30, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
        }

        static float Holds(StringBuilder sb, float start, int count, float hold, float gap)
        {
            float t = start;
            for (int i = 0; i < count; i++)
            {
                Line(sb, t, "PinchHold", hold, DuskRitualController.PlantId);
                t += hold;
                Line(sb, t, "Release", -1f, DuskRitualController.PlantId);
                if (i + 1 < count) t += gap;
            }
            return t;
        }

        static void Trail(StringBuilder sb, float t, float dur)
        {
            Line(sb, t, "Look", dur, DuskRitualController.PlantId);
        }

        static void Line(StringBuilder sb, float t, string intent, float dur, string target)
        {
            sb.Append("{\"t\":").Append(t.ToString("0.00", CultureInfo.InvariantCulture));
            sb.Append(",\"intent\":\"").Append(intent).Append("\"");
            if (!string.IsNullOrEmpty(target))
                sb.Append(",\"target\":\"").Append(target).Append("\"");
            if (dur >= 0f)
                sb.Append(",\"dur\":").Append(dur.ToString("0.00", CultureInfo.InvariantCulture));
            sb.Append("}\n");
        }

        static string RunDir()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            return Path.Combine(root, "orchestration", "runs", "sundial", "T-SUN-018");
        }

        static void Shot(Camera cam, string path)
        {
            Texture2D warm = FrameGrab.RenderToTexture(cam, 320, 180, 1);
            UnityEngine.Object.Destroy(warm);
            Texture2D tex = FrameGrab.RenderToTexture(cam, Framings.DefaultWidth, Framings.DefaultHeight, 4);
            byte[] png = tex.EncodeToPNG();
            Color32[] pixels = tex.GetPixels32();
            File.WriteAllBytes(path, png);
            ImageStats stats = ImageCheck.Analyze(pixels, tex.width, tex.height, png);
            File.WriteAllText(path + ".check.json", ImageCheck.ToJson(stats));
            UnityEngine.Object.Destroy(tex);
            Assert.Less(stats.MagentaFrac, 0.001f, path + " magentaFrac " + stats.MagentaFrac);
            Assert.Greater(new FileInfo(path).Length, 1000, path);
        }

        static Camera PoseDial()
        {
            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] == null) continue;
                string name = behaviours[i].GetType().Name;
                if (name == "SeatedRig" || name == "KeyboardMouseHeadPose")
                    behaviours[i].enabled = false;
            }
            Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t != null && (t.name == "PcRoomPlate" || t.name == "PassthroughPlate"))
                    t.gameObject.SetActive(false);
            }
            GameObject eye = GameObject.Find("EyeCamera");
            Assert.IsNotNull(eye, "EyeCamera missing");
            Camera cam = eye.GetComponent<Camera>();
            Assert.IsNotNull(cam);
            GameObject root = GameObject.Find("DialRoot");
            Assert.IsNotNull(root, "DialRoot missing");
            DialG1Framing framing = cam.gameObject.GetComponent<DialG1Framing>();
            if (framing == null) framing = cam.gameObject.AddComponent<DialG1Framing>();
            framing.Target = root.transform;
            framing.Cam = cam;
            framing.Apply();
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 50f;
            cam.allowHDR = false;
            return cam;
        }

        static void AttachPlate(Camera cam)
        {
            string path = Path.Combine(Application.dataPath, "Art", "Plates", "plate-dial.png");
            Assert.IsTrue(File.Exists(path), path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(path)), path);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            Shader shader = Shader.Find("Fidelity/Plate");
            Assert.IsNotNull(shader, "Fidelity/Plate missing");
            var mat = new Material(shader);
            mat.SetTexture("_MainTex", tex);
            mat.SetFloat("_Exposure", 1f);
            const float depth = 2f;
            const float fov = 30f;
            float worldHeight = 2f * depth * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float worldWidth = worldHeight * (1824f / 1024f);
            var go = new GameObject("CapturePlate");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, depth);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(worldWidth, worldHeight, 1f);
            var mesh = new Mesh { name = "JourneyPlate" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static void DisableAsyncShaders()
        {
            Type shaderUtil = Type.GetType("UnityEditor.ShaderUtil, UnityEditor");
            if (shaderUtil == null) return;
            PropertyInfo prop = shaderUtil.GetProperty("allowAsyncCompilation", BindingFlags.Static | BindingFlags.Public);
            if (prop != null) prop.SetValue(null, false);
        }
    }
}
