using System;
using System.Collections.Generic;
using UnityEngine;

namespace GardenVR.Audio
{
    /// <summary>
    /// One row of <c>Assets/Audio/cues.json</c>. Field names match the manifest contract in
    /// <c>docs/audio/CHOICES.md</c>. A placeholder never plays, even if a path is filled in.
    /// </summary>
    [Serializable]
    public sealed class CueDefinition
    {
        public string id;
        public string[] clips;
        public string bus;
        public string space;
        public int priorityBand;
        public int maxConcurrent = 1;
        public float cooldownS;
        public float pitchJitter;
        public float gainDb;
        public bool loop;
        public bool placeholder;

        public bool Is3D
        {
            get { return space == "3d"; }
        }

        public bool MissingClip
        {
            get
            {
                if (placeholder) return true;
                if (clips == null || clips.Length == 0) return true;
                for (int i = 0; i < clips.Length; i++)
                {
                    if (!string.IsNullOrEmpty(clips[i])) return false;
                }
                return true;
            }
        }
    }

    /// <summary>
    /// Named cues for one app. Paths are relative to <c>Assets/Audio/</c>.
    /// </summary>
    [Serializable]
    public sealed class CueManifest
    {
        public string app;
        public int version;
        public CueDefinition[] cues;

        [NonSerialized] Dictionary<string, CueDefinition> _byId;

        public static CueManifest Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new ArgumentException("manifest is empty");
            CueManifest manifest = JsonUtility.FromJson<CueManifest>(json);
            if (manifest == null || manifest.cues == null) throw new ArgumentException("manifest did not parse");
            manifest.Bind();
            return manifest;
        }

        public void Bind()
        {
            _byId = new Dictionary<string, CueDefinition>(StringComparer.Ordinal);
            if (cues == null) return;
            for (int i = 0; i < cues.Length; i++)
            {
                CueDefinition cue = cues[i];
                if (cue == null || string.IsNullOrEmpty(cue.id)) continue;
                _byId[cue.id] = cue;
            }
        }

        public bool TryGet(string id, out CueDefinition cue)
        {
            if (_byId == null) Bind();
            if (id != null && _byId.TryGetValue(id, out cue)) return true;
            cue = null;
            return false;
        }

        public bool Contains(string id)
        {
            CueDefinition cue;
            return TryGet(id, out cue);
        }
    }
}
