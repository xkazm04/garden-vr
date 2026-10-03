using System;
using System.Collections;
using System.IO;
using System.Reflection;
using GardenVR.Capture;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GardenVR.Sundial.Tests.PlayMode
{
    /// <summary>
    /// Writes a 4 s tend sequence when GARDEN_SEQ_DIR is set. The acceptance run leaves it unset,
    /// so this test returns immediately and stays green.
    /// </summary>
    public class TendRecordTests
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
        public IEnumerator Record_TendSequence()
        {
            string dir = Environment.GetEnvironmentVariable("GARDEN_SEQ_DIR");
            if (string.IsNullOrEmpty(dir))
                yield break;

            Directory.CreateDirectory(dir);
            DisableAsyncShaders();
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            SundialPlay.Play(controller, SundialPlay.LookThenPinch("plant.midday", 0.40f, 0.45f));
            Camera cam = PoseCamera();

            var recorder = cam.gameObject.AddComponent<SequenceRecorder>();
            recorder.enabled = false;
            recorder.fps = 10;
            recorder.width = 912;
            recorder.height = 512;
            recorder.msaa = 4;
            recorder.outputDirectory = dir;
            recorder.enabled = true;

            yield return SundialPlay.Seconds(4.2f);
            recorder.enabled = false;

            int frames = Directory.GetFiles(dir, "f*.png").Length;
            Assert.GreaterOrEqual(frames, 36, "sequence is shorter than about 4 s at 10 fps");
        }

        static Camera PoseCamera()
        {
            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] == null) continue;
                string name = behaviours[i].GetType().Name;
                if (name == "SeatedRig" || name == "KeyboardMouseHeadPose")
                    behaviours[i].enabled = false;
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

        static void DisableAsyncShaders()
        {
            Type shaderUtil = Type.GetType("UnityEditor.ShaderUtil, UnityEditor");
            if (shaderUtil == null) return;
            PropertyInfo prop = shaderUtil.GetProperty("allowAsyncCompilation", BindingFlags.Static | BindingFlags.Public);
            if (prop != null) prop.SetValue(null, false);
        }
    }

    /// <summary>DialG1 is authored in DialRoot local space. Re-apply it before the sequence recorder renders.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class DialG1Framing : MonoBehaviour
    {
        public Transform Target;
        public Camera Cam;
        Framing _framing;

        public void Apply()
        {
            if (_framing == null) _framing = Framings.LoadApp().Get("DialG1");
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
}
