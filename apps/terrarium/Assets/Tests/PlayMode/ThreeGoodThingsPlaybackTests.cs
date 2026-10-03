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
    public class ThreeGoodThingsPlaybackTests
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
        public IEnumerator ThreeGoodThings_ThreeDrops_CountsTheDay()
        {
            string seq = Environment.GetEnvironmentVariable("GARDEN_SEQ_DIR");
            if (!string.IsNullOrEmpty(seq))
            {
                Directory.CreateDirectory(seq);
                DisableAsyncShaders();
            }

            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            Assert.IsTrue(controller.GladBuilt, "the three leaves were not built");
            RitualHarness.Play(controller, ThreePinches());
            RitualHarness.FixedStep();
            SequenceRecorder recorder = null;
            if (!string.IsNullOrEmpty(seq))
                recorder = ArmRecorder(controller, seq);

            bool sawTwo = false;
            bool sawDone = false;
            float app = 0f;
            while (app < 120f)
            {
                yield return null;
                app += Time.deltaTime;
                if (controller.GladPlaced == 2 && controller.Snapshot().Fronds == 0) sawTwo = true;
                if (controller.GladLine == GladRitual.LineDone) sawDone = true;
                if (controller.GladSettled && app > 4.2f) break;
            }

            if (recorder != null) recorder.enabled = false;
            TerrariumState end = controller.Snapshot();
            Assert.IsTrue(sawTwo, "two drops counted the day early");
            Assert.IsTrue(sawDone, "the drops never said done");
            Assert.IsTrue(controller.GladSettled, "the drops did not settle: " + end.ToJson());
            Assert.LessOrEqual(app, 120f);
            Assert.AreEqual(3, controller.GladPlaced);
            Assert.AreEqual(0, end.Breaths);
            Assert.AreEqual(BreathPhase.Waiting, end.Phase);
            Assert.AreEqual(1, end.Fronds);
            Assert.AreEqual(0, end.DewToday);
            Assert.AreEqual(1, end.Rituals);
            Assert.AreEqual(1f, end.Vitality, 0.001f);
            Assert.AreEqual(3, controller.CueCount("dew.drop"));
            Assert.AreEqual(0, controller.CueCount("answer.chime"));
            Assert.IsNull(controller.GladLine);
            Assert.Less(controller.GladDropY(0), JarView.MossBedY + 0.012f);
            Assert.Less(controller.GladDropY(1), JarView.MossBedY + 0.012f);
            Assert.Less(controller.GladDropY(2), JarView.MossBedY + 0.012f);

            TerrariumSave doc = controller.Service.Document;
            Assert.AreEqual(controller.Service.TodayIndex, doc.GladDay);
            string json = doc.ToJson();
            Assert.IsTrue(json.Contains("\"GladDay\":"), json);
            Assert.IsFalse(json.Contains("glad.leaf"), json);
            Assert.IsFalse(json.Contains("good thing"), json);
            Assert.IsFalse(json.Contains("\u2014"), json);
            string live = Path.Combine(GardenService.DirectoryOverride, SaveStore<TerrariumSave>.LiveName);
            string disk = File.ReadAllText(live);
            Assert.IsTrue(disk.Contains("\"GladDay\":" + doc.GladDay.Value.ToString(CultureInfo.InvariantCulture)), disk);
            Assert.IsFalse(disk.Contains("glad.leaf"), disk);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ThreeGoodThings_AlreadyDone_DoesNotGrowAgain()
        {
            var when = new DateTimeOffset(2026, 10, 3, 21, 0, 0, TimeSpan.Zero);
            int day = GardenDay.From(when, GardenDay.DefaultBoundary).Index;
            var save = new TerrariumSave();
            save.FrondDays = new[] { day };
            save.LastRitualDay = day;
            save.RitualsCompleted = 1;
            save.GladDay = day;
            save.FirstRunStep = "tour.done";
            string dir = Path.Combine(Path.GetTempPath(), "gvr-ter-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, SaveStore<TerrariumSave>.LiveName), save.ToJson());
            GardenService.DirectoryOverride = dir;
            GardenService.ClockOverride = new FixedClock(when, TimeZoneInfo.Utc);

            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            Assert.IsTrue(controller.GladBuilt, "the three leaves were not built");
            RitualHarness.Play(controller, ThreePinches());
            RitualHarness.FixedStep();

            float app = 0f;
            while (app < 8f)
            {
                yield return null;
                app += Time.deltaTime;
            }

            TerrariumState end = controller.Snapshot();
            Assert.AreEqual(1, end.Fronds);
            Assert.AreEqual(0, end.DewToday);
            Assert.AreEqual(1, end.Rituals);
            Assert.AreEqual(0, controller.CueCount("dew.drop"));
            Assert.AreEqual(day, controller.Service.Document.GladDay);
            Assert.IsTrue(controller.GladSettled);
        }

        static string ThreePinches()
        {
            var sb = new StringBuilder();
            Line(sb, 0.30f, "Pinch", GladRitual.LeafId(0));
            Line(sb, 0.70f, "Pinch", GladRitual.LeafId(0));
            Line(sb, 1.15f, "Pinch", GladRitual.LeafId(1));
            Line(sb, 1.70f, "Pinch", GladRitual.LeafId(2));
            Line(sb, 3.40f, "Pinch", GladRitual.LeafId(0));
            Line(sb, 3.40f, "Look", null, 2.2f);
            return sb.ToString();
        }

        static void Line(StringBuilder sb, float t, string intent, string target)
        {
            Line(sb, t, intent, target, -1f);
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
