#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GardenVR.Sundial.Editor
{
    /// <summary>
    /// Spike S4. Imports the painted earth (apps/sundial/Art/Scripts/soilmound_s4.py build) and writes the
    /// <c>Fidelity/SoilMound</c> material the dial loads for variant=soilmound. Idempotent.
    /// Batch: -batchmode -nographics -quit -executeMethod GardenVR.Sundial.Editor.SoilMoundSetup.Run
    /// </summary>
    public static class SoilMoundSetup
    {
        public const string Dir = "Assets/Resources/SoilMound";
        public const string PaintPath = Dir + "/soil_mound.png";
        public const string DataPath = Dir + "/soil-mound.json";
        public const string MaterialPath = Dir + "/Soil_Mound.mat";
        const string PaperPath = "Assets/Art/Textures/dial_paper_h.png";

        [MenuItem("Garden VR/Sundial/Build Soil Mound")]
        public static void Run()
        {
            try
            {
                if (!File.Exists(PaintPath) || !File.Exists(DataPath))
                    throw new InvalidOperationException("run apps/sundial/Art/Scripts/soilmound_s4.py build first");
                AssetDatabase.ImportAsset(PaintPath, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(PaintPath) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("paint did not import");
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
                importer.isReadable = false;
                importer.mipmapEnabled = true;
                importer.streamingMipmaps = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 8;
                importer.maxTextureSize = 1024;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var android = importer.GetPlatformTextureSettings("Android");
                android.overridden = true;
                android.maxTextureSize = 1024;
                android.format = TextureImporterFormat.ASTC_6x6;
                importer.SetPlatformTextureSettings(android);
                importer.SaveAndReimport();

                Shader shader = Shader.Find("Fidelity/SoilMound");
                if (shader == null) throw new InvalidOperationException("shader not found: Fidelity/SoilMound");
                var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                if (material == null)
                {
                    material = new Material(shader) { name = "Soil_Mound" };
                    AssetDatabase.CreateAsset(material, MaterialPath);
                }
                else if (material.shader != shader)
                {
                    material.shader = shader;
                }
                material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(PaintPath));
                material.SetTexture("_PaperH", AssetDatabase.LoadAssetAtPath<Texture2D>(PaperPath));
                material.SetColor("_Lit", new Color(1.02f, 0.99f, 0.95f, 1f));
                material.SetColor("_Shade", new Color(0.80f, 0.76f, 0.72f, 1f));
                material.SetColor("_Ink", new Color(0.165f, 0.149f, 0.133f, 1f));
                material.SetFloat("_Step", 0.05f);
                material.SetFloat("_Feather", 0.07f);
                material.SetFloat("_Cut", 0.45f);
                material.SetFloat("_Gran", 0.22f);
                material.SetFloat("_PaperTiling", 3.75f);
                material.SetFloat("_Grain", 0.045f);
                material.SetFloat("_ShadowStrength", 0.85f);
                material.SetFloat("_PebbleInk", 0.75f);
                material.SetFloat("_WashTint", 0.55f);
                material.SetFloat("_EdgeGran", 0.5f);
                material.SetColor("_WashFallback", new Color(0.957f, 0.929f, 0.875f, 1f));
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();
                Debug.Log("[SoilMoundSetup] OK " + MaterialPath);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[SoilMoundSetup] FAIL " + e.Message);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
#endif
