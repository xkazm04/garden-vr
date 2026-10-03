using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using GardenVR.Capture;
using GardenVR.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GardenVR.Sundial.Tests.PlayMode
{
    /// <summary>
    /// Settings opens the three arc edges. A pinch-hold drags the midday edge along the rim.
    /// 14:20 leaves midday, the morning plant becomes due, and a reload keeps the boundary.
    /// A second drag that would erase the morning hour stops at one hour. The shadow stays on the clock.
    /// Frames are written only when GARDEN_ARC_DIR is set.
    /// </summary>
    public class ArcTimesPlayTests
    {
        float _savedDelta;
        string _saveDir;

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
        public IEnumerator ArcTimes_DragEdge_Persists()
        {
            _saveDir = SundialPlay.FreshDir();
            SundialController.SaveDirectoryOverride = _saveDir;
            SundialController.ClockOverride = SundialPlay.Clock1420();
            SundialService.DevSeedOnFresh = true;
            SundialController controller = null;
            yield return SundialPlay.LoadMain(c => controller = c);
            SundialPlay.FixedStep();
            yield return null;

            ArcTimesController edges = controller.GetComponent<ArcTimesController>();
            Assert.IsNotNull(edges, "ArcTimesController is missing");
            yield return Until(() => controller.View.gnomonDeg > 120f, 3f);
            Assert.AreEqual(125f, controller.State.GnomonDeg, 0.2f);
            Assert.AreEqual(ArcId.Midday, controller.State.Arc);
            Assert.IsFalse(edges.EdgesVisible);
            Assert.IsFalse(MorningDue(controller));
            Assert.IsTrue(MiddayDue(controller));
            Assert.AreEqual(180, controller.Service.Save.Settings.BoundaryMin);
            Assert.AreEqual(11 * 60, controller.Service.ArcSchedule.MiddayMin);
            string ledger = Fingerprint(controller.Ledger);
            AssertWithinReach(false);

            string dir = Environment.GetEnvironmentVariable("GARDEN_ARC_DIR");
            bool record = !string.IsNullOrEmpty(dir);
            Camera cam = null;
            if (record)
            {
                Directory.CreateDirectory(dir);
                DisableAsyncShaders();
                cam = PoseDial();
                AttachPlate(cam);
                for (int warm = 0; warm < 4; warm++)
                    yield return null;
            }

            SundialPlay.Play(controller, MoveToFifteen());
            bool sawOpen = false;
            bool sawDrag = false;
            bool sawMorning = false;
            float app = 0f;
            while (app < 4f)
            {
                yield return null;
                app += Time.deltaTime;
                if (!sawOpen && controller.Settings.Open && edges.EdgesVisible && !edges.Dragging && app < 0.65f)
                {
                    sawOpen = true;
                    Assert.AreEqual(ArcTimesController.MiddayLabel, edges.Label(ArcEdge.Midday));
                    Assert.AreEqual(InkLetter.Hand, GameObject.Find(ArcTimesController.MiddayId).GetComponentInChildren<TextMesh>().font);
                    Assert.IsFalse(edges.Label(ArcEdge.Morning).Contains("\u2014"));
                    Assert.IsFalse(HasDigit(edges.Label(ArcEdge.Dusk)));
                    AssertWithinReach(true);
                    if (record) Shot(cam, Path.Combine(dir, "settings-open.png"));
                }
                if (edges.Dragging) sawDrag = true;
                if (controller.Service.Save.Settings.MiddayMin == 15 * 60 && controller.State.Arc == ArcId.Morning)
                    sawMorning = true;
                if (sawMorning && !edges.Dragging && app > 1.6f)
                    break;
            }

            Assert.IsTrue(sawOpen, "settings did not show the arc edges");
            Assert.IsTrue(sawDrag, "the pinch-hold did not grab an edge");
            Assert.IsTrue(sawMorning, "14:20 did not follow the midday edge. midday="
                + controller.Service.Save.Settings.MiddayMin + " arc=" + controller.State.Arc);
            yield return null;
            Assert.IsFalse(edges.Dragging);
            Assert.IsFalse(controller.Service.Scrubbing);
            Assert.AreEqual(15 * 60, controller.Service.Save.Settings.MiddayMin);
            Assert.AreEqual(6 * 60, controller.Service.Save.Settings.MorningMin);
            Assert.AreEqual(18 * 60, controller.Service.Save.Settings.DuskMin);
            Assert.AreEqual(180, controller.Service.Save.Settings.BoundaryMin);
            Assert.AreEqual(ArcId.Morning, controller.State.Arc);
            Assert.AreEqual(125f, controller.State.GnomonDeg, 0.2f);
            Assert.AreEqual(125f, controller.View.gnomonDeg, 1.5f);
            Assert.IsTrue(MorningDue(controller));
            Assert.IsFalse(MiddayDue(controller));
            Assert.AreEqual("morning", controller.View.waitingTarget);
            Assert.IsTrue(controller.View.plantDue[0]);
            Assert.IsFalse(controller.View.plantDue[3]);
            Assert.AreEqual(ledger, Fingerprint(controller.Ledger));
            Assert.AreEqual(ArcId.WindDown, controller.Service.ArcSchedule.ArcAt(2 * 60 + 59));
            Assert.IsFalse(controller.Service.ArcSchedule.ArcAt(3 * 60).HasValue);

            string json = File.ReadAllText(Path.Combine(_saveDir, "save.json"));
            StringAssert.Contains("\"MiddayMin\":900", json);
            StringAssert.Contains("\"BoundaryMin\":180", json);
            var again = new SundialService(SundialPlay.Clock1420(), _saveDir);
            Assert.AreEqual(LoadOutcome.Loaded, again.Outcome);
            Assert.AreEqual(900, again.Save.Settings.MiddayMin);
            Assert.AreEqual(180, again.Save.Settings.BoundaryMin);
            Assert.AreEqual(ArcId.Morning, again.State.Arc);
            Assert.AreEqual(125f, again.State.GnomonDeg, 0.2f);
            Assert.IsTrue(again.State.Plants[0].DueNow);
            Assert.IsFalse(again.State.Plants[1].DueNow);
            if (record) Shot(cam, Path.Combine(dir, "edge-moved.png"));

            SundialPlay.Play(controller, PullUnderAnHour());
            bool sawGuard = false;
            app = 0f;
            while (app < 3f)
            {
                yield return null;
                app += Time.deltaTime;
                if (!edges.Dragging && controller.Service.Save.Settings.MiddayMin == 7 * 60 && app > 0.8f)
                {
                    sawGuard = true;
                    break;
                }
            }

            Assert.IsTrue(sawGuard, "the short arc was not guarded. midday=" + controller.Service.Save.Settings.MiddayMin);
            yield return null;
            Assert.AreEqual(7 * 60, controller.Service.Save.Settings.MiddayMin);
            Assert.AreEqual(6 * 60, controller.Service.Save.Settings.MorningMin);
            Assert.AreEqual(18 * 60, controller.Service.Save.Settings.DuskMin);
            Assert.AreEqual(180, controller.Service.Save.Settings.BoundaryMin);
            Assert.AreEqual(ArcId.Midday, controller.State.Arc);
            Assert.AreEqual(125f, controller.State.GnomonDeg, 0.2f);
            Assert.IsFalse(MorningDue(controller));
            Assert.IsTrue(MiddayDue(controller));
            Assert.AreEqual(ledger, Fingerprint(controller.Ledger));
            string guarded = File.ReadAllText(Path.Combine(_saveDir, "save.json"));
            StringAssert.Contains("\"MiddayMin\":420", guarded);
            if (record)
            {
                Shot(cam, Path.Combine(dir, "edge-guarded.png"));
                File.WriteAllText(Path.Combine(dir, "frames.txt"), "settings-open\nedge-moved\nedge-guarded\n");
            }
        }

        static bool MorningDue(SundialController controller)
        {
            PlantState plant = controller.Service.PlantFor(controller.Service.HabitForArc("morning"));
            Assert.IsNotNull(plant);
            return plant.DueNow;
        }

        static bool MiddayDue(SundialController controller)
        {
            PlantState plant = controller.Service.PlantFor(controller.Service.HabitForArc("midday"));
            Assert.IsNotNull(plant);
            return plant.DueNow;
        }

        static string MoveToFifteen()
        {
            var text = new StringBuilder();
            text.Append("{\"t\":0.10,\"intent\":\"Poke\",\"target\":\"tab.settings\"}\n");
            text.Append(Hold(ArcTimesController.MiddayId, 0.70f, 0.20f));
            text.Append(Hold(RimScrubController.RimId(6), 0.95f, 0.16f));
            text.Append(Hold(RimScrubController.RimId(7), 1.15f, 0.16f));
            text.Append(Hold(RimScrubController.RimId(8), 1.35f, 0.16f));
            text.Append(Hold(RimScrubController.RimId(9), 1.55f, 0.20f));
            text.Append("{\"t\":1.90,\"intent\":\"Release\",\"target\":\"");
            text.Append(RimScrubController.RimId(9));
            text.Append("\"}\n");
            return text.ToString();
        }

        static string PullUnderAnHour()
        {
            var text = new StringBuilder();
            text.Append(Hold(ArcTimesController.MiddayId, 0.15f, 0.20f));
            text.Append(Hold(RimScrubController.RimId(0), 0.45f, 0.30f));
            text.Append("{\"t\":0.90,\"intent\":\"Release\",\"target\":\"");
            text.Append(RimScrubController.RimId(0));
            text.Append("\"}\n");
            return text.ToString();
        }

        static string Hold(string target, float t, float dur)
        {
            return "{\"t\":" + t.ToString("0.00", CultureInfo.InvariantCulture)
                + ",\"intent\":\"PinchHold\",\"target\":\"" + target
                + "\",\"dur\":" + dur.ToString("0.00", CultureInfo.InvariantCulture) + "}\n";
        }

        static string Fingerprint(Ledger ledger)
        {
            var text = new StringBuilder();
            var events = ledger.Events;
            text.Append(events.Count);
            for (int i = 0; i < events.Count; i++)
            {
                TendEvent ev = events[i];
                text.Append('|');
                if (ev == null) continue;
                text.Append(ev.HabitId);
                text.Append(':');
                text.Append(ev.Day);
                text.Append(ev.UndoneAtUtcMs.HasValue ? ":u" : "");
            }
            return text.ToString();
        }

        static void AssertWithinReach(bool edgesUp)
        {
            GameObject eye = GameObject.Find("EyeCamera");
            Assert.IsNotNull(eye, "EyeCamera missing");
            if (!edgesUp) return;
            GameObject mark = GameObject.Find(ArcTimesController.MiddayId);
            Assert.IsNotNull(mark, "midday edge missing");
            float distance = Vector3.Distance(eye.transform.position, mark.transform.position);
            Assert.Less(distance, RimScrubController.TwoFootMetres,
                "edge is " + distance.ToString("0.000") + " m from the eye");
        }

        static bool HasDigit(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] >= '0' && text[i] <= '9') return true;
            }
            return false;
        }

        static IEnumerator Until(Func<bool> ready, float seconds)
        {
            float app = 0f;
            while (app < seconds)
            {
                if (ready()) yield break;
                yield return null;
                app += Time.deltaTime;
            }
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
            var mesh = new Mesh { name = "ArcPlate" };
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

        static void Shot(Camera cam, string path)
        {
            var framing = cam.GetComponent<DialG1Framing>();
            if (framing != null) framing.Apply();
            Texture2D tex = FrameGrab.RenderToTexture(cam, 912, 512, 4);
            try
            {
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.Destroy(tex);
            }
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
