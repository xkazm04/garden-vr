using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using GardenVR.Capture;
using GardenVR.Core;
using GardenVR.Room;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GardenVR.Terrarium.Tests
{
    public class FirstRunPlaybackTests
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
        [Timeout(600000)]
        public IEnumerator FirstRun_SixBreaths()
        {
            string jsonl = File.ReadAllText(Path.Combine(Application.dataPath, "Tests/Playback/first-run.jsonl"));
            RitualHarness.FixedStep();
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            for (int i = 0; i < 8 && controller.Director == null; i++)
                yield return null;
            FirstRunDirector director = controller.Director;
            Assert.IsNotNull(director);
            Assert.IsTrue(director.Running);
            Assert.AreEqual(LoadOutcome.Fresh, controller.Service.Outcome);
            RitualHarness.Play(controller, jsonl);

            bool sawWords = false;
            bool sawGhost = false;
            bool ghostLeft = false;
            float answerAt = -1f;
            float app = 0f;
            while (app < 180f)
            {
                yield return null;
                app += Time.deltaTime;
                if (!sawWords && director.WordsVisible && controller.Session.Breaths == 0
                    && controller.AppTime >= 8f && controller.AppTime < 9.4f
                    && director.WordsText != null && director.WordsText.Contains("Hold to breathe in."))
                    sawWords = true;
                if (!sawGhost && director.GhostVisible && controller.Session.Breaths == 0 && controller.AppTime >= 8f)
                    sawGhost = true;
                if (controller.Session.Breaths >= 1 && !director.GhostVisible)
                    ghostLeft = true;
                if (answerAt < 0f && controller.HasAnswer)
                    answerAt = controller.AppTime;
                if (controller.Service.ActiveHabits().Count >= 2 && director.Step == FirstRunSteps.Done)
                    break;
            }

            Assert.IsTrue(sawWords, "the hold line was not up before the first breath");
            Assert.IsTrue(director.WordsText == null || director.WordsText.IndexOf('\u2014') < 0);
            Assert.IsTrue(sawGhost, "the ghost hand was not beside the jar");
            Assert.IsTrue(ghostLeft, "the ghost hand stayed after the first counted breath");
            Assert.IsTrue(director.ArrivalMoved, "the jar did not drop in");
            Assert.GreaterOrEqual(answerAt, 0f, "the garden did not answer");
            Assert.LessOrEqual(answerAt, 180f);
            Assert.AreEqual(6, controller.Session.Breaths);
            Assert.AreEqual(2, controller.Service.ActiveHabits().Count);
            Assert.AreEqual("walk", controller.Service.ActiveHabits()[0].PresetKey);
            Assert.AreEqual("water", controller.Service.ActiveHabits()[1].PresetKey);
            Assert.AreEqual(FirstRunSteps.Done, director.Step);
            Assert.AreEqual(FirstRunSteps.Done, controller.Service.Document.FirstRunStep);
            Assert.IsTrue(controller.VoiceShellVisible, "the voice shell was not offered");
            Debug.Log("answerAt=" + answerAt.ToString("0.000", CultureInfo.InvariantCulture));
            Debug.Log("[FirstRun] timeline " + director.TimelineLog());
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator FirstRun_ResumeAfterQuit()
        {
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            RitualHarness.Play(controller, TwoThenHold());
            RitualHarness.FixedStep();

            bool quit = false;
            float app = 0f;
            while (app < 20f)
            {
                yield return null;
                app += Time.deltaTime;
                if (controller.Session.Breaths >= 2 && controller.Session.Phase == BreathPhase.Inhaling)
                {
                    quit = true;
                    break;
                }
            }
            Assert.IsTrue(quit, "breath 3 did not start");
            Assert.AreEqual(FirstRunSteps.Breathe, controller.Service.Document.FirstRunStep);
            Assert.IsTrue(File.Exists(Path.Combine(GardenService.DirectoryOverride, "save.json")));

            yield return RitualHarness.OpenMain();
            controller = RitualHarness.Controller();
            for (int i = 0; i < 6; i++) yield return null;

            FirstRunDirector director = controller.Director;
            Assert.IsTrue(director.Running);
            Assert.AreEqual(LoadOutcome.Loaded, controller.Service.Outcome);
            Assert.AreEqual(FirstRunSteps.Breathe, director.Step);
            Assert.AreEqual(FirstRunSteps.Breathe, controller.Service.Document.FirstRunStep);
            Assert.IsFalse(director.ArrivalMoved, "the jar played its arrival again");
            Assert.Less(director.JarLift, 0.001f);
            Assert.AreEqual(0, controller.CueCount("jar.land"));
            Assert.AreEqual(0, controller.CueCount("jar.lid"));
            Assert.IsTrue(controller.AwaitingContinue, "the ritual was not offered back");
            GameObject prompt = GameObject.Find("ContinuePrompt");
            Assert.IsNotNull(prompt);
            Assert.IsTrue(prompt.activeInHierarchy);
            Assert.AreEqual("Continue breathing?", EtchedLettering.Read(prompt));
            Assert.IsFalse(controller.RestorePromptVisible);

            int breaths = controller.Session.Breaths;
            float until = controller.AppTime + 2f;
            while (controller.AppTime < until) yield return null;
            Assert.AreEqual(breaths, controller.Session.Breaths, "the ritual restarted itself");
            Assert.AreEqual(BreathPhase.Waiting, controller.Session.Phase);
            Assert.Less(director.JarLift, 0.001f);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator FirstRun_NotOnFailedLoad()
        {
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            controller.Service.Save();
            controller.Service.Save();
            string dir = GardenService.DirectoryOverride;
            string live = Path.Combine(dir, SaveStore<TerrariumSave>.LiveName);
            Assert.IsTrue(File.Exists(Path.Combine(dir, SaveStore<TerrariumSave>.Prev1Name)));
            File.WriteAllBytes(live, new byte[0]);

            yield return RitualHarness.OpenMain();
            controller = RitualHarness.Controller();
            for (int i = 0; i < 8; i++) yield return null;

            Assert.AreEqual(LoadOutcome.Failed, controller.Service.Outcome);
            Assert.IsFalse(controller.FirstRunStarted);
            Assert.IsNotNull(controller.Director);
            Assert.IsFalse(controller.Director.Running);
            Assert.IsTrue(controller.RestorePromptVisible);
            Assert.AreEqual(0, controller.Updates);
            Assert.AreEqual(0, controller.CueCount("jar.land"));
            Assert.IsNull(GameObject.Find("EtchedWords"));
            Assert.IsNull(GameObject.Find("GhostHand"));
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator FirstRun_IdleHintReturns()
        {
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            RitualHarness.FixedStep();
            float app = 0f;
            while (app < 12f && (controller.Director == null || !controller.Director.WordsVisible))
            {
                yield return null;
                app += Time.deltaTime;
            }
            Assert.IsTrue(controller.Director.WordsVisible, "words did not appear");
            StringAssert.Contains("Hold to breathe in.", controller.Director.WordsText);
            StringAssert.Contains(controller.HoldBinding, controller.Director.WordsText);

            RitualHarness.Play(controller, TwoBreaths());
            app = 0f;
            bool faded = false;
            while (app < 16f)
            {
                yield return null;
                app += Time.deltaTime;
                if (controller.Session.Breaths >= 2 && controller.Director.WordsAlpha < 0.05f)
                {
                    faded = true;
                    break;
                }
            }
            Assert.IsTrue(faded, "words did not fade after breath 2");
            Assert.IsFalse(controller.Director.GhostVisible);

            app = 0f;
            bool back = false;
            while (app < 10f)
            {
                yield return null;
                app += Time.deltaTime;
                string text = controller.Director.WordsText;
                if (controller.Director.WordsVisible && text != null
                    && text.Contains("Hold to breathe in.") && text.Contains("and let go."))
                {
                    back = true;
                    break;
                }
            }
            Assert.IsTrue(back, "the etched hint did not return after 8 s idle");
            Assert.GreaterOrEqual(app, 7f);
            Assert.Less(app, 9.5f);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator FirstRun_ReducedMotion_EndStates()
        {
            GardenService.ReducedMotionOverride = true;
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            RitualHarness.FixedStep();
            Assert.IsTrue(controller.Service.Settings.ReducedMotion);
            yield return null;
            yield return null;

            FirstRunDirector director = controller.Director;
            Assert.IsTrue(director.Running);
            Assert.Less(director.JarLift, 0.001f);
            PcRoomPlate plate = UnityEngine.Object.FindAnyObjectByType<PcRoomPlate>();
            Assert.IsNotNull(plate);
            Assert.Greater(plate.CurrentExposure, 0.85f, "the room was still fading");
            PcDeskAnchor desk = UnityEngine.Object.FindAnyObjectByType<PcDeskAnchor>();
            Assert.IsNotNull(desk);
            Assert.IsFalse(desk.enabled, "the desk trace was still running");
            Assert.AreEqual(4f, (float)controller.Service.Settings.InhaleSec, 0.001f);

            float maxLift = director.JarLift;
            float app = 0f;
            while (app < 5f)
            {
                yield return null;
                app += Time.deltaTime;
                if (director.JarLift > maxLift) maxLift = director.JarLift;
            }
            Assert.Less(maxLift, 0.001f, "reduced motion still dropped the jar");
            Assert.IsFalse(director.ArrivalMoved);

            RitualHarness.Play(controller, ShortJourney());
            float answerAt = -1f;
            app = 0f;
            while (app < 50f)
            {
                yield return null;
                app += Time.deltaTime;
                if (answerAt < 0f && controller.HasAnswer) answerAt = controller.AppTime;
                if (controller.Service.ActiveHabits().Count >= 2 && director.Step == FirstRunSteps.Done)
                    break;
            }

            Assert.Greater(answerAt, 20f, "breath timing collapsed");
            Assert.Less(answerAt, 40f);
            Assert.AreEqual(6, controller.Session.Breaths);
            Assert.AreEqual(FirstRunSteps.Done, director.Step);
            Assert.AreEqual(2, controller.Service.ActiveHabits().Count);
            Assert.IsTrue(controller.VoiceShellVisible);
            Assert.Less(director.JarLift, 0.001f);
            GameObject packet = GameObject.Find("Packet-read");
            Assert.IsNotNull(packet, "a seed packet was not waiting in place");
            Vector3 at = packet.transform.localPosition;
            yield return null;
            yield return null;
            yield return null;
            Assert.Less((packet.transform.localPosition - at).magnitude, 0.0001f);
        }

        /// <summary>Canonical 0-100 s seated capture. Quiet unless GARDEN_SEQ_DIR is set.</summary>
        [UnityTest]
        [Timeout(3600000)]
        public IEnumerator Record_FirstRun()
        {
            string dir = Environment.GetEnvironmentVariable("GARDEN_SEQ_DIR");
            if (string.IsNullOrEmpty(dir))
                yield break;

            Directory.CreateDirectory(dir);
            DisableAsyncShaders();
            string jsonl = File.ReadAllText(Path.Combine(Application.dataPath, "Tests/Playback/first-run.jsonl"));
            RitualHarness.FixedStep();
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            for (int i = 0; i < 8 && controller.Director == null; i++)
                yield return null;
            Assert.IsNotNull(controller.Director);
            RitualHarness.Play(controller, jsonl);
            Camera cam = SeatedCamera();
            var recorder = cam.gameObject.AddComponent<SequenceRecorder>();
            recorder.enabled = false;
            recorder.fps = 24;
            recorder.width = Framings.DefaultWidth;
            recorder.height = Framings.DefaultHeight;
            recorder.msaa = 1;
            recorder.outputDirectory = dir;
            recorder.enabled = true;

            float app = 0f;
            while (app < 100f)
            {
                yield return null;
                app += Time.deltaTime;
            }
            recorder.enabled = false;
            string stillDir = Path.GetDirectoryName(dir);
            File.WriteAllText(Path.Combine(stillDir, "frames.txt"),
                "appTime=" + app.ToString("0.000", CultureInfo.InvariantCulture) + "\n" +
                "step=" + controller.Director.Step + "\n" +
                controller.Director.TimelineLog() + "\n");
            Assert.Greater(app, 99f);
        }

        static Camera SeatedCamera()
        {
            GameObject eye = GameObject.Find("EyeCamera");
            Assert.IsNotNull(eye, "EyeCamera missing");
            Camera cam = eye.GetComponent<Camera>();
            Assert.IsNotNull(cam);
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 50f;
            cam.allowHDR = false;
            return cam;
        }

        static void DisableAsyncShaders()
        {
            Type shaderUtil = Type.GetType("UnityEditor.ShaderUtil, UnityEditor");
            if (shaderUtil == null) return;
            PropertyInfo prop = shaderUtil.GetProperty("allowAsyncCompilation", BindingFlags.Static | BindingFlags.Public);
            if (prop != null) prop.SetValue(null, false);
        }

        static string TwoThenHold()
        {
            return "{\"t\":0,\"intent\":\"PinchHold\",\"target\":\"jar\",\"dur\":1.5}\n"
                + "{\"t\":1.5,\"intent\":\"Release\",\"target\":\"jar\"}\n"
                + "{\"t\":2.8,\"intent\":\"PinchHold\",\"target\":\"jar\",\"dur\":1.5}\n"
                + "{\"t\":4.3,\"intent\":\"Release\",\"target\":\"jar\"}\n"
                + "{\"t\":5.6,\"intent\":\"PinchHold\",\"target\":\"jar\",\"dur\":4}\n"
                + "{\"t\":5.6,\"intent\":\"Look\",\"target\":\"jar\",\"dur\":6}\n";
        }

        static string TwoBreaths()
        {
            return "{\"t\":0,\"intent\":\"PinchHold\",\"target\":\"jar\",\"dur\":1.5}\n"
                + "{\"t\":1.5,\"intent\":\"Release\",\"target\":\"jar\"}\n"
                + "{\"t\":2.8,\"intent\":\"PinchHold\",\"target\":\"jar\",\"dur\":1.5}\n"
                + "{\"t\":4.3,\"intent\":\"Release\",\"target\":\"jar\"}\n"
                + "{\"t\":4.3,\"intent\":\"Look\",\"target\":\"jar\",\"dur\":1.4}\n";
        }

        static string ShortJourney()
        {
            var sb = new StringBuilder();
            sb.Append("{\"t\":0.2,\"intent\":\"Look\",\"target\":\"jar\",\"dur\":8.5}\n");
            float t = 9f;
            float lastRelease = 0f;
            for (int i = 0; i < 6; i++)
            {
                sb.Append("{\"t\":").Append(t.ToString("0.###", CultureInfo.InvariantCulture));
                sb.Append(",\"intent\":\"PinchHold\",\"target\":\"jar\",\"dur\":1.5}\n");
                t += 1.5f;
                lastRelease = t;
                sb.Append("{\"t\":").Append(t.ToString("0.###", CultureInfo.InvariantCulture));
                sb.Append(",\"intent\":\"Release\",\"target\":\"jar\"}\n");
                t += 1.3f;
            }
            sb.Append("{\"t\":").Append(lastRelease.ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append(",\"intent\":\"Look\",\"target\":\"jar\",\"dur\":12}\n");
            sb.Append("{\"t\":32,\"intent\":\"Poke\",\"target\":\"seed.walk\"}\n");
            sb.Append("{\"t\":33.5,\"intent\":\"Poke\",\"target\":\"seed.water\"}\n");
            return sb.ToString();
        }
    }
}
