using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GardenVR.Capture
{
    /// <summary>
    /// PlayMode driver. Sets Time.captureDeltaTime, runs until a predicate or a timeout, and writes a state JSON object.
    /// </summary>
    public sealed class PlaybackHarness
    {
        readonly float _previousDelta;
        public float Fps { get; }

        public PlaybackHarness(float fps)
        {
            if (fps <= 0f) throw new ArgumentOutOfRangeException("fps");
            Fps = fps;
            _previousDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / fps;
        }

        public void Restore()
        {
            Time.captureDeltaTime = _previousDelta;
        }

        public IEnumerator RunUntil(Func<bool> predicate, float timeoutSeconds)
        {
            if (predicate == null) throw new ArgumentNullException("predicate");
            if (timeoutSeconds < 0f) throw new ArgumentOutOfRangeException("timeoutSeconds");
            float elapsed = 0f;
            while (!predicate())
            {
                if (elapsed >= timeoutSeconds) yield break;
                yield return null;
                elapsed += Time.deltaTime;
            }
        }

        public void WriteState(string path, IReadOnlyDictionary<string, string> state)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path");
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, CaptureJson.StringMap(state));
        }
    }
}
