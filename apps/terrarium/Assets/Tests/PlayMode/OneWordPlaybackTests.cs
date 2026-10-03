using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using GardenVR.Capture;
using GardenVR.Core;
using GardenVR.Input;
using GardenVR.Room;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GardenVR.Terrarium.Tests
{
    public class OneWordPlaybackTests
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
        [Timeout(180000)]
        public IEnumerator OneWord_PickedAndKept()
        {
            string seq = Environment.GetEnvironmentVariable("GARDEN_SEQ_DIR");
            if (!string.IsNullOrEmpty(seq))
            {
                Directory.CreateDirectory(seq);
                DisableAsyncShaders();
            }

            int day = SeedToday(null);
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            Assert.IsTrue(controller.WordsBuilt, "the word stones were not built");
            Assert.IsTrue(controller.WordsOffered, "the stones were not offered after the ritual");
            Assert.AreEqual(OneWordRitual.LineOffer, controller.WordLine);
            Assert.AreEqual("calm", controller.WordStoneLabel(0));
            Assert.AreEqual("quiet", controller.WordStoneLabel(4));
            Assert.IsFalse(OneWordRitual.LineOffer.Contains("\u2014"));
            TerrariumState before = controller.Snapshot();
            Assert.AreEqual(1, before.Fronds);
            Assert.AreEqual(1, before.Rituals);
            Assert.AreEqual(0, before.DewToday);

            RitualHarness.Play(controller, PickQuiet());
            RitualHarness.FixedStep();
            SequenceRecorder recorder = null;
            if (!string.IsNullOrEmpty(seq))
                recorder = ArmRecorder(controller, seq);

            bool sawWord = false;
            bool sawLook = false;
            float app = 0f;
            while (app < 12f)
            {
                yield return null;
                app += Time.deltaTime;
                if (controller.WordLine == "quiet") sawWord = true;
                if (controller.LookBackPlaying
                    && controller.LookBackWord == "quiet"
                    && controller.LookBackEtch == "quiet")
                    sawLook = true;
                if (controller.WordSettled && sawLook && !controller.LookBackPlaying && app > 5f) break;
            }

            if (recorder != null) recorder.enabled = false;
            TerrariumState end = controller.Snapshot();
            Assert.IsTrue(sawWord, "the chosen word was not etched while it sank");
            Assert.IsTrue(sawLook, "the look-back did not etch the word");
            Assert.IsTrue(controller.WordSettled, "the stone did not settle: " + end.ToJson());
            Assert.AreEqual(4, controller.WordIndex);
            Assert.AreEqual("quiet", controller.WordStoneLabel(4));
            Assert.Less(controller.WordStoneY(4), JarView.MossBedY + 0.002f);
            Assert.AreEqual(1, end.Fronds);
            Assert.AreEqual(1, end.Rituals);
            Assert.AreEqual(0, end.DewToday);
            Assert.AreEqual(1f, end.Vitality, 0.001f);
            Assert.AreEqual(1, controller.CueCount("pebble.tap"));
            Assert.AreEqual(0, controller.CueCount("dew.drop"));
            Assert.AreEqual(0, controller.CueCount("answer.chime"));
            Assert.IsNull(controller.WordLine);
            Assert.IsFalse(controller.WordsOffered);

            TerrariumSave doc = controller.Service.Document;
            Assert.AreEqual(1, doc.DayWords.Count);
            Assert.AreEqual(day, doc.DayWords[0].Day);
            Assert.AreEqual("quiet", doc.DayWords[0].Word);
            string json = doc.ToJson();
            Assert.IsTrue(json.Contains("\"Word\":\"quiet\""), json);
            Assert.IsFalse(json.Contains("score"), json);
            Assert.IsFalse(json.Contains("mood"), json);
            Assert.IsFalse(json.Contains("\u2014"), json);
            string live = Path.Combine(GardenService.DirectoryOverride, SaveStore<TerrariumSave>.LiveName);
            string disk = File.ReadAllText(live);
            Assert.IsTrue(disk.Contains("\"Word\":\"quiet\""), disk);
            Assert.IsFalse(disk.Contains("\u2014"), disk);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator OneWord_AlreadyKept_Stays()
        {
            int day = SeedToday("light");
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            Assert.IsTrue(controller.WordsBuilt, "the word stones were not built");
            Assert.IsFalse(controller.WordsOffered);
            Assert.IsTrue(controller.WordSettled);
            Assert.AreEqual(5, controller.WordIndex);
            Assert.AreEqual("light", controller.WordStoneLabel(5));
            Assert.Less(controller.WordStoneY(5), JarView.MossBedY + 0.002f);

            RitualHarness.Play(controller, PickAgain());
            RitualHarness.FixedStep();
            float app = 0f;
            while (app < 2f)
            {
                yield return null;
                app += Time.deltaTime;
            }

            TerrariumState end = controller.Snapshot();
            Assert.AreEqual(1, end.Fronds);
            Assert.AreEqual(0, end.DewToday);
            Assert.AreEqual(1, end.Rituals);
            Assert.AreEqual(0, controller.CueCount("pebble.tap"));
            Assert.AreEqual(5, controller.WordIndex);
            Assert.AreEqual(1, controller.Service.Document.DayWords.Count);
            Assert.AreEqual(day, controller.Service.Document.DayWords[0].Day);
            Assert.AreEqual("light", controller.Service.Document.DayWords[0].Word);
        }

        static int SeedToday(string word)
        {
            var when = new DateTimeOffset(2026, 10, 3, 21, 0, 0, TimeSpan.Zero);
            int day = GardenDay.From(when, GardenDay.DefaultBoundary).Index;
            var save = new TerrariumSave();
            save.FrondDays = new[] { day };
            save.LastRitualDay = day;
            save.RitualsCompleted = 1;
            save.FirstRunStep = "tour.done";
            if (!string.IsNullOrEmpty(word))
                save.DayWords.Add(new KeptWord { Day = day, Word = word });
            string dir = Path.Combine(Path.GetTempPath(), "gvr-ter-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, SaveStore<TerrariumSave>.LiveName), save.ToJson());
            GardenService.DirectoryOverride = dir;
            GardenService.ClockOverride = new FixedClock(when, TimeZoneInfo.Utc);
            return day;
        }

        static string PickQuiet()
        {
            var sb = new StringBuilder();
            Line(sb, 0.40f, "Pinch", OneWordRitual.StoneId(4), -1f);
            Line(sb, 0.80f, "Pinch", OneWordRitual.StoneId(0), -1f);
            Line(sb, 2.30f, "PinchHold", JarRitualController.CorkId, 2.50f);
            Line(sb, 4.80f, "Release", JarRitualController.CorkId, -1f);
            Line(sb, 4.80f, "Look", null, 3f);
            return sb.ToString();
        }

        static string PickAgain()
        {
            var sb = new StringBuilder();
            Line(sb, 0.30f, "Pinch", OneWordRitual.StoneId(0), -1f);
            Line(sb, 0.70f, "Pinch", OneWordRitual.StoneId(4), -1f);
            Line(sb, 0.70f, "Look", null, 1.2f);
            return sb.ToString();
        }

        static void Line(StringBuilder sb, float t, string intent, string target, float dur)
        {
            sb.Append("{\"t\":").Append(t.ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append(",\"intent\":\"").Append(intent).Append("\"");
            if (!string.IsNullOrEmpty(target))
                sb.Append(",\"target\":\"").Append(target).Append("\"");
            if (dur >= 0f)
                sb.Append(",\"dur\":").Append(dur.ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append("}\n");
        }

        static SequenceRecorder ArmRecorder(JarRitualController controller, string dir)
        {
            SeatedRig rig = UnityEngine.Object.FindAnyObjectByType<SeatedRig>();
            if (rig != null) rig.enabled = false;
            GameObject eye = GameObject.Find("EyeCamera");
            Assert.IsNotNull(eye, "EyeCamera missing");
            Camera cam = eye.GetComponent<Camera>();
            Assert.IsNotNull(cam);
            var framing = cam.gameObject.GetComponent<RitualJarFraming>();
            if (framing == null) framing = cam.gameObject.AddComponent<RitualJarFraming>();
            framing.Target = controller.transform;
            framing.Cam = cam;
            framing.Apply();
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 50f;
            cam.allowHDR = false;
            var recorder = cam.gameObject.AddComponent<SequenceRecorder>();
            recorder.enabled = false;
            recorder.fps = 10;
            recorder.width = 912;
            recorder.height = 512;
            recorder.msaa = 4;
            recorder.outputDirectory = dir;
            recorder.enabled = true;
            return recorder;
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
