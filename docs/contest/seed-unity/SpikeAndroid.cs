using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using Debug = UnityEngine.Debug;

namespace Terrarium.EditorTools
{
    /// <summary>
    /// Quest APK from the same scene: OpenXR + Meta Quest feature + hand tracking, IL2CPP ARM64, Vulkan, Linear.
    /// Builds only; installing to a headset is a separate, explicit step (adb install) the spike did not do.
    /// Run with -buildTarget Android so the editor starts on the right platform.
    /// </summary>
    public static class SpikeAndroid
    {
        public static void BuildQuestApk() => Build(new[] { "Assets/Scenes/Terrarium.unity" }, "Terrarium-spike.apk", "com.habitgarden.terrarium.spike");
        public static void BuildFidelityApk() => Build(new[] { "Assets/Fidelity/HeroGallery.unity" }, "HabitGarden-fidelity.apk", "com.habitgarden.fidelity");

        static void Build(string[] scenes, string apkName, string appId)
        {
            var sw = Stopwatch.StartNew();
            const BuildTargetGroup g = BuildTargetGroup.Android;
            // The Hub's Android module came without its OpenJDK on this machine ("JDK not found"); point at a JDK 17.
            var jdk = System.Environment.GetEnvironmentVariable("SPIKE_JDK");
            if (!string.IsNullOrEmpty(jdk))
            {
                EditorPrefs.SetBool("JdkUseEmbedded", false);
                EditorPrefs.SetString("JdkPath", jdk);
                var t = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.Android.AndroidExternalToolsSettings")).FirstOrDefault(x => x != null);
                t?.GetProperty("jdkRootPath")?.SetValue(null, jdk);
                Debug.Log($"[SpikeAndroid] JDK -> {jdk} (settings type found: {t != null})");
            }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(g, BuildTarget.Android);

#pragma warning disable CS0618
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
#pragma warning restore CS0618
            if (!perTarget.HasSettingsForBuildTarget(g)) perTarget.CreateDefaultSettingsForBuildTarget(g);
            if (!perTarget.HasManagerSettingsForBuildTarget(g)) perTarget.CreateDefaultManagerSettingsForBuildTarget(g);
            var gs = perTarget.SettingsForBuildTarget(g);
            gs.InitManagerOnStart = true;
            XRPackageMetadataStore.AssignLoader(gs.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", g);
            EditorUtility.SetDirty(perTarget); EditorUtility.SetDirty(gs); EditorUtility.SetDirty(gs.Manager);

            FeatureHelpers.RefreshFeatures(g);
            var oxr = OpenXRSettings.GetSettingsForBuildTargetGroup(g);
            foreach (var f in oxr.GetFeatures())
            {
                var n = f.GetType().FullName ?? "";
                if (n.EndsWith("MetaQuestFeature") || n.EndsWith(".HandTracking") || n.EndsWith("HandInteractionProfile") ||
                    n.EndsWith("MetaHandTrackingAim") || n == "Meta.XR.MetaXRFeature")
                { f.enabled = true; EditorUtility.SetDirty(f); }
            }
            Debug.Log("[SpikeAndroid] OpenXR features (Android) enabled: " + string.Join(", ", oxr.GetFeatures().Where(f => f.enabled).Select(f => f.GetType().Name)));

            var nbt = NamedBuildTarget.Android;
            PlayerSettings.SetScriptingBackend(nbt, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.SetApplicationIdentifier(nbt, appId);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            // hand tracking + passthrough in the Meta project config, by reflection (the round-2 APK had no hand-tracking permission)
            var cfgT = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("OVRProjectConfig")).FirstOrDefault(x => x != null);
            var cfg = cfgT?.GetProperty("CachedProjectConfig")?.GetValue(null);
            if (cfg != null)
            {
                void SetEnum(string field, string val) { var f = cfgT.GetField(field); if (f != null) f.SetValue(cfg, System.Enum.Parse(f.FieldType, val)); Debug.Log($"[SpikeAndroid] OVRProjectConfig.{field} = {(f != null ? f.GetValue(cfg) : "(no field)")}"); }
                SetEnum("handTrackingSupport", "ControllersAndHands");
                var pp = cfgT.GetProperty("insightPassthroughSupport");
                if (pp != null) { pp.SetValue(cfg, System.Enum.Parse(pp.PropertyType, "Required")); Debug.Log("[SpikeAndroid] OVRProjectConfig.insightPassthroughSupport = " + pp.GetValue(cfg)); }
                cfgT.GetMethod("CommitProjectConfig")?.Invoke(null, new[] { cfg });
            }
            else Debug.LogWarning("[SpikeAndroid] OVRProjectConfig not found; hand-tracking permission not set");
            AssetDatabase.SaveAssets();

            var outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Build/Android/" + apkName));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            var s = report.summary;
            Debug.Log($"[SpikeAndroid] APK {s.result} in {sw.Elapsed.TotalSeconds:0.0}s, {s.totalSize / 1048576.0:0.0} MB, errors={s.totalErrors} -> {outPath}");
            if (s.result != BuildResult.Succeeded)
            {
                foreach (var step in report.steps)
                    foreach (var m in step.messages.Where(m => m.type == LogType.Error || m.type == LogType.Exception))
                        Debug.Log("[SpikeAndroid] build error: " + m.content);
                EditorApplication.Exit(2);
            }
        }
    }
}
