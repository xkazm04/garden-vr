#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GardenVR.Sundial.Editor
{
    /// <summary>
    /// Spike S3, T-SUN-049. Imports the three parts atlases (apps/sundial/Art/Scripts/leafplant_s3.py slice) and writes the
    /// <c>Fidelity/Leaf</c> materials the dial loads for variant=leafplant. Idempotent.
    /// Batch: -batchmode -nographics -quit -executeMethod GardenVR.Sundial.Editor.LeafPlantSetup.Run
    /// </summary>
    public static class LeafPlantSetup
    {
        public const string Dir = "Assets/Resources/LeafPlant";
        public const string AtlasPath = Dir + "/midday-parts.png";
        public const string MaterialPath = Dir + "/Leaf_Midday.mat";
        public const string PartsPath = Dir + "/midday-parts.json";

        [MenuItem("Garden VR/Sundial/Build Leaf Plant")]
        public static void Run()
        {
            try
            {
                foreach (LeafSpecies species in LeafSpecies.ByArc)
                    BuildSpecies(species);
                AssetDatabase.SaveAssets();
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[LeafPlantSetup] FAIL " + e.Message);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        /// <summary>One parts atlas import and one <c>Fidelity/Leaf</c> material per species (T-SUN-049).</summary>
        static void BuildSpecies(LeafSpecies species)
        {
            string atlasPath = Dir + "/" + species.Id + "-parts.png";
            string partsPath = Dir + "/" + species.Id + "-parts.json";
            string materialPath = "Assets/Resources/" + species.MaterialResource + ".mat";
            if (!File.Exists(atlasPath) || !File.Exists(partsPath))
                throw new InvalidOperationException("run apps/sundial/Art/Scripts/leafplant_s3.py slice first (" + species.Id + ")");
            AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("atlas did not import: " + atlasPath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            // Read/Write is on for the halo silhouette, which rasterises the real cards against the atlas alpha.
            importer.isReadable = true;
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

            Shader shader = Shader.Find("Fidelity/Leaf");
            if (shader == null) throw new InvalidOperationException("shader not found: Fidelity/Leaf");
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(materialPath) };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath));
            material.SetColor("_Color", Color.white);
            material.SetColor("_Lit", new Color(1.04f, 1.03f, 0.98f, 1f));
            material.SetColor("_Shade", new Color(0.70f, 0.73f, 0.84f, 1f));
            material.SetVector("_LightDir", new Vector4(-0.28f, 0.86f, 0.42f, 0f));
            material.SetFloat("_Step", 0.12f);
            material.SetFloat("_Soft", 0.05f);
            material.SetFloat("_Cut", 0.45f);
            material.SetFloat("_SwayAmp", 0.0012f);
            material.SetFloat("_SwayFps", 6f);
            EditorUtility.SetDirty(material);
            Debug.Log("[LeafPlantSetup] OK " + materialPath);
        }
    }
}
#endif
