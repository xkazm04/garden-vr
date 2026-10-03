#if UNITY_EDITOR
using UnityEditor;

namespace GardenVR.Terrarium.Editor
{
    /// <summary>
    /// The sprig card has to be readable so leaf cards can combine into one mesh.
    /// Textures keep their alpha so the black paper keys out instead of painting a black square.
    /// </summary>
    [InitializeOnLoad]
    static class CompanionImport
    {
        static CompanionImport()
        {
            FixMesh("Assets/Art/Models/sprig.fbx");
            FixMesh("Assets/Resources/Companions/sprig.fbx");
            FixTex("Assets/Resources/Companions/sprig_glow.png");
            FixTex("Assets/Resources/Companions/sprig_moon.png");
            FixTex("Assets/Resources/Companions/sprig_gold.png");
        }

        static void FixMesh(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;
            bool dirty = false;
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                dirty = true;
            }
            if (!importer.bakeAxisConversion)
            {
                importer.bakeAxisConversion = true;
                dirty = true;
            }
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                dirty = true;
            }
            if (dirty) importer.SaveAndReimport();
        }

        static void FixTex(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            bool dirty = false;
            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }
            if (importer.alphaSource != TextureImporterAlphaSource.FromInput)
            {
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                dirty = true;
            }
            if (!importer.sRGBTexture)
            {
                importer.sRGBTexture = true;
                dirty = true;
            }
            // A 4 cm card mips down into the black paper and the leaf reads as charcoal.
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }
            if (dirty) importer.SaveAndReimport();
        }
    }
}
#endif
