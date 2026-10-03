#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace GardenVR.Terrarium.Editor
{
    /// <summary>
    /// Bakes the OFL Cormorant Garamond face into a TextMeshPro asset and a frosted material.
    /// Batch: -executeMethod GardenVR.Terrarium.Editor.EtchFontSetup.Run
    /// </summary>
    public static class EtchFontSetup
    {
        public const string TtfPath = "Assets/Fonts/CormorantGaramond/CormorantGaramond-Regular.ttf";
        public const string AssetPath = "Assets/Resources/Etch/CormorantGaramond SDF.asset";
        public const string MaterialPath = "Assets/Resources/Etch/EtchedFrost.mat";

        public static void Run()
        {
            try
            {
                Build();
                Debug.Log("[EtchFont] OK " + AssetPath);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[EtchFont] FAIL " + e.Message);
                EditorApplication.Exit(1);
            }
        }

        static void Build()
        {
            if (!File.Exists(TtfPath)) throw new InvalidOperationException("font file missing: " + TtfPath);
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/Etch");

            var importer = AssetImporter.GetAtPath(TtfPath) as TrueTypeFontImporter;
            if (importer != null)
            {
                importer.fontTextureCase = FontTextureCase.Dynamic;
                importer.includeFontData = true;
                importer.SaveAndReimport();
            }

            var font = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
            if (font == null) throw new InvalidOperationException("Unity did not import " + TtfPath);

            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetPath) != null)
                AssetDatabase.DeleteAsset(AssetPath);
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(MaterialPath) != null)
                AssetDatabase.DeleteAsset(MaterialPath);

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
                font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (asset == null) throw new InvalidOperationException("CreateFontAsset returned null");
            asset.name = "CormorantGaramond SDF";
            AssetDatabase.CreateAsset(asset, AssetPath);
            var fontData = new SerializedObject(asset);
            SerializedProperty clear = fontData.FindProperty("m_ClearDynamicDataOnBuild");
            if (clear != null) clear.boolValue = false;
            fontData.ApplyModifiedPropertiesWithoutUndo();

            Texture2D atlas = asset.atlasTextures != null && asset.atlasTextures.Length > 0 ? asset.atlasTextures[0] : null;
            if (atlas == null) throw new InvalidOperationException("font atlas missing");
            atlas.name = "CormorantGaramond SDF Atlas";
            atlas.hideFlags = HideFlags.None;
            AssetDatabase.AddObjectToAsset(atlas, asset);

            if (asset.material == null) throw new InvalidOperationException("font material missing");
            asset.material.name = "CormorantGaramond SDF Material";
            asset.material.hideFlags = HideFlags.None;
            AssetDatabase.AddObjectToAsset(asset.material, asset);

            string missing;
            bool added = asset.TryAddCharacters(EtchedLettering.Glyphs, out missing, false);
            if (!string.IsNullOrEmpty(missing))
                throw new InvalidOperationException("missing glyphs: " + missing);
            if (!added)
                throw new InvalidOperationException("TryAddCharacters failed");

            // Adding glyphs can replace the atlas texture. Keep the live one on the asset.
            Texture2D live = asset.atlasTextures[0];
            if (live != null && !AssetDatabase.Contains(live))
            {
                live.name = "CormorantGaramond SDF Atlas";
                live.hideFlags = HideFlags.None;
                AssetDatabase.AddObjectToAsset(live, asset);
            }
            if (asset.material != null && live != null)
                asset.material.SetTexture("_MainTex", live);

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            var frost = new Material(asset.material) { name = "EtchedFrost" };
            EtchedLettering.ApplyFrost(frost);
            AssetDatabase.CreateAsset(frost, MaterialPath);

            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset");
            if (settings == null) throw new InvalidOperationException("TMP Settings missing");
            var so = new SerializedObject(settings);
            SerializedProperty prop = so.FindProperty("m_defaultFontAsset");
            if (prop == null) throw new InvalidOperationException("m_defaultFontAsset missing");
            prop.objectReferenceValue = asset;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("[EtchFont] face=" + asset.faceInfo.familyName + " style=" + asset.faceInfo.styleName
                + " glyphs=" + EtchedLettering.Glyphs.Length.ToString());
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
