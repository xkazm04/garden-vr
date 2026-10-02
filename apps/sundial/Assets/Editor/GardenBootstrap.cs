#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GardenVR.EditorTools
{
    /// <summary>One-time project setup, safe to re-run: URP asset + renderer, Linear colour, and a seated PC rig scene.
    /// Run: Unity -batchmode -quit -projectPath apps/APP -executeMethod GardenVR.EditorTools.GardenBootstrap.Run</summary>
    public static class GardenBootstrap
    {
        public static void Run()
        {
            Directory.CreateDirectory("Assets/Settings");
            var rendererPath = "Assets/Settings/GardenRenderer.asset";
            var pipelinePath = "Assets/Settings/GardenURP.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null) { renderer = ScriptableObject.CreateInstance<UniversalRendererData>(); AssetDatabase.CreateAsset(renderer, rendererPath); }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null) { pipeline = UniversalRenderPipelineAsset.Create(renderer); AssetDatabase.CreateAsset(pipeline, pipelinePath); }
            pipeline.msaaSampleCount = 4;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = pipeline; }
            PlayerSettings.colorSpace = ColorSpace.Linear;

            Directory.CreateDirectory("Assets/Scenes");
            var scenePath = "Assets/Scenes/Main.unity";
            if (!File.Exists(scenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var rig = new GameObject("SeatedRig");
                var cam = new GameObject("EyeCamera", typeof(Camera), typeof(AudioListener));
                cam.transform.SetParent(rig.transform, false);
                cam.transform.localPosition = new Vector3(0f, 1.15f, 0f); // seated adult eye height (m)
                cam.transform.localRotation = Quaternion.Euler(28f, 0f, 0f); // looking down at a desk ~0.5 m ahead
                cam.GetComponent<Camera>().fieldOfView = 60f;
                cam.GetComponent<Camera>().nearClipPlane = 0.02f;
                var light = new GameObject("KeyLight", typeof(Light));
                light.GetComponent<Light>().type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                var desk = new GameObject("DeskAnchor"); // apps place the hero object here (0.5 m ahead, desk height)
                desk.transform.position = new Vector3(0f, 0.75f, 0.5f);
                EditorSceneManager.SaveScene(scene, scenePath);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[GardenBootstrap] done: URP=" + pipelinePath + " colorSpace=" + PlayerSettings.colorSpace);
        }
    }
}
#endif
