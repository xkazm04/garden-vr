#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GardenVR.Sundial.Editor
{
    /// <summary>
    /// Windows player for the owner. Batch: -executeMethod GardenVR.Sundial.Editor.Build.Windows
    /// Writes apps/sundial/Builds/sundial/Sundial.exe, which git ignores.
    /// </summary>
    public static class Build
    {
        public static void Windows()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "sundial"));
            Directory.CreateDirectory(folder);
            string exe = Path.Combine(folder, "Sundial.exe");
            bool splash = PlayerSettings.SplashScreen.show;
            bool background = PlayerSettings.runInBackground;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.runInBackground = true;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { SceneSetup.ScenePath },
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            PlayerSettings.SplashScreen.show = splash;
            PlayerSettings.runInBackground = background;
            BuildResult result = report.summary.result;
            if (result == BuildResult.Succeeded)
            {
                Debug.Log("Build Succeeded " + exe);
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.Log("Build Failed " + result);
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
