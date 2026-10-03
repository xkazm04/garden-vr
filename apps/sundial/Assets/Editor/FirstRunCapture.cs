#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GardenVR.Sundial.Editor
{
    /// <summary>
    /// Plays the canonical first-run script at SeatedPOV and encodes first-run.mp4.
    /// Batch: -executeMethod GardenVR.Sundial.Editor.FirstRunCapture.Record
    /// Do not pass -quit. The recorder exits the editor when the file is closed.
    /// </summary>
    public static class FirstRunCapture
    {
        public const string PrefExit = "sundial.firstRun.recordExit";

        public static void Record()
        {
            string repo = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            string run = Path.Combine(repo, "orchestration", "runs", "sundial", "T-SUN-011");
            Directory.CreateDirectory(run);
            string mp4 = Path.Combine(run, "first-run.mp4");
            string script = Path.Combine(Application.dataPath, "Tests", "Playback", "first-run.jsonl");
            string save = Path.Combine(Path.GetTempPath(), "sundial-firstrun-record");
            if (Directory.Exists(save)) Directory.Delete(save, true);
            Directory.CreateDirectory(save);
            Environment.SetEnvironmentVariable("GARDEN_FIRSTRUN_MP4", mp4);
            Environment.SetEnvironmentVariable("GARDEN_FIRSTRUN_SCRIPT", script);
            Environment.SetEnvironmentVariable("GARDEN_FIRSTRUN_SAVE", save);
            EditorPrefs.SetBool(PrefExit, true);
            ShaderUtil.allowAsyncCompilation = false;
            EditorSceneManager.OpenScene(SceneSetup.ScenePath);
            Debug.Log("[FirstRun] record enter " + mp4);
            EditorApplication.isPlaying = true;
        }
    }

    [InitializeOnLoad]
    static class FirstRunRecordExit
    {
        static FirstRunRecordExit()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(FirstRunCapture.PrefExit, false))
                ShaderUtil.allowAsyncCompilation = false;
            if (state == PlayModeStateChange.EnteredEditMode && EditorPrefs.GetBool(FirstRunCapture.PrefExit, false))
            {
                EditorPrefs.SetBool(FirstRunCapture.PrefExit, false);
                int code = EditorPrefs.GetInt("sundial.firstRun.recordCode", 0);
                EditorPrefs.DeleteKey("sundial.firstRun.recordCode");
                EditorApplication.Exit(code);
            }
        }
    }
}
#endif
