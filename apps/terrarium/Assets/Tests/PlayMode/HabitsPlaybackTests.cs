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
    public class HabitsPlaybackTests
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
        [Timeout(300000)]
        public IEnumerator Habits_PlantThree()
        {
            RitualHarness.FixedStep();
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            controller.HandleDev(DevCommand.SeedPackets);
            yield return null;
            Assert.AreEqual(6, CountTargets("seed."), "six packets along the desk");
            Assert.AreEqual(1, controller.CueCount("seed.appear"));

            RitualHarness.Play(controller, Pokes("seed.walk", "seed.water", "seed.read"));
            yield return Seconds(1.4f);

            Assert.AreEqual(3, controller.Service.Document.Habits.Count);
            Assert.AreEqual("walk", controller.Service.Document.Habits[0].Id);
            Assert.AreEqual("walk", controller.Service.Document.Habits[0].PresetKey);
            Assert.AreEqual("GlowSprig", controller.Service.Document.Habits[0].Species);
            Assert.AreEqual(HabitKind.LifeCheckIn, controller.Service.Document.Habits[0].Kind);
            Assert.AreEqual("MoonMoss", controller.Service.Document.Habits[1].Species);
            Assert.AreEqual("StarFern", controller.Service.Document.Habits[2].Species);
            Assert.AreEqual(3, CountTargets("seed."), "planted packets leave the desk");

            CompanionSprig walk = Sprig("walk");
            Assert.IsNotNull(walk);
            Assert.AreEqual(0, walk.Leaves);
            Assert.IsFalse(controller.YesterdayVisible("walk"));
            AssertTodayAtLeast3cm("walk");

            string json = File.ReadAllText(Path.Combine(GardenService.DirectoryOverride, "save.json"));
            StringAssert.Contains("walk", json);
            StringAssert.Contains("water", json);
            StringAssert.Contains("read", json);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Checkin_Today_GrowsLeaf()
        {
            RitualHarness.FixedStep();
            yield return RitualHarness.OpenMain();
            JarRitualController controller = Plant(new[] { "seed.walk" });
            yield return Seconds(1.0f);

            RitualHarness.Play(controller, LookThenPinch("label.walk.today", 0.40f, 0.30f));
            yield return Seconds(0.6f);
            Assert.AreEqual(1, controller.ShownLeaves("walk"));
            Assert.AreEqual(0, controller.Ledger.KeptDays("walk"));
            Assert.IsTrue(controller.UndoMarkVisible("walk"));
            Assert.AreEqual(1, controller.CueCount("habit.pluck"));
            Assert.AreEqual(0, controller.LastPluckSemitones);

            yield return Seconds(6.2f);
            TendEvent ev = Live("walk", controller.Ledger);
            Assert.IsNotNull(ev);
            Assert.AreEqual(controller.Service.TodayIndex, ev.Day);
            Assert.IsFalse(ev.Late);
            Assert.AreEqual(TendSource.Pinch, ev.Source);
            Assert.AreEqual(1, controller.Ledger.KeptDays("walk"));
            Assert.AreEqual(1, controller.ShownLeaves("walk"));
            Assert.IsFalse(controller.UndoMarkVisible("walk"));
            Assert.AreEqual(1, Sprig("walk").Leaves);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Checkin_Yesterday_DatedYesterday()
        {
            RitualHarness.FixedStep();
            yield return RitualHarness.OpenMain();
            JarRitualController controller = Plant(new[] { "seed.walk" });
            yield return Seconds(1.0f);
            Assert.IsTrue(controller.ShiftDay(1));
            yield return null;
            Assert.IsTrue(controller.YesterdayVisible("walk"));

            RitualHarness.Play(controller, Pokes("label.walk.yesterday"));
            yield return Seconds(6.8f);

            TendEvent ev = Live("walk", controller.Ledger);
            Assert.IsNotNull(ev);
            Assert.AreEqual(controller.Service.TodayIndex - 1, ev.Day);
            Assert.IsTrue(ev.Late);
            Assert.AreEqual(TendSource.Backfill, ev.Source);
            DateTimeOffset stamp = DateTimeOffset.FromUnixTimeMilliseconds(ev.AtUtcMs);
            Assert.AreEqual(4, stamp.UtcDateTime.Day, "the stamp is the real clock, on the new day");
            Assert.AreEqual(1, controller.ShownLeaves("walk"));
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Undo_InsideWindow_Restores()
        {
            RitualHarness.FixedStep();
            yield return RitualHarness.OpenMain();
            JarRitualController controller = Plant(new[] { "seed.walk" });
            yield return Seconds(1.0f);
            int habits = controller.Service.Document.Habits.Count;

            RitualHarness.Play(controller, PokesAt(new[] { "label.walk.today", "undo.walk" }, new[] { 0.20f, 0.80f }));
            yield return Seconds(1.5f);

            Assert.AreEqual(0, controller.Ledger.KeptDays("walk"));
            Assert.AreEqual(0, controller.ShownLeaves("walk"));
            Assert.AreEqual(0, controller.Ledger.Events.Count);
            Assert.AreEqual(habits, controller.Service.Document.Habits.Count);
            Assert.AreEqual(1, controller.CueCount("habit.pluck"));
            Assert.AreEqual(1, controller.CueCount("habit.undo"));
            Assert.IsFalse(controller.UndoMarkVisible("walk"));
            Assert.AreEqual(0, Sprig("walk").Leaves);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Undo_FlushOnPause_Commits()
        {
            RitualHarness.FixedStep();
            yield return RitualHarness.OpenMain();
            JarRitualController controller = Plant(new[] { "seed.walk" });
            yield return Seconds(1.0f);

            RitualHarness.Play(controller, Pokes("label.walk.today"));
            yield return Seconds(0.55f);
            Assert.AreEqual(0, controller.Ledger.KeptDays("walk"));
            Assert.IsTrue(controller.UndoMarkVisible("walk"));

            var go = new GameObject("PauseProbe");
            SceneManager.MoveGameObjectToScene(go, controller.gameObject.scene);
            var probe = go.AddComponent<PauseProbe>();
            controller.SetSource(probe);
            probe.Raise();

            TendEvent ev = Live("walk", controller.Ledger);
            Assert.IsNotNull(ev);
            Assert.AreEqual(controller.Service.TodayIndex, ev.Day);
            Assert.IsFalse(ev.Late);
            Assert.IsFalse(controller.UndoMarkVisible("walk"));
            Assert.IsFalse(controller.TryUndoHabit());
            Assert.AreEqual(0, controller.CueCount("habit.undo"));
            Assert.AreEqual(1, controller.ShownLeaves("walk"));
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Habits_FourthIgnored()
        {
            RitualHarness.FixedStep();
            yield return RitualHarness.OpenMain();
            JarRitualController controller = Plant(new[] { "seed.walk", "seed.water", "seed.read" });
            yield return Seconds(1.4f);
            Assert.AreEqual(3, controller.Service.Document.Habits.Count);

            RitualHarness.Play(controller, Pokes("seed.stretch"));
            yield return Seconds(0.8f);

            Assert.AreEqual(3, controller.Service.Document.Habits.Count);
            Assert.IsNull(GameObject.Find("Companion-stretch"));
            GameObject packet = GameObject.Find("Packet-stretch");
            Assert.IsNotNull(packet);
            Assert.IsTrue(packet.activeInHierarchy);
            Assert.AreEqual(3, CountTargets("seed."));
        }

        static JarRitualController Plant(string[] seeds)
        {
            JarRitualController controller = RitualHarness.Controller();
            controller.HandleDev(DevCommand.SeedPackets);
            RitualHarness.Play(controller, Pokes(seeds));
            return controller;
        }

        static void AssertTodayAtLeast3cm(string preset)
        {
            GameObject today = GameObject.Find("Today-" + preset);
            Assert.IsNotNull(today);
            var box = today.GetComponent<BoxCollider>();
            Assert.IsNotNull(box);
            Vector3 world = Vector3.Scale(box.size, today.transform.lossyScale);
            Assert.GreaterOrEqual(world.x, 0.03f);
            Assert.GreaterOrEqual(world.y, 0.03f);
        }

        static CompanionSprig Sprig(string preset)
        {
            GameObject go = GameObject.Find("Companion-" + preset);
            Assert.IsNotNull(go, "Companion-" + preset);
            return go.GetComponent<CompanionSprig>();
        }

        static TendEvent Live(string habitId, Ledger ledger)
        {
            TendEvent found = null;
            for (int i = 0; i < ledger.Events.Count; i++)
            {
                TendEvent ev = ledger.Events[i];
                if (ev.HabitId == habitId && !ev.UndoneAtUtcMs.HasValue) found = ev;
            }
            return found;
        }

        static int CountTargets(string prefix)
        {
            int count = 0;
            IntentTarget[] all = UnityEngine.Object.FindObjectsByType<IntentTarget>(FindObjectsInactive.Exclude);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Id != null && all[i].Id.StartsWith(prefix, StringComparison.Ordinal)) count++;
            }
            return count;
        }

        static IEnumerator Seconds(float seconds)
        {
            float app = 0f;
            while (app < seconds)
            {
                yield return null;
                app += Time.deltaTime;
            }
        }

        static string Pokes(params string[] ids)
        {
            var times = new float[ids.Length];
            for (int i = 0; i < ids.Length; i++) times[i] = 0.20f + i * 0.30f;
            return PokesAt(ids, times);
        }

        static string PokesAt(string[] ids, float[] times)
        {
            var sb = new StringBuilder();
            float end = 0.4f;
            for (int i = 0; i < ids.Length; i++)
            {
                sb.Append("{\"t\":").Append(times[i].ToString("0.00", CultureInfo.InvariantCulture));
                sb.Append(",\"intent\":\"Poke\",\"target\":\"").Append(ids[i]).Append("\"}\n");
                end = times[i] + 0.4f;
            }
            sb.Append("{\"t\":").Append(end.ToString("0.00", CultureInfo.InvariantCulture));
            sb.Append(",\"intent\":\"Look\",\"target\":\"").Append(ids[ids.Length - 1]).Append("\",\"dur\":0.20}\n");
            return sb.ToString();
        }

        static string LookThenPinch(string target, float lookDur, float pinchAt)
        {
            return "{\"t\":0.00,\"intent\":\"Look\",\"target\":\"" + target + "\",\"dur\":" + lookDur.ToString("0.00", CultureInfo.InvariantCulture) + "}\n" +
                   "{\"t\":" + pinchAt.ToString("0.00", CultureInfo.InvariantCulture) + ",\"intent\":\"Pinch\",\"target\":\"" + target + "\"}\n";
        }

        sealed class PauseProbe : MonoBehaviour, IHandIntentSource
        {
            public event Action<HandIntent> Intent;
            public event Action SystemPause;
            public bool IsPinching { get { return false; } }
            public Ray LookRay { get { return new Ray(Vector3.zero, Vector3.forward); } }
            public float PinchStrength { get { return 0f; } }
            public bool IsTracked { get { return true; } }
            public string BindingHint(HandIntentKind kind) { return ""; }

            public void Raise()
            {
                if (SystemPause != null) SystemPause.Invoke();
            }
        }
    }
}
