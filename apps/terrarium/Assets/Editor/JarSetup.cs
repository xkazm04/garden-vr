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
        public const string PrefabPath = "Assets/Prefabs/Jar.prefab";
        public const string QuadPath = "Assets/Art/Models/CardQuad.asset";
        public const string LiveFiddlePath = "Assets/Art/Models/FiddleLive.asset";
        public const string MaterialDir = "Assets/Art/Materials";

        static readonly string[] Textures =
        {
            "condensation", "cork_side", "cork_top", "fern_albedo", "fern_emission", "halo", "mist",
            "moss_band", "moss_card", "moss_fuzz", "moss_tile", "moss_top", "ring", "soil_band", "spore"
        };

        [MenuItem("Garden VR/Terrarium/Build Jar")]
        public static void Run()
        {
            try
            {
                ConfigureTextures();
                ConfigureModel();
                var library = CreateLibrary();
                var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                if (modelPrefab == null) throw new InvalidOperationException("model not imported: " + ModelPath);
                LogSourceMeshes(modelPrefab);
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
                bool alpha = name == "fern_albedo" || name == "moss_card" || name == "moss_fuzz" || name == "mist";
                bool linear = name == "condensation" || name == "ring" || name == "fern_emission";
                bool repeat = name.IndexOf("band", StringComparison.Ordinal) >= 0
                    || name.IndexOf("side", StringComparison.Ordinal) >= 0
                    || name == "condensation";
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = !linear;
                importer.alphaIsTransparency = alpha;
                importer.mipmapEnabled = true;
                importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
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

        static void LogSourceMeshes(GameObject modelPrefab)
        {
            int fiddleVerts = -1;
            var filters = modelPrefab.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i].sharedMesh;
                int tris = mesh != null ? mesh.triangles.Length / 3 : 0;
                int verts = mesh != null ? mesh.vertexCount : 0;
                Debug.Log("[JarSetup] source " + filters[i].name + " verts=" + verts + " tris=" + tris
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

            var library = new JarLibrary { Quad = SaveQuad() };
            library.Glass = Mat("Jar_Glass", "Fidelity/Glass", m =>
            {
                m.SetTexture("_Cond", Tex("condensation"));
                m.SetColor("_Tint", new Color(0.03f, 0.08f, 0.07f, 0.02f));
                m.SetColor("_Rim", new Color(0.45f, 0.95f, 0.78f, 0.6f));
                m.SetFloat("_RimPower", 3.0f);
                m.SetColor("_Inner", new Color(0.05f, 0.22f, 0.15f));
                m.SetVector("_InnerY", new Vector4(0.012f, 0.025f, 0f, 0f));
                m.SetFloat("_Drops", 0.45f);
                m.SetColor("_Streak", new Color(0.75f, 1f, 0.92f, 0.18f));
                m.SetShaderPassEnabled("SRPDefaultUnlit", false);
            });
            library.Moss = Mat("Jar_Moss", "Fidelity/Glow", m =>
            {
                m.SetTexture("_MainTex", Tex("moss_band"));
                m.SetColor("_Tint", Color.white);
                m.SetTexture("_EmissionTex", Tex("moss_band"));
                m.SetColor("_Emission", new Color(0.05f, 0.22f, 0.10f));
                m.SetColor("_Rim", new Color(0.20f, 0.60f, 0.28f));
                m.SetFloat("_RimPower", 2.2f);
                m.SetFloat("_GradBottom", 0.75f);
                m.SetFloat("_GradTop", 1.08f);
                m.SetVector("_GradY", new Vector4(0.03f, 0.048f, 0f, 0f));
                m.SetTexture("_TopTex", Tex("moss_band"));
                m.SetFloat("_TopTile", 20f);
                m.SetFloat("_TopAmount", 1f);
                m.SetFloat("_Tri", 0f);
            });
            library.Soil = Mat("Jar_Soil", "Fidelity/Glow", m =>
            {
                m.SetTexture("_MainTex", Tex("soil_band"));
                m.SetColor("_Tint", Color.white);
                m.SetTexture("_EmissionTex", Tex("soil_band"));
                m.SetColor("_Emission", new Color(0.03f, 0.18f, 0.10f));
                m.SetColor("_Rim", new Color(0.05f, 0.25f, 0.18f));
                m.SetFloat("_RimPower", 3f);
                m.SetFloat("_GradBottom", 0.9f);
                m.SetFloat("_GradTop", 1.1f);
                m.SetVector("_GradY", new Vector4(0f, 0.03f, 0f, 0f));
            });
            library.Cork = Mat("Jar_Cork", "Fidelity/Glow", m =>
            {
                m.SetTexture("_MainTex", Tex("cork_side"));
                m.SetColor("_Tint", new Color(0.92f, 0.84f, 0.76f));
                m.SetTexture("_EmissionTex", Texture2D.whiteTexture);
                m.SetColor("_Emission", Color.black);
                m.SetColor("_Rim", new Color(0.06f, 0.05f, 0.04f));
                m.SetFloat("_RimPower", 3f);
                m.SetFloat("_GradBottom", 0.75f);
                m.SetFloat("_GradTop", 1.05f);
                m.SetVector("_GradY", new Vector4(0.118f, 0.14f, 0f, 0f));
                m.SetFloat("_Tri", 30f);
                m.SetTexture("_TopTex", Tex("cork_top"));
                m.SetFloat("_TopTile", 9f);
                m.SetFloat("_TopAmount", 1f);
            });
            Material Fern(string materialName, float emissionScale) => Mat(materialName, "Fidelity/Glow", m =>
            {
                m.SetTexture("_MainTex", Tex("fern_albedo"));
                m.SetColor("_Tint", new Color(0.80f, 1.05f, 0.82f));
                m.SetTexture("_EmissionTex", Tex("fern_emission"));
                m.SetColor("_Emission", new Color(0.35f, 1.0f, 0.62f) * emissionScale);
                m.SetColor("_Rim", Color.black);
                m.SetFloat("_Cutoff", 0.5f);
                m.SetFloat("_GradBottom", 0.85f);
                m.SetFloat("_GradTop", 1.0f);
                m.SetVector("_GradY", new Vector4(0.04f, 0.1f, 0f, 0f));
            });
            library.Fern = Fern("Jar_Fern", 0.75f);
            library.FernNew = Fern("Jar_FernNew", 1.0f);
            library.Fiddle = Mat("Jar_Fiddle", "Fidelity/Glow", m =>
            {
                m.SetTexture("_MainTex", Texture2D.whiteTexture);
                m.SetColor("_Tint", new Color(0.55f, 0.85f, 0.30f));
                m.SetTexture("_EmissionTex", Texture2D.whiteTexture);
                m.SetColor("_Emission", new Color(0.30f, 0.65f, 0.22f));
                m.SetColor("_Rim", new Color(0.55f, 1.0f, 0.55f));
                m.SetFloat("_RimPower", 1.6f);
                m.SetFloat("_GradBottom", 0.8f);
                m.SetFloat("_GradTop", 1.15f);
                m.SetVector("_GradY", new Vector4(0.05f, 0.11f, 0f, 0f));
            });
            library.Seedling = Mat("Jar_Seedling", "Fidelity/Glow", m =>
            {
                m.SetTexture("_MainTex", Texture2D.whiteTexture);
                m.SetColor("_Tint", new Color(0.30f, 0.62f, 0.25f));
                m.SetTexture("_EmissionTex", Texture2D.whiteTexture);
                m.SetColor("_Emission", new Color(0.10f, 0.35f, 0.12f));
                m.SetColor("_Rim", new Color(0.40f, 0.95f, 0.45f));
                m.SetFloat("_RimPower", 2.0f);
            });
            library.Dew = Mat("Jar_Dew", "Fidelity/Glow", m =>
            {
                m.SetTexture("_MainTex", Texture2D.whiteTexture);
                m.SetColor("_Tint", new Color(0.6f, 1f, 0.85f));
                m.SetTexture("_EmissionTex", Texture2D.whiteTexture);
                m.SetColor("_Emission", new Color(0.5f, 1f, 0.8f));
                m.SetColor("_Rim", new Color(0.2f, 0.6f, 0.5f));
                m.SetFloat("_RimPower", 1.5f);
            });
            library.Ring = Mat("Jar_Ring", "Fidelity/Card", m =>
            {
                m.SetTexture("_MainTex", Tex("ring"));
                m.SetColor("_Color", new Color(0.75f, 1.35f, 1.05f));
                m.SetColor("_Color2", new Color(0.03f, 0.15f, 0.09f));
                m.SetFloat("_Src", (float)BlendMode.One);
                m.SetFloat("_Dst", (float)BlendMode.One);
                m.SetFloat("_Ring", 1f);
                m.SetFloat("_Fill", 0.5f);
                m.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
                m.renderQueue = 2990;
            });
            library.Spill = CardMat("Jar_Spill", Tex("halo"), new Color(0.05f, 0.20f, 0.13f), Color.black, false, -11);
            library.JarHalo = CardMat("Jar_Halo", Tex("halo"), new Color(0.05f, 0.22f, 0.15f), Color.black, false, 20);
            library.CoilHalo = CardMat("Jar_CoilHalo", Tex("halo"), new Color(0.30f, 0.85f, 0.45f) * 0.55f, Color.black, false, 21);
            library.Mist = CardMat("Jar_Mist", Tex("mist"), new Color(0.72f, 0.86f, 0.84f, 0.8f), Color.black, true, 30);
            library.Spore = CardMat("Jar_Spore", Tex("spore"), new Color(1.6f, 1.3f, 0.6f), Color.black, false, 40);
            return library;
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
