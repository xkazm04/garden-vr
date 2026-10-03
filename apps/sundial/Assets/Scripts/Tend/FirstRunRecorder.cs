using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using GardenVR.Capture;
using GardenVR.Input;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GardenVR.Sundial
{
    /// <summary>
    /// Records the canonical first run when GARDEN_FIRSTRUN_MP4 is set. Editor play mode only.
    /// Frames go to ffmpeg as raw RGBA so the 180 s take does not fill the disk with PNGs.
    /// </summary>
    public sealed class FirstRunRecorder : MonoBehaviour
    {
        public const int Fps = 24;
        public const int Width = 1824;
        public const int Height = 1024;

        Process _ffmpeg;
        Stream _stdin;
        Camera _cam;
        int _frames;
        int _warmup = 2;
        bool _closed;
        float _previousDelta;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplyOverrides()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GARDEN_FIRSTRUN_MP4"))) return;
            string dir = Environment.GetEnvironmentVariable("GARDEN_FIRSTRUN_SAVE");
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            SundialController.SaveDirectoryOverride = dir;
            SundialController.ClockOverride = new GardenVR.Core.FixedClock(
                new DateTimeOffset(2026, 10, 3, 14, 20, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
            SundialService.DevSeedOnFresh = false;
            SundialService.FreshReducedMotion = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!Application.isPlaying) return;
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GARDEN_FIRSTRUN_MP4"))) return;
            var go = new GameObject("FirstRunRecorder");
            go.AddComponent<FirstRunRecorder>();
        }

        void Awake()
        {
            _previousDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / Fps;
            string mp4 = Environment.GetEnvironmentVariable("GARDEN_FIRSTRUN_MP4");
            string scriptPath = Environment.GetEnvironmentVariable("GARDEN_FIRSTRUN_SCRIPT");
            if (string.IsNullOrEmpty(mp4) || string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
            {
                Debug.LogError("[FirstRun] record missing script or output");
                Application.Quit(1);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(mp4));
            StartEncoder(mp4);
            var keys = FindObjectsByType<KeyboardMouseIntentSource>(FindObjectsInactive.Include);
            for (int i = 0; i < keys.Length; i++) keys[i].enabled = false;
            SundialController controller = FindAnyObjectByType<SundialController>();
            if (controller == null)
            {
                Debug.LogError("[FirstRun] record has no controller");
                Application.Quit(1);
                return;
            }
            var host = new GameObject("FirstRunScript");
            var source = host.AddComponent<ScriptedIntentSource>();
            source.Load(File.ReadAllText(scriptPath));
            controller.SetSource(source);
            GameObject eye = GameObject.Find("EyeCamera");
            _cam = eye != null ? eye.GetComponent<Camera>() : Camera.main;
            if (_cam == null) Debug.LogError("[FirstRun] record has no EyeCamera");
        }

        void StartEncoder(string mp4)
        {
            string args = "-y -f rawvideo -pix_fmt rgba -s " + Width + "x" + Height
                + " -r " + Fps + " -i - -vf vflip -an -c:v libx264 -pix_fmt yuv420p -movflags +faststart \""
                + mp4 + "\"";
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            _ffmpeg = Process.Start(psi);
            _ffmpeg.BeginOutputReadLine();
            _ffmpeg.BeginErrorReadLine();
            _stdin = _ffmpeg.StandardInput.BaseStream;
            Debug.Log("[FirstRun] record ffmpeg pid=" + _ffmpeg.Id);
        }

        void LateUpdate()
        {
            if (_closed || _cam == null || _stdin == null) return;
            Texture2D tex = FrameGrab.RenderToTexture(_cam, Width, Height, 4);
            byte[] raw = tex.GetRawTextureData();
            Destroy(tex);
            if (_warmup > 0)
            {
                _warmup--;
                return;
            }
            if (raw == null || raw.Length != Width * Height * 4)
            {
                Debug.LogError("[FirstRun] record bad frame bytes " + (raw == null ? 0 : raw.Length));
                Close(1);
                return;
            }
            _stdin.Write(raw, 0, raw.Length);
            _frames++;
            if (_frames % 120 == 0)
                Debug.Log("[FirstRun] record frames=" + _frames + " t=" + (_frames / (float)Fps).ToString("0.0"));
            int goal = GoalFrames();
            if (_frames >= goal) Close(0);
        }

        static int GoalFrames()
        {
            int seconds = 180;
            string raw = Environment.GetEnvironmentVariable("GARDEN_FIRSTRUN_SECONDS");
            int parsed;
            if (!string.IsNullOrEmpty(raw) && int.TryParse(raw, out parsed) && parsed > 0)
                seconds = parsed;
            return seconds * Fps;
        }

        void Close(int code)
        {
            if (_closed) return;
            _closed = true;
            Time.captureDeltaTime = _previousDelta;
            try
            {
                if (_stdin != null) _stdin.Close();
            }
            catch (Exception e)
            {
                Debug.LogError("[FirstRun] record stdin " + e.Message);
            }
            if (_ffmpeg != null && !_ffmpeg.HasExited)
            {
                if (!_ffmpeg.WaitForExit(120000))
                {
                    Debug.LogError("[FirstRun] record ffmpeg timed out");
                    try { _ffmpeg.Kill(); } catch (Exception) { }
                }
            }
            Debug.Log("[FirstRun] record closed frames=" + _frames + " code=" + code);
            // Application.Quit is ignored in the editor, so play mode would spin forever.
            if (Application.isEditor)
                StopEditorPlay(code);
            else
                Application.Quit(code);
        }

        static void StopEditorPlay(int code)
        {
            Type prefs = Type.GetType("UnityEditor.EditorPrefs, UnityEditor");
            if (prefs != null)
            {
                MethodInfo setInt = prefs.GetMethod("SetInt", new[] { typeof(string), typeof(int) });
                if (setInt != null) setInt.Invoke(null, new object[] { "sundial.firstRun.recordCode", code });
            }
            Type editor = Type.GetType("UnityEditor.EditorApplication, UnityEditor");
            PropertyInfo playing = editor == null ? null : editor.GetProperty("isPlaying", BindingFlags.Static | BindingFlags.Public);
            if (playing != null) playing.SetValue(null, false, null);
            else Application.Quit(code);
        }

        void OnDestroy()
        {
            if (!_closed) Close(1);
        }
    }
}
