using System;
using System.Collections;
using System.IO;
using System.Reflection;
using GardenVR.Capture;
using GardenVR.Core;
using GardenVR.Room;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GardenVR.Terrarium.Tests
{
    public class RitualRecordTests
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
        [Timeout(1200000)]
        public IEnumerator Record_SixBreathSequence()
        {
            string dir = Environment.GetEnvironmentVariable("GARDEN_SEQ_DIR");
            if (string.IsNullOrEmpty(dir))
                yield break;

            Directory.CreateDirectory(dir);
            string stillDir = Path.GetDirectoryName(dir);
            DisableAsyncShaders();
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            controller.Mark("Record");
            string jsonl = File.ReadAllText(Path.Combine(Application.dataPath, "Tests/Playback/six-breaths.jsonl"));
            RitualHarness.Play(controller, jsonl);
            Camera cam = PoseCamera(controller.transform);

            var recorder = cam.gameObject.AddComponent<SequenceRecorder>();
            recorder.enabled = false;
            recorder.fps = 24;
            recorder.width = Framings.DefaultWidth;
            recorder.height = Framings.DefaultHeight;
            recorder.msaa = 4;
            recorder.outputDirectory = dir;
            recorder.enabled = true;

            var watch = cam.gameObject.AddComponent<RitualFrameWatch>();
            watch.Dir = dir;
            watch.StillDir = stillDir;
            watch.Controller = controller;

            float app = 0f;
            while (app < 70f)
            {
                yield return null;
                app += Time.deltaTime;
                if (controller.Snapshot().Phase == BreathPhase.Complete && controller.AnswerTime > 2.6f)
                    break;
            }

            recorder.enabled = false;
            Assert.IsTrue(watch.SavedInhale, "mid-inhale still was not grabbed");
            Assert.IsTrue(watch.SavedFog, "exhale fog still was not grabbed");
            Assert.IsTrue(watch.SavedAnswer, "answer still was not grabbed");
            Assert.Greater(watch.Frame, 40 * 24, "sequence is shorter than 40 s at 24 fps");
            File.WriteAllText(Path.Combine(stillDir, "frames.txt"),
                "frames=" + (watch.Frame + 1) + "\n" +
                "mid-inhale=" + watch.InhaleFrame + "\n" +
                "exhale-fog=" + watch.FogFrame + "\n" +
                "answer=" + watch.AnswerFrame + "\n" +
                "appTime=" + app.ToString("0.###") + "\n");
        }

        static Camera PoseCamera(Transform jar)
        {
            SeatedRig rig = UnityEngine.Object.FindAnyObjectByType<SeatedRig>();
            if (rig != null) rig.enabled = false;
            GameObject eye = GameObject.Find("EyeCamera");
            Assert.IsNotNull(eye, "EyeCamera missing");
            Camera cam = eye.GetComponent<Camera>();
            Assert.IsNotNull(cam);
            var framing = cam.gameObject.GetComponent<RitualJarFraming>();
            if (framing == null) framing = cam.gameObject.AddComponent<RitualJarFraming>();
            framing.Target = jar;
            framing.Cam = cam;
            framing.Apply();
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 50f;
            cam.allowHDR = false;
            if (cam.GetComponent<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();
            return cam;
        }

        static void DisableAsyncShaders()
        {
            Type shaderUtil = Type.GetType("UnityEditor.ShaderUtil, UnityEditor");
            if (shaderUtil == null) return;
            PropertyInfo prop = shaderUtil.GetProperty("allowAsyncCompilation", BindingFlags.Static | BindingFlags.Public);
            if (prop != null) prop.SetValue(null, false);
        }
    }

    /// <summary>
    /// JarG1 is authored in the jar's local space. Re-apply it before the sequence recorder renders.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class RitualJarFraming : MonoBehaviour
    {
        public Transform Target;
        public Camera Cam;
        Framing _framing;

        public void Apply()
        {
            if (_framing == null) _framing = Framings.LoadApp().Get("JarG1");
            if (Cam == null) Cam = GetComponent<Camera>();
            if (Target == null || Cam == null || _framing == null) return;
            Vector3 eye = Target.TransformPoint(_framing.Eye);
            Vector3 look = Target.TransformPoint(_framing.LookAt);
            Vector3 forward = look - eye;
            if (forward.sqrMagnitude < 1e-8f) return;
            Cam.fieldOfView = _framing.Fov;
            Cam.transform.SetPositionAndRotation(
                eye,
                Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(-_framing.LensShift.y, _framing.LensShift.x, 0f));
        }

        void LateUpdate()
        {
            Apply();
        }
    }

    [DefaultExecutionOrder(500)]
    public sealed class RitualFrameWatch : MonoBehaviour
    {
        public string Dir;
        public string StillDir;
        public JarRitualController Controller;
        public int Frame = -1;
        public bool SavedInhale;
        public bool SavedFog;
        public bool SavedAnswer;
        public int InhaleFrame = -1;
        public int FogFrame = -1;
        public int AnswerFrame = -1;

        void LateUpdate()
        {
            Frame++;
            if (Controller == null || string.IsNullOrEmpty(Dir) || string.IsNullOrEmpty(StillDir)) return;
            string src = Path.Combine(Dir, SequenceRecorder.FrameFileName(Frame));
            if (!File.Exists(src)) return;
            TerrariumState state = Controller.Snapshot();
            if (!SavedInhale
                && state.Breaths == 2
                && state.Phase == BreathPhase.Inhaling
                && Controller.Session.PhaseTime >= 2.0f
                && Controller.Session.PhaseTime <= 2.6f)
            {
                Copy(src, "mid-inhale.png");
                SavedInhale = true;
                InhaleFrame = Frame;
            }
            if (!SavedFog && state.Breaths == 2 && state.Phase == BreathPhase.Exhaling && state.Fog > 0.5f)
            {
                Copy(src, "exhale-fog.png");
                SavedFog = true;
                FogFrame = Frame;
            }
            if (!SavedAnswer && state.Phase == BreathPhase.Complete && Controller.AnswerTime >= 1.3f && Controller.AnswerTime <= 1.9f)
            {
                Copy(src, "answer.png");
                SavedAnswer = true;
                AnswerFrame = Frame;
            }
        }

        void Copy(string src, string name)
        {
            File.Copy(src, Path.Combine(StillDir, name), true);
        }
    }
}
