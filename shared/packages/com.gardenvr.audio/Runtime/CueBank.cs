using System;
using System.Collections.Generic;
using UnityEngine;

namespace GardenVR.Audio
{
    /// <summary>
    /// The cue manifest and every clip it names, as one serialized asset a built player can load.
    /// Lives at <c>Resources/GardenAudioBank</c>. The editor builds it from <c>Assets/Audio</c>
    /// (<c>CueBankBuilder</c>); the service reads it the same way in the editor and in a player.
    /// App code never touches it: it plays cue ids.
    /// </summary>
    public sealed class CueBank : ScriptableObject
    {
        public const string ResourceName = "GardenAudioBank";

        public TextAsset manifest;
        /// <summary>Clip paths relative to <c>Assets/Audio/</c>, as written in the manifest.</summary>
        public string[] clipPaths = new string[0];
        public AudioClip[] clips = new AudioClip[0];

        [NonSerialized] Dictionary<string, AudioClip> _byPath;

        public AudioClip Find(string relative)
        {
            if (string.IsNullOrEmpty(relative) || clipPaths == null || clips == null) return null;
            if (_byPath == null)
            {
                _byPath = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
                int n = Math.Min(clipPaths.Length, clips.Length);
                for (int i = 0; i < n; i++)
                {
                    if (clips[i] != null && !string.IsNullOrEmpty(clipPaths[i]))
                        _byPath[clipPaths[i].Replace((char)92, '/')] = clips[i];
                }
            }
            AudioClip clip;
            return _byPath.TryGetValue(relative.Replace((char)92, '/'), out clip) ? clip : null;
        }

        void OnEnable() { _byPath = null; }
    }
}
