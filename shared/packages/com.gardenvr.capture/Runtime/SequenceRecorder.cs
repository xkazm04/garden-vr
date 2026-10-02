using System;
using System.IO;
using UnityEngine;

namespace GardenVR.Capture
{
    /// <summary>
    /// PlayMode recorder. Writes f0000.png, f0001.png, ... at a fixed capture delta.
    /// Directory is <see cref="outputDirectory"/> or the env var GARDEN_SEQ_DIR.
    /// Start() renders one discarded frame so the first saved frame is not a shader placeholder.
    /// Callers should set ShaderUtil.allowAsyncCompilation = false before entering play mode.
    /// </summary>
    public sealed class SequenceRecorder : MonoBehaviour
    {
        public int fps = 30;
        public int width = 1824;
        public int height = 1024;
        public int msaa = 4;
        public string outputDirectory;

        float _previousDelta;
        bool _ownsDelta;
        Camera _cam;
        string _dir;
        int _index;

        public static string FrameFileName(int index)
        {
            return "f" + index.ToString("0000") + ".png";
        }

        void OnEnable()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = Camera.main;
            _dir = string.IsNullOrEmpty(outputDirectory) ? Environment.GetEnvironmentVariable("GARDEN_SEQ_DIR") : outputDirectory;
            if (fps > 0)
            {
                _previousDelta = Time.captureDeltaTime;
                Time.captureDeltaTime = 1f / fps;
                _ownsDelta = true;
            }
        }

        void Start()
        {
            if (_cam != null && !string.IsNullOrEmpty(_dir))
            {
                Texture2D warm = FrameGrab.RenderToTexture(_cam, width, height, msaa < 1 ? 1 : msaa);
                Destroy(warm);
            }
        }

        void OnDisable()
        {
            if (_ownsDelta) Time.captureDeltaTime = _previousDelta;
            _ownsDelta = false;
        }

        void LateUpdate()
        {
            if (string.IsNullOrEmpty(_dir) || _cam == null) return;
            Directory.CreateDirectory(_dir);
            int samples = msaa < 1 ? 1 : msaa;
            Texture2D tex = FrameGrab.RenderToTexture(_cam, width, height, samples);
            byte[] png = tex.EncodeToPNG();
            Destroy(tex);
            File.WriteAllBytes(Path.Combine(_dir, FrameFileName(_index)), png);
            _index++;
        }
    }
}
