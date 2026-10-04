#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GardenVR.Sundial.Editor
{
    /// <summary>
    /// Spike S5. Sets the import of the baked halo masks (apps/sundial/Art/Scripts/halo_s5.py bake). R is the core and G the
    /// falloff, read as masks, so the textures are linear. Idempotent.
    /// Batch: -batchmode -nographics -quit -executeMethod GardenVR.Sundial.Editor.Halo2Setup.Run
    /// </summary>
    public static class Halo2Setup
    {
        public const string Dir = "Assets/Resources/Halo2";

        [MenuItem("Garden VR/Sundial/Import Halo2 Masks")]
        public static void Run()
        {
            try
            {
                if (!File.Exists(Dir + "/halo2.json"))
                    throw new InvalidOperationException("run apps/sundial/Art/Scripts/halo_s5.py bake first");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                string[] files = Directory.GetFiles(Dir, "*.png");
                Array.Sort(files, StringComparer.Ordinal);
                int n = 0;
                foreach (string file in files)
                {
                    string path = file.Replace(Path.DirectorySeparatorChar, '/');
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) throw new InvalidOperationException("not imported: " + path);
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = false;
                    importer.alphaSource = TextureImporterAlphaSource.None;
                    importer.alphaIsTransparency = false;
                    importer.isReadable = false;
                    importer.mipmapEnabled = true;
                    importer.streamingMipmaps = false;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.filterMode = FilterMode.Trilinear;
                    importer.anisoLevel = 4;
                    importer.maxTextureSize = 1024;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    var android = importer.GetPlatformTextureSettings("Android");
                    android.overridden = true;
                    android.maxTextureSize = 512;
                    android.format = TextureImporterFormat.ASTC_6x6;
                    importer.SetPlatformTextureSettings(android);
                    importer.SaveAndReimport();
                    n++;
                }
                AssetDatabase.SaveAssets();
                Debug.Log("[Halo2Setup] OK " + n + " masks");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[Halo2Setup] FAIL " + e.Message);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
#endif
