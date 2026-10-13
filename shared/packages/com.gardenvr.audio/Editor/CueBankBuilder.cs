using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GardenVR.Audio.Editor
{
    /// <summary>
    /// Writes <c>Assets/Audio/Resources/GardenAudioBank.asset</c> from <c>Assets/Audio/cues.json</c>: the manifest
    /// as a TextAsset plus every clip it names. A player has no <c>Assets/Audio</c> folder and no AssetDatabase,
    /// so this asset is how it finds its cues. Rebuilt on a build, on any change under <c>Assets/Audio</c>,
    /// and from the menu. Nothing is written when the content already matches.
    /// </summary>
    public static class CueBankBuilder
    {
        public const string AudioRoot = "Assets/Audio/";
        public const string ManifestPath = "Assets/Audio/cues.json";
        public const string BankPath = "Assets/Audio/Resources/" + CueBank.ResourceName + ".asset";

        [MenuItem("Garden VR/Audio/Rebuild Cue Bank")]
        public static void Rebuild() { Build(); }

        /// <summary>Returns true when the asset was written.</summary>
        public static bool Build()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestPath);
            if (text == null) return false;

            CueManifest manifest;
            try { manifest = CueManifest.Parse(text.text); }
            catch (System.ArgumentException e)
            {
                Debug.LogWarning("cue bank: " + ManifestPath + " does not parse: " + e.Message);
                return false;
            }

            var paths = new List<string>();
            var clips = new List<AudioClip>();
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (CueDefinition cue in manifest.cues)
            {
                if (cue == null || cue.placeholder || cue.clips == null) continue;
                foreach (string rel in cue.clips)
                {
                    if (string.IsNullOrEmpty(rel) || !seen.Add(rel)) continue;
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioRoot + rel.Replace((char)92, '/'));
                    if (clip == null) continue; // a missing file stays a PLACEHOLDER, as before
                    paths.Add(rel);
                    clips.Add(clip);
                }
            }

            var bank = AssetDatabase.LoadAssetAtPath<CueBank>(BankPath);
            if (bank != null && bank.manifest == text && Same(bank.clipPaths, paths) && Same(bank.clips, clips))
                return false;

            bool created = bank == null;
            if (created)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(BankPath));
                bank = ScriptableObject.CreateInstance<CueBank>();
            }
            bank.manifest = text;
            bank.clipPaths = paths.ToArray();
            bank.clips = clips.ToArray();
            if (created) AssetDatabase.CreateAsset(bank, BankPath);
            else EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssetIfDirty(bank);
            return true;
        }

        static bool Same<T>(T[] a, List<T> b)
        {
            if (a == null || a.Length != b.Count) return false;
            var cmp = EqualityComparer<T>.Default;
            for (int i = 0; i < a.Length; i++)
            {
                if (!cmp.Equals(a[i], b[i])) return false;
            }
            return true;
        }
    }

    sealed class CueBankBuildStep : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return -100; } }
        public void OnPreprocessBuild(BuildReport report) { CueBankBuilder.Build(); }
    }

    sealed class CueBankWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!Touches(imported) && !Touches(deleted) && !Touches(moved)) return;
            EditorApplication.delayCall += () => CueBankBuilder.Build();
        }

        static bool Touches(string[] paths)
        {
            foreach (string p in paths)
            {
                if (p.StartsWith(CueBankBuilder.AudioRoot) && !p.StartsWith(CueBankBuilder.AudioRoot + "Resources/")
                    && !p.EndsWith(".cs") && !p.EndsWith(".meta"))
                    return true;
            }
            return false;
        }
    }
}
