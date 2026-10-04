using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GardenVR.Terrarium.Editor
{
    /// <summary>
    /// Windows player for the owner. Batch: -executeMethod GardenVR.Terrarium.Editor.Build.Windows
    /// Output is apps/terrarium/Build/Terrarium.exe (git-ignored), with PLAY.md copied beside it.
    /// The player starts on the sprint look; L switches to look A (JarRitualController).
    /// </summary>
    public static class Build
    {
        public static void Windows()
        {
            string dir = "Build";
            Directory.CreateDirectory(dir);
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { SceneSetup.ScenePath },
                locationPathName = Path.Combine(dir, "Terrarium.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report != null && report.summary.result == BuildResult.Succeeded)
            {
                // The owner opens the folder, so the guide travels with the exe.
                if (File.Exists("PLAY.md")) File.Copy("PLAY.md", Path.Combine(dir, "PLAY.md"), true);
                Debug.Log("Build Succeeded");
            }
            else
                Debug.Log("Build Failed");
        }
    }
}
