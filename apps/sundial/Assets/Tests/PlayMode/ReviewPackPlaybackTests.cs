using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using GardenVR.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GardenVR.Sundial.Tests.PlayMode
{
    /// <summary>
    /// One playback of every hand intent, plus the provider binding hints the player shows.
    /// The log is the R1 check that PLAY.md matches the build.
    /// </summary>
    public class ReviewPackPlaybackTests
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
        public IEnumerator EachControl_ByPlayback()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.FixedStep();
            yield return null;

            KeyboardMouseIntentSource keys = Object.FindAnyObjectByType<KeyboardMouseIntentSource>();
            Assert.IsNotNull(keys, "Main has no keyboard provider");
            var hints = new StringBuilder();
            hints.Append("Look=").Append(keys.BindingHint(HandIntentKind.Look));
            hints.Append(" Pinch=").Append(keys.BindingHint(HandIntentKind.Pinch));
            hints.Append(" PinchHold=").Append(keys.BindingHint(HandIntentKind.PinchHold));
            hints.Append(" Release=").Append(keys.BindingHint(HandIntentKind.Release));
            hints.Append(" Poke=").Append(keys.BindingHint(HandIntentKind.Poke));
            hints.Append(" PalmOpen=").Append(keys.BindingHint(HandIntentKind.PalmOpen));
            Debug.Log("[Review] hints " + hints);

            Assert.AreEqual("mouse", keys.BindingHint(HandIntentKind.Look));
            Assert.AreEqual("click", keys.BindingHint(HandIntentKind.Pinch));
            Assert.AreEqual("Space or mouse", keys.BindingHint(HandIntentKind.PinchHold));
            Assert.AreEqual("release", keys.BindingHint(HandIntentKind.Release));
            Assert.AreEqual("F", keys.BindingHint(HandIntentKind.Poke));
            Assert.AreEqual("hold P", keys.BindingHint(HandIntentKind.PalmOpen));

            var sb = new StringBuilder();
            Line(sb, 0.00f, "Look", 0.40f, "plant.midday");
            Line(sb, 0.20f, "Pinch", -1f, "plant.midday");
            Line(sb, 0.70f, "PinchHold", 2.00f, DuskRitualController.PlantId);
            Line(sb, 2.70f, "Release", -1f, DuskRitualController.PlantId);
            Line(sb, 2.70f, "Look", 1.40f, DuskRitualController.PlantId);
            Line(sb, 4.20f, "Poke", -1f, SettingsTabs.TabId);
            Line(sb, 4.60f, "PalmOpen", -1f, null);
            ScriptedIntentSource source = SundialPlay.Play(controller, sb.ToString());
            Assert.AreEqual(keys.BindingHint(HandIntentKind.PinchHold), source.BindingHint(HandIntentKind.PinchHold));
            Assert.AreEqual(keys.BindingHint(HandIntentKind.PalmOpen), source.BindingHint(HandIntentKind.PalmOpen));

            bool sawHalo = false;
            bool sawTend = false;
            bool sawHold = false;
            bool sawRelease = false;
            bool sawPoke = false;
            bool sawPalm = false;
            float app = 0f;
            while (app < 5.20f)
            {
                yield return null;
                app += Time.deltaTime;
                if (controller.LookHaloOn) sawHalo = true;
                if (controller.LiveCount("top3") == 1) sawTend = true;
                DuskRitualController dusk = controller.GetComponent<DuskRitualController>();
                if (dusk != null && dusk.Active) sawHold = true;
                if (dusk != null && dusk.Breaths >= 1) sawRelease = true;
                if (controller.Settings != null && controller.Settings.Open) sawPoke = true;
                if (controller.Dismissed) sawPalm = true;
            }

            string log = "hints " + hints + "\r\n"
                + "Look halo=" + sawHalo + "\r\n"
                + "Pinch tend top3=" + controller.LiveCount("top3") + "\r\n"
                + "PinchHold duskActive=" + sawHold + "\r\n"
                + "Release breaths=" + Breaths(controller) + "\r\n"
                + "Poke settingsOpen=" + sawPoke + "\r\n"
                + "PalmOpen dismissed=" + sawPalm + "\r\n"
                + "clock=2026-10-03 14:20Z dev-seed\r\n";
            Debug.Log("[Review] " + log.Replace("\r\n", " | "));
            WriteLog(log);

            Assert.IsTrue(sawHalo, "Look did not light the halo");
            Assert.IsTrue(sawTend, "Pinch did not tend midday");
            Assert.AreEqual(1, controller.LiveCount("top3"));
            Assert.IsTrue(sawHold, "PinchHold did not start the dusk breath");
            Assert.IsTrue(sawRelease, "Release did not count a breath");
            Assert.IsTrue(sawPoke, "Poke did not open Settings");
            Assert.IsTrue(sawPalm, "PalmOpen did not put the dial away");
        }

        static int Breaths(SundialController controller)
        {
            DuskRitualController dusk = controller.GetComponent<DuskRitualController>();
            return dusk == null ? 0 : dusk.Breaths;
        }

        static void WriteLog(string text)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            string dir = Path.Combine(root, "orchestration", "runs", "sundial", "T-SUN-032");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "playback-controls.txt"), text);
        }

        static void Line(StringBuilder sb, float t, string intent, float dur, string target)
        {
            sb.Append("{\"t\":").Append(t.ToString("0.00", CultureInfo.InvariantCulture));
            sb.Append(",\"intent\":\"").Append(intent).Append("\"");
            if (dur >= 0f)
                sb.Append(",\"dur\":").Append(dur.ToString("0.00", CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(target))
                sb.Append(",\"target\":\"").Append(target).Append("\"");
            sb.Append("}\n");
        }
    }
}
