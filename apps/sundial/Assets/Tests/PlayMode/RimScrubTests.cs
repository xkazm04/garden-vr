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
using UnityEngine.TestTools;

namespace GardenVR.Sundial.Tests.PlayMode
{
    /// <summary>
    /// Pinch-hold the rim and drag backward. The shadow reads earlier today, then yesterday.
    /// A pinch and a backfill during the read do not touch the ledger. Release eases home.
    /// Frames are written only when GARDEN_RIM_DIR is set.
    /// </summary>
    public class RimScrubTests
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
        public IEnumerator RimScrub_ReadOnly_SnapsBack()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.FixedStep();
            yield return null;

            RimScrubController rim = controller.GetComponent<RimScrubController>();
            Assert.IsNotNull(rim, "RimScrubController is missing");
            yield return Until(() => controller.View.gnomonDeg > 120f, 3f);
            Assert.Greater(controller.View.gnomonDeg, 120f, "the shadow did not settle on the afternoon");

            Seed(controller);
            yield return null;
            string ledger = Fingerprint(controller.Ledger);
            string state = controller.StateJson;
            int tocks = controller.CueCount(SundialController.CueTock);
            Assert.AreEqual(2, controller.LiveCount("water"), ledger);
            Assert.AreEqual(TileState.Today, Morning(controller).Window[6]);
            AssertWithinReach();

            SundialPlay.Play(controller, Pinch(RimScrubController.RimId(8)));
            yield return SundialPlay.Seconds(0.25f);
            Assert.IsFalse(rim.Scrubbing, "a click on the rim should not scrub");
            Assert.IsFalse(controller.Service.Scrubbing);
            Assert.AreEqual(ledger, Fingerprint(controller.Ledger));

            string dir = Environment.GetEnvironmentVariable("GARDEN_RIM_DIR");
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

