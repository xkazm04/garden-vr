#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using GardenVR.Input;
using GardenVR.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Terrarium.Editor
{
    /// <summary>
    /// Imports the night-jar FBX and textures, builds <c>Assets/Prefabs/Jar.prefab</c>, and parents it to <c>PcDeskAnchor</c>.
    /// Idempotent. Batch: -executeMethod GardenVR.Terrarium.Editor.JarSetup.Run
    /// </summary>
    public static class JarSetup
    {
        public const string ModelPath = "Assets/Art/Models/night_jar.fbx";
        public const string FlowerModelPath = "Assets/Art/Models/flower.fbx";
        public const string S3ModelPath = "Assets/Art/Models/s3_moss.fbx";
        public const string S4ModelPath = "Assets/Art/Models/s4_fiddle.fbx";
        public const string S2ModelPath = "Assets/Art/Models/s2_jar.fbx";
        public const string PrefabPath = "Assets/Prefabs/Jar.prefab";
        public const string QuadPath = "Assets/Art/Models/CardQuad.asset";
        public const string ContactBandPath = "Assets/Art/Models/ContactBand.asset";
        public const string LiveFiddlePath = "Assets/Art/Models/FiddleLive.asset";
        public const string MaterialDir = "Assets/Art/Materials";

        static readonly string[] Textures =
        {
            "condensation", "cork_side", "cork_top", "fern_albedo", "fern_emission", "fiddle_hairs", "halo", "mist",
            "s1_drops", "s1_studio", "s2_refract_strip", "s3_strata", "s3_strand", "s4_frond_albedo", "s4_frond_thick",
            "moss_band", "moss_card", "moss_fuzz", "moss_macro", "moss_macro_b", "moss_tile", "moss_top", "petal", "ring", "soil_band", "spore"
        };

        [MenuItem("Garden VR/Terrarium/Build Jar")]
        public static void Run()
        {
            try
            {
                ConfigureTextures();
                ConfigureModel();
                ConfigureS3Model();
                ConfigureS4Model();
                ConfigureS2Model();
                var library = CreateLibrary();
                var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                if (modelPrefab == null) throw new InvalidOperationException("model not imported: " + ModelPath);
                LogSourceMeshes(modelPrefab);
                library.S3Model = InstantiateS3();
                BuildPrefab(modelPrefab, library);
                PlaceOnDesk();
                AssetDatabase.SaveAssets();
                Debug.Log("[JarSetup] done prefab=" + PrefabPath + " scene=" + SceneSetup.ScenePath);
            }
            catch (Exception e)
            {
                Debug.LogError("[JarSetup] FAIL " + e);
                EditorApplication.Exit(1);
            }
        }

        static void ConfigureTextures()
        {
            foreach (string name in Textures)
            {
                string path = "Assets/Art/Textures/" + name + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("texture missing: " + path);
                bool alpha = name == "fern_albedo" || name == "s4_frond_albedo" || name == "moss_card" || name == "moss_fuzz" || name == "mist" || name == "petal";
                bool linear = name == "condensation" || name == "ring" || name == "fern_emission" || name == "s1_drops" || name == "s3_strand" || name == "s4_frond_thick";
                bool repeat = name.IndexOf("band", StringComparison.Ordinal) >= 0
                    || name.IndexOf("side", StringComparison.Ordinal) >= 0
                    || name == "condensation"
                    || name == "cork_top"
                    || name == "moss_macro"
                    || name == "moss_macro_b"
                    || name == "fiddle_hairs"
                    || name == "s1_drops"
                    || name == "s3_strata"
                    || name == "s3_strand";
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = !linear;
                importer.alphaIsTransparency = alpha;
                importer.mipmapEnabled = true;
                importer.mipMapsPreserveCoverage = alpha;
                importer.alphaTestReferenceValue = 0.5f;
                importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                if (name == "s1_drops")
                {
                    importer.wrapModeU = TextureWrapMode.Repeat;
                    importer.wrapModeV = TextureWrapMode.Clamp;
                }
                if (name == "s1_studio" || name == "s2_refract_strip")
                    importer.mipmapEnabled = false;
                if (name == "s2_refract_strip") importer.filterMode = FilterMode.Bilinear;
                if (name == "s3_strata")
                {
                    importer.wrapModeU = TextureWrapMode.Repeat;
                    importer.wrapModeV = TextureWrapMode.Clamp;
                }
                if (name == "s3_strand")
                {
                    importer.filterMode = FilterMode.Trilinear;
                    importer.anisoLevel = 4;
                }
                if (name == "fiddle_hairs")
                {
                    // The crozier is about a centimetre wide on screen. The default mip drops the hairs.
                    importer.mipMapBias = -1.5f;
                    importer.anisoLevel = 16;
                    importer.filterMode = FilterMode.Trilinear;
                }
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
        }

        static void ConfigureModel()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("model missing: " + ModelPath);
            importer.importBlendShapes = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            // Import the file's normals and do not weld. Calculating normals, or welding vertices that
            // land near each other, splits the five fiddleheads differently and the CPU blend has nothing to lerp.
            importer.importNormals = ModelImporterNormals.Import;
            importer.normalSmoothingAngle = 70f;
            importer.weldVertices = false;
            importer.isReadable = true;
            importer.optimizeMeshPolygons = false;
            importer.optimizeMeshVertices = false;
            importer.SaveAndReimport();
        }

        static void ConfigureS3Model()
        {
            var importer = AssetImporter.GetAtPath(S3ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("model missing: " + S3ModelPath);
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            // The shell mesh carries its own rim normals. Import them as written.
            importer.importNormals = ModelImporterNormals.Import;
            importer.weldVertices = false;
            importer.isReadable = true;
            importer.optimizeMeshPolygons = false;
            importer.optimizeMeshVertices = false;
            importer.SaveAndReimport();
        }

        static void ConfigureS2Model()
        {
            var importer = AssetImporter.GetAtPath(S2ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("model missing: " + S2ModelPath);
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.weldVertices = false;
            importer.isReadable = false;
            importer.optimizeMeshPolygons = false;
            importer.optimizeMeshVertices = false;
            importer.SaveAndReimport();
        }

        static Mesh LoadS2Jar(string name)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(S2ModelPath))
            {
                var mesh = asset as Mesh;
                if (mesh != null && mesh.name == name) return mesh;
            }
            throw new InvalidOperationException("s2 jar mesh missing: " + name + " in " + S2ModelPath);
        }

        static void ConfigureS4Model()
        {
            var importer = AssetImporter.GetAtPath(S4ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("model missing: " + S4ModelPath);
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            // The fuzz shells push out along these normals, and the vertex colour carries the core weight.
            importer.importNormals = ModelImporterNormals.Import;
            importer.weldVertices = false;
            importer.isReadable = true;
            importer.optimizeMeshPolygons = false;
            importer.optimizeMeshVertices = false;
            importer.SaveAndReimport();
        }

        /// <summary>The five fuzzy fiddlehead states, in uncoil order, as the meshes of the imported model.</summary>
        static Mesh[] LoadS4FiddleStates()
        {
            string[] names = { "FiddleS4_0", "FiddleS4_25", "FiddleS4_50", "FiddleS4_75", "FiddleS4_100" };
            var found = new Mesh[names.Length];
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(S4ModelPath))
            {
                var mesh = asset as Mesh;
                if (mesh == null) continue;
                int index = Array.IndexOf(names, mesh.name);
                if (index >= 0) found[index] = mesh;
            }
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] == null) throw new InvalidOperationException("s4 fiddle mesh missing: " + names[i]);
                if (!found[i].isReadable) throw new InvalidOperationException("s4 fiddle mesh is not readable: " + names[i]);
                if (found[i].vertexCount != found[0].vertexCount)
                    throw new InvalidOperationException("s4 fiddle meshes do not share topology: " + names[i]);
            }
            Debug.Log("[JarSetup] s4 fiddle states verts=" + found[0].vertexCount + " tris=" + found[0].triangles.Length / 3);
            return found;
        }

        static GameObject InstantiateS3()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(S3ModelPath);
            if (prefab == null) throw new InvalidOperationException("s3 model not imported: " + S3ModelPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            return instance;
        }

        static void LogSourceMeshes(GameObject modelPrefab)
        {
            int fiddleVerts = -1;
            var filters = modelPrefab.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i].sharedMesh;
                int tris = mesh != null ? mesh.triangles.Length / 3 : 0;
                int verts = mesh != null ? mesh.vertexCount : 0;
                int uv2 = mesh != null && mesh.uv2 != null ? mesh.uv2.Length : 0;
                Debug.Log("[JarSetup] source " + filters[i].name + " verts=" + verts + " tris=" + tris
                    + " uv2=" + uv2
                    + " readable=" + (mesh != null && mesh.isReadable)
                    + " euler=" + filters[i].transform.localEulerAngles.ToString("F1"));
                if (filters[i].name.StartsWith("Fiddle", StringComparison.Ordinal))
                {
                    if (fiddleVerts < 0) fiddleVerts = verts;
                    else if (verts != fiddleVerts)
                        throw new InvalidOperationException("fiddle meshes do not share topology: " + filters[i].name + " has " + verts + ", expected " + fiddleVerts);
                }
            }
        }

        static JarLibrary CreateLibrary()
        {
            EnsureFolder(MaterialDir);
            Texture2D Tex(string name)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/" + name + ".png");
                if (tex == null) throw new InvalidOperationException("texture not loaded: " + name);
                return tex;
            }

            var library = new JarLibrary { Quad = SaveQuad(), ContactBand = SaveContactBand() };
            library.Glass = Mat("Jar_Glass", "Fidelity/JarGlass", m =>
            {
                m.SetTexture("_Cond", Tex("condensation"));
                m.SetTexture("_Bead", Tex("droplet_normal"));
                m.SetTexture("_Studio", Tex("s1_studio"));
                m.SetTexture("_DropN", Tex("s1_drops"));
                m.SetTexture("_RefractStrip", Tex("s2_refract_strip"));
                m.SetVector("_S2Cfg", new Vector4(0.010f, 0.06f, 1f, 0.25f));
                // Clear pane. Volume is edge absorption. See Assets/Art/LOCKED.md.
                m.SetColor("_Tint", new Color(0.75f, 0.94f, 0.84f, 0.004f));
                m.SetColor("_Rim", new Color(0.48f, 0.72f, 0.78f, 0.32f));
                m.SetFloat("_RimPower", 2.40f);
                m.SetColor("_Inner", new Color(0.34f, 0.86f, 0.48f, 1f));
                m.SetVector("_InnerY", new Vector4(0.078f, 0.052f, 0f, 0f));
                m.SetColor("_Volume", new Color(0.42f, 0.62f, 0.66f, 0.75f));
                m.SetVector("_VolumeY", new Vector4(0.038f, 0.072f, 0f, 0f));
                m.SetFloat("_Drops", 1.15f);
                m.SetFloat("_Refract", 36f);
                m.SetColor("_Streak", new Color(0.50f, 0.66f, 0.74f, 0.12f));
                m.SetShaderPassEnabled("SRPDefaultUnlit", false);
            });
            library.Moss = Mat("Jar_Moss", "Fidelity/MossVelvet", m =>
            {
                // Locked emission from T-TER-019. Triplanar, so the macro is not a strip over the carpet.
                // MossVelvet matches Glow on the JarG1 plate (28.2 deg). The seated 60 deg lens crosses _FarStart.
                m.SetTexture("_MainTex", Tex("moss_macro"));
                m.SetColor("_Tint", Color.white);
                m.SetTexture("_EmissionTex", Tex("moss_macro"));
                m.SetColor("_Emission", new Color(0.22f, 0.62f, 0.18f));
                m.SetColor("_Rim", new Color(0.20f, 0.48f, 0.18f));
                m.SetFloat("_RimPower", 3.2f);
                m.SetFloat("_GradBottom", 0.58f);
                m.SetFloat("_GradTop", 1.02f);
                m.SetVector("_GradY", new Vector4(0.016f, 0.032f, 0f, 0f));
                m.SetTexture("_TopTex", Tex("moss_macro"));
                m.SetFloat("_TopTile", 1f);
                m.SetFloat("_TopAmount", 0f);
                m.SetFloat("_Tri", 0f);
                m.SetFloat("_Cutoff", 0f);
                m.SetFloat("_FarStart", 0.36f);
                m.SetFloat("_FarEnd", 0.46f);
                m.SetFloat("_FarMip", 3.6f);
                m.SetFloat("_Velvet", 1f);
                m.SetFloat("_TopEmit", 0.32f);
                m.enableInstancing = true;
            });
            library.MossCard = Mat("Jar_MossCard", "Fidelity/MossVelvet", m =>
            {
                // Same Glow equation on the JarG1 plate. The tuft sheet is what reads as cells from the chair,
                // so the seated lens blurs the colour and keeps the alpha at mip 0.
                m.SetTexture("_MainTex", Tex("moss_card"));
                m.SetColor("_Tint", Color.white);
                m.SetTexture("_EmissionTex", Tex("moss_card"));
                m.SetColor("_Emission", new Color(0.12f, 0.42f, 0.11f));
                m.SetColor("_Rim", new Color(0.18f, 0.44f, 0.16f));
                m.SetFloat("_RimPower", 2.2f);
                m.SetFloat("_Cutoff", 0.04f);
                m.SetFloat("_Soft", 0.30f);
                m.SetFloat("_Trans", 0.25f);
                m.SetFloat("_GradBottom", 0.70f);
                m.SetFloat("_GradTop", 1.08f);
                m.SetVector("_GradY", new Vector4(0.018f, 0.032f, 0f, 0f));
                m.SetFloat("_Tri", 0f);
                m.SetFloat("_TopAmount", 0f);
                m.SetFloat("_FarStart", 0.36f);
                m.SetFloat("_FarEnd", 0.46f);
                m.SetFloat("_FarMip", 4.5f);
                m.SetFloat("_Velvet", 0.85f);
                m.SetFloat("_TopEmit", 0.40f);
                m.enableInstancing = true;
            });
            // Spike S3. Same colour values as Jar_Moss and Jar_MossCard, so the glow equation is shared.
            // JarView.ApplyS3 rewrites emission, rim, tip and alpha to coverage every Apply.
            Action<Material, Texture2D> furBase = (m, albedo) =>
            {
                m.SetTexture("_MainTex", albedo);
                m.SetTexture("_EmissionTex", albedo);
                m.SetTexture("_StrandTex", Tex("s3_strand"));
                m.SetColor("_Tint", new Color(0.80f, 0.95f, 0.92f));
                m.SetColor("_Emission", new Color(0.22f, 0.62f, 0.18f));
                m.SetColor("_Rim", new Color(0.20f, 0.48f, 0.18f));
                m.SetFloat("_RimPower", 3.2f);
                m.SetFloat("_GradBottom", 0.58f);
                m.SetFloat("_GradTop", 1.02f);
                m.SetVector("_GradY", new Vector4(0.016f, 0.032f, 0f, 0f));
                m.SetFloat("_StrandTile", 3.6f);
                m.SetFloat("_FurLen", 0.0026f);
                m.SetFloat("_AoBase", 0.08f);
                m.SetFloat("_TipBoost", 1.9f);
                m.SetFloat("_Cards", 0f);
                m.SetFloat("_AlphaToMask", 1f);
                m.SetFloat("_Cull", (float)CullMode.Back);
                m.SetFloat("_FarStart", 0.36f);
                m.SetFloat("_FarEnd", 0.46f);
                m.SetFloat("_TopEmit", 0.32f);
                m.SetFloat("_SeatedEmit", 0.72f);
            };
            library.S3Base = Mat("Jar_MossS3Base", "Fidelity/MossShell", m => furBase(m, Tex("moss_macro")));
            library.S3Shell = Mat("Jar_MossS3Shell", "Fidelity/MossShell", m => furBase(m, Tex("moss_macro")));
            library.S3Sprig = Mat("Jar_MossS3Sprig", "Fidelity/MossShell", m =>
            {
                m.SetTexture("_MainTex", Tex("moss_card"));
                m.SetTexture("_EmissionTex", Tex("moss_card"));
                m.SetTexture("_StrandTex", Tex("s3_strand"));
                m.SetColor("_Tint", Color.white);
                m.SetColor("_Emission", new Color(0.12f, 0.42f, 0.11f));
                m.SetColor("_Rim", new Color(0.18f, 0.44f, 0.16f));
                m.SetFloat("_RimPower", 2.2f);
                m.SetFloat("_Cutoff", 0.04f);
                m.SetFloat("_Trans", 0.25f);
                m.SetFloat("_GradBottom", 0.70f);
                m.SetFloat("_GradTop", 1.08f);
                m.SetVector("_GradY", new Vector4(0.018f, 0.032f, 0f, 0f));
                m.SetFloat("_Cards", 1f);
                m.SetFloat("_AoBase", 1f);
                m.SetFloat("_AlphaToMask", 1f);
                m.SetFloat("_Cull", (float)CullMode.Off);
                m.SetFloat("_FarStart", 0.36f);
                m.SetFloat("_FarEnd", 0.46f);
                m.SetFloat("_TopEmit", 0.40f);
                m.SetFloat("_SeatedEmit", 0.72f);
            });
            library.S3Soil = Mat("Jar_SoilS3", "Fidelity/Glow", m =>
            {
                m.SetTexture("_MainTex", Tex("s3_strata"));
                m.SetTexture("_EmissionTex", Tex("s3_strata"));
                m.SetColor("_Tint", new Color(1f, 1f, 1f));
                m.SetColor("_Emission", new Color(0.002f, 0.006f, 0.004f));
                m.SetColor("_Rim", new Color(0.03f, 0.08f, 0.05f));
                m.SetFloat("_RimPower", 3.4f);
                m.SetFloat("_GradBottom", 0.70f);
                m.SetFloat("_GradTop", 1.0f);
                m.SetVector("_GradY", new Vector4(0.002f, 0.024f, 0f, 0f));
                m.SetFloat("_Tri", 0f);
                m.SetTexture("_TopTex", Tex("s3_strata"));
                m.SetFloat("_TopTile", 22f);
                m.SetFloat("_TopAmount", 0f);
            });
            library.Soil = Mat("Jar_Soil", "Fidelity/Glow", m =>
            {
                m.SetTexture("_MainTex", Tex("soil_band"));
                m.SetTexture("_EmissionTex", Tex("soil_band"));
                m.SetColor("_Tint", new Color(0.72f, 0.88f, 0.76f));
                m.SetColor("_Emission", new Color(0.002f, 0.006f, 0.004f));
                m.SetColor("_Rim", new Color(0.03f, 0.08f, 0.05f));
                m.SetFloat("_RimPower", 3.4f);
                // Fine crumbs. A coarse tile on the bed read as gravel.
                // Darker where it meets the glass, still dark under the moss.
                m.SetFloat("_GradBottom", 0.55f);
                m.SetFloat("_GradTop", 0.82f);
                m.SetVector("_GradY", new Vector4(0.002f, 0.024f, 0f, 0f));
                m.SetFloat("_Tri", 22f);
                m.SetTexture("_TopTex", Tex("soil_band"));
                m.SetFloat("_TopTile", 22f);
                m.SetFloat("_TopAmount", 0.85f);
            });
            library.Cork = Mat("Jar_Cork", "Fidelity/Glow", m =>
            {
                // The texture is already the warm tan. No dark tint, no rim gloss, lit from the jar below.
                m.SetTexture("_MainTex", Tex("cork_side"));
                m.SetColor("_Tint", Color.white);
                m.SetTexture("_EmissionTex", Texture2D.whiteTexture);
                m.SetColor("_Emission", Color.black);
                m.SetColor("_Rim", Color.black);
                m.SetFloat("_RimPower", 4f);
                m.SetFloat("_GradBottom", 1.12f);
                m.SetFloat("_GradTop", 0.84f);
                m.SetVector("_GradY", new Vector4(0.118f, 0.14f, 0f, 0f));
                m.SetFloat("_Tri", 16f);
                m.SetTexture("_TopTex", Tex("cork_top"));
                m.SetFloat("_TopTile", 14f);
                m.SetFloat("_TopAmount", 1f);
            });
            Material Fern(string materialName, float emissionScale) => Mat(materialName, "Fidelity/Glow", m =>
            {
                m.SetTexture("_MainTex", Tex("fern_albedo"));
                m.SetColor("_Tint", new Color(0.72f, 0.92f, 0.68f));
                m.SetTexture("_EmissionTex", Tex("fern_emission"));
                // Veins carry the backlight. The rim is the bright edge the reference shows.
                m.SetColor("_Emission", new Color(0.16f, 0.54f, 0.30f) * emissionScale);
                m.SetColor("_Rim", new Color(0.32f, 0.50f, 0.42f));
                m.SetFloat("_RimPower", 1.7f);
                m.SetFloat("_Edge", 0.16f);
                m.SetFloat("_Cutoff", 0.16f);
                m.SetFloat("_Soft", 0.20f);
                m.SetFloat("_Trans", 0.75f);
                m.SetFloat("_GradBottom", 0.70f);
                m.SetFloat("_GradTop", 1.02f);
                m.SetVector("_GradY", new Vector4(0.032f, 0.096f, 0f, 0f));
                m.enableInstancing = true;
            });
            library.Fern = Fern("Jar_Fern", 0.75f);
            library.FernNew = Fern("Jar_FernNew", 1.0f);
            library.Fiddle = Mat("Jar_Fiddle", "Fidelity/Glow", m =>
            {
                m.SetTexture("_MainTex", Tex("fiddle_hairs"));
                m.SetTextureScale("_MainTex", new Vector2(1.2f, 2.4f));
                m.SetTextureScale("_EmissionTex", new Vector2(1.2f, 2.4f));
                m.SetColor("_Tint", new Color(0.55f, 0.84f, 0.32f));
                m.SetTexture("_EmissionTex", Tex("fiddle_hairs"));
                m.SetColor("_Emission", new Color(0.46f, 0.95f, 0.55f));
                m.SetColor("_Rim", new Color(0.48f, 0.70f, 0.52f));
                m.SetFloat("_RimPower", 1.15f);
                m.SetFloat("_Edge", 0.10f);
                m.SetFloat("_Trans", 0.45f);
                m.SetFloat("_GradBottom", 0.75f);
                m.SetFloat("_GradTop", 1.12f);
                m.SetVector("_GradY", new Vector4(0.032f, 0.100f, 0f, 0f));
            });
            // Spike S4. The A fiddle and the A fronds are untouched. These are the inputs of variants s4, s4f and s4h.
            library.GlowShader = Shader.Find("Fidelity/Glow");
            library.S4FrondShader = Shader.Find("Fidelity/FrondBacklit");
            if (library.GlowShader == null || library.S4FrondShader == null) throw new InvalidOperationException("s4 frond shaders not found");
            library.S4FrondAlbedo = Tex("s4_frond_albedo");
            library.S4FrondThick = Tex("s4_frond_thick");
            library.FernAlbedoA = Tex("fern_albedo");
            library.S2Jar = LoadS2Jar("JarS2Lean");
            library.S2JarFull = LoadS2Jar("JarS2");
            library.S4FiddleStates = LoadS4FiddleStates();
            library.S4Fiddle = Mat("Jar_FiddleS4", "Fidelity/FiddleFuzz", m =>
            {
                // Same colours as Jar_Fiddle, so the glow equation and the emission values are shared. JarView rewrites
                // emission, rim and the tip window on every Apply. The hair texture runs along the tube.
                m.SetTexture("_MainTex", Tex("fiddle_hairs"));
                m.SetTextureScale("_MainTex", new Vector2(1.2f, 0.46f));
                m.SetTexture("_EmissionTex", Tex("fiddle_hairs"));
                m.SetColor("_Tint", new Color(0.48f, 0.70f, 0.18f));
                m.SetFloat("_Flat", 0.85f);
                m.SetFloat("_EmScale", 0.75f);
                m.SetColor("_Emission", new Color(0.46f, 0.95f, 0.55f));
                m.SetColor("_Rim", new Color(0.48f, 0.70f, 0.52f));
                m.SetFloat("_RimPower", 1.15f);
                m.SetFloat("_Trans", 0.45f);
                m.SetFloat("_GradBottom", 0.75f);
                m.SetFloat("_GradTop", 1.12f);
                m.SetVector("_GradY", new Vector4(0.032f, 0.100f, 0f, 0f));
                m.SetTexture("_StrandTex", Tex("s3_strand"));
                m.SetVector("_StrandTile", new Vector4(2f, 1f, 0f, 0f));
                m.SetFloat("_FurLen", 0.0016f);
                m.SetFloat("_AoBase", 0.55f);
                m.SetColor("_FuzzTint", new Color(1.35f, 1.40f, 1.15f));
                m.SetColor("_CoreColor", new Color(0.50f, 0.62f, 0.10f));
                m.SetFloat("_CoreBoost", 2.0f);
                m.SetFloat("_CorePower", 1.4f);
                m.SetFloat("_AlphaToMask", 1f);
            });
            library.Seedling = Mat("Jar_Seedling", "Fidelity/Glow", m =>
            {
                m.SetTexture("_MainTex", Texture2D.whiteTexture);
                m.SetColor("_Tint", new Color(0.28f, 0.55f, 0.24f));
                m.SetTexture("_EmissionTex", Texture2D.whiteTexture);
                m.SetColor("_Emission", new Color(0.03f, 0.08f, 0.04f));
                m.SetColor("_Rim", new Color(0.16f, 0.38f, 0.18f));
                m.SetFloat("_RimPower", 2.0f);
            });
            library.Dew = Mat("Jar_Dew", "Fidelity/Glow", m =>
            {
                m.SetTexture("_MainTex", Texture2D.whiteTexture);
                m.SetColor("_Tint", new Color(0.6f, 1f, 0.85f));
                m.SetTexture("_EmissionTex", Texture2D.whiteTexture);
                m.SetColor("_Emission", new Color(0.70f, 0.98f, 0.84f));
                m.SetColor("_Rim", new Color(0.2f, 0.6f, 0.5f));
                m.SetFloat("_RimPower", 1.5f);
            });
            library.Ring = Mat("Jar_Ring", "Fidelity/Card", m =>
            {
                m.SetTexture("_MainTex", Tex("ring"));
                m.SetColor("_Color", new Color(0.46f, 0.82f, 0.54f));
                m.SetColor("_Color2", new Color(0.12f, 0.32f, 0.18f));
                m.SetFloat("_Src", (float)BlendMode.One);
                m.SetFloat("_Dst", (float)BlendMode.One);
                m.SetFloat("_Ring", 1f);
                m.SetFloat("_Fill", 0.5f);
                m.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
                m.renderQueue = 2990;
            });
            library.Spill = CardMat("Jar_Spill", Tex("halo"), new Color(0.20f, 0.48f, 0.28f), Color.black, false, -11);
            library.JarHalo = CardMat("Jar_Halo", Tex("halo"), new Color(0.16f, 0.40f, 0.24f), Color.black, false, 20);
            library.CoilHalo = CardMat("Jar_CoilHalo", Tex("halo"), new Color(0.42f, 0.78f, 0.50f), Color.black, false, 21);
            library.Spill.SetFloat("_Falloff", 1.35f);
            library.Spill.SetVector("_Focus", new Vector4(0.50f, 0.46f, 0.58f, 0f));
            library.JarHalo.SetFloat("_Falloff", 1.15f);
            library.JarHalo.SetVector("_Focus", new Vector4(0.50f, 0.42f, 0.62f, 0f));
            library.CoilHalo.SetFloat("_Falloff", 1.05f);
            library.CoilHalo.SetVector("_Focus", new Vector4(0.50f, 0.50f, 0.48f, 0f));
            library.Mist = CardMat("Jar_Mist", Tex("mist"), new Color(0.84f, 0.93f, 0.90f, 0.80f), Color.black, true, 30);
            library.Spore = CardMat("Jar_Spore", Tex("spore"), new Color(0.9490196f, 0.8235294f, 0.4784314f) * 1.85f, Color.black, false, 40);
            // Spike S5. Steam puffs (alpha flipbook), the desk reflection streak (additive) and the foot contact line (alpha).
            ConfigureS5Textures();
            library.Steam = CardMat("Jar_SteamS5", Tex("s5_steam"), new Color(0.80f, 0.93f, 0.89f, 0.62f), Color.black, true, 31);
            library.DeskStreak = CardMat("Jar_DeskStreakS5", Tex("s5_desk_streak"), new Color(0.30f, 0.72f, 0.46f), new Color(0.10f, 0.30f, 0.18f), false, -10);
            library.Contact = CardMat("Jar_ContactS5", Tex("s5_contact"), new Color(0.006f, 0.020f, 0.014f, 0.85f), Color.black, true, -12);
            library.Flower = Mat("Jar_Flower", "Fidelity/Glow", m =>
            {
                m.SetTexture("_MainTex", Tex("petal"));
                m.SetColor("_Tint", new Color(1.05f, 0.86f, 0.52f));
                m.SetTexture("_EmissionTex", Tex("petal"));
                m.SetColor("_Emission", new Color(0.72f, 0.46f, 0.16f));
                m.SetColor("_Rim", new Color(1.0f, 0.78f, 0.36f));
                m.SetFloat("_RimPower", 2.1f);
                m.SetFloat("_Cutoff", 0f);
                m.SetFloat("_GradBottom", 0.9f);
                m.SetFloat("_GradTop", 1.2f);
                m.SetVector("_GradY", new Vector4(0.04f, 0.08f, 0f, 0f));
            });
            library.FlowerMesh = LoadFlowerMesh();
            return library;
        }

        static Mesh LoadFlowerMesh()
        {
            var importer = AssetImporter.GetAtPath(FlowerModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("model missing: " + FlowerModelPath);
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.normalSmoothingAngle = 60f;
            importer.weldVertices = false;
            importer.isReadable = true;
            importer.optimizeMeshPolygons = false;
            importer.optimizeMeshVertices = false;
            importer.SaveAndReimport();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlowerModelPath);
            if (prefab == null) throw new InvalidOperationException("flower not imported: " + FlowerModelPath);
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            Mesh found = null;
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i].sharedMesh;
                if (mesh == null) continue;
                int tris = mesh.triangles.Length / 3;
                Debug.Log("[JarSetup] flower " + filters[i].name + " verts=" + mesh.vertexCount + " tris=" + tris);
                if (filters[i].name == "Flower" || mesh.name == "Flower") found = mesh;
            }
            if (found == null && filters.Length == 1) found = filters[0].sharedMesh;
            if (found == null) throw new InvalidOperationException("flower mesh missing");
            int flowerTris = found.triangles.Length / 3;
            if (flowerTris >= 1500) throw new InvalidOperationException("flower over 1500 tris: " + flowerTris);
            return found;
        }

        /// <summary>The flipbook keeps no mips (they would bleed one frame into the next) and every S5 card clamps its edge.</summary>
        static void ConfigureS5Textures()
        {
            foreach (string name in new[] { "s5_steam", "s5_desk_streak", "s5_contact" })
            {
                string path = "Assets/Art/Textures/" + name + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("texture missing: " + path);
                bool wantMips = name != "s5_steam";
                if (importer.wrapMode == TextureWrapMode.Clamp && importer.mipmapEnabled == wantMips && importer.alphaIsTransparency) continue;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = wantMips;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }
        }

        static Material CardMat(string name, Texture2D tex, Color color, Color color2, bool alpha, int queueOffset)
        {
            return Mat(name, "Fidelity/Card", m =>
            {
                m.SetTexture("_MainTex", tex);
                m.SetColor("_Color", color);
                m.SetColor("_Color2", color2);
                m.SetFloat("_Src", alpha ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
                m.SetFloat("_Dst", alpha ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.One);
                m.SetFloat("_Boil", 0f);
                m.SetFloat("_Ring", 0f);
                m.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
                m.renderQueue = 3000 + queueOffset;
            });
        }

        static Material Mat(string name, string shaderName, Action<Material> init)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("shader not found: " + shaderName);
            string path = MaterialDir + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            init(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Spike S5. A flat annulus in the XZ plane, radius 43 to 57 mm, uv 0..1 over a 0.12 m square (the s5_contact.png layout).
        /// A band instead of a square card keeps the contact line out from behind the glass.
        /// </summary>
        static Mesh SaveContactBand()
        {
            const int segments = 96;
            const float inner = 0.043f, outer = 0.057f, extent = 0.12f;
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ContactBandPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "ContactBand" };
                AssetDatabase.CreateAsset(mesh, ContactBandPath);
            }
            mesh.Clear();
            var vertices = new Vector3[segments * 2];
            var uvs = new Vector2[segments * 2];
            var normals = new Vector3[segments * 2];
            var colors = new Color[segments * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                vertices[i * 2] = new Vector3(c * inner, 0f, s * inner);
                vertices[i * 2 + 1] = new Vector3(c * outer, 0f, s * outer);
                uvs[i * 2] = new Vector2(vertices[i * 2].x / extent + 0.5f, vertices[i * 2].z / extent + 0.5f);
                uvs[i * 2 + 1] = new Vector2(vertices[i * 2 + 1].x / extent + 0.5f, vertices[i * 2 + 1].z / extent + 0.5f);
                normals[i * 2] = normals[i * 2 + 1] = Vector3.up;
                colors[i * 2] = colors[i * 2 + 1] = Color.white;
                int n = (i + 1) % segments;
                int t = i * 6;
                triangles[t] = i * 2; triangles[t + 1] = i * 2 + 1; triangles[t + 2] = n * 2;
                triangles[t + 3] = i * 2 + 1; triangles[t + 4] = n * 2 + 1; triangles[t + 5] = n * 2;
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static Mesh SaveQuad()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(QuadPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "CardQuad" };
                AssetDatabase.CreateAsset(mesh, QuadPath);
            }
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static void BuildPrefab(GameObject modelPrefab, JarLibrary library)
        {
            EnsureFolder("Assets/Prefabs");
            var root = new GameObject("Jar");
            try
            {
                var view = root.AddComponent<JarView>();
                if (root.GetComponent<JarRitualController>() == null)
                    root.AddComponent<JarRitualController>();
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
                if (model == null) throw new InvalidOperationException("could not instance " + ModelPath);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                view.Build(model, library);
                view.breath = 0.5f;
                view.uncoil = 0.3f;
                view.fog = 0.45f;
                view.answer = 0f;
                view.vitality = 1f;
                view.time = 3f;
                view.Apply();
                PersistLiveFiddle(view);
                WireTargets(view);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (saved == null) throw new InvalidOperationException("could not save " + PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void PersistLiveFiddle(JarView view)
        {
            if (view.fiddle == null) return;
            Mesh live = view.fiddle.sharedMesh;
            if (live == null || live.name != "FiddleLive") return;
            var copy = UnityEngine.Object.Instantiate(live);
            copy.name = "FiddleLive";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(LiveFiddlePath) != null) AssetDatabase.DeleteAsset(LiveFiddlePath);
            AssetDatabase.CreateAsset(copy, LiveFiddlePath);
            view.fiddle.sharedMesh = copy;
        }

        static void WireTargets(JarView view)
        {
            Transform glass = null;
            Transform cork = null;
            Transform[] all = view.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].GetComponent<MeshFilter>() == null) continue;
                if (all[i].name == "Jar") glass = all[i];
                if (all[i].name == "Cork") cork = all[i];
            }
            if (glass == null || cork == null) throw new InvalidOperationException("jar or cork mesh missing after build");
            IntentTarget[] targets = view.GetComponentsInChildren<IntentTarget>(true);
            bool jarWired = false;
            bool corkWired = false;
            for (int i = 0; i < targets.Length; i++)
            {
                IntentTarget target = targets[i];
                var data = new SerializedObject(target);
                SerializedProperty id = data.FindProperty("id");
                SerializedProperty hit = data.FindProperty("hitCollider");
                if (id == null || hit == null) throw new InvalidOperationException("IntentTarget fields not found");
                if (target.gameObject == view.gameObject)
                {
                    id.stringValue = "jar";
                    hit.objectReferenceValue = glass.GetComponent<Collider>();
                    jarWired = true;
                }
                else if (target.gameObject == cork.gameObject)
                {
                    id.stringValue = "jar.cork";
                    hit.objectReferenceValue = cork.GetComponent<Collider>();
                    corkWired = true;
                }
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            if (!jarWired || !corkWired) throw new InvalidOperationException("IntentTarget ids jar and jar.cork were not both wired");
        }

        static void PlaceOnDesk()
        {
            var scene = EditorSceneManager.OpenScene(SceneSetup.ScenePath, OpenSceneMode.Single);
            PcDeskAnchor desk = UnityEngine.Object.FindAnyObjectByType<PcDeskAnchor>();
            if (desk == null) throw new InvalidOperationException("PcDeskAnchor missing from " + SceneSetup.ScenePath);
            var stale = new List<GameObject>();
            for (int i = 0; i < desk.transform.childCount; i++)
            {
                Transform child = desk.transform.GetChild(i);
                if (child.name == "Jar") stale.Add(child.gameObject);
            }
            for (int i = 0; i < stale.Count; i++) UnityEngine.Object.DestroyImmediate(stale[i]);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException("prefab missing: " + PrefabPath);
            var jar = (GameObject)PrefabUtility.InstantiatePrefab(prefab, desk.transform);
            if (jar == null) throw new InvalidOperationException("could not instance " + PrefabPath);
            jar.name = "Jar";
            jar.transform.localPosition = Vector3.zero;
            jar.transform.localRotation = Quaternion.identity;
            jar.transform.localScale = Vector3.one;

            // JarG1 is the round-3 camera in world space. The acceptance shot does not pass -target.
            // Keep the jar parented to the desk, and move the rig so that desk sits on the world origin
            // the camera was measured against. The eye stays 0.30 m above the jar and 0.40 m back.
            GameObject rig = GameObject.Find("SeatedRig");
            if (rig == null) throw new InvalidOperationException("SeatedRig missing");
            rig.transform.position -= desk.transform.position;
            Vector3 deskNow = desk.transform.position;
            Debug.Log("[JarSetup] desk world " + deskNow.ToString("F4") + " rig " + rig.transform.position.ToString("F4"));
            if (deskNow.sqrMagnitude > 1e-8f) throw new InvalidOperationException("desk did not land on the origin: " + deskNow);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            string parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            string name = Path.GetFileName(assetFolder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
