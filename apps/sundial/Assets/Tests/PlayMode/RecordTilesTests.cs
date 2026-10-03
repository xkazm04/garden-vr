using System;
using System.Collections;
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
    /// The week's tiles, yesterday's one late log, and the journeys a missed day and day 7 take.
    /// The clock is injected. Nothing here reads a keyboard or a mouse.
    /// </summary>
    public class RecordTilesTests
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
        public IEnumerator Backfill_Once_Late()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            Assert.IsTrue(controller.ShiftDay(1), "yesterday has to exist");
            SundialPlay.FixedStep();
            yield return null;
            Assert.IsTrue(controller.AskVisible("midday"), "yesterday's pale tile should carry the ask");
            Assert.AreEqual(TileState.Missed, Midday(controller).Window[5]);

            string script =
                "{\"t\":0.15,\"intent\":\"Poke\",\"target\":\"tile.midday.yesterday.ask\"}\n" +
                "{\"t\":0.40,\"intent\":\"Pinch\",\"target\":\"prompt.backfill.no\"}\n" +
                "{\"t\":0.70,\"intent\":\"Look\",\"target\":\"tile.midday.yesterday.ask\",\"dur\":0.30}\n" +
                "{\"t\":0.90,\"intent\":\"Pinch\",\"target\":\"tile.midday.yesterday.ask\"}\n" +
                "{\"t\":1.15,\"intent\":\"Pinch\",\"target\":\"prompt.backfill.yes\"}\n";
            SundialPlay.Play(controller, script);

            bool sawPrompt = false;
            bool declined = false;
            bool sawWords = false;
            float app = 0f;
            while (app < 1.70f)
            {
                yield return null;
                app += Time.deltaTime;
                if (controller.PromptVisible)
                {
                    sawPrompt = true;
                    if (!sawWords) sawWords = ReadChip();
                }
                if (sawPrompt && !controller.PromptVisible && LateCount(controller, "top3") == 0)
                    declined = true;
            }

            Assert.IsTrue(sawPrompt, "the paper chip did not open");
            Assert.IsTrue(sawWords, "the chip did not say the question and the two answers");
            Assert.IsTrue(declined, "Not this time should close the chip and write nothing");
            PlantState plant = Midday(controller);
            Assert.AreEqual(TileState.Late, plant.Window[5]);
            Assert.IsFalse(plant.CanBackfillYesterday);
            Assert.IsFalse(controller.AskVisible("midday"), "the ask leaves after the one late log");
            Assert.IsFalse(controller.PromptVisible);
            Assert.AreEqual(1, controller.CueCount(SundialController.CueHatch));
            Assert.AreEqual(1, LateCount(controller, "top3"));
            Assert.AreEqual(TendSource.Backfill, LateEvent(controller, "top3").Source);
            Assert.Greater(LateEvent(controller, "top3").AtUtcMs, 0L);

            SundialPlay.Play(controller,
                "{\"t\":0.10,\"intent\":\"Poke\",\"target\":\"tile.midday.yesterday.ask\"}\n" +
                "{\"t\":0.30,\"intent\":\"Pinch\",\"target\":\"prompt.backfill.yes\"}\n");
            yield return SundialPlay.Seconds(0.50f);
            Assert.IsFalse(controller.PromptVisible, "a second attempt is not offered");
            Assert.IsFalse(controller.AskVisible("midday"));
            Assert.AreEqual(1, LateCount(controller, "top3"));
            Assert.AreEqual(TileState.Late, Midday(controller).Window[5]);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Backfill_NotAfterBoundary()
        {
            FixedClock clock = SundialPlay.Clock1420();
            SundialController.SaveDirectoryOverride = SundialPlay.FreshDir();
            SundialController.ClockOverride = clock;
            SundialController controller = null;
            yield return SundialPlay.LoadMain(c => controller = c);
            Assert.IsTrue(controller.ShiftDay(1));
            controller.Service.Arm("top3");
            controller.Service.Flush();
            SundialPlay.FixedStep();
            yield return null;
            Assert.IsTrue(controller.AskVisible("midday"));

            GardenDay today = controller.Service.Today();
            int missed = today.Index - 1;
            DateTimeOffset boundary = today.EndsAt(TimeZoneInfo.Utc, GardenDay.DefaultBoundary);
            clock.Now = clock.Now.Add(boundary - controller.Service.Clock.Now);
            controller.Service.Step(0f);

            HabitDef habit = controller.Service.HabitForArc("midday");
            TendResult refused = SundialRules.Backfill(habit, controller.Ledger, today, controller.Service.Clock);
            Assert.IsFalse(refused.Ok);
            Assert.AreEqual("after-boundary", refused.Reason);

            yield return null;
            Assert.IsFalse(controller.AskVisible("midday"), "the ask ends at the boundary");
            Assert.IsFalse(controller.Service.BackfillOffered(habit));
            SundialPlay.Play(controller,
                "{\"t\":0.10,\"intent\":\"Poke\",\"target\":\"tile.midday.yesterday.ask\"}\n" +
                "{\"t\":0.30,\"intent\":\"Pinch\",\"target\":\"prompt.backfill.yes\"}\n");
            yield return SundialPlay.Seconds(0.50f);
            Assert.IsFalse(controller.PromptVisible);
            Assert.AreEqual(0, LateCount(controller, "top3"));

            TendResult rolled = controller.Service.BackfillYesterday(habit);
            Assert.IsFalse(rolled.Ok);
            Assert.AreEqual("already-kept", rolled.Reason);
            PlantState plant = Midday(controller);
            int slot = 6 - (controller.Service.Today().Index - missed);
            Assert.GreaterOrEqual(slot, 0);
            Assert.Less(slot, 7);
            Assert.AreEqual(TileState.Missed, plant.Window[slot], "the day that aged out stays a quiet miss");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Missed_NoShrink()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.FixedStep();
            for (int i = 0; i < 3; i++)
            {
                Keep(controller, "top3");
                Assert.IsTrue(controller.ShiftDay(1));
            }
            yield return null;
            PlantState grown = Midday(controller);
            Assert.AreEqual(3, grown.LifetimeKept);
            Assert.AreEqual(Stage.Young, grown.Stage);
            int card = controller.View.stageMidday;
            Assert.AreEqual((int)Stage.Young, card);

            Assert.IsTrue(controller.ShiftDay(3));
            yield return null;
            PlantState missed = Midday(controller);
            Assert.AreEqual(3, missed.LifetimeKept);
            Assert.AreEqual(Stage.Young, missed.Stage);
            Assert.AreEqual(card, controller.View.stageMidday, "a miss does not step the stage card back");
            int pale = 0;
            for (int i = 0; i < 6; i++)
            {
                if (missed.Window[i] == TileState.Missed) pale++;
            }
            Assert.GreaterOrEqual(pale, 3, "three missed days should sit in the row");

            controller.View.stageMidday = 0;
            yield return null;
            Assert.AreEqual(card, controller.View.stageMidday, "the stage card stays on the fullest drawing this session has shown");
            Assert.AreEqual(3, Midday(controller).LifetimeKept);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Week_BloomOpensAt5_ClosesBelow()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.FixedStep();
            for (int i = 0; i < 5; i++)
            {
                Keep(controller, "top3");
                if (i < 4) Assert.IsTrue(controller.ShiftDay(1));
            }
            yield return null;
            PlantState open = Midday(controller);
            Assert.AreEqual(5, open.WindowKept);
            Assert.AreEqual(Bloom.Open, open.Bloom);
            int kept = open.LifetimeKept;
            Stage stage = open.Stage;
            int card = controller.View.stageMidday;
            Assert.IsFalse(controller.View.IsBloomClosing(1));
            Assert.AreEqual(1, controller.View.ShownBloomCard(1));

            Assert.IsTrue(controller.ShiftDay(3));
            yield return null;
            PlantState bud = Midday(controller);
            Assert.AreEqual(Bloom.Bud, bud.Bloom);
            Assert.Less(bud.WindowKept, 5);
            Assert.GreaterOrEqual(bud.WindowKept, 3);
            Assert.AreEqual(kept, bud.LifetimeKept);
            Assert.AreEqual(stage, bud.Stage);
            Assert.AreEqual(card, controller.View.stageMidday);
            Assert.IsTrue(controller.View.IsBloomClosing(1), "the open flower folds before the bud is all that remains");
            Assert.AreEqual(1, controller.View.ShownBloomCard(1));

            yield return SundialPlay.Seconds(1.45f);
            Assert.IsFalse(controller.View.IsBloomClosing(1));
            Assert.AreEqual(0, controller.View.ShownBloomCard(1), "after 1.2 s the overlay is the bud");
            Assert.AreEqual(card, controller.View.stageMidday);
            Assert.AreEqual(kept, Midday(controller).LifetimeKept);
            Assert.AreEqual(stage, Midday(controller).Stage);

            controller.View.bloomMidday = 2f;
            controller.View.time += 0.05f;
            controller.View.Apply();
            controller.View.reducedMotion = true;
            controller.View.bloomMidday = 1f;
            controller.View.time += 0.05f;
            controller.View.Apply();
            Assert.IsFalse(controller.View.IsBloomClosing(1), "reduced motion swaps, it does not fold");
            Assert.AreEqual(0, controller.View.ShownBloomCard(1));
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Day7_TilesReadOldestFirst()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.FixedStep();
            Keep(controller, "top3");
            Assert.IsTrue(controller.ShiftDay(1));
            Keep(controller, "top3");
            Assert.IsTrue(controller.ShiftDay(1));
            Assert.IsTrue(controller.ShiftDay(1));
            Keep(controller, "top3");
            Assert.IsTrue(controller.ShiftDay(1));
            Keep(controller, "top3");
            Assert.IsTrue(controller.ShiftDay(1));
            Assert.IsTrue(controller.ShiftDay(1));
            yield return null;

            var expect = new[]
            {
                TileState.Kept, TileState.Kept, TileState.Missed, TileState.Kept,
                TileState.Kept, TileState.Missed, TileState.Today
            };
            PlantState plant = Midday(controller);
            Assert.AreEqual(7, plant.Window.Length);
            for (int i = 0; i < 7; i++)
            {
                Assert.AreEqual(expect[i], plant.Window[i], "rules slot " + i);
                Assert.AreEqual(SundialArcs.TileDigit(expect[i]), controller.View.tiles[7 + i], "dial slot " + i);
                Assert.AreEqual(Label(expect[i]), TileLabels.For(expect[i]));
            }
            Assert.AreEqual(TileState.Kept, plant.Window[0]);
            Assert.AreEqual(TileState.Today, plant.Window[6]);
            Assert.AreEqual(TileLabels.Neutral, TileLabels.For((TileState)42));
            Assert.AreEqual(TileLabels.Neutral, TileLabels.For((TileState)42));
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Record_HonestTiles()
        {
            string dir = RunDir();
            Directory.CreateDirectory(dir);
            DisableAsyncShaders();

            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.FixedStep();
            for (int i = 0; i < 3; i++)
            {
                Keep(controller, "top3");
                Assert.IsTrue(controller.ShiftDay(1));
            }
            Assert.IsTrue(controller.ShiftDay(1));
            int grown = controller.Service.PlantFor(controller.Service.HabitForArc("midday")).LifetimeKept;
            Stage grownStage = controller.Service.PlantFor(controller.Service.HabitForArc("midday")).Stage;
            yield return SundialPlay.Seconds(1.70f);
            PlantState missed = Midday(controller);
            Assert.AreEqual(grown, missed.LifetimeKept);
            Assert.AreEqual(grownStage, missed.Stage);
            Assert.AreEqual(TileState.Missed, missed.Window[5]);
            Camera cam = PoseDial();
            AttachPlate(cam);
            Color32[] missedPx = Shot(cam, Path.Combine(dir, "missed.png"));

            TendResult filled = controller.Service.BackfillYesterday(controller.Service.HabitForArc("midday"));
            Assert.IsTrue(filled.Ok, filled.Reason);
            Assert.IsTrue(filled.Event.Late);
            yield return null;
            PlantState hatched = Midday(controller);
            Assert.AreEqual(TileState.Late, hatched.Window[5]);
            Assert.AreEqual(grownStage, hatched.Stage);
            Assert.IsFalse(controller.AskVisible("midday"));
            Color32[] backPx = Shot(cam, Path.Combine(dir, "backfilled.png"));

            yield return SundialPlay.Unload();
            var duskClock = new FixedClock(new DateTimeOffset(2026, 10, 3, 21, 30, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
            SundialController.SaveDirectoryOverride = SundialPlay.FreshDir();
            SundialController.ClockOverride = duskClock;
            yield return SundialPlay.LoadMain(c => controller = c);
            SundialPlay.FixedStep();
            for (int i = 0; i < 14; i++)
            {
                TendResult ritual = controller.Service.TendRitual("breaths");
                Assert.IsTrue(ritual.Ok, ritual.Reason);
                Assert.IsTrue(controller.ShiftDay(1));
            }
            Assert.IsTrue(controller.ShiftDay(1));
            yield return SundialPlay.Seconds(1.70f);
            HabitDef duskHabit = controller.Service.HabitForArc("winddown");
            PlantState dusk = controller.Service.PlantFor(duskHabit);
            Assert.AreEqual(Bloom.Open, dusk.Bloom);
            Assert.AreEqual(Stage.Full, dusk.Stage);
            int pale = 0;
            int keptTiles = 0;
            for (int i = 0; i < dusk.Window.Length; i++)
            {
                if (dusk.Window[i] == TileState.Missed) pale++;
                if (dusk.Window[i] == TileState.Kept || dusk.Window[i] == TileState.Late) keptTiles++;
            }
            Assert.AreEqual(1, pale);
            Assert.GreaterOrEqual(keptTiles, 5);
            Assert.AreEqual(1, controller.View.ShownBloomCard(2));
            cam = PoseDial();
            AttachPlate(cam);
            Color32[] dayPx = Shot(cam, Path.Combine(dir, "day7.png"));

            int width = Framings.DefaultWidth;
            int height = Framings.DefaultHeight;
            var sheet = new Color32[width * 3 * height];
            Blit(sheet, missedPx, width, height, 0, width * 3);
            Blit(sheet, backPx, width, height, width, width * 3);
            Blit(sheet, dayPx, width, height, width * 2, width * 3);
            FrameGrab.WritePixels(Path.Combine(dir, "record.png"), sheet, width * 3, height);
            Assert.IsTrue(File.Exists(Path.Combine(dir, "record.png")));
        }

        static bool ReadChip()
        {
            GameObject question = GameObject.Find("BackfillQuestion");
            GameObject yes = GameObject.Find(SundialController.PromptYesId);
            GameObject no = GameObject.Find(SundialController.PromptNoId);
            if (question == null || yes == null || no == null) return false;
            TextMesh questionMesh = question.GetComponent<TextMesh>();
            TextMesh yesMesh = yes.GetComponent<TextMesh>();
            TextMesh noMesh = no.GetComponent<TextMesh>();
            if (questionMesh == null || yesMesh == null || noMesh == null) return false;
            return questionMesh.text == SundialController.PromptQuestion
                && yesMesh.text == SundialController.PromptYesLine
                && noMesh.text == SundialController.PromptNoLine;
        }

        static void Keep(SundialController controller, string habitId)
        {
            controller.Service.Arm(habitId);
            TendEvent committed = controller.Service.Flush();
            Assert.IsNotNull(committed, habitId);
        }

        static PlantState Midday(SundialController controller)
        {
            return SundialPlay.Midday(controller);
        }

        static int LateCount(SundialController controller, string habitId)
        {
            int count = 0;
            var events = controller.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                TendEvent ev = events[i];
                if (ev != null && ev.HabitId == habitId && ev.Late && !ev.UndoneAtUtcMs.HasValue) count++;
            }
            return count;
        }

        static TendEvent LateEvent(SundialController controller, string habitId)
        {
            var events = controller.Ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                TendEvent ev = events[i];
                if (ev != null && ev.HabitId == habitId && ev.Late && !ev.UndoneAtUtcMs.HasValue) return ev;
            }
            Assert.Fail("no late event for " + habitId);
            return null;
        }

        static string Label(TileState state)
        {
            switch (state)
            {
                case TileState.Kept: return TileLabels.Kept;
                case TileState.Late: return TileLabels.Late;
                case TileState.Missed: return TileLabels.Missed;
                case TileState.Today: return TileLabels.Today;
                case TileState.Before: return TileLabels.Before;
                default: return TileLabels.Neutral;
            }
        }

        static string RunDir()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            return Path.Combine(root, "orchestration", "runs", "sundial", "T-SUN-010");
        }

        static Color32[] Shot(Camera cam, string path)
        {
            Texture2D warm = FrameGrab.RenderToTexture(cam, 320, 180, 1);
            UnityEngine.Object.Destroy(warm);
            Texture2D tex = FrameGrab.RenderToTexture(cam, Framings.DefaultWidth, Framings.DefaultHeight, 4);
            byte[] png = tex.EncodeToPNG();
            Color32[] pixels = tex.GetPixels32();
            File.WriteAllBytes(path, png);
            ImageStats stats = ImageCheck.Analyze(pixels, tex.width, tex.height, png);
            File.WriteAllText(path + ".check.json", ImageCheck.ToJson(stats));
            UnityEngine.Object.Destroy(tex);
            Assert.Less(stats.MagentaFrac, 0.001f, path + " magentaFrac " + stats.MagentaFrac);
            Assert.Greater(new FileInfo(path).Length, 1000, path);
            return pixels;
        }

        static void Blit(Color32[] dest, Color32[] src, int sw, int sh, int x0, int destW)
        {
            for (int y = 0; y < sh; y++)
            {
                int row = y * destW + x0;
                int srcRow = y * sw;
                for (int x = 0; x < sw; x++) dest[row + x] = src[srcRow + x];
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
            var mesh = new Mesh { name = "RecordPlate" };
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
