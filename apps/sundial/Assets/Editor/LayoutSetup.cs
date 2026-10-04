#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GardenVR.Sundial.Editor
{
    /// <summary>
    /// T-SUN-052. Imports the remapped face (apps/sundial/Art/Scripts/layout_t052_face.py) with the same importer settings as
    /// dial_face.png (1024 cap, trilinear, clamp) and writes <c>Resources/Layout/Dial_Face_Layout.mat</c>: a copy of Dial_Face with
    /// the new texture and the ink ring on the inner edge of the narrower band. Idempotent.
    /// Batch: -batchmode -nographics -quit -executeMethod GardenVR.Sundial.Editor.LayoutSetup.Run
    /// </summary>
    public static class LayoutSetup
    {
        public const string Dir = "Assets/Resources/Layout";
        public const string FacePath = Dir + "/dial_face_layout.png";
        public const string MaterialPath = Dir + "/Dial_Face_Layout.mat";
        const string SourceMaterial = "Assets/Art/Materials/Dial_Face.mat";
        const string SourceFace = "Assets/Art/Textures/dial_face.png";
        // T-SUN-050. The S1 face and control map through the same remap, and the S1 material copied onto them.
        public const string FaceS1Path = Dir + "/dial_face_layout_s1.png";
        public const string ControlPath = Dir + "/dial_control_layout.png";
        public const string MaterialS1Path = Dir + "/Dial_Face_Layout_S1.mat";
        const string SourceMaterialS1 = "Assets/Art/Materials/Dial_Face_S1.mat";
        const string SourceFaceS1 = "Assets/Art/Textures/dial_face_s1.png";
        const string SourceControl = "Assets/Art/Textures/dial_control.png";

        [MenuItem("Garden VR/Sundial/Build Layout Face")]
        public static void Run()
        {
            try
            {
                if (!File.Exists(FacePath)) throw new InvalidOperationException("run apps/sundial/Art/Scripts/layout_t052_face.py first");
                AssetDatabase.ImportAsset(FacePath, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(FacePath) as TextureImporter;
                var source = AssetImporter.GetAtPath(SourceFace) as TextureImporter;
                if (importer == null || source == null) throw new InvalidOperationException("face textures did not import");
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.isReadable = false;
                importer.mipmapEnabled = true;
                importer.streamingMipmaps = false;
                importer.wrapMode = source.wrapMode;
                importer.filterMode = source.filterMode;
                importer.anisoLevel = source.anisoLevel;
                importer.maxTextureSize = 1024;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = source.textureCompression;
                importer.SaveAndReimport();

                var origin = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterial);
                if (origin == null) throw new InvalidOperationException("missing " + SourceMaterial);
                var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                if (material == null)
                {
                    material = new Material(origin) { name = "Dial_Face_Layout" };
                    AssetDatabase.CreateAsset(material, MaterialPath);
                }
                else
                {
                    material.CopyPropertiesFromMaterial(origin);
                }
                material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(FacePath));
                // _InkRingR is in UV metres on the face: the ink ring's fraction of the 0.1472 m face radius.
                material.SetFloat("_InkRingR", DialLayout.InkRing * 0.1472f);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();
                BuildSprintFace();
                Debug.Log("[LayoutSetup] OK " + MaterialPath + " " + MaterialS1Path);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[LayoutSetup] FAIL " + e.Message);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        /// <summary>Imports the two remapped S1 textures like their sources and writes <c>Dial_Face_Layout_S1.mat</c>. Idempotent.</summary>
        static void BuildSprintFace()
        {
            if (!File.Exists(FaceS1Path) || !File.Exists(ControlPath))
                throw new InvalidOperationException("run apps/sundial/Art/Scripts/layout_t052_face.py --s1 first");
            ImportLike(FaceS1Path, SourceFaceS1);
            ImportLike(ControlPath, SourceControl);
            var origin = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialS1);
            if (origin == null) throw new InvalidOperationException("missing " + SourceMaterialS1);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialS1Path);
            if (material == null)
            {
                material = new Material(origin) { name = "Dial_Face_Layout_S1" };
                AssetDatabase.CreateAsset(material, MaterialS1Path);
            }
            else
            {
                material.CopyPropertiesFromMaterial(origin);
            }
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(FaceS1Path));
            material.SetTexture("_ControlTex", AssetDatabase.LoadAssetAtPath<Texture2D>(ControlPath));
            material.SetFloat("_InkRingR", DialLayout.InkRing * 0.1472f);
            material.EnableKeyword("_GVR_WATERCOLOUR");
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }

        /// <summary>The importer settings of <paramref name="source"/> on <paramref name="path"/> (type, colour space, size, filter, format).</summary>
        static void ImportLike(string path, string sourcePath)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            var source = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
            if (importer == null || source == null) throw new InvalidOperationException("textures did not import: " + path);
            importer.textureType = source.textureType;
            importer.sRGBTexture = source.sRGBTexture;
            importer.alphaSource = source.alphaSource;
            importer.alphaIsTransparency = source.alphaIsTransparency;
            importer.isReadable = false;
            importer.mipmapEnabled = source.mipmapEnabled;
            importer.streamingMipmaps = false;
            importer.wrapMode = source.wrapMode;
            importer.filterMode = source.filterMode;
            importer.anisoLevel = source.anisoLevel;
            importer.maxTextureSize = source.maxTextureSize;
            importer.npotScale = source.npotScale;
            importer.textureCompression = source.textureCompression;
            foreach (string platform in new[] { "DefaultTexturePlatform", "Android", "Standalone" })
            {
                TextureImporterPlatformSettings from = source.GetPlatformTextureSettings(platform);
                if (from != null && (from.overridden || platform == "DefaultTexturePlatform")) importer.SetPlatformTextureSettings(from);
            }
            importer.SaveAndReimport();
        }
    }
}
#endif
