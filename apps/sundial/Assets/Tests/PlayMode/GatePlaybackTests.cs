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

namespace GardenVR.Sundial.Tests.PlayMode
{
    /// <summary>
    /// Gate S2 and A7. Ten fresh playthroughs of the canonical journey must end on the same
    /// state JSON. Reduced motion shows the finished picture instead of the in-between frames.
    /// </summary>
    public class GatePlaybackTests
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
        public IEnumerator CanonicalJourney_TenRuns()
        {
            string first = null;
            var log = new StringBuilder();
            for (int run = 1; run <= 10; run++)
            {
                string state = null;
                yield return OneJourney(value => state = value);
                string hash = Sha256(state);
                log.Append("run ").Append(run.ToString(CultureInfo.InvariantCulture));
                log.Append(" sha256=").Append(hash).Append('\n');
                Debug.Log("[playback] run=" + run + " sha256=" + hash);
                if (run == 1) first = state;
                else Assert.AreEqual(first, state, "run " + run + " end state drifted");
                WriteLog(log, first, run == 10);
            }

            Assert.IsNotNull(first);
            Assert.IsTrue(first.Contains("\"chapter\":\"firstRun\""));
            Assert.IsTrue(first.Contains("\"chapter\":\"normalDay\""));
            Assert.IsTrue(first.Contains("\"chapter\":\"missedDay\""));
            Assert.IsTrue(first.Contains("\"chapter\":\"day7\""));
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator ReducedMotion_ShowsEndStates()
        {
            SundialController controller = null;
            yield return Boot(SundialPlay.FreshDir(), Clock(2026, 10, 3, 14, 20), false, true, c => controller = c);
            float app = 0f;
            while (app < 0.35f)
            {
                yield return null;
                app += Time.deltaTime;
            }

            Assert.GreaterOrEqual(controller.View.appear, 0.99f, "the dial should already be drawn");
            Assert.AreEqual(125f, controller.View.gnomonDeg, 0.75f, "the shadow should already sit at now");
            Assert.IsFalse(controller.View.boil, "reduced motion stops the boil");
            Assert.IsTrue(controller.View.reducedMotion);
            Assert.AreEqual(FirstRunSteps.PickMorning, controller.Wizard.Step);
            GameObject packet = GameObject.Find("seed.morning");
            Assert.IsNotNull(packet, "the packets should already be up");
            Assert.Greater(packet.transform.localPosition.y, controller.View.faceY, "a packet still rising is not the end pose");
            Assert.IsFalse(controller.View.showPlants);

            yield return Boot(SundialPlay.FreshDir(), Clock(2026, 10, 3, 14, 20), true, true, c => controller = c);
            Assert.IsFalse(controller.View.boil);
            SundialPlay.Play(controller, "{\"t\":0.05,\"intent\":\"Pinch\",\"target\":\"plant.midday\"}\n");
            bool sawFill = false;
            app = 0f;
            while (app < 0.28f)
            {
                yield return null;
                app += Time.deltaTime;
                if (controller.View.pulse <= 0f && !controller.UndoVisible) continue;
                sawFill = true;
                Assert.AreEqual(1f, controller.View.tileFill[13], 0.001f, "the ink should already fill today's tile");
                Assert.AreEqual(SundialArcs.TileDigit(TileState.Kept), controller.View.tiles[13]);
                break;
            }

            Assert.IsTrue(sawFill, "the pinch did not reach the tile");
            Assert.IsFalse(controller.View.boil);
        }

        static IEnumerator OneJourney(Action<string> done)
        {
            var doc = new StringBuilder();
            string dir = SundialPlay.FreshDir();
            SundialController controller = null;
            yield return Boot(dir, Clock(2026, 10, 3, 14, 20), false, false, c => controller = c);
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
            Assert.Greater(controller.Wizard.FirstTendAt, 0f);
            Assert.LessOrEqual(controller.Wizard.FirstTendAt, 60f);
            Assert.LessOrEqual(controller.Wizard.RitualDoneAt, 180f);
            Append(doc, "firstRun", controller.StateJson);

            yield return Boot(dir, Clock(2026, 10, 4, 7, 10), false, false, c => controller = c);
            Assert.AreEqual(LoadOutcome.Loaded, controller.Outcome);
            SundialPlay.Play(controller, PinchCommit("plant.morning"));
            yield return SundialPlay.Seconds(2.1f);
            Assert.IsFalse(controller.Dismissed);
            Assert.GreaterOrEqual(controller.LiveCount("water"), 1);

            yield return Boot(dir, Clock(2026, 10, 4, 12, 30), false, false, c => controller = c);
            SundialPlay.Play(controller, PinchCommit("plant.midday"));
            yield return SundialPlay.Seconds(2.1f);
            Assert.IsFalse(controller.Dismissed);
            Assert.GreaterOrEqual(controller.LiveCount("top3"), 2);

            yield return Boot(dir, Clock(2026, 10, 4, 22, 10), false, false, c => controller = c);
            DuskRitualController dusk = controller.GetComponent<DuskRitualController>();
            Assert.IsNotNull(dusk);
            Assert.IsTrue(dusk.OfferVisible, "the wind-down hour should offer the breaths");
            SundialPlay.Play(controller, DuskScripts.ThreeBreaths());
            yield return SundialPlay.Seconds(13f);
            Assert.IsTrue(dusk.Answered, "the normal day did not finish the breaths");
            Assert.GreaterOrEqual(controller.LiveCount("breaths"), 2);
            Append(doc, "normalDay", controller.StateJson);

            yield return Boot(dir, Clock(2026, 10, 6, 14, 20), false, false, c => controller = c);
            PlantState midday = Plant(controller, "midday");
            Assert.AreEqual(TileState.Missed, midday.Window[5]);
            Assert.AreEqual(TileState.Today, midday.Window[6]);
            Assert.IsTrue(midday.CanBackfillYesterday);
            Assert.AreEqual(2, midday.LifetimeKept);
            Assert.AreEqual(Stage.Sprout, midday.Stage);
            Append(doc, "missedDay", controller.StateJson);

            yield return Boot(SundialPlay.FreshDir(), Clock(2026, 10, 3, 21, 30), true, false, c => controller = c);
            GrowWeek(controller);
            yield return null;
            PlantState wind = Plant(controller, "winddown");
            Assert.AreEqual(Bloom.Open, wind.Bloom);
            Assert.AreEqual(Stage.Full, wind.Stage);
            Assert.AreEqual(TileState.Missed, wind.Window[5]);
            Assert.AreEqual(TileState.Today, wind.Window[6]);
            Append(doc, "day7", controller.StateJson);

            if (done != null) done(doc.ToString());
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

        static string PinchCommit(string target)
        {
            return "{\"t\":0.10,\"intent\":\"Look\",\"target\":\"" + target + "\",\"dur\":0.40}\n" +
                   "{\"t\":0.40,\"intent\":\"Pinch\",\"target\":\"" + target + "\"}\n" +
                   "{\"t\":0.70,\"intent\":\"PalmOpen\"}\n" +
                   "{\"t\":1.40,\"intent\":\"PalmOpen\"}\n";
        }

        static void Append(StringBuilder doc, string chapter, string stateJson)
        {
            doc.Append("{\"chapter\":\"").Append(chapter).Append("\",\"state\":");
            doc.Append(stateJson);
            doc.Append("}\n");
        }

        static IEnumerator Boot(string dir, IClock clock, bool devSeed, bool reduced, Action<SundialController> ready)
        {
            SundialService.DevSeedOnFresh = devSeed;
            SundialService.FreshReducedMotion = reduced ? (bool?)true : null;
            SundialController.SaveDirectoryOverride = dir;
            SundialController.ClockOverride = clock;
            FirstRunWizard.SuppressBreathOffer = false;
            Time.captureDeltaTime = 1f / 60f;
            yield return SundialPlay.LoadMain(ready);
            SundialPlay.FixedStep();
            yield return null;
        }

        static FixedClock Clock(int year, int month, int day, int hour, int minute)
        {
            return new FixedClock(new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
        }

        static void WriteLog(StringBuilder runs, string state, bool done)
        {
            var body = new StringBuilder();
            body.Append(runs);
            if (done && state != null)
            {
                body.Append("identical=true\n");
                body.Append("--- end-state ---\n");
                body.Append(state);
            }
            string dir = EvidenceDir();
            File.WriteAllText(Path.Combine(dir, "playback-10x.txt"), body.ToString());
        }

        static string EvidenceDir()
        {
            string env = Environment.GetEnvironmentVariable("GARDEN_RUN_DIR");
            string dir = string.IsNullOrEmpty(env)
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "orchestration", "runs", "sundial", "T-SUN-019"))
                : env;
            Directory.CreateDirectory(dir);
            return dir;
        }

        static string Sha256(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? "");
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }
    }
}
