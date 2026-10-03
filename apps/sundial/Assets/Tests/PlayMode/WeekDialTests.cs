using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using GardenVR.Capture;
using GardenVR.Core;
using GardenVR.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GardenVR.Sundial.Tests.PlayMode
{
    /// <summary>
    /// Pinch-hold the page corner to open the last four weeks, then hold it again to come back.
    /// The record is seeded across 28 days with misses and a backfill. A shot is written only when
    /// GARDEN_WEEK_DIR is set, so a full run does not rewrite the evidence.
    /// </summary>
    public class WeekDialTests
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
        public IEnumerator WeekDial_TurnsAndReturns()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.FixedStep();
            yield return null;

            WeekDialController week = Week(controller);
            Assert.IsTrue(week.OnDayPage, "the dial should open on today's page");
            Assert.IsTrue(week.CornerVisible, "the page corner is missing");
            Assert.IsFalse(week.CaptionVisible, "the week caption shows on the day page");
            AssertWithinReach(week);
            Seed(controller);
            week.RefreshPage();
            yield return null;

            WeekDial dial = week.Record;
            Assert.IsNotNull(dial);
            AssertTiles(dial.For("water"),
                "KKKMLKM" + "KKKMLKK" + "KKKMLKK" + "KKKMLKT");
            AssertTiles(dial.For("top3"),
                "BBBBBBB" + "KKKKKKK" + "MLKKKKK" + "MKKKKKT");
            AssertTiles(dial.For("breaths"),
                "BBBBBBB" + "BBBBBBB" + "BBBBBBB" + "KKKMLKT");
            string json = dial.ToJson();
            string lowerJson = json.ToLowerInvariant();
            Assert.IsFalse(lowerJson.Contains("streak") || lowerJson.Contains("fail") || lowerJson.Contains("shame") || lowerJson.Contains("wilt"), json);
            Assert.IsFalse(WeekPageInk.ShameRed(week.Ink), "the page uses a red mark");
            AssertMark(week, "water", 0, 0, "kept");
            AssertMark(week, "water", 0, 3, "miss");
            AssertMark(week, "water", 0, 4, "late");
            AssertMark(week, "water", 3, 6, "today");
            AssertMark(week, "breaths", 0, 0, "before");
            AssertMark(week, "top3", 2, 0, "miss");
            AssertMark(week, "top3", 2, 1, "late");

            SundialPlay.Play(controller, Pinch(0.05f));
            yield return SundialPlay.Seconds(0.35f);
            Assert.IsTrue(week.OnDayPage, "a pinch should not turn the page");
            Assert.AreEqual(0, week.CueCount(WeekDialController.CueTurn));

            SundialPlay.Play(controller, Hold(0.05f, 0.28f, 0.40f));
            yield return SundialPlay.Seconds(0.70f);
            Assert.IsTrue(week.OnDayPage, "a short hold should not turn the page");
            Assert.AreEqual(0, week.CueCount(WeekDialController.CueTurn));

            SundialPlay.Play(controller, Hold(0.10f, 0.90f, 1.15f));
            yield return Until(() => week.OnWeekPage, 3f);
            Assert.IsTrue(week.OnWeekPage, "the corner hold did not turn to the week page");
            yield return null;
            Assert.AreEqual(WeekDialController.CaptionLine, week.Caption);
            Assert.IsFalse(week.Caption.Contains("\u2014"));
            string lower = week.Caption.ToLowerInvariant();
            Assert.IsFalse(lower.Contains("streak") || lower.Contains("fail") || lower.Contains("shame") || lower.Contains("wilt"));
            Assert.IsFalse(HasDigit(week.Caption), week.Caption);
            Assert.IsTrue(week.CaptionVisible);
            Assert.AreEqual("Water", week.PlantLabel(0));
            Assert.AreEqual("Top three", week.PlantLabel(1));
            Assert.AreEqual("Breaths", week.PlantLabel(2));
            for (int arc = 0; arc < 3; arc++)
            {
                Assert.IsTrue(week.PlantLabelVisible(arc), "plant label " + arc);
                Assert.IsFalse(HasDigit(week.PlantLabel(arc)), week.PlantLabel(arc));
                string words = week.PlantLabel(arc).ToLowerInvariant();
                Assert.IsFalse(words.Contains("streak") || words.Contains("fail") || words.Contains("shame") || words.Contains("wilt"));
            }
            TextMesh caption = GameObject.Find("WeekCaption").GetComponent<TextMesh>();
            Assert.AreEqual(InkLetter.Hand, caption.font);
            Assert.AreEqual(1, week.CueCount(WeekDialController.CueTurn));
            Assert.IsTrue(controller.View.weekPage);
            Assert.IsFalse(controller.View.tileRenderer.enabled, "today's tiles should step aside");
            Assert.IsTrue(week.CornerVisible, "the corner should stay, so the page can turn back");
            string dir = System.Environment.GetEnvironmentVariable("GARDEN_WEEK_DIR");
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, "week-ink.png"), week.PagePng());
                DisableAsyncShaders();
                Camera cam = PoseDial();
                AttachPlate(cam);
                for (int warm = 0; warm < 6; warm++)
                    yield return null;
                Shot(cam, Path.Combine(dir, "week-page.png"));
            }

            yield return SundialPlay.Seconds(0.40f);
            int kept = controller.LiveCount("water");
            SundialPlay.Play(controller, Hold(0.10f, 0.90f, 1.15f));
            yield return Until(() => week.OnDayPage, 4f);
            Assert.IsTrue(week.OnDayPage, "the second hold did not turn back");
            yield return null;
            Assert.IsFalse(controller.View.weekPage);
            Assert.IsTrue(controller.View.tileRenderer.enabled, "today's tiles should return");
            Assert.IsFalse(week.CaptionVisible);
            Assert.AreEqual(2, week.CueCount(WeekDialController.CueTurn));
            Assert.AreEqual(kept, controller.LiveCount("water"), "turning the page must not change the record");
            PlantState midday = SundialPlay.Midday(controller);
            WeekRecord top = week.Record.For("top3");
            for (int i = 0; i < 7; i++)
                Assert.AreEqual(top.Days[21 + i], midday.Window[i]);
            if (!string.IsNullOrEmpty(dir))
            {
                Camera cam = PoseDial();
                AttachPlate(cam);
                for (int warm = 0; warm < 4; warm++)
                    yield return null;
                Shot(cam, Path.Combine(dir, "day-page.png"));
            }
        }

        static void Seed(SundialController controller)
        {
            SundialService service = controller.Service;
            int day0 = service.Today().Index - 27;
            HabitDef water = service.HabitForArc("morning");
            HabitDef top3 = service.HabitForArc("midday");
            HabitDef breaths = service.HabitForArc("winddown");
            Assert.IsNotNull(water);
            Assert.IsNotNull(top3);
            Assert.IsNotNull(breaths);
            water.CreatedDay = day0;
            top3.CreatedDay = day0 + 7;
            breaths.CreatedDay = day0 + 21;
            DateTimeOffset end = service.Clock.Now;
            for (int i = 0; i < 28; i++)
            {
                DateTimeOffset day = end.AddDays(i - 27);
                var morning = new DateTimeOffset(day.Year, day.Month, day.Day, 8, 0, 0, day.Offset);
                var afternoon = new DateTimeOffset(day.Year, day.Month, day.Day, 14, 20, 0, day.Offset);
                service.JumpTo(morning);
                if (i > 0 && (i - 1) % 7 == 4)
                {
                    TendResult filled = service.BackfillYesterday(water);
                    Assert.IsTrue(filled.Ok, filled.Reason);
                    Assert.IsTrue(filled.Event.Late);
                }
                if (i == 16)
                {
                    TendResult filled = service.BackfillYesterday(top3);
                    Assert.IsTrue(filled.Ok, filled.Reason);
                    Assert.IsTrue(filled.Event.Late);
                }
                if (i == 26)
                {
                    TendResult filled = service.BackfillYesterday(breaths);
                    Assert.IsTrue(filled.Ok, filled.Reason);
                    Assert.IsTrue(filled.Event.Late);
                }
                service.JumpTo(afternoon);
                Assert.AreEqual(day0 + i, service.Today().Index);
                if (KeepWater(i))
                {
                    TendResult tend = controller.Ledger.Tend(water.Id, service.Today(), TendSource.Pinch, service.Clock);
                    Assert.IsTrue(tend.Ok, tend == null ? "null" : tend.Reason);
                    if (i == 6) Assert.IsTrue(controller.Ledger.Undo(tend.Event.Id, service.Clock, 6));
                }
                if (KeepTop3(i))
                {
                    TendResult tend = controller.Ledger.Tend(top3.Id, service.Today(), TendSource.Pinch, service.Clock);
                    Assert.IsTrue(tend.Ok, tend == null ? "null" : tend.Reason);
                }
                if (KeepBreaths(i))
                {
                    TendResult tend = controller.Ledger.Tend(breaths.Id, service.Today(), TendSource.Pinch, service.Clock);
                    Assert.IsTrue(tend.Ok, tend == null ? "null" : tend.Reason);
                }
            }
            service.JumpTo(end);
        }

        static bool KeepWater(int day)
        {
            return day != 27 && day % 7 != 3 && day % 7 != 4;
        }

        static bool KeepTop3(int day)
        {
            return day >= 7 && day != 27 && day != 14 && day != 15 && day != 21;
        }

        static bool KeepBreaths(int day)
        {
            return day == 21 || day == 22 || day == 23 || day == 26;
        }

        static void AssertTiles(WeekRecord row, string pattern)
        {
            Assert.IsNotNull(row);
            Assert.AreEqual(28, row.Days.Length);
            Assert.AreEqual(28, pattern.Length);
            for (int i = 0; i < pattern.Length; i++)
                Assert.AreEqual(Expect(pattern[i]), row.Days[i], row.HabitId + " day " + i);
        }

        static TileState Expect(char mark)
        {
            switch (mark)
            {
                case 'K': return TileState.Kept;
                case 'L': return TileState.Late;
                case 'M': return TileState.Missed;
                case 'T': return TileState.Today;
                case 'B': return TileState.Before;
                default: throw new InvalidOperationException("mark " + mark);
            }
        }

        static void AssertMark(WeekDialController week, string habit, int weekIndex, int day, string kind)
        {
            float x, z;
            Assert.IsTrue(week.TryMark(habit, weekIndex, day, out x, out z), habit);
            Color center = week.SampleDial(x, z);
            float lum = 0.30f * center.r + 0.59f * center.g + 0.11f * center.b;
            float warmth = center.r - center.b;
            if (kind == "kept")
            {
                Assert.Greater(center.r, 0.88f, habit + " kept");
                Assert.Greater(warmth, 0.14f, habit + " kept wash");
            }
            else if (kind == "miss")
            {
                Assert.Greater(lum, 0.74f, habit + " miss");
                Assert.Less(warmth, 0.18f, habit + " miss should stay pale");
                Assert.Less(center.r, 0.93f, habit + " miss");
            }
            else if (kind == "late")
            {
                Assert.Less(lum, 0.74f, habit + " late");
                Assert.Greater(lum, 0.40f, habit + " late");
            }
            else if (kind == "today")
            {
                Assert.Greater(center.r, 0.90f, habit + " today centre");
                Color ring = week.SampleDial(x + WeekPageInk.MarkRadius * 0.92f, z);
                Assert.Less(ring.r, 0.50f, habit + " today ring");
            }
            else if (kind == "before")
            {
                Assert.Greater(center.r, 0.90f, habit + " before");
                Assert.Greater(center.g, 0.88f, habit + " before");
            }
            else
            {
                Assert.Fail(kind);
            }
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

        static void AssertWithinReach(WeekDialController week)
        {
            GameObject eye = GameObject.Find("EyeCamera");
            Assert.IsNotNull(eye, "EyeCamera missing");
            GameObject corner = GameObject.Find(WeekDialController.CornerId);
            Assert.IsNotNull(corner, "page corner");
            float distance = Vector3.Distance(eye.transform.position, corner.transform.position);
            Assert.Less(distance, WeekDialController.TwoFootMetres, "corner is " + distance.ToString("0.000") + " m from the eye");
            Assert.IsNotNull(week);
        }

        static WeekDialController Week(SundialController controller)
        {
            WeekDialController week = controller.GetComponent<WeekDialController>();
            Assert.IsNotNull(week, "WeekDialController is missing");
            return week;
        }

        static string Pinch(float t)
        {
            return "{\"t\":" + t.ToString("0.00", CultureInfo.InvariantCulture) + ",\"intent\":\"Pinch\",\"target\":\"" + WeekDialController.CornerId + "\"}\n";
        }

        static string Hold(float t, float dur, float release)
        {
            return "{\"t\":" + t.ToString("0.00", CultureInfo.InvariantCulture) + ",\"intent\":\"PinchHold\",\"target\":\"" + WeekDialController.CornerId + "\",\"dur\":" + dur.ToString("0.00", CultureInfo.InvariantCulture) + "}\n"
                + "{\"t\":" + release.ToString("0.00", CultureInfo.InvariantCulture) + ",\"intent\":\"Release\"}\n";
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
            var mesh = new Mesh { name = "WeekPlate" };
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
            Texture2D tex = FrameGrab.RenderToTexture(cam, Framings.DefaultWidth, Framings.DefaultHeight, 4);
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
