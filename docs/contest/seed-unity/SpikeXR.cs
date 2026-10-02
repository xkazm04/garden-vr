using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using Debug = UnityEngine.Debug;

namespace Terrarium.EditorTools
{
    /// <summary>
    /// XR side of the spike, batchmode-safe:
    ///   ConfigureXR   OpenXR loader for Standalone (the XR Simulator is a PC OpenXR runtime), hand tracking +
    ///                 hand interaction profile, and Meta XR Operator activated (its API layer registered in OpenXR settings)
    ///   BuildWindows  a Windows player the Simulator and Operator can drive with no editor open
    /// </summary>
    public static class SpikeXR
    {
        [MenuItem("Terrarium/Configure XR")]
        public static void ConfigureXR()
        {
            const BuildTargetGroup g = BuildTargetGroup.Standalone;
#pragma warning disable CS0618
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (perTarget == null)
            {
                Directory.CreateDirectory("Assets/XR");
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }
#pragma warning restore CS0618
            if (!perTarget.HasSettingsForBuildTarget(g)) perTarget.CreateDefaultSettingsForBuildTarget(g);
            if (!perTarget.HasManagerSettingsForBuildTarget(g)) perTarget.CreateDefaultManagerSettingsForBuildTarget(g);
            var gs = perTarget.SettingsForBuildTarget(g);
            gs.InitManagerOnStart = true;
            bool assigned = XRPackageMetadataStore.AssignLoader(gs.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", g);
            EditorUtility.SetDirty(perTarget); EditorUtility.SetDirty(gs); EditorUtility.SetDirty(gs.Manager);
            Debug.Log($"[SpikeXR] OpenXR loader assigned for Standalone: {assigned}; loaders now: {string.Join(", ", gs.Manager.activeLoaders.Select(l => l.GetType().Name))}");

            FeatureHelpers.RefreshFeatures(g);
            var oxr = OpenXRSettings.GetSettingsForBuildTargetGroup(g);
            if (oxr == null) { Debug.LogError("[SpikeXR] no OpenXRSettings for Standalone"); return; }
            foreach (var f in oxr.GetFeatures())
            {
                var n = f.GetType().FullName ?? "";
                if (n.EndsWith(".HandTracking") || n.EndsWith("HandInteractionProfile") || n.EndsWith("MetaHandTrackingAim") || n.EndsWith("ApiLayersFeature") || n == "Meta.XR.MetaXRFeature")
                    f.enabled = true;
                EditorUtility.SetDirty(f);
            }
            Debug.Log("[SpikeXR] OpenXR features (Standalone):\n" + string.Join("\n", oxr.GetFeatures().Select(f => $"  [{(f.enabled ? "x" : " ")}] {f.GetType().FullName}")));

            // Meta XR Operator: the SDK's own activator registers XR_APILAYER_METAX_operator into OpenXR's API Layers feature.
            var act = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Meta.XR.Editor.MetaXROperatorActivator")).FirstOrDefault(t => t != null);
            if (act == null) Debug.LogWarning("[SpikeXR] MetaXROperatorActivator type not found");
            else
            {
                act.GetMethod("Activate", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.Invoke(null, new object[] { "spike-batchmode" });
                var on = (bool)act.GetProperty("IsActivated", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null);
                Debug.Log($"[SpikeXR] Meta XR Operator activated: {on}");
            }
            var layers = oxr.GetFeature<ApiLayersFeature>();
            if (layers != null) Debug.Log($"[SpikeXR] ApiLayersFeature enabled={layers.enabled}; operator layer enabled={layers.apiLayers.IsEnabled("XR_APILAYER_METAX_operator", System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture)}");

            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.productName = "Breathing Terrarium Spike";
            AssetDatabase.SaveAssets();
        }

        // MetaXRFeature requests XR_METAX1_agentic_external_tool; without it RegisterAgenticTool returns ErrorFeatureUnsupported (spike run 1).
        public static void ConfigureAndBuild() { ConfigureXR(); BuildWindows(); }

        [MenuItem("Terrarium/Build Windows")]
        public static void BuildWindows()
        {
            var sw = Stopwatch.StartNew();
            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Build/Win64"));
            // Core v207's OVRDumpBuildInfo copies this file without overwrite: an IOException on every rebuild (spike run 3).
            var stale = Path.Combine(outDir, "RuntimeActionBindings.json");
            if (File.Exists(stale)) File.Delete(stale);
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Terrarium.unity" },
                locationPathName = Path.Combine(outDir, "Terrarium.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[SpikeXR] Build {s.result} in {sw.Elapsed.TotalSeconds:0.0}s, {s.totalSize / 1048576.0:0.0} MB, errors={s.totalErrors} warnings={s.totalWarnings} -> {opts.locationPathName}");
            if (s.result != BuildResult.Succeeded) EditorApplication.Exit(2);
        }
    }
}
