using System;
using System.Collections;
using System.Collections.Generic;
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
    /// Pinch one of five ink symbols. It tends midday and stays in the arc.
    /// Record_GratitudeCaptures stays quiet unless GARDEN_GRATITUDE_DIR is set.
    /// </summary>
    public class GratitudeRitualTests
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
        public IEnumerator Gratitude_InksSymbol_TendsMidday()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.FixedStep();
            yield return null;

            GratitudeRitualController gratitude = Gratitude(controller);
            Assert.AreEqual(ArcId.Midday, controller.State.Arc, "14:20 is midday");
            Assert.IsTrue(gratitude.OfferVisible, "the mark prompt is missing");
            Assert.AreEqual(GratitudeRitualController.OfferLine, LineText("GratitudePrompt"));
            Assert.IsFalse(GratitudeRitualController.OfferLine.Contains("\u2014"));
            string lower = GratitudeRitualController.OfferLine.ToLowerInvariant();
            Assert.IsFalse(lower.Contains("streak") || lower.Contains("shame") || lower.Contains("wilt"));
            Assert.AreEqual(TileState.Today, SundialPlay.Midday(controller).Window[6]);
            Assert.AreEqual(Stage.Seed, SundialPlay.Midday(controller).Stage);
            AssertNoTextInput();
            AssertDistinctSymbols();
            AssertWithinReach();

            int leaf = GratitudeRecord.ParseKey("leaf");
            SundialPlay.Play(controller, GratitudeScripts.Pinch(leaf, 0.20f));
            float app = 0f;
            while (app < 3f && !gratitude.Settled)
            {
                yield return null;
                app += Time.deltaTime;
            }

            Assert.IsTrue(gratitude.Answered, "the pinch did not ink, app " + app.ToString("0.00"));
            Assert.IsTrue(gratitude.Settled, "the ink did not settle");
            Assert.AreEqual(leaf, gratitude.Choice);
            Assert.Less(app, 2f, "inking took more than 2 s of app time");
            Assert.IsFalse(gratitude.OfferVisible);
            Assert.IsFalse(controller.UndoVisible, "a ritual tend must not offer undo");
            Assert.IsFalse(controller.Dismissed);
            Assert.AreEqual(1, gratitude.CueCount(GratitudeRitualController.CueInk));
            Assert.AreEqual(1, controller.CueCount(SundialController.CueFlutter));
            for (int i = 0; i < GratitudeRitualController.SymbolCount; i++)
                Assert.AreEqual(i == leaf, gratitude.SymbolShown(i), GratitudeRitualController.SymbolId(i));
            AssertKept(controller, leaf);

            SundialPlay.Play(controller, GratitudeScripts.Pinch(GratitudeRecord.ParseKey("star"), 0.15f));
            float again = 0f;
            while (again < 0.8f)
            {
                yield return null;
                again += Time.deltaTime;
            }
            Assert.AreEqual(leaf, gratitude.Choice, "a second symbol replaced the first");
            Assert.AreEqual(1, controller.LiveCount("top3"));
            Assert.IsFalse(gratitude.SymbolShown(GratitudeRecord.ParseKey("star")));

            string dir = SundialController.SaveDirectoryOverride;
            yield return SundialPlay.Unload();
            SundialController.SaveDirectoryOverride = dir;
            SundialController.ClockOverride = SundialPlay.Clock1420();
            SundialService.DevSeedOnFresh = true;
            SundialController reloaded = null;
            yield return SundialPlay.LoadMain(c => reloaded = c);
            yield return null;
            yield return null;

            GratitudeRitualController restored = Gratitude(reloaded);
            Assert.IsTrue(restored.Answered, "the saved mark did not come back");
            Assert.AreEqual(leaf, restored.Choice);
            Assert.IsTrue(restored.Settled);
            Assert.IsFalse(restored.OfferVisible);
            Assert.IsTrue(restored.SymbolShown(leaf));
            Assert.AreEqual(TileState.Kept, SundialPlay.Midday(reloaded).Window[6]);
            Assert.AreEqual(1, reloaded.LiveCount("top3"));
            Assert.AreEqual(0, restored.CueCount(GratitudeRitualController.CueInk), "reloading inked again");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Record_GratitudeCaptures()
        {
            string dir = Environment.GetEnvironmentVariable("GARDEN_GRATITUDE_DIR");
            if (string.IsNullOrEmpty(dir))
                yield break;

            Directory.CreateDirectory(dir);
            WriteSymbolSheet(Path.Combine(dir, "symbols.png"));
            DisableAsyncShaders();
            SundialController.SaveDirectoryOverride = SundialPlay.FreshDir();
            SundialController.ClockOverride = SundialPlay.Clock1420();
            SundialController controller = null;
            yield return SundialPlay.LoadMain(c => controller = c);
            yield return null;
            GratitudeRitualController gratitude = Gratitude(controller);
            Assert.IsTrue(gratitude.OfferVisible);

            Camera cam = PoseDial();
            AttachPlate(cam);
            for (int i = 0; i < 6; i++)
                yield return null;
            Shot(cam, Path.Combine(dir, "offer.png"));

            SundialPlay.Play(controller, GratitudeScripts.Pinch(GratitudeRecord.ParseKey("leaf"), 0.15f));
            float app = 0f;
            while (app < 3f && !gratitude.Settled)
            {
                yield return null;
                app += Time.deltaTime;
            }
            for (int i = 0; i < 4; i++)
                yield return null;
            Shot(cam, Path.Combine(dir, "inked.png"));

            Assert.IsTrue(gratitude.Answered, "the recorded pinch did not ink");
            Assert.AreEqual(1, gratitude.Choice);
            Assert.Greater(new FileInfo(Path.Combine(dir, "symbols.png")).Length, 1000);
            Assert.Greater(new FileInfo(Path.Combine(dir, "offer.png")).Length, 10000);
            Assert.Greater(new FileInfo(Path.Combine(dir, "inked.png")).Length, 10000);
            File.WriteAllText(Path.Combine(dir, "frames.txt"),
                "choice=" + gratitude.Choice + "\n" +
                "settled=" + gratitude.Settled + "\n" +
                "kept=" + SundialPlay.Midday(controller).Window[6] + "\n");
        }

        static void AssertKept(SundialController controller, int symbol)
        {
            PlantState plant = SundialPlay.Midday(controller);
            Assert.AreEqual(TileState.Kept, plant.Window[plant.Window.Length - 1]);
            Assert.AreEqual(Stage.Sprout, plant.Stage);
            Assert.AreEqual(1, controller.LiveCount("top3"));
            TendEvent live = null;
            int count = 0;
            var events = controller.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                TendEvent ev = events[i];
                if (ev.HabitId != "top3" || ev.UndoneAtUtcMs.HasValue) continue;
                count++;
                live = ev;
            }
            Assert.AreEqual(1, count, "the midday habit should have one live tend");
            Assert.AreEqual(TendSource.Ritual, live.Source);
            Assert.AreEqual(ArcId.Midday, controller.Service.Gratitude.TendsArc);
            Assert.AreEqual(TendSource.Ritual, controller.Service.Gratitude.TendsAs);

            string savePath = Path.Combine(SundialController.SaveDirectoryOverride, "save.json");
            string save = File.ReadAllText(savePath);
            Assert.IsTrue(save.Contains("\"Source\":\"Ritual\""), save);
            Assert.IsTrue(save.Contains("\"HabitId\":\"top3\""), save);
            Assert.IsFalse(save.Contains("\"Note\""), save);
            Assert.IsFalse(save.Contains("\"Text\""), save);
            JsonObject root = Json.ParseObject(save);
            Assert.IsTrue(root.Has("Gratitude"), save);
            JsonArray rows = root.Get("Gratitude").AsArray();
            Assert.AreEqual(1, rows.Count);
            JsonObject row = rows[0].AsObject();
            Assert.AreEqual(2, row.Count, "a gratitude row must be the day and the symbol");
            Assert.AreEqual(JsonKind.Number, row.Get("Day").Kind);
            Assert.AreEqual(JsonKind.Number, row.Get("Symbol").Kind);
            Assert.AreEqual(symbol, row.Get("Symbol").AsInt());
            Assert.IsFalse(row.Has("Note"));
        }

        static void AssertNoTextInput()
        {
            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] == null) continue;
                string name = behaviours[i].GetType().Name;
                Assert.IsFalse(name.IndexOf("InputField", StringComparison.Ordinal) >= 0, name);
            }
        }

        static void AssertDistinctSymbols()
        {
            var hashes = new HashSet<int>();
            for (int i = 0; i < GratitudeRitualController.SymbolCount; i++)
            {
                Texture2D tex = Glyph(i);
                Color32[] pixels = tex.GetPixels32();
                int ink = 0;
                unchecked
                {
                    int hash = 17;
                    for (int p = 0; p < pixels.Length; p++)
                    {
                        if (pixels[p].a > 40) ink++;
                        hash = hash * 31 + pixels[p].a;
                    }
                    Assert.Greater(ink, 80, GratitudeRitualController.SymbolId(i) + " is blank");
                    Assert.IsTrue(hashes.Add(hash), GratitudeRitualController.SymbolId(i) + " matches another symbol");
                }
            }
        }

        static Texture2D Glyph(int index)
        {
            GameObject mark = GameObject.Find(GratitudeRitualController.SymbolId(index));
            Assert.IsNotNull(mark, GratitudeRitualController.SymbolId(index));
            Renderer renderer = mark.GetComponentInChildren<Renderer>();
            Assert.IsNotNull(renderer, mark.name);
            var tex = renderer.sharedMaterial.GetTexture("_MainTex") as Texture2D;
            Assert.IsNotNull(tex, mark.name);
            return tex;
        }

        static void AssertWithinReach()
        {
            GameObject eye = GameObject.Find("EyeCamera");
            Assert.IsNotNull(eye, "EyeCamera missing");
            Vector3 from = eye.transform.position;
            for (int i = 0; i < GratitudeRitualController.SymbolCount; i++)
            {
                GameObject mark = GameObject.Find(GratitudeRitualController.SymbolId(i));
                Assert.IsNotNull(mark, GratitudeRitualController.SymbolId(i));
                Assert.IsTrue(mark.activeInHierarchy, mark.name + " is hidden");
                float distance = Vector3.Distance(from, mark.transform.position);
                Assert.Less(distance, GratitudeRitualController.TwoFootMetres,
                    mark.name + " is " + distance.ToString("0.000") + " m from the eye");
            }
        }

        static GratitudeRitualController Gratitude(SundialController controller)
        {
            GratitudeRitualController gratitude = controller.GetComponent<GratitudeRitualController>();
            Assert.IsNotNull(gratitude, "GratitudeRitualController is missing");
            return gratitude;
        }

        static string LineText(string name)
        {
            GameObject go = GameObject.Find(name);
            Assert.IsNotNull(go, name);
            TextMesh mesh = go.GetComponent<TextMesh>();
            Assert.IsNotNull(mesh, name);
            return mesh.text;
        }

        static void WriteSymbolSheet(string path)
        {
            const int cell = 180;
            const int gap = 28;
            int count = GratitudeRecord.SymbolCount;
            int width = count * cell + (count + 1) * gap;
            int height = cell + gap * 2;
            var sheet = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var paper = new Color(0.953f, 0.933f, 0.886f, 1f);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = paper;
            sheet.SetPixels(pixels);
            for (int s = 0; s < count; s++)
            {
                Texture2D glyph = GratitudeInk.Make(s, false, cell);
                int x0 = gap + s * (cell + gap);
                Blit(sheet, glyph, x0, gap);
                UnityEngine.Object.Destroy(glyph);
            }
            sheet.Apply(false, false);
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            UnityEngine.Object.Destroy(sheet);
        }

        static void Blit(Texture2D dest, Texture2D src, int x0, int y0)
        {
            Color[] pixels = src.GetPixels();
            int n = src.width;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    Color ink = pixels[y * n + x];
                    if (ink.a <= 0.01f) continue;
                    Color under = dest.GetPixel(x0 + x, y0 + y);
                    dest.SetPixel(x0 + x, y0 + y, Color.Lerp(under, ink, ink.a));
                }
            }
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
            var mesh = new Mesh { name = "GratitudePlate" };
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

    static class GratitudeScripts
    {
        public static string Pinch(int symbol, float t)
        {
            var sb = new StringBuilder();
            string id = GratitudeRitualController.SymbolId(symbol);
            Line(sb, 0.05f, "Look", 0.40f, id);
            Line(sb, t, "Pinch", -1f, id);
            return sb.ToString();
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
