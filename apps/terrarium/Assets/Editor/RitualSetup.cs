#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace GardenVR.Terrarium.Editor
{
    /// <summary>
    /// Puts <see cref="JarRitualController"/> on the jar prefab. Idempotent.
    /// Batch: -executeMethod GardenVR.Terrarium.Editor.RitualSetup.Run
    /// </summary>
    [InitializeOnLoad]
    static class RitualBatchShaders
    {
        static RitualBatchShaders()
        {
            if (!Application.isBatchMode) return;
            try { ShaderUtil.allowAsyncCompilation = false; }
            catch (Exception e) { Debug.LogWarning("[Ritual] async shaders stay on: " + e.Message); }
        }
    }

    public static class RitualSetup
    {
        [MenuItem("Garden VR/Terrarium/Wire Ritual")]
        public static void Run()
        {
            try
            {
                var root = PrefabUtility.LoadPrefabContents(JarSetup.PrefabPath);
                try
                {
                    if (root.GetComponent<JarView>() == null)
                        throw new InvalidOperationException("Jar prefab has no JarView");
                    if (root.GetComponent<JarRitualController>() == null)
                        root.AddComponent<JarRitualController>();
                    PrefabUtility.SaveAsPrefabAsset(root, JarSetup.PrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                Debug.Log("[RitualSetup] done prefab=" + JarSetup.PrefabPath);
            }
            catch (Exception e)
            {
                Debug.LogError("[RitualSetup] FAIL " + e);
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
