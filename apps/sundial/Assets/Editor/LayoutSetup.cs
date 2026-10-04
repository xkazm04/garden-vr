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
                Debug.Log("[LayoutSetup] OK " + MaterialPath);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[LayoutSetup] FAIL " + e.Message);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
#endif
