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
    /// Three self-paced breaths at the wind-down plant. captureDeltaTime is 1/60.
    /// Record_DuskSequence stays quiet unless GARDEN_SEQ_DIR is set.
    /// </summary>
    public class DuskRitualTests
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
        public IEnumerator Dusk_ThreeBreaths()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.FixedStep();
            yield return null;
            DuskRitualController dusk = Dusk(controller);
            Assert.IsTrue(controller.State.Arc == ArcId.Midday, "14:20 is midday; the prompt is still due on day 1");
            Assert.IsTrue(dusk.OfferVisible, "the ritual prompt is missing");
            Assert.AreEqual(DuskRitualController.OfferLine, LineText("BreathPrompt"));
            Assert.IsFalse(DuskRitualController.OfferLine.Contains("\u2014"));
            Assert.AreEqual(TileState.Today, Wind(controller).Window[6]);

            SundialPlay.Play(controller, DuskScripts.ThreeBreaths());
            float app = 0f;
            float peak = 0f;
            bool shrank = false;
            bool ignoredQuickPinch = false;
            while (app < 20f)
            {
                yield return null;
                app += Time.deltaTime;
                if (app < 0.40f)
                {
                    Assert.IsFalse(dusk.Active, "a quick pinch must not start the ritual");
                    Assert.AreEqual(0, controller.LiveCount("breaths"));
                    ignoredQuickPinch = true;
                }
                if (dusk.CircleAmount > peak) peak = dusk.CircleAmount;
                if (peak > 0.30f && dusk.CircleAmount < peak - 0.10f) shrank = true;
                if (dusk.Answered && dusk.CircleAmount < 0.05f) break;
            }

            Assert.IsTrue(ignoredQuickPinch, "the script never passed the quick-pinch window");
            Assert.IsTrue(dusk.Answered, "three breaths did not finish, app " + app.ToString("0.00"));
            Assert.LessOrEqual(app, 40f, "the ritual took more than 40 s of app time");
            Assert.Greater(peak, 0.30f, "the ink circle never swelled");
            Assert.IsTrue(shrank, "the ink circle never shrank on the release");
            Assert.IsFalse(dusk.FillRing);
            Assert.AreEqual(3, dusk.Breaths);
            Assert.AreEqual(1, dusk.CueCount(DuskRitualController.CueChime));
            Assert.IsTrue(dusk.LeafOpened);
            Assert.AreEqual(Stage.Sprout, dusk.LeafStage);
            AssertKept(controller);
            Assert.IsFalse(dusk.OfferVisible);
            yield return null;
            Assert.IsTrue(dusk.SparkleVisible, "the sparkle ring did not play");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Dusk_FidgetsIgnored()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.FixedStep();
            DuskRitualController dusk = Dusk(controller);
            SundialPlay.Play(controller, DuskScripts.FidgetsThenThree());
            float app = 0f;
            bool sawIgnored = false;
            while (app < 20f)
            {
                yield return null;
                app += Time.deltaTime;
                if (!sawIgnored && app > 4.2f && app < 4.6f)
                {
                    Assert.AreEqual(0, dusk.Breaths, "a 1.0 s hold counted");
                    Assert.GreaterOrEqual(Count(dusk, BreathEventKind.ShortInhaleIgnored), 3);
                    Assert.AreEqual(0, Count(dusk, BreathEventKind.BreathCounted));
                    sawIgnored = true;
                }
                if (dusk.Answered && dusk.CircleAmount < 0.05f) break;
            }

            Assert.IsTrue(sawIgnored, "the fidget window was missed");
            Assert.IsTrue(dusk.Answered, "the real breaths did not finish");
            Assert.AreEqual(3, Count(dusk, BreathEventKind.ShortInhaleIgnored));
            Assert.AreEqual(3, dusk.Breaths);
            float first = FirstTime(dusk, BreathEventKind.BreathCounted);
            Assert.Greater(first, 5f, "the first counted breath arrived too early");
            AssertKept(controller);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Dusk_PauseAndResume_NoLoss()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.FixedStep();
            DuskRitualController dusk = Dusk(controller);
            SundialPlay.Play(controller, DuskScripts.PauseOnSecondInhale());
            float app = 0f;
            float frozen = -1f;
            bool sawFreeze = false;
            bool sawContinue = false;
            bool sawHeldThroughPause = false;
            while (app < 80f)
            {
                yield return null;
                app += Time.deltaTime;
                if (!sawFreeze && dusk.PausedFor > 0.50f && dusk.Breaths == 1)
                {
                    frozen = dusk.CircleAmount;
                    sawFreeze = true;
                    Assert.Greater(frozen, 0.05f, "the pause did not catch a live inhale");
                    Assert.IsFalse(dusk.Answered);
                    Assert.Greater(dusk.CircleDiameter, 0.01f, "the circle dropped while paused");
                }
                if (sawFreeze && !sawContinue && dusk.PausedFor > 30f && dusk.PausedFor < 50f)
                {
                    Assert.AreEqual(1, dusk.Breaths);
                    Assert.AreEqual(frozen, dusk.CircleAmount, 0.0001f);
                    Assert.IsFalse(dusk.AwaitingContinue, "Continue? arrived before 60 s");
                    sawHeldThroughPause = true;
                }
                if (!sawContinue && dusk.AwaitingContinue)
                {
                    sawContinue = true;
                    Assert.Greater(dusk.PausedFor, 60f);
                    Assert.AreEqual(1, dusk.Breaths);
                    Assert.AreEqual(frozen, dusk.CircleAmount, 0.0001f);
                    Assert.AreEqual(DuskRitualController.ContinueLine, LineText("ContinuePrompt"));
                    Assert.IsFalse(dusk.OfferVisible, "the breath offer must not replace Continue?");
                }
                if (sawContinue && app > 65.4f && app < 66.0f)
                {
                    Assert.IsFalse(dusk.AwaitingContinue, "Continue? stayed up after the pinch");
                    Assert.AreEqual(1, dusk.Breaths, "the ritual advanced before a fresh pinch");
                    Assert.AreEqual(frozen, dusk.CircleAmount, 0.0001f);
                }
                if (dusk.Answered && dusk.CircleAmount < 0.05f) break;
            }

            Assert.IsTrue(sawFreeze, "the circle never paused");
            Assert.IsTrue(sawHeldThroughPause, "the pause did not hold for half a minute");
            Assert.IsTrue(sawContinue, "Continue? was never offered");
            Assert.IsTrue(dusk.Answered, "the ritual did not finish after resume, app " + app.ToString("0.00"));
            Assert.AreEqual(3, dusk.Breaths);
            Assert.AreEqual(1, dusk.CueCount(DuskRitualController.CueChime));
            AssertKept(controller);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Dusk_ReducedMotion_SameTiming()
        {
            var swell = new List<BreathFrame>();
            var fill = new List<BreathFrame>();
            float swellDiameter = -1f;
            float fillDiameter = -1f;

            SundialController first = null;
            yield return SundialPlay.Open(c => first = c);
            SundialPlay.FixedStep();
            yield return null;
            first.Service.SetReducedMotion(false);
            DuskRitualController dusk = Dusk(first);
            SundialPlay.Play(first, DuskScripts.ThreeBreaths());
            yield return Sample(dusk, swell, false, d => swellDiameter = d);
            Assert.IsFalse(dusk.FillRing);
            Assert.IsTrue(dusk.Answered);

            yield return SundialPlay.Open(c => first = c);
            SundialPlay.FixedStep();
            yield return null;
            first.Service.SetReducedMotion(true);
            dusk = Dusk(first);
            Assert.IsTrue(dusk.OfferVisible, "reduced motion must still offer the breaths");
            SundialPlay.Play(first, DuskScripts.ThreeBreaths());
            yield return Sample(dusk, fill, true, d => fillDiameter = d);

            Assert.IsTrue(dusk.FillRing);
            Assert.IsTrue(dusk.Answered);
            Assert.AreEqual(swell.Count, fill.Count, "reduced motion changed the frame count");
            Assert.Greater(swell.Count, 200, "the sample is too short to compare");
            for (int i = 0; i < swell.Count; i++)
            {
                Assert.AreEqual(swell[i].Time, fill[i].Time, 0.0001f, "session time at " + i);
                Assert.AreEqual(swell[i].Amount, fill[i].Amount, 0.0001f, "circle amount at " + i);
                Assert.AreEqual(swell[i].Breaths, fill[i].Breaths, "breaths at " + i);
                Assert.AreEqual(swell[i].Phase, fill[i].Phase, "phase at " + i);
            }
            Assert.Greater(fillDiameter, 0f, "the fill ring was never measured");
            Assert.Greater(swellDiameter, 0f, "the swell circle was never measured");
            Assert.Greater(fillDiameter, swellDiameter + 0.02f, "reduced motion still swells the circle");
            Assert.AreEqual(DuskRitualController.MaxDiameter, fillDiameter, 0.0001f);
            AssertKept(first);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Record_DuskSequence()
        {
            string dir = Environment.GetEnvironmentVariable("GARDEN_SEQ_DIR");
            if (string.IsNullOrEmpty(dir))
                yield break;

            Directory.CreateDirectory(dir);
            string runDir = RunDir();
            Directory.CreateDirectory(runDir);
            DisableAsyncShaders();
            SundialController.SaveDirectoryOverride = SundialPlay.FreshDir();
            SundialController.ClockOverride = Clock1840();
            SundialController controller = null;
            yield return SundialPlay.LoadMain(c => controller = c);
            SundialPlay.FixedStep();
            yield return null;
            DuskRitualController dusk = Dusk(controller);
            Assert.IsTrue(controller.State.Arc == ArcId.WindDown, "18:40 should be the wind-down arc");
            Assert.IsTrue(dusk.OfferVisible);

            Camera cam = PoseDial();
            AttachPlate(cam);
            SundialPlay.Play(controller, DuskScripts.SlowThree());
            var recorder = cam.gameObject.AddComponent<SequenceRecorder>();
            recorder.enabled = false;
            recorder.fps = 10;
            recorder.width = 912;
            recorder.height = 512;
            recorder.msaa = 4;
            recorder.outputDirectory = dir;
            recorder.enabled = true;

            var grab = cam.gameObject.AddComponent<DuskStillGrab>();
            grab.Cam = cam;
            grab.Dusk = dusk;
            grab.InhalePath = Path.Combine(runDir, "dusk-inhale.png");
            grab.AnswerPath = Path.Combine(runDir, "dusk-answer.png");

            float app = 0f;
            while (app < 18.8f)
            {
                yield return null;
                app += Time.deltaTime;
            }
            recorder.enabled = false;

            Assert.IsTrue(grab.SavedInhale, "mid-inhale still was not grabbed");
            Assert.IsTrue(grab.SavedAnswer, "answer still was not grabbed");
            Assert.IsTrue(dusk.Answered, "the recorded ritual did not finish");
            int frames = Directory.GetFiles(dir, "f*.png").Length;
            Assert.GreaterOrEqual(frames, 150, "sequence is shorter than 15 s at 10 fps");
            File.WriteAllText(Path.Combine(runDir, "frames.txt"),
                "frames=" + frames + "\n" +
                "inhaleFrame=" + grab.InhaleFrame + "\n" +
                "answerFrame=" + grab.AnswerFrame + "\n" +
                "appTime=" + app.ToString("0.00", CultureInfo.InvariantCulture) + "\n" +
                "breaths=" + dusk.Breaths + "\n");
        }

        static IEnumerator Sample(DuskRitualController dusk, List<BreathFrame> frames, bool fill, Action<float> onBand)
        {
            while (true)
            {
                yield return null;
                BreathPhase phase = dusk.Session == null ? BreathPhase.Waiting : dusk.Session.Phase;
                float time = dusk.Session == null ? 0f : dusk.Session.Time;
                int breaths = dusk.Session == null ? 0 : dusk.Breaths;
                frames.Add(new BreathFrame(time, dusk.CircleAmount, breaths, phase));
                Assert.AreEqual(fill, dusk.FillRing);
                if (dusk.CircleAmount >= 0.30f && dusk.CircleAmount <= 0.60f && dusk.CircleDiameter > 0f)
                    onBand(dusk.CircleDiameter);
                if (dusk.Answered && dusk.CircleAmount < 0.05f) break;
                if (dusk.AppTime > 25f) break;
            }
        }

        static void AssertKept(SundialController controller)
        {
            PlantState plant = Wind(controller);
            Assert.AreEqual(TileState.Kept, plant.Window[plant.Window.Length - 1]);
            Assert.AreEqual(Stage.Sprout, plant.Stage);
            Assert.AreEqual(1, controller.LiveCount("breaths"));
            Assert.IsFalse(controller.UndoVisible, "a ritual tend must not offer undo");
            TendEvent live = null;
            int count = 0;
            IReadOnlyList<TendEvent> events = controller.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                TendEvent ev = events[i];
                if (ev.HabitId != "breaths" || ev.UndoneAtUtcMs.HasValue) continue;
                count++;
                live = ev;
            }
            Assert.AreEqual(1, count, "the breaths habit should have one live tend");
            Assert.AreEqual(TendSource.Ritual, live.Source);
            string savePath = Path.Combine(SundialController.SaveDirectoryOverride, "save.json");
            string save = File.ReadAllText(savePath);
            Assert.IsTrue(save.Contains("\"Source\":\"Ritual\""), save);
        }

        static DuskRitualController Dusk(SundialController controller)
        {
            DuskRitualController dusk = controller.GetComponent<DuskRitualController>();
            Assert.IsNotNull(dusk, "DuskRitualController is missing");
            return dusk;
        }

        static PlantState Wind(SundialController controller)
        {
            HabitDef habit = controller.Service.HabitForArc("winddown");
            Assert.IsNotNull(habit, "wind-down habit missing");
            Assert.AreEqual("breaths", habit.Id);
            PlantState plant = controller.Service.PlantFor(habit);
            Assert.IsNotNull(plant, "wind-down plant missing");
            Assert.IsNotNull(plant.Window);
            Assert.GreaterOrEqual(plant.Window.Length, 7);
            return plant;
        }

        static string LineText(string objectName)
        {
            GameObject go = GameObject.Find(objectName);
            Assert.IsNotNull(go, objectName + " is not showing");
            TextMesh mesh = go.GetComponent<TextMesh>();
            Assert.IsNotNull(mesh, objectName);
            return mesh.text;
        }

        static int Count(DuskRitualController dusk, BreathEventKind kind)
        {
            int count = 0;
            if (dusk.Session == null) return 0;
            for (int i = 0; i < dusk.Session.Events.Count; i++)
            {
                if (dusk.Session.Events[i].Kind == kind) count++;
            }
            return count;
        }

        static float FirstTime(DuskRitualController dusk, BreathEventKind kind)
        {
            for (int i = 0; i < dusk.Session.Events.Count; i++)
            {
                if (dusk.Session.Events[i].Kind == kind) return dusk.Session.Events[i].Time;
            }
            Assert.Fail("missing event " + kind);
            return -1f;
        }

        static FixedClock Clock1840()
        {
            var now = new DateTimeOffset(2026, 10, 3, 18, 40, 0, TimeSpan.Zero);
            return new FixedClock(now, TimeZoneInfo.Utc);
        }

        static string RunDir()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            return Path.Combine(root, "orchestration", "runs", "sundial", "T-SUN-009");
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
            var mesh = new Mesh { name = "DuskPlate" };
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

        struct BreathFrame
        {
            public readonly float Time;
            public readonly float Amount;
            public readonly int Breaths;
            public readonly BreathPhase Phase;

            public BreathFrame(float time, float amount, int breaths, BreathPhase phase)
            {
                Time = time;
                Amount = amount;
                Breaths = breaths;
                Phase = phase;
            }
        }
    }

    /// <summary>Full-frame DialG1 stills, after the dusk circle has been placed.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class DuskStillGrab : MonoBehaviour
    {
        public Camera Cam;
        public DuskRitualController Dusk;
        public string InhalePath;
        public string AnswerPath;
        public bool SavedInhale;
        public bool SavedAnswer;
        public int InhaleFrame = -1;
        public int AnswerFrame = -1;
        int _frame;

        void LateUpdate()
        {
            int frame = _frame;
            _frame++;
            if (Dusk == null || Cam == null) return;
            if (!SavedInhale
                && Dusk.Breaths == 0
                && Dusk.Session != null
                && Dusk.Session.Phase == BreathPhase.Inhaling
                && Dusk.CircleAmount >= 0.45f)
            {
                Write(InhalePath);
                SavedInhale = true;
                InhaleFrame = frame;
            }
            if (!SavedAnswer && Dusk.Answered)
            {
                Write(AnswerPath);
                SavedAnswer = true;
                AnswerFrame = frame;
            }
        }

        void Write(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            Texture2D tex = FrameGrab.RenderToTexture(Cam, Framings.DefaultWidth, Framings.DefaultHeight, 4);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
        }
    }

    /// <summary>A trailing Look keeps the scripted source ticking so the last exhale can count.</summary>
    static class DuskScripts
    {
        public static string ThreeBreaths()
        {
            var sb = new StringBuilder();
            Line(sb, 0.10f, "Pinch", -1f, DuskRitualController.PlantId);
            float t = Holds(sb, 0.50f, 3, 2.0f, 1.40f);
            Trail(sb, t, 2.2f);
            return sb.ToString();
        }

        public static string FidgetsThenThree()
        {
            var sb = new StringBuilder();
            Line(sb, 0f, "Pinch", -1f, DuskRitualController.BreathPromptId);
            float t = Holds(sb, 0.20f, 3, 1.0f, 0.40f);
            t = Holds(sb, t + 0.40f, 3, 2.0f, 1.40f);
            Trail(sb, t, 2.2f);
            return sb.ToString();
        }

        public static string PauseOnSecondInhale()
        {
            var sb = new StringBuilder();
            Line(sb, 0.00f, "PinchHold", 2.0f, DuskRitualController.PlantId);
            Line(sb, 2.00f, "Release", -1f, DuskRitualController.PlantId);
            Line(sb, 3.40f, "PinchHold", 2.0f, DuskRitualController.PlantId);
            Line(sb, 3.90f, "PalmOpen", -1f, null);
            Line(sb, 65.00f, "Pinch", -1f, DuskRitualController.ContinuePromptId);
            Line(sb, 66.20f, "PinchHold", 2.0f, DuskRitualController.PlantId);
            Line(sb, 68.20f, "Release", -1f, DuskRitualController.PlantId);
            Line(sb, 69.60f, "PinchHold", 2.0f, DuskRitualController.PlantId);
            Line(sb, 71.60f, "Release", -1f, DuskRitualController.PlantId);
            Trail(sb, 71.60f, 2.5f);
            return sb.ToString();
        }

        public static string SlowThree()
        {
            var sb = new StringBuilder();
            Line(sb, 0f, "Look", 1.20f, DuskRitualController.PlantId);
            float t = Holds(sb, 1.20f, 3, 3.4f, 2.0f);
            Trail(sb, t, 4.0f);
            return sb.ToString();
        }

        static float Holds(StringBuilder sb, float start, int count, float hold, float gap)
        {
            float t = start;
            for (int i = 0; i < count; i++)
            {
                Line(sb, t, "PinchHold", hold, DuskRitualController.PlantId);
                t += hold;
                Line(sb, t, "Release", -1f, DuskRitualController.PlantId);
                if (i + 1 < count) t += gap;
            }
            return t;
        }

        static void Trail(StringBuilder sb, float t, float dur)
        {
            Line(sb, t, "Look", dur, DuskRitualController.PlantId);
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
