#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using GardenVR.Capture;
using GardenVR.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GardenVR.Sundial.Editor
{
    /// <summary>
    /// Imports the drawn dial, builds <c>Assets/Prefabs/Dial.prefab</c>, and parents it under <c>DialRoot</c>.
    /// Idempotent. Does not rebuild the scene. Batch: -executeMethod GardenVR.Sundial.Editor.DialSetup.Run
    /// The boil reel is <c>DialSetup.BoilSequence</c> (needs a GPU, so no -nographics).
    /// </summary>
    public static class DialSetup
    {
        public const string ModelPath = "Assets/Art/Models/drawn_dial.fbx";
        public const string TilePath = "Assets/Art/Models/Tile.fbx";
        public const string PrefabPath = "Assets/Prefabs/Dial.prefab";
        public const string MaterialDir = "Assets/Art/Materials";
        public const string CardPath = "Assets/Art/Models/DialCard.asset";
        public const string ShadowPath = "Assets/Art/Models/DialShadow.asset";
        public const string CatcherPath = "Assets/Art/Models/DialCatcher.asset";
        public const string TilesLivePath = "Assets/Art/Models/TilesLive.asset";

        const int FrameWidth = 1824;
        const int FrameHeight = 1024;

        static readonly string[] TextureNames =
        {
            "dial_face", "soil", "dial_paper", "gnomon", "gnomon_shadow",
            "plant_sunrise_0", "plant_sunrise_1", "plant_sunrise_2",
            "plant_midday_0", "plant_midday_1", "plant_midday_2",
            "plant_dusk_0", "plant_dusk_1", "plant_dusk_2",
            "halo_sunrise", "halo_midday", "halo_dusk"
        };

        [MenuItem("Garden VR/Sundial/Build Dial")]
        public static void Run()
        {
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                AssetDatabase.Refresh();
                ConfigureTextures();
                ConfigureModel(ModelPath);
                ConfigureModel(TilePath);
                var library = CreateLibrary();
                var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                if (modelPrefab == null) throw new InvalidOperationException("model not imported: " + ModelPath);
                LogModel(modelPrefab);
                Mesh tileMesh = LoadTileMesh();
                LogMesh(tileMesh, "Tile");
                BuildPrefab(modelPrefab, tileMesh, library);
                PlaceUnderDialRoot();
                SceneSetup.FixSeatedView();
                AssetDatabase.SaveAssets();
                Debug.Log("[DialSetup] done prefab=" + PrefabPath + " scene=" + SceneSetup.ScenePath);
            }
            catch (Exception e)
            {
                Debug.LogError("[DialSetup] FAIL " + e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>30 frames at DialG1, 10 fps, three seconds. Writes boil/f0000.png .. f0029.png.</summary>
        [MenuItem("Garden VR/Sundial/Boil Sequence")]
        public static void BoilSequence()
        {
            try
            {
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                    throw new InvalidOperationException("no graphics device; run BoilSequence without -nographics");
                ShaderUtil.allowAsyncCompilation = false;
                EditorSceneManager.OpenScene(SceneSetup.ScenePath, OpenSceneMode.Single);
                DialView view = UnityEngine.Object.FindAnyObjectByType<DialView>();
                if (view == null) throw new InvalidOperationException("DialView missing from " + SceneSetup.ScenePath);
                Transform dialRoot = FindDialRoot();
                DisableWorldPlates();

                Camera cam = CreateDialCamera(dialRoot);
                Texture2D plate = LoadPlate(Path.Combine(Application.dataPath, "Art", "Plates", "plate-dial.png"));
                AttachPlate(cam, plate);
                Warm(view);

                string dir = Path.Combine(RunDir(), "boil");
                Directory.CreateDirectory(dir);
                UnityEngine.Object.DestroyImmediate(FrameGrab.RenderToTexture(cam, 320, 180, 1));
                for (int frame = 0; frame < 30; frame++)
                {
                    view.halo = 1f;
                    view.haloTarget = "midday";
                    view.gnomonDeg = 105f;
                    view.boil = true;
                    view.time = frame / 10f;
                    view.Apply();
                    Texture2D shot = FrameGrab.RenderToTexture(cam, FrameWidth, FrameHeight, 4);
                    File.WriteAllBytes(Path.Combine(dir, "f" + frame.ToString("0000") + ".png"), shot.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(shot);
                }
                UnityEngine.Object.DestroyImmediate(plate);
                Debug.Log("[DialSetup] boil frames 30 dir=" + dir);
            }
            catch (Exception e)
            {
                Debug.LogError("[DialSetup] FAIL " + e);
                EditorApplication.Exit(1);
            }
        }

        static string RunDir()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "orchestration", "runs", "sundial", "T-SUN-005"));
        }

        static void ConfigureTextures()
        {
            foreach (string name in TextureNames)
            {
                string path = "Assets/Art/Textures/" + name + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("texture missing: " + path);
                bool alpha = name.StartsWith("plant_", StringComparison.Ordinal)
                    || name.StartsWith("halo_", StringComparison.Ordinal)
                    || name == "gnomon_shadow";
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = alpha;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = name == "dial_face" || name == "gnomon_shadow" ? 2048 : 1024;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
        }

        static void ConfigureModel(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("model missing: " + path);
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
            importer.isReadable = true;
            importer.generateSecondaryUV = false;
            importer.swapUVChannels = false;
            importer.optimizeMeshPolygons = false;
            importer.optimizeMeshVertices = false;
            importer.SaveAndReimport();
        }

        static void LogModel(GameObject modelPrefab)
        {
            var filters = modelPrefab.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0) throw new InvalidOperationException("drawn dial has no meshes");
            for (int i = 0; i < filters.Length; i++)
                LogMesh(filters[i].sharedMesh, filters[i].name);
        }

        static Mesh LoadTileMesh()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(TilePath);
            for (int i = 0; i < assets.Length; i++)
            {
                var mesh = assets[i] as Mesh;
                if (mesh != null) return mesh;
            }
            throw new InvalidOperationException("tile mesh missing in " + TilePath);
        }

        static void LogMesh(Mesh mesh, string name)
        {
            if (mesh == null) throw new InvalidOperationException("missing mesh " + name);
            var uv1 = new List<Vector2>();
            var uv2 = new List<Vector2>();
            mesh.GetUVs(1, uv1);
            mesh.GetUVs(2, uv2);
            int colors = mesh.colors != null ? mesh.colors.Length : 0;
            if (colors == 0 && mesh.colors32 != null) colors = mesh.colors32.Length;
            int tris = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) tris += (int)(mesh.GetIndexCount(s) / 3);
            Debug.Log("[DialSetup] mesh " + name
                + " verts=" + mesh.vertexCount
                + " tris=" + tris
                + " colors=" + colors
                + " uv0=" + (mesh.uv != null ? mesh.uv.Length : 0)
                + " uv1=" + uv1.Count
                + " uv2=" + uv2.Count
                + " readable=" + mesh.isReadable);
            if (!mesh.isReadable) throw new InvalidOperationException(name + " is not readable");
            if (uv1.Count != mesh.vertexCount || uv2.Count != mesh.vertexCount)
                throw new InvalidOperationException(name + " is missing outline UV channels (Nxy, Nz)");
        }

        static DialLibrary CreateLibrary()
        {
            EnsureFolder("Assets/Art");
            EnsureFolder("Assets/Art/Models");
            EnsureFolder(MaterialDir);
            EnsureFolder("Assets/Prefabs");
            Texture2D Tex(string name)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/" + name + ".png");
                if (tex == null) throw new InvalidOperationException("texture not loaded: " + name);
                return tex;
            }

            var library = new DialLibrary
            {
                Card = SaveCard(),
                ShadowMesh = SaveShadow(),
                CatcherQuad = SaveCatcher(),
                MorningCards = new[] { Tex("plant_sunrise_0"), Tex("plant_sunrise_1"), Tex("plant_sunrise_2") },
                MiddayCards = new[] { Tex("plant_midday_0"), Tex("plant_midday_1"), Tex("plant_midday_2") },
                WindDownCards = new[] { Tex("plant_dusk_0"), Tex("plant_dusk_1"), Tex("plant_dusk_2") },
                Halos = new[] { Tex("halo_sunrise"), Tex("halo_midday"), Tex("halo_dusk") }
            };

            // The painted face already has its ink ring. Keep it one pass (_Outline 0 disables the hull count).
            library.Face = Toon("Dial_Face", Tex("dial_face"), false, 0f, -0.99f, 0.04f, new Color(1.05f, 1.08f, 1.18f), new Color(1.05f, 1.08f, 1.18f));
            library.Rim = Toon("Dial_Rim", Tex("dial_paper"), true, 3.0f, 0.12f, 0.07f, Color.white, new Color(0.78f, 0.72f, 0.64f));
            library.Soil = Toon("Dial_Soil", Tex("dial_face"), true, 2.8f, 0.05f, 0.05f, Color.white, new Color(0.78f, 0.72f, 0.64f));
            library.Gnomon = Toon("Dial_Gnomon", Tex("gnomon"), true, 2.6f, 0.18f, 0.03f, Color.white, new Color(0.55f, 0.48f, 0.40f));
            library.Gnomon.SetColor("_Spec", new Color(0.35f, 0.28f, 0.12f, 1f));
            library.Gnomon.SetFloat("_SpecStep", 0.9f);
            library.Tiles = Toon("Dial_Tile", Texture2D.whiteTexture, true, 2.2f, 0.2f, 0.04f, Color.white, new Color(0.78f, 0.72f, 0.64f));
            library.Tiles.SetFloat("_TileMode", 1f);
            library.Tiles.SetFloat("_BoilPx", 0.8f);

            library.Morning = Plant("Dial_PlantMorning", library.MorningCards[2]);
            library.Midday = Plant("Dial_PlantMidday", library.MiddayCards[2]);
            library.WindDown = Plant("Dial_PlantWindDown", library.WindDownCards[1]);
            library.Halo = Mat("Dial_Halo", "Fidelity/Card", m =>
            {
                m.SetTexture("_MainTex", library.Halos[1]);
                m.SetColor("_Color", new Color(1.6f, 1.25f, 0.55f));
                m.SetColor("_Color2", new Color(0.85f, 0.62f, 0.28f));
                m.SetFloat("_Src", (float)BlendMode.One);
                m.SetFloat("_Dst", (float)BlendMode.One);
                m.SetFloat("_Boil", 0f);
                m.SetFloat("_BoilPx", DialView.BoilPixels);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
                m.SetFloat("_Coverage", 0f);
                m.SetFloat("_Mask", 1f);
                m.SetFloat("_Ring", 0f);
                m.renderQueue = 3008;
            });
            library.Catcher = Mat("Dial_Catcher", "Fidelity/ShadowCatcher", m =>
            {
                m.SetColor("_ShadowColor", new Color(0.55f, 0.46f, 0.40f, 1f));
                m.SetFloat("_Fade", 0.16f);
                m.SetFloat("_Contact", 0.55f);
                // Transparent queue so DstColor Zero multiplies. The geometry queue writes the colour opaque.
                m.renderQueue = 2501;
            });
            library.Shadow = Mat("Dial_GnomonShadow", "Fidelity/Card", m =>
            {
                m.SetTexture("_MainTex", Tex("gnomon_shadow"));
                // Colour and falloff live in the SVG. A dark multiply here hid the wash on the soil.
                m.SetColor("_Color", Color.white);
                m.SetColor("_Color2", Color.black);
                m.SetFloat("_Src", (float)BlendMode.SrcAlpha);
                m.SetFloat("_Dst", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_Boil", 0f);
                m.SetFloat("_BoilPx", 0f);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
                m.SetFloat("_Coverage", 0f);
                m.SetFloat("_Mask", 0f);
                m.SetFloat("_Ring", 0f);
                m.renderQueue = 2440;
            });
            return library;
        }

        static Material Toon(string name, Texture tex, bool hull, float outlinePx, float step, float grain, Color lit, Color shade)
        {
            return Mat(name, "Fidelity/Toon", m =>
            {
                m.SetTexture("_MainTex", tex);
                m.SetColor("_Lit", lit);
                m.SetColor("_Shade", shade);
                m.SetFloat("_Step", step);
                m.SetFloat("_Feather", 0.04f);
                m.SetFloat("_Grain", grain);
                m.SetFloat("_ShadowStrength", 0f);
                m.SetFloat("_Outline", hull ? 0.001f : 0f);
                m.SetFloat("_OutlinePx", hull ? outlinePx : 0f);
                m.SetFloat("_Boil", 0f);
                m.SetFloat("_BoilPx", hull ? DialView.BoilPixels : 0f);
                m.SetFloat("_BoilTime", -1f);
                m.SetFloat("_TileMode", 0f);
                m.SetColor("_Ink", new Color(0.165f, 0.149f, 0.133f, 1f));
                m.SetShaderPassEnabled("SRPDefaultUnlit", hull);
            });
        }

        static Material Plant(string name, Texture2D tex)
        {
            return Mat(name, "Fidelity/Card", m =>
            {
                m.SetTexture("_MainTex", tex);
                m.SetColor("_Color", Color.white);
                m.SetColor("_Color2", Color.black);
                m.SetFloat("_Src", (float)BlendMode.One);
                m.SetFloat("_Dst", (float)BlendMode.Zero);
                m.SetFloat("_Boil", 0f);
                m.SetFloat("_BoilPx", DialView.BoilPixels);
                m.SetFloat("_ZWrite", 1f);
                m.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
                m.SetFloat("_Coverage", 1f);
                m.SetFloat("_Mask", 0f);
                m.SetFloat("_Ring", 0f);
                m.renderQueue = 2450;
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

        static Mesh SaveCard()
        {
            return SaveMesh(CardPath, "DialCard", mesh =>
            {
                const int rows = 6;
                int count = (rows + 1) * 2;
                var verts = new Vector3[count];
                var uv = new Vector2[count];
                var colors = new Color[count];
                var tris = new int[rows * 6];
                for (int i = 0; i <= rows; i++)
                {
                    float y = i / (float)rows;
                    float z = Mathf.Sin(y * Mathf.PI) * 0.03f;
                    verts[i * 2] = new Vector3(-0.5f, y, z);
                    verts[i * 2 + 1] = new Vector3(0.5f, y, z);
                    uv[i * 2] = new Vector2(0f, y);
                    uv[i * 2 + 1] = new Vector2(1f, y);
                    colors[i * 2] = Color.white;
                    colors[i * 2 + 1] = Color.white;
                }
                int t = 0;
                for (int i = 0; i < rows; i++)
                {
                    int a = i * 2;
                    tris[t++] = a;
                    tris[t++] = a + 2;
                    tris[t++] = a + 1;
                    tris[t++] = a + 1;
                    tris[t++] = a + 2;
                    tris[t++] = a + 3;
                }
                mesh.vertices = verts;
                mesh.uv = uv;
                mesh.colors = colors;
                mesh.triangles = tris;
                mesh.RecalculateBounds();
            });
        }

        static Mesh SaveShadow()
        {
            return SaveMesh(ShadowPath, "DialShadow", mesh =>
            {
                // Wedge: narrow at the gnomon, wide and soft at the far end of the wash.
                // Local +Z is the shadow direction. UV v grows away from the nib.
                mesh.vertices = new[]
                {
                    new Vector3(-0.016f, 0f, 0.012f), new Vector3(0.016f, 0f, 0.012f),
                    new Vector3(0.078f, 0f, 0.168f), new Vector3(-0.078f, 0f, 0.168f)
                };
                mesh.uv = new[] { new Vector2(0.38f, 0f), new Vector2(0.62f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
                mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            });
        }

        static Mesh SaveCatcher()
        {
            return SaveMesh(CatcherPath, "DialCatcher", mesh =>
            {
                mesh.vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
                };
                mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
                mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            });
        }

        static Mesh SaveMesh(string path, string name, Action<Mesh> fill)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }
            mesh.Clear();
            fill(mesh);
            mesh.name = name;
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static void BuildPrefab(GameObject modelPrefab, Mesh tileMesh, DialLibrary library)
        {
            var root = new GameObject("Dial");
            try
            {
                var view = root.AddComponent<DialView>();
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
                if (model == null) throw new InvalidOperationException("could not instance " + ModelPath);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                view.Build(model, tileMesh, library);
                PersistTiles(view);
                view.Apply();
                ExpectTargets(view);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (saved == null) throw new InvalidOperationException("could not save " + PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void PersistTiles(DialView view)
        {
            Mesh live = view.BuiltTileMesh;
            if (live == null) throw new InvalidOperationException("combined tile mesh was not built");
            var uv3 = new List<Vector2>();
            live.GetUVs(3, uv3);
            Debug.Log("[DialSetup] tiles combined verts=" + live.vertexCount
                + " tris=" + (live.triangles.Length / 3)
                + " uv3=" + uv3.Count
                + " colors=" + (live.colors != null ? live.colors.Length : 0));
            if (uv3.Count != live.vertexCount) throw new InvalidOperationException("combined tiles are missing the tile index UV");
            if (AssetDatabase.LoadAssetAtPath<Mesh>(TilesLivePath) != null) AssetDatabase.DeleteAsset(TilesLivePath);
            var copy = UnityEngine.Object.Instantiate(live);
            copy.name = "TilesLive";
            AssetDatabase.CreateAsset(copy, TilesLivePath);
            view.tileRenderer.GetComponent<MeshFilter>().sharedMesh = copy;
        }

        static void ExpectTargets(DialView view)
        {
            var ids = new HashSet<string>();
            IntentTarget[] targets = view.GetComponentsInChildren<IntentTarget>(true);
            for (int i = 0; i < targets.Length; i++) ids.Add(targets[i].Id);
            string[] arcs = { "morning", "midday", "winddown" };
            for (int a = 0; a < arcs.Length; a++)
            {
                string plant = "plant." + arcs[a];
                if (!ids.Contains(plant)) throw new InvalidOperationException("missing IntentTarget " + plant);
                for (int slot = 0; slot < DialView.TilesPerArc; slot++)
                {
                    string tile = "tile." + arcs[a] + "." + slot;
                    if (!ids.Contains(tile)) throw new InvalidOperationException("missing IntentTarget " + tile);
                }
            }
        }

        static void PlaceUnderDialRoot()
        {
            var scene = EditorSceneManager.OpenScene(SceneSetup.ScenePath, OpenSceneMode.Single);
            Transform dialRoot = FindDialRoot();
            for (int i = dialRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = dialRoot.GetChild(i);
                if (child.name == "Dial") UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException("prefab missing: " + PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance == null) throw new InvalidOperationException("could not instance " + PrefabPath);
            instance.name = "Dial";
            instance.transform.SetParent(dialRoot, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            if (dialRoot.GetComponent<IntentTarget>() == null || dialRoot.GetComponent<IntentTarget>().Id != SceneSetup.DialId)
                throw new InvalidOperationException("DialRoot lost its dial IntentTarget");
            // The desk intro draws this bar at runtime. Its prefab scale is the finished line, so an
            // edit-mode capture paints a mint strip across the near rim. Park it until play mode.
            GameObject trace = GameObject.Find("DeskTrace");
            if (trace != null)
                trace.transform.localScale = new Vector3(0.0001f, trace.transform.localScale.y, 1f);
            EditorSceneManager.SaveScene(scene);
        }

        static Transform FindDialRoot()
        {
            IntentTarget[] targets = UnityEngine.Object.FindObjectsByType<IntentTarget>(FindObjectsInactive.Include);
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null && targets[i].Id == SceneSetup.DialId) return targets[i].transform;
            }
            throw new InvalidOperationException("DialRoot (IntentTarget dial) is missing. DialSetup does not rebuild the scene.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static void DisableWorldPlates()
        {
            Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null) continue;
                if (t.name == "PcRoomPlate" || t.name == "PassthroughPlate") t.gameObject.SetActive(false);
            }
        }

        static Camera CreateDialCamera(Transform dialRoot)
        {
            var go = new GameObject("CaptureCamera");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = false;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 50f;
            cam.fieldOfView = SceneSetup.DialG1Fov;
            cam.allowHDR = false;
            cam.allowMSAA = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.aspect = FrameWidth / (float)FrameHeight;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = false;
            Vector3 eye = dialRoot.TransformPoint(SceneSetup.DialG1Eye);
            Vector3 look = dialRoot.TransformPoint(SceneSetup.DialG1Look);
            cam.transform.rotation = Quaternion.LookRotation(look - eye, Vector3.up) * Quaternion.Euler(-SceneSetup.DialG1LensShift.y, SceneSetup.DialG1LensShift.x, 0f);
            cam.transform.position = eye;
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
            for (int i = 0; i < cameras.Length; i++)
                if (cameras[i] != cam) cameras[i].enabled = false;
            return cam;
        }

        static void AttachPlate(Camera cam, Texture2D plate)
        {
            Shader shader = Shader.Find("Fidelity/Plate");
            if (shader == null) throw new InvalidOperationException("shader not found: Fidelity/Plate");
            var mat = new Material(shader);
            mat.SetTexture("_MainTex", plate);
            mat.SetFloat("_Exposure", 1f);
            ShaderUtil.CompilePass(mat, 0, true);
            const float d = 2f;
            float worldHeight = 2f * d * Mathf.Tan(SceneSetup.DialG1Fov * 0.5f * Mathf.Deg2Rad);
            float worldWidth = worldHeight * (FrameWidth / (float)FrameHeight);
            var go = new GameObject("CapturePlate");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, d);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(worldWidth, worldHeight, 1f);
            var mesh = new Mesh { name = "BoilPlate" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static Texture2D LoadPlate(string path)
        {
            if (!File.Exists(path)) throw new InvalidOperationException("plate not found: " + path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!tex.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("could not read plate: " + path);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        static void Warm(DialView view)
        {
            var mats = new List<Material>();
            if (view.boilMats != null) mats.AddRange(view.boilMats);
            Renderer[] renderers = view.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material mat = renderers[i].sharedMaterial;
                if (mat != null && !mats.Contains(mat)) mats.Add(mat);
            }
            for (int i = 0; i < mats.Count; i++)
            {
                Material mat = mats[i];
                if (mat == null || mat.shader == null) continue;
                for (int pass = 0; pass < mat.shader.passCount; pass++)
                    ShaderUtil.CompilePass(mat, pass, true);
            }
        }
    }
}
#endif
