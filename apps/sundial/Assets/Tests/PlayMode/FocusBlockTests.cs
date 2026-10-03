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
    /// A 25-minute shadow hour. Time is injected. The pinch still comes from the scripted hand.
    /// Record_FocusShadow stays quiet unless GARDEN_FOCUS_DIR is set.
    /// </summary>
    public class FocusBlockTests
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
        public IEnumerator FocusBlock_TwentyFiveMinutes_ByPlayback()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.FixedStep();
            yield return null;

            FocusBlockController focus = Focus(controller);
            Assert.AreEqual(ArcId.Midday, controller.State.Arc, "14:20 is midday");
            Assert.IsTrue(focus.OfferVisible, "the quiet-hour prompt is missing");
            Assert.AreEqual(FocusBlockController.OfferLine, LineText("FocusPrompt"));
            Assert.IsFalse(FocusBlockController.OfferLine.Contains("\u2014"));
            string lower = FocusBlockController.OfferLine.ToLowerInvariant();
            Assert.IsFalse(lower.Contains("fail") || lower.Contains("streak") || lower.Contains("shame") || lower.Contains("wilt"));
            Assert.AreEqual("A quiet hour?", FocusBlockController.OfferLine);
            PlantState midday = SundialPlay.Midday(controller);
            Assert.AreEqual(TileState.Today, midday.Window[6]);
            Assert.AreEqual(Stage.Seed, midday.Stage);
            AssertWithinReach();
            string savePath = Path.Combine(SundialController.SaveDirectoryOverride, "save.json");
            string before = File.ReadAllText(savePath);
            Assert.IsFalse(before.Contains("\"Focus\""), "an idle save must omit the hour");

            SundialPlay.Play(controller, PinchAt(0.12f));
            yield return Until(() => focus.Running, 2f);
            Assert.IsTrue(focus.Running, "the gnomon pinch did not open the hour");
            yield return null;
            Assert.IsFalse(focus.OfferVisible, "the prompt stays up while the hour runs");
            Assert.AreEqual(1, focus.CueCount(FocusBlockController.CueBegin));
            Assert.AreEqual(1, controller.CueCount(SundialController.CueInk));
            Assert.AreEqual(0, focus.CueCount(FocusBlockController.CueDone));

            FocusBlock block = focus.Block;
            Assert.AreEqual(ArcId.Midday, block.Arc);
            Assert.AreEqual(125f, block.StartGnomonDeg, 0.01f);
            focus.RefreshInk();
            Assert.GreaterOrEqual(focus.DrawnDegrees, 2f, "the pen should read as down at the first moment");
            Assert.Less(focus.DrawnDegrees, 8f);
            float previous = block.Progress;

            for (int minute = 1; minute <= 24; minute++)
            {
                controller.Service.JumpTo(block.Started.AddMinutes(minute));
                focus.RefreshInk();
                Assert.IsTrue(focus.Running, "minute " + minute + " left the hour");
                Assert.LessOrEqual(Math.Abs(block.ElapsedSeconds - minute * 60), 1, "elapsed at minute " + minute);
                Assert.GreaterOrEqual(block.Progress, previous, "progress fell at minute " + minute);
                previous = block.Progress;
                if (minute == 8)
                {
                    Assert.Greater(focus.DrawnDegrees, 20f);
                    Assert.Less(focus.DrawnDegrees, 40f);
                    float along = InkNear(focus, block.StartGnomonDeg + 10f, 0.07f);
                    float ahead = InkNear(focus, block.StartGnomonDeg + 80f, 0.07f);
                    Assert.Greater(along, 0.12f, "ink missing along the shadow at 8 minutes");
                    Assert.Less(ahead, 0.05f, "ink ran ahead of the pen");
                }
            }

            controller.Service.JumpTo(block.Started.AddSeconds(FocusBlock.DurationSeconds + 1));
            focus.RefreshInk();
            yield return null;

            Assert.IsTrue(focus.Complete);
            Assert.IsTrue(focus.Counted);
            Assert.IsFalse(focus.EndedEarly, "a full hour must not read as an early end");
            Assert.AreEqual(1f, block.Progress, 0.0001f);
            Assert.AreEqual(FocusBlock.DurationSeconds, block.ElapsedSeconds);
            Assert.AreEqual(ArcId.Midday, block.Arc);
            Assert.IsFalse(block.TendAuthorised, "the tend should already be in the ledger");
            Assert.IsFalse(controller.UndoVisible, "a ritual tend must not offer undo");
            Assert.IsFalse(controller.Dismissed);
            Assert.IsTrue(focus.OfferVisible, "the prompt returns after the hour");
            AssertKept(controller, "top3");
            Assert.AreEqual(1, focus.CueCount(FocusBlockController.CueBegin));
            Assert.AreEqual(1, focus.CueCount(FocusBlockController.CueDone));
            Assert.AreEqual(1, controller.CueCount(SundialController.CueInk));
            Assert.AreEqual(1, controller.CueCount(SundialController.CueFlutter));

            float full = InkNear(focus, block.StartGnomonDeg + FocusBlock.SweepDegrees, 0.07f);
            float opposite = InkNear(focus, block.StartGnomonDeg + FocusBlock.SweepDegrees + 180f, 0.07f);
            Assert.Greater(full, 0.15f, "the finished sweep has no ink on the shadow");
            Assert.Less(opposite, 0.05f, "ink crossed to the far side of the dial");
            Assert.Greater(focus.DrawnDegrees, 80f);

            string save = File.ReadAllText(savePath);
            Assert.IsTrue(save.Contains("\"SchemaVersion\":1"), save);
            Assert.IsTrue(save.Contains("\"Source\":\"Ritual\""), save);
            Assert.IsTrue(save.Contains("\"HabitId\":\"top3\""), save);
            Assert.IsTrue(save.Contains("\"Phase\":\"Complete\""), save);
            Assert.IsTrue(save.Contains("\"TendPending\":false"), save);
            Assert.IsTrue(save.Contains("\"Arc\":\"Midday\""), save);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator FocusBlock_EndEarly_StillCounts()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.FixedStep();
            yield return null;

            FocusBlockController focus = Focus(controller);
            SundialPlay.Play(controller, PinchAt(0.08f));
            yield return Until(() => focus.Running, 2f);
            Assert.IsTrue(focus.Running, "the hour did not start");
            DateTimeOffset started = focus.Block.Started;

            controller.Service.JumpTo(started.AddMinutes(5));
            SundialPlay.Play(controller, PinchAt(0.08f));
            yield return Until(() => focus.Complete, 2f);

            Assert.IsTrue(focus.Complete);
            Assert.IsTrue(focus.Counted);
            Assert.IsTrue(focus.EndedEarly, "ending at five minutes should stay an early end");
            Assert.LessOrEqual(Math.Abs(focus.Block.ElapsedSeconds - 5 * 60), 2);
            Assert.AreEqual(0.2f, focus.Block.Progress, 0.02f);
            Assert.IsFalse(controller.UndoVisible);
            Assert.IsFalse(controller.Dismissed);
            AssertKept(controller, "top3");
            Assert.AreEqual(Stage.Sprout, SundialPlay.Midday(controller).Stage);
            yield return null;
            Assert.AreEqual(1, focus.CueCount(FocusBlockController.CueDone));

            DateTimeOffset firstStart = started;
            SundialPlay.Play(controller, PinchAt(0.08f));
            yield return Until(() => focus.Running && focus.Block.Started != firstStart, 2f);
            Assert.IsTrue(focus.Running, "a second pinch should open another hour");
            Assert.IsFalse(focus.EndedEarly);
            DateTimeOffset second = focus.Block.Started;
            controller.Service.JumpTo(second.AddSeconds(FocusBlock.DurationSeconds + 1));
            yield return null;

            Assert.IsTrue(focus.Complete, "a second hour the same day must not fail");
            Assert.IsTrue(focus.Counted);
            Assert.IsFalse(focus.EndedEarly);
            Assert.AreEqual(1, controller.LiveCount("top3"), "a second hour the same day keeps the one live tend");
            Assert.AreEqual(1, LiveRituals(controller, "top3"));
            Assert.IsFalse(controller.UndoVisible);
            Assert.AreEqual(Stage.Sprout, SundialPlay.Midday(controller).Stage);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator FocusBlock_PauseAndReload_KeepsTheHour()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.FixedStep();
            yield return null;

            FocusBlockController focus = Focus(controller);
            Assert.IsFalse(focus.Running);
            controller.NotifyFocusLost();
            yield return null;
            Assert.IsFalse(focus.Running, "focus loss with no hour must not start one");
            Assert.IsFalse(focus.Paused);
            Assert.IsFalse(focus.Complete);
            Assert.IsFalse(controller.Dismissed, "focus loss must not put the dial away");
            Assert.AreEqual(0, controller.LiveCount("top3"));

            SundialPlay.Play(controller, PinchAt(0.08f));
            yield return Until(() => focus.Running, 2f);
            Assert.IsTrue(focus.Running);
            DateTimeOffset started = focus.Block.Started;
            string dir = SundialController.SaveDirectoryOverride;

            controller.Service.JumpTo(started.AddMinutes(10));
            focus.RefreshInk();
            Assert.IsTrue(focus.Running);
            Assert.LessOrEqual(Math.Abs(focus.Block.ElapsedSeconds - 600), 1);
            Assert.AreEqual(0, controller.LiveCount("top3"));
            string runningSave = File.ReadAllText(Path.Combine(dir, "save.json"));
            Assert.IsTrue(runningSave.Contains("\"Phase\":\"Running\""), runningSave);

            yield return SundialPlay.Unload();
            SundialService.DevSeedOnFresh = true;
            SundialController.SaveDirectoryOverride = dir;
            SundialController.ClockOverride = new FixedClock(started.AddMinutes(10), TimeZoneInfo.Utc);
            SundialController reloaded = null;
            yield return SundialPlay.LoadMain(c => reloaded = c);
            SundialPlay.FixedStep();
            yield return null;

            focus = Focus(reloaded);
            Assert.IsTrue(focus.Running, "a quit in the middle must come back still running");
            Assert.IsFalse(focus.Paused);
            Assert.LessOrEqual(Math.Abs(focus.Block.ElapsedSeconds - 600), 1);
            Assert.AreEqual(0, reloaded.LiveCount("top3"), "reloading a running hour must not tend");
            Assert.AreEqual(0, focus.CueCount(FocusBlockController.CueBegin), "reloading must not replay the opening cue");
            Assert.AreEqual(TileState.Today, SundialPlay.Midday(reloaded).Window[6]);

            reloaded.Service.JumpTo(started.AddSeconds(FocusBlock.DurationSeconds + 1));
            yield return null;
            Assert.IsTrue(focus.Complete);
            Assert.IsTrue(focus.Counted);
            Assert.IsFalse(focus.EndedEarly);
            Assert.AreEqual(FocusBlock.DurationSeconds, focus.Block.ElapsedSeconds);
            AssertKept(reloaded, "top3");

            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.FixedStep();
            yield return null;
            focus = Focus(controller);
            SundialPlay.Play(controller, PinchAt(0.08f));
            yield return Until(() => focus.Running, 2f);
            started = focus.Block.Started;

            controller.NotifyFocusLost();
            Assert.IsTrue(focus.Paused, "Esc should hold the hour");
            Assert.IsFalse(focus.Complete);
            Assert.IsFalse(controller.Dismissed);
            Assert.AreEqual(0, controller.LiveCount("top3"));
            controller.NotifyFocusLost();
            Assert.IsTrue(focus.Running, "a second Esc should continue the hour");
            Assert.IsFalse(focus.EndedEarly);

            controller.Service.JumpTo(started.AddMinutes(10));
            Assert.IsTrue(controller.Service.PauseFocus());
            yield return null;
            Assert.IsTrue(focus.Paused);
            Assert.IsFalse(focus.OfferVisible);
            Assert.LessOrEqual(Math.Abs(focus.Block.ElapsedSeconds - 600), 1);
            controller.Service.JumpTo(started.AddMinutes(50));
            Assert.IsTrue(focus.Paused, "a pause must ignore the gap");
            Assert.LessOrEqual(Math.Abs(focus.Block.ElapsedSeconds - 600), 1);
            Assert.AreEqual(TileState.Today, SundialPlay.Midday(controller).Window[6]);
            Assert.IsTrue(controller.Service.ResumeFocus());
            Assert.IsTrue(focus.Running);
            Assert.LessOrEqual(Math.Abs(focus.Block.ElapsedSeconds - 600), 1);
            controller.Service.JumpTo(started.AddMinutes(65));
            yield return null;
            Assert.IsTrue(focus.Complete);
            Assert.IsTrue(focus.Counted);
            Assert.IsFalse(focus.EndedEarly, "ten active minutes, a pause, then fifteen more is a full hour");
            Assert.AreEqual(FocusBlock.DurationSeconds, focus.Block.ElapsedSeconds);
            AssertKept(controller, "top3");
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Record_FocusShadow()
        {
            string dir = Environment.GetEnvironmentVariable("GARDEN_FOCUS_DIR");
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
            SundialPlay.FixedStep();
            yield return null;

            FocusBlockController focus = Focus(controller);
            Assert.IsTrue(focus.OfferVisible);
            Camera cam = PoseDial();
            AttachPlate(cam);
            SundialPlay.Play(controller, PinchAt(0.08f));
            yield return Until(() => focus.Running, 2f);
            Assert.IsTrue(focus.Running, "the recorded pinch did not open the hour");
            for (int warm = 0; warm < 12; warm++)
                yield return null;

            DateTimeOffset started = focus.Block.Started;
            var recorder = cam.gameObject.AddComponent<SequenceRecorder>();
            recorder.enabled = false;
            recorder.fps = 10;
            recorder.width = 912;
            recorder.height = 512;
            recorder.msaa = 4;
            recorder.outputDirectory = dir;
            recorder.enabled = true;

            const int framesWanted = 49;
            for (int i = 0; i < framesWanted; i++)
            {
                double seconds = FocusBlock.DurationSeconds * (i / (double)(framesWanted - 1));
                controller.Service.JumpTo(started.AddSeconds(seconds));
                focus.RefreshInk();
                yield return null;
            }
            recorder.enabled = false;

            int frames = Directory.GetFiles(dir, "f*.png").Length;
            Assert.GreaterOrEqual(frames, framesWanted, "the shadow sequence is short");
            Assert.IsTrue(focus.Complete, "the recorded hour did not finish");
            Assert.Greater(focus.DrawnDegrees, 80f);
            File.WriteAllText(Path.Combine(runDir, "frames.txt"),
                "frames=" + frames + "\n" +
                "drawn=" + focus.DrawnDegrees.ToString("0.0", CultureInfo.InvariantCulture) + "\n" +
                "ink=" + focus.InkPixels + "\n" +
                "elapsed=" + focus.Block.ElapsedSeconds + "\n" +
                "early=" + focus.EndedEarly + "\n");
        }

        static void AssertKept(SundialController controller, string habitId)
        {
            PlantState plant = SundialPlay.Midday(controller);
            Assert.AreEqual(habitId, "top3");
            Assert.AreEqual(TileState.Kept, plant.Window[6]);
            Assert.AreEqual(Stage.Sprout, plant.Stage);
            Assert.AreEqual(1, controller.LiveCount(habitId));
            Assert.AreEqual(1, LiveRituals(controller, habitId));
        }

        static int LiveRituals(SundialController controller, string habitId)
        {
            int count = 0;
            var events = controller.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                TendEvent ev = events[i];
                if (ev == null || ev.HabitId != habitId || ev.UndoneAtUtcMs.HasValue) continue;
                Assert.AreEqual(TendSource.Ritual, ev.Source);
                count++;
            }
            return count;
        }

        static void AssertWithinReach()
        {
            GameObject eye = GameObject.Find("EyeCamera");
            Assert.IsNotNull(eye, "EyeCamera missing");
            GameObject gnomon = GameObject.Find(FocusBlockController.GnomonId);
            Assert.IsNotNull(gnomon, "gnomon target missing");
            IntentTarget target = gnomon.GetComponent<IntentTarget>();
            Assert.IsNotNull(target);
            Assert.AreEqual(FocusBlockController.GnomonId, target.Id);
            float distance = Vector3.Distance(eye.transform.position, gnomon.transform.position);
            Assert.Less(distance, FocusBlockController.TwoFootMetres,
                "gnomon is " + distance.ToString("0.000") + " m from the eye");
        }

        static float InkNear(FocusBlockController focus, float deg, float radius)
        {
            float best = 0f;
            for (int i = -2; i <= 2; i++)
            {
                Vector2 along = OnDial(deg, radius + i * 0.003f);
                float alpha = focus.SampleDial(along.x, along.y);
                if (alpha > best) best = alpha;
                Vector2 side = OnDial(deg + i * 1.2f, radius);
                alpha = focus.SampleDial(side.x, side.y);
                if (alpha > best) best = alpha;
            }
            return best;
        }

        static Vector2 OnDial(float deg, float radius)
        {
            float rad = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad) * radius, -Mathf.Sin(rad) * radius);
        }

        static FocusBlockController Focus(SundialController controller)
        {
            FocusBlockController focus = controller.GetComponent<FocusBlockController>();
            Assert.IsNotNull(focus, "FocusBlockController is missing");
            return focus;
        }

        static string LineText(string name)
        {
            GameObject go = GameObject.Find(name);
            Assert.IsNotNull(go, name);
            TextMesh mesh = go.GetComponent<TextMesh>();
            Assert.IsNotNull(mesh, name);
            return mesh.text;
        }

        static IEnumerator Until(Func<bool> ready, float seconds)
        {
            float app = 0f;
            while (app < seconds && !ready())
            {
                yield return null;
                app += Time.deltaTime;
            }
        }

        static string PinchAt(float t)
        {
            return "{\"t\":" + t.ToString("0.00", CultureInfo.InvariantCulture) + ",\"intent\":\"Pinch\",\"target\":\"gnomon\"}\n";
        }

        static string RunDir()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            return Path.Combine(root, "orchestration", "runs", "sundial", "T-SUN-025");
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
            var mesh = new Mesh { name = "FocusPlate" };
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
}
