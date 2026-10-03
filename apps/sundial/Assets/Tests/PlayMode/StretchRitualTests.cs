using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using GardenVR.Capture;
using GardenVR.Core;
using GardenVR.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GardenVR.Sundial.Tests.PlayMode
{
    /// <summary>
    /// Three reaches at the ink marks. captureDeltaTime is 1/60.
    /// Record_StretchSequence stays quiet unless GARDEN_SEQ_DIR is set.
    /// </summary>
    public class StretchRitualTests
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
        public IEnumerator Stretch_ThreeReaches_TendsMorning()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.FixedStep();
            yield return null;

            StretchRitualController stretch = Stretch(controller);
            Assert.AreEqual(ArcId.Midday, controller.State.Arc, "14:20 is midday; day 1 still offers the reach");
            Assert.IsTrue(stretch.OfferVisible, "the reach prompt is missing");
            Assert.AreEqual(StretchRitualController.OfferLine, LineText("StretchPrompt"));
            Assert.IsFalse(StretchRitualController.OfferLine.Contains("\u2014"));
            string lower = StretchRitualController.OfferLine.ToLowerInvariant();
            Assert.IsFalse(lower.Contains("streak") || lower.Contains("shame") || lower.Contains("wilt"));
            Assert.AreEqual(TileState.Today, Morning(controller).Window[6]);
            Assert.AreEqual(Stage.Seed, Morning(controller).Stage);
            AssertWithinReach();
            Transform plant = controller.View.transform.Find("plant.morning");
            Assert.IsNotNull(plant, "plant.morning missing");
            float baseY = plant.localScale.y;
            Assert.Greater(baseY, 0.01f);

            SundialPlay.Play(controller, StretchScripts.ThreeReaches());
            float app = 0f;
            bool sawIgnored = false;
            float peak = 0f;
            while (app < 12f)
            {
                yield return null;
                app += Time.deltaTime;
                if (stretch.Pose > peak) peak = stretch.Pose;
                if (!sawIgnored && app > 0.40f && app < 0.55f)
                {
                    Assert.AreEqual(0, stretch.Reaches, "a palm on a later mark counted");
                    Assert.IsFalse(controller.Dismissed, "a palm on a mark put the dial away");
                    Assert.IsFalse(stretch.Answered);
                    sawIgnored = true;
                }
                if (stretch.Answered && stretch.PlantHeight > 1.25f) break;
            }

            Assert.IsTrue(sawIgnored, "the out-of-order palm was missed");
            Assert.IsTrue(stretch.Answered, "three reaches did not finish, app " + app.ToString("0.00"));
            Assert.LessOrEqual(app, 8f, "the ritual took more than 8 s of app time");
            Assert.AreEqual(3, stretch.Reaches);
            Assert.AreEqual(1f, stretch.Session.Stretch, 0.0001f);
            Assert.Greater(peak, 0.7f, "the plant never stretched");
            Assert.Greater(stretch.PlantHeight, 1.25f);
            Assert.AreEqual(1, stretch.CueCount(StretchRitualController.CueStretch));
            Assert.AreEqual(1, controller.CueCount(SundialController.CueFlutter));
            Assert.IsFalse(stretch.OfferVisible);
            Assert.IsFalse(controller.Dismissed);
            AssertKept(controller);

            Assert.Greater(plant.localScale.y, baseY * 1.2f, "the morning card did not grow taller");

            SundialPlay.Play(controller, "{\"t\":0.20,\"intent\":\"PalmOpen\"}\n");
            float away = 0f;
            while (away < 1.5f && !controller.Dismissed)
            {
                yield return null;
                away += Time.deltaTime;
            }
            Assert.IsTrue(controller.Dismissed, "a palm away from the marks should still put the dial away");
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Record_StretchSequence()
        {
            string dir = Environment.GetEnvironmentVariable("GARDEN_SEQ_DIR");
            if (string.IsNullOrEmpty(dir))
                yield break;

            Directory.CreateDirectory(dir);
            string runDir = RunDir();
            Directory.CreateDirectory(runDir);
            DisableAsyncShaders();
            SundialController.SaveDirectoryOverride = SundialPlay.FreshDir();
            SundialController.ClockOverride = SundialPlay.Clock1420();
            SundialController controller = null;
            yield return SundialPlay.LoadMain(c => controller = c);
            yield return null;
            StretchRitualController stretch = Stretch(controller);
            Assert.IsTrue(stretch.OfferVisible);

            Camera cam = PoseDial();
            AttachPlate(cam);
            SundialPlay.Play(controller, StretchScripts.SlowThree());
            var recorder = cam.gameObject.AddComponent<SequenceRecorder>();
            recorder.enabled = false;
            recorder.fps = 10;
            recorder.width = 912;
            recorder.height = 512;
            recorder.msaa = 4;
            recorder.outputDirectory = dir;
            recorder.enabled = true;

            float app = 0f;
            while (app < 6.6f)
            {
                yield return null;
                app += Time.deltaTime;
            }
            recorder.enabled = false;

            Assert.IsTrue(stretch.Answered, "the recorded reach did not finish");
            int frames = Directory.GetFiles(dir, "f*.png").Length;
            Assert.GreaterOrEqual(frames, 50, "sequence is shorter than 5 s at 10 fps");
            File.WriteAllText(Path.Combine(runDir, "frames.txt"),
                "frames=" + frames + "\n" +
                "appTime=" + app.ToString("0.00", CultureInfo.InvariantCulture) + "\n" +
                "reaches=" + stretch.Reaches + "\n" +
                "pose=" + stretch.Pose.ToString("0.00", CultureInfo.InvariantCulture) + "\n");
        }

        static void AssertKept(SundialController controller)
        {
            PlantState plant = Morning(controller);
            Assert.AreEqual(TileState.Kept, plant.Window[plant.Window.Length - 1]);
            Assert.AreEqual(Stage.Sprout, plant.Stage);
            Assert.AreEqual(1, controller.LiveCount("water"));
            Assert.IsFalse(controller.UndoVisible, "a ritual tend must not offer undo");
            TendEvent live = null;
            int count = 0;
            var events = controller.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                TendEvent ev = events[i];
                if (ev.HabitId != "water" || ev.UndoneAtUtcMs.HasValue) continue;
                count++;
                live = ev;
            }
            Assert.AreEqual(1, count, "the morning habit should have one live tend");
            Assert.AreEqual(TendSource.Ritual, live.Source);
            Assert.AreEqual(ArcId.Morning, stretchArc(controller));
            string savePath = Path.Combine(SundialController.SaveDirectoryOverride, "save.json");
            string save = File.ReadAllText(savePath);
            Assert.IsTrue(save.Contains("\"Source\":\"Ritual\""), save);
            Assert.IsTrue(save.Contains("\"HabitId\":\"water\""), save);
        }

        static ArcId stretchArc(SundialController controller)
        {
            StretchRitualController stretch = Stretch(controller);
            Assert.IsNotNull(stretch.Session);
            return stretch.Session.TendsArc;
        }

        static void AssertWithinReach()
        {
            GameObject eye = GameObject.Find("EyeCamera");
            Assert.IsNotNull(eye, "EyeCamera missing");
            Vector3 from = eye.transform.position;
            for (int i = 0; i < StretchRitualController.MarkCount; i++)
            {
                GameObject mark = GameObject.Find(StretchRitualController.MarkId(i));
                Assert.IsNotNull(mark, StretchRitualController.MarkId(i));
                Assert.IsTrue(mark.activeInHierarchy, mark.name + " is hidden");
                float distance = Vector3.Distance(from, mark.transform.position);
                Assert.Less(distance, StretchRitualController.TwoFootMetres,
                    mark.name + " is " + distance.ToString("0.000") + " m from the eye");
            }
        }

        static StretchRitualController Stretch(SundialController controller)
        {
            StretchRitualController stretch = controller.GetComponent<StretchRitualController>();
            Assert.IsNotNull(stretch, "StretchRitualController is missing");
            return stretch;
        }

        static PlantState Morning(SundialController controller)
        {
            HabitDef habit = controller.Service.HabitForArc("morning");
            Assert.IsNotNull(habit, "morning habit missing");
            Assert.AreEqual("water", habit.Id);
            PlantState plant = controller.Service.PlantFor(habit);
            Assert.IsNotNull(plant, "morning plant missing");
            Assert.IsNotNull(plant.Window);
            Assert.GreaterOrEqual(plant.Window.Length, 7);
            return plant;
        }

        static string LineText(string name)
        {
            GameObject go = GameObject.Find(name);
            Assert.IsNotNull(go, name);
            TextMesh mesh = go.GetComponent<TextMesh>();
            Assert.IsNotNull(mesh, name);
            return mesh.text;
        }

        static string RunDir()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            return Path.Combine(root, "orchestration", "runs", "sundial", "T-SUN-022");
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
            var framing = cam.gameObject.GetComponent<DialG1Framing>();
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
            var mesh = new Mesh { name = "StretchPlate" };
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

    static class StretchScripts
    {
        public static string ThreeReaches()
        {
            var sb = new StringBuilder();
            Look(sb, 0.05f, 0.30f, 2);
            Palm(sb, 0.18f, 2);
            Look(sb, 0.55f, 0.50f, 0);
            Palm(sb, 0.80f, 0);
            Look(sb, 1.30f, 0.50f, 1);
            Palm(sb, 1.55f, 1);
            Look(sb, 2.05f, 0.50f, 2);
            Palm(sb, 2.30f, 2);
            return sb.ToString();
        }

        public static string SlowThree()
        {
            var sb = new StringBuilder();
            Look(sb, 0.20f, 0.90f, 0);
            Palm(sb, 0.70f, 0);
            Look(sb, 1.60f, 0.90f, 1);
            Palm(sb, 2.10f, 1);
            Look(sb, 3.00f, 0.90f, 2);
            Palm(sb, 3.50f, 2);
            Look(sb, 3.80f, 2.60f, 2);
            return sb.ToString();
        }

        static void Look(StringBuilder sb, float t, float dur, int mark)
        {
            Line(sb, t, "Look", dur, StretchRitualController.MarkId(mark));
        }

        static void Palm(StringBuilder sb, float t, int mark)
        {
            Line(sb, t, "PalmOpen", -1f, StretchRitualController.MarkId(mark));
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
    }
}