            SundialPlay.Play(controller, DragScript());
            bool sawYesterday = false;
            bool sawSettle = false;
            float app = 0f;
            int shot = 0;
            while (app < 6f)
            {
                yield return null;
                app += Time.deltaTime;
                if (record && cam != null && shot < 28 && app >= shot * 0.12f)
                {
                    Shot(cam, Path.Combine(dir, "f" + shot.ToString("0000") + ".png"));
                    shot++;
                }
                if (!sawYesterday && rim.DaysBack >= 1)
                {
                    sawYesterday = true;
                    Assert.IsTrue(rim.Scrubbing);
                    Assert.IsTrue(controller.Service.Scrubbing);
                    Assert.AreEqual(RimScrub.Yesterday, rim.Caption);
                    Assert.IsFalse(rim.Caption.Contains("\u2014"), rim.Caption);
                    Assert.IsFalse(HasDigit(rim.Caption), rim.Caption);
                    string lower = rim.Caption.ToLowerInvariant();
                    Assert.IsFalse(lower.Contains("streak") || lower.Contains("fail") || lower.Contains("shame") || lower.Contains("wilt"));
                    TextMesh words = GameObject.Find("RimCaption").GetComponent<TextMesh>();
                    Assert.AreEqual(InkLetter.Hand, words.font);
                    Assert.AreEqual(125f, controller.State.GnomonDeg, 0.2f);
                    Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(controller.View.gnomonDeg, controller.State.GnomonDeg)), 40f);
                    Assert.AreEqual(1, controller.View.tiles[6], "yesterday's kept morning should show while reading");
                    Assert.AreEqual(3, controller.View.tiles[5], "the missed day beside it stays a miss");
                    Assert.AreEqual(TileState.Today, Morning(controller).Window[6]);
                    Assert.IsFalse(controller.Service.IsPending);
                    TendResult refused = controller.Service.BackfillYesterday(controller.Service.HabitForArc("morning"));
                    Assert.IsNotNull(refused);
                    Assert.IsFalse(refused.Ok, refused.Reason);
                    Assert.AreEqual("reading", refused.Reason);
                    Assert.IsFalse(controller.Service.TryStartFocus());
                    Assert.AreEqual(ledger, Fingerprint(controller.Ledger));
                    Assert.AreEqual(state, controller.StateJson);
                    Assert.AreEqual(tocks, controller.CueCount(SundialController.CueTock));
                }
                if (rim.Settling) sawSettle = true;
                if (sawYesterday && sawSettle && !rim.Reading)
                    break;
            }

            Assert.IsTrue(sawYesterday, "the drag did not reach yesterday rewind="
                + rim.RewindDegrees.ToString("0.0", CultureInfo.InvariantCulture)
                + " days=" + rim.DaysBack + " minute=" + rim.Minute + " scrub=" + rim.Scrubbing);
            Assert.IsTrue(sawSettle, "release did not ease the shadow home");
            yield return null;
            Assert.IsFalse(rim.Reading);
            Assert.IsFalse(rim.Scrubbing);
            Assert.IsFalse(rim.Settling);
            Assert.IsFalse(controller.Service.Scrubbing);
            Assert.AreEqual("", rim.Caption);
            Assert.AreEqual(125f, controller.State.GnomonDeg, 0.2f);
            Assert.AreEqual(controller.State.GnomonDeg, controller.View.gnomonDeg, 1.5f);
            Assert.AreEqual(4, controller.View.tiles[6], "today's open tile should return");
            Assert.AreEqual(TileState.Today, Morning(controller).Window[6]);
            Assert.IsFalse(controller.Service.IsPending);
            Assert.AreEqual(2, controller.LiveCount("water"));
            Assert.AreEqual(ledger, Fingerprint(controller.Ledger));
            Assert.AreEqual(state, controller.StateJson);
            Assert.AreEqual(tocks, controller.CueCount(SundialController.CueTock));
            if (record)
            {
                Assert.GreaterOrEqual(shot, 8, "the scrub sequence is short");
                File.WriteAllText(Path.Combine(dir, "frames.txt"), "frames=" + shot + "\n");
            }
        }

        static void Seed(SundialController controller)
        {
            SundialService service = controller.Service;
            HabitDef water = service.HabitForArc("morning");
            Assert.IsNotNull(water);
            Assert.AreEqual("water", water.Id);
            int today = service.Today().Index;
            water.CreatedDay = today - 4;
            Tend(controller, water, today - 3);
            Tend(controller, water, today - 1);
        }

        static void Tend(SundialController controller, HabitDef habit, int dayIndex)
        {
            var day = new GardenDay(dayIndex);
            DateTime civil = day.CivilDate;
            var wall = new DateTimeOffset(civil.Year, civil.Month, civil.Day, 8, 0, 0, TimeSpan.Zero);
            TendResult tend = controller.Ledger.Tend(habit.Id, day, TendSource.Pinch, new FixedClock(wall, TimeZoneInfo.Utc));
            Assert.IsTrue(tend.Ok, tend == null ? "null" : tend.Reason);
        }

        static PlantState Morning(SundialController controller)
        {
            HabitDef habit = controller.Service.HabitForArc("morning");
            PlantState plant = controller.Service.PlantFor(habit);
            Assert.IsNotNull(plant);
            Assert.IsNotNull(plant.Window);
            Assert.GreaterOrEqual(plant.Window.Length, 7);
            return plant;
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
                text.Append(ev.Id);
                text.Append(':');
                text.Append(ev.HabitId);
                text.Append(':');
                text.Append(ev.Day);
                text.Append(':');
                text.Append(ev.Late ? "L" : "K");
                text.Append(':');
                text.Append(ev.UndoneAtUtcMs.HasValue ? ev.UndoneAtUtcMs.Value.ToString(CultureInfo.InvariantCulture) : "-");
            }
            return text.ToString();
        }

        static string Pinch(string target)
        {
            return "{\"t\":0.00,\"intent\":\"Pinch\",\"target\":\"" + target + "\"}\n";
        }

        static string DragScript()
        {
            var text = new StringBuilder();
            int index = 8;
            const int steps = 17;
            for (int i = 0; i < steps; i++)
            {
                float t = i * 0.12f;
                text.Append("{\"t\":");
                text.Append(t.ToString("0.00", CultureInfo.InvariantCulture));
                text.Append(",\"intent\":\"PinchHold\",\"target\":\"");
                text.Append(RimScrubController.RimId(index));
                text.Append("\",\"dur\":0.12}\n");
                index--;
                if (index < 0) index += RimScrubController.RimMarks;
            }
            text.Append("{\"t\":1.44,\"intent\":\"Pinch\",\"target\":\"plant.morning\"}\n");
            text.Append("{\"t\":1.56,\"intent\":\"Poke\",\"target\":\"tile.morning.5\"}\n");
            text.Append("{\"t\":2.16,\"intent\":\"Release\",\"target\":\"");
            text.Append(RimScrubController.RimId(index));
            text.Append("\"}\n");
            return text.ToString();
        }

        static void AssertWithinReach()
        {
            GameObject eye = GameObject.Find("EyeCamera");
            Assert.IsNotNull(eye, "EyeCamera missing");
            GameObject mark = GameObject.Find(RimScrubController.RimId(8));
            Assert.IsNotNull(mark, "rim mark missing");
            IntentTarget target = mark.GetComponent<IntentTarget>();
            Assert.IsNotNull(target);
            Assert.AreEqual(RimScrubController.RimId(8), target.Id);
            float distance = Vector3.Distance(eye.transform.position, mark.transform.position);
            Assert.Less(distance, RimScrubController.TwoFootMetres,
                "rim is " + distance.ToString("0.000") + " m from the eye");
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
            var mesh = new Mesh { name = "RimPlate" };
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
