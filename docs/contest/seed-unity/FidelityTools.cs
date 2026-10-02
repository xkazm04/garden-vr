using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Fidelity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

/// <summary>
/// Round 3, the fidelity gate. Batchmode entry points (no editor window):
///   FidelityTools.Prepare   texture/model import settings, materials
///   FidelityTools.CaptureAll stills at the owner's reference framing + short frame sequences
///   FidelityTools.Measure    draw calls, batches, triangles, texture memory, transparent overdraw, GPU time proxy
///   FidelityTools.MakeScenes the two hero scenes for the headset gallery build (no plate: passthrough instead)
/// Output: C:\hgspike\fidelity\renders and ...\logs
/// </summary>
public static class FidelityTools
{
    const string Root = "Assets/Fidelity";
    static string FidDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../fidelity"));
    static string Renders => Path.Combine(FidDir, "renders");
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ------------------------------------------------------------------ import
    public static void Prepare()
    {
        AssetDatabase.Refresh();
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures", Root + "/Plates" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            bool plate = path.Contains("/Plates/");
            bool alpha = path.Contains("fern_albedo") || path.Contains("plant_");
            bool linear = path.Contains("condensation") || path.Contains("ring") || path.Contains("halo_line") || path.Contains("fern_emission");
            ti.sRGBTexture = !linear;
            ti.alphaIsTransparency = alpha;
            ti.mipmapEnabled = !plate;
            ti.wrapMode = path.Contains("_band") || path.Contains("_side") || path.Contains("condensation") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.maxTextureSize = plate ? 2048 : 2048;
            ti.textureCompression = plate ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            ti.anisoLevel = path.Contains("dial_top") ? 4 : 1;
            // what Quest would get: ASTC 6x6 for everything we ship (the plate never ships: passthrough replaces it)
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = !plate, maxTextureSize = path.Contains("dial_top") || path.Contains("fern_") ? 1024 : 512, format = TextureImporterFormat.ASTC_6x6 });
            ti.SaveAndReimport();
        }
        foreach (var f in new[] { "night_jar.fbx", "drawn_dial.fbx" })
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath($"{Root}/Models/{f}");
            mi.importBlendShapes = true; mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importAnimation = false; mi.importCameras = false; mi.importLights = false;
            mi.globalScale = 1; mi.useFileScale = true; mi.bakeAxisConversion = true;
            mi.importNormals = ModelImporterNormals.Calculate; mi.normalSmoothingAngle = 70; mi.isReadable = true;   // recompute normals after the axis bake
            mi.SaveAndReimport();
        }
        Debug.Log("[Fid] Prepare done");
    }

    static T Load<T>(string p) where T : Object { var o = AssetDatabase.LoadAssetAtPath<T>(p); if (!o) Debug.LogError("[Fid] missing " + p); return o; }
    static Texture2D Tex(string n) => Load<Texture2D>($"{Root}/Textures/{n}.png");

    static Material Mat(string name, string shader, Action<Material> init)
    {
        Directory.CreateDirectory(Root + "/Materials");
        var path = $"{Root}/Materials/{name}.mat";
        var sh = Shader.Find(shader); if (!sh) Debug.LogError("[Fid] shader not found " + shader);
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh; init(m); EditorUtility.SetDirty(m); return m;
    }

    static Material CardMat(string name, Texture2D tex, Color c, Color c2, bool alpha = false, float boil = 0, int queueOffset = 0) => Mat(name, "Fidelity/Card", m =>
    {
        m.SetTexture("_MainTex", tex); m.SetColor("_Color", c); m.SetColor("_Color2", c2);
        m.SetFloat("_Src", alpha ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
        m.SetFloat("_Dst", alpha ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.One);
        m.SetFloat("_Boil", boil); m.SetFloat("_Ring", 0); m.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
        m.renderQueue = 3000 + queueOffset;
    });

    static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

    static Mesh _quad;
    static Mesh Quad(bool pivotBottom = false)
    {
        var m = new Mesh { name = pivotBottom ? "QuadBottom" : "Quad" };
        float y0 = pivotBottom ? 0 : -0.5f, y1 = pivotBottom ? 1 : 0.5f;
        m.vertices = new[] { new Vector3(-0.5f, y0, 0), new Vector3(0.5f, y0, 0), new Vector3(0.5f, y1, 0), new Vector3(-0.5f, y1, 0) };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        m.colors = Enumerable.Repeat(Color.white, 4).ToArray();
        m.triangles = new[] { 0, 2, 1, 0, 3, 2 }; m.RecalculateBounds(); return m;
    }

    static GameObject Card(string name, Transform parent, Material mat, Vector3 pos, Vector2 size, bool pivotBottom = false, Quaternion? rot = null)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = Quad(pivotBottom);
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        go.transform.localPosition = pos; go.transform.localScale = new Vector3(size.x, size.y, 1);
        if (rot.HasValue) go.transform.localRotation = rot.Value;
        return go;
    }

    static void SetMat(Transform t, Material m, ShadowCastingMode cast = ShadowCastingMode.Off)
    {
        var r = t.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = cast; r.receiveShadows = cast != ShadowCastingMode.Off;
    }

    // ------------------------------------------------------------------ camera + plate
    static Camera MakeCamera(Vector3 pos, Vector3 target, float vfov, Vector2 screenOffsetDeg, Texture2D plate, float plateExposure = 1f)
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
        cam.nearClipPlane = 0.02f; cam.farClipPlane = 5f; cam.fieldOfView = vfov; cam.allowHDR = false; cam.allowMSAA = true;
        go.transform.position = pos; go.transform.LookAt(target);
        go.transform.rotation = go.transform.rotation * Quaternion.Euler(-screenOffsetDeg.y, screenOffsetDeg.x, 0);
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None; data.renderShadows = true;
        if (plate)
        {
            const float d = 2.0f; float h = 2 * d * Mathf.Tan(vfov * 0.5f * Mathf.Deg2Rad); float w = h * 1824f / 1024f;
            var pm = Mat("PlateMat_" + plate.name, "Fidelity/Plate", m => { m.SetTexture("_MainTex", plate); m.SetFloat("_Exposure", plateExposure); });
            var pg = Card("PassthroughPlate", go.transform, pm, new Vector3(0, 0, d), new Vector2(w, h));
        }
        return cam;
    }

    // ------------------------------------------------------------------ hero 1: the night jar
    public static FidelityHero BuildJar()
    {
        var root = new GameObject("NightJar"); var hero = root.AddComponent<FidelityHero>(); hero.kind = FidelityHero.Kind.NightJar;
        var model = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>($"{Root}/Models/night_jar.fbx"));
        model.transform.SetParent(root.transform, false);
        var b = new Bounds(); foreach (var r in model.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        Debug.Log($"[Fid] jar model bounds {b}");

        var mint = new Color(0.30f, 1.0f, 0.62f);
        var glass = Mat("Jar_Glass", "Fidelity/Glass", m =>
        {
            m.SetTexture("_Cond", Tex("condensation")); m.SetColor("_Tint", new Color(0.03f, 0.08f, 0.07f, 0.02f));
            m.SetColor("_Rim", new Color(0.45f, 0.95f, 0.78f, 0.6f)); m.SetFloat("_RimPower", 3.0f);
            m.SetColor("_Inner", new Color(0.05f, 0.22f, 0.15f)); m.SetVector("_InnerY", new Vector4(0.012f, 0.025f, 0, 0));
            m.SetFloat("_Drops", 0.45f); m.SetColor("_Streak", new Color(0.75f, 1f, 0.92f, 0.18f));
        });
        var moss = Mat("Jar_Moss", "Fidelity/Glow", m =>
        {
            m.SetTexture("_MainTex", Tex("moss_band")); m.SetColor("_Tint", Color.white); m.SetTexture("_EmissionTex", Tex("moss_band"));
            m.SetColor("_Emission", new Color(0.05f, 0.22f, 0.10f)); m.SetColor("_Rim", new Color(0.20f, 0.60f, 0.28f)); m.SetFloat("_RimPower", 2.2f);
            m.SetFloat("_GradBottom", 0.75f); m.SetFloat("_GradTop", 1.25f); m.SetVector("_GradY", new Vector4(0.03f, 0.048f, 0, 0));
            m.SetTexture("_TopTex", Tex("moss_band")); m.SetFloat("_TopTile", 9f); m.SetFloat("_TopAmount", 1f);
        });
        moss.SetFloat("_Tri", 0f); moss.SetTexture("_TopTex", Tex("moss_band")); moss.SetFloat("_TopTile", 20f); moss.SetFloat("_GradTop", 1.08f); moss.SetFloat("_TopAmount", 1f);
        var tuftMat = Mat("Jar_MossTuft", "Fidelity/Glow", m =>
        {
            m.SetTexture("_MainTex", Tex("moss_card")); m.SetColor("_Tint", new Color(0.95f, 1.05f, 0.95f)); m.SetTexture("_EmissionTex", Tex("moss_card"));
            m.SetColor("_Emission", new Color(0.05f, 0.24f, 0.10f)); m.SetColor("_Rim", Color.black); m.SetFloat("_Cutoff", 0.45f);
            m.SetFloat("_GradBottom", 0.75f); m.SetFloat("_GradTop", 1.25f); m.SetVector("_GradY", new Vector4(0.03f, 0.052f, 0, 0));
        });
        var soil = Mat("Jar_Soil", "Fidelity/Glow", m =>
        {
            m.SetTexture("_MainTex", Tex("soil_band")); m.SetColor("_Tint", Color.white); m.SetTexture("_EmissionTex", Tex("soil_band"));
            m.SetColor("_Emission", new Color(0.03f, 0.18f, 0.10f)); m.SetColor("_Rim", new Color(0.05f, 0.25f, 0.18f)); m.SetFloat("_RimPower", 3f);
            m.SetFloat("_GradBottom", 0.9f); m.SetFloat("_GradTop", 1.1f); m.SetVector("_GradY", new Vector4(0, 0.03f, 0, 0));
        });
        var cork = Mat("Jar_Cork", "Fidelity/Glow", m =>
        {
            m.SetTexture("_MainTex", Tex("cork_side")); m.SetColor("_Tint", new Color(0.92f, 0.84f, 0.76f)); m.SetTexture("_EmissionTex", Texture2D.whiteTexture);
            m.SetColor("_Emission", Color.black); m.SetColor("_Rim", new Color(0.06f, 0.05f, 0.04f)); m.SetFloat("_RimPower", 3f);
            m.SetFloat("_GradBottom", 0.75f); m.SetFloat("_GradTop", 1.05f); m.SetVector("_GradY", new Vector4(0.118f, 0.14f, 0, 0));
            m.SetFloat("_Tri", 30f); m.SetTexture("_TopTex", Tex("cork_top")); m.SetFloat("_TopTile", 9f); m.SetFloat("_TopAmount", 1f);   // triplanar cork sides, planar lit cork top
        });
        Material FernMat(string n, float emi) => Mat(n, "Fidelity/Glow", m =>
        {
            m.SetTexture("_MainTex", Tex("fern_albedo")); m.SetColor("_Tint", new Color(0.80f, 1.05f, 0.82f)); m.SetTexture("_EmissionTex", Tex("fern_emission"));
            m.SetColor("_Emission", new Color(0.35f, 1.0f, 0.62f) * emi); m.SetColor("_Rim", Color.black); m.SetFloat("_Cutoff", 0.5f);
            m.SetFloat("_GradBottom", 0.85f); m.SetFloat("_GradTop", 1.0f); m.SetVector("_GradY", new Vector4(0.04f, 0.1f, 0, 0));
        });
        var fern = FernMat("Jar_Fern", 0.75f); var fernNew = FernMat("Jar_FernNew", 1.0f);
        var stem = Mat("Jar_Fiddle", "Fidelity/Glow", m =>
        {
            m.SetTexture("_MainTex", Texture2D.whiteTexture); m.SetColor("_Tint", new Color(0.55f, 0.85f, 0.30f)); m.SetTexture("_EmissionTex", Texture2D.whiteTexture);
            m.SetColor("_Emission", new Color(0.30f, 0.65f, 0.22f)); m.SetColor("_Rim", new Color(0.55f, 1.0f, 0.55f)); m.SetFloat("_RimPower", 1.6f);
            m.SetFloat("_GradBottom", 0.8f); m.SetFloat("_GradTop", 1.15f); m.SetVector("_GradY", new Vector4(0.05f, 0.11f, 0, 0));
        });

        var mt = model.transform;
        SetMat(Find(mt, "Jar"), glass); SetMat(Find(mt, "Moss"), moss); SetMat(Find(mt, "Soil"), soil); SetMat(Find(mt, "Cork"), cork);
        var fid = Find(mt, "Fiddle0"); SetMat(fid, stem); hero.fiddle = fid.GetComponent<MeshFilter>();
        hero.fiddleStates = new[] { "Fiddle0", "Fiddle25", "Fiddle50", "Fiddle75", "Fiddle100" }.Select(n => Find(mt, n).GetComponent<MeshFilter>().sharedMesh).ToArray();
        foreach (var n in new[] { "Fiddle25", "Fiddle50", "Fiddle75", "Fiddle100" }) Find(mt, n).gameObject.SetActive(false);
        fid.localScale = Vector3.one * 0.9f; fid.localPosition += new Vector3(0.004f, 0.004f, 0);
        var seedMat = Mat("Jar_Seedling", "Fidelity/Glow", m =>
        {
            m.SetTexture("_MainTex", Texture2D.whiteTexture); m.SetColor("_Tint", new Color(0.30f, 0.62f, 0.25f)); m.SetTexture("_EmissionTex", Texture2D.whiteTexture);
            m.SetColor("_Emission", new Color(0.10f, 0.35f, 0.12f)); m.SetColor("_Rim", new Color(0.40f, 0.95f, 0.45f)); m.SetFloat("_RimPower", 2.0f);
        });
        var seed = Find(mt, "Seedling"); SetMat(seed, seedMat); var seedStem = Find(mt, "SeedStem"); SetMat(seedStem, stem);
        seed.localPosition = new Vector3(-0.009f, 0.045f, -0.012f); seedStem.localPosition = seed.localPosition; seed.localScale = seedStem.localScale = Vector3.one * 0.55f;
        var seed2 = Object.Instantiate(seed.gameObject, mt).transform; seed2.localPosition = new Vector3(0.010f, 0.044f, -0.012f); seed2.localRotation = Quaternion.Euler(0, 70, 0) * seed.localRotation; seed2.localScale = Vector3.one * 0.45f;
        // fronds: the authored folded card, posed like the owner's frame - one each side leaning out, facing the viewer
        var frond = Find(mt, "Frond"); SetMat(frond, fern);
        var axis = frond.localRotation;   // the importer's Z-up -> Y-up node rotation; pose on top of it
        frond.localPosition = new Vector3(-0.010f, 0.040f, 0.004f); frond.localRotation = Quaternion.Euler(-8, 0, 29) * axis; frond.localScale = Vector3.one * 0.74f;
        var frondR = Object.Instantiate(frond.gameObject, mt).transform; frondR.name = "FrondRight";
        frondR.localPosition = new Vector3(0.011f, 0.040f, 0.006f); frondR.localRotation = Quaternion.Euler(-8, 0, -31) * axis; frondR.localScale = new Vector3(-0.72f, 0.72f, 0.72f);
        var fnew = Object.Instantiate(frond.gameObject, mt).transform; fnew.name = "FrondNew"; SetMat(fnew, fernNew);
        fnew.localPosition = new Vector3(0.002f, 0.042f, -0.004f); fnew.localRotation = Quaternion.Euler(-12, 0, 6) * axis; fnew.localScale = Vector3.one * 0.95f;
        hero.newFrond = fnew.GetComponent<Renderer>(); hero.newFrondMat = fernNew; hero.mossMat = moss; hero.glassMat = glass;
        var dew = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(dew.GetComponent<Collider>());
        dew.name = "Dew"; dew.transform.SetParent(root.transform, false); dew.transform.localScale = Vector3.one * 0.0032f;
        var fm = fnew.GetComponent<MeshFilter>().sharedMesh; var fuv = fm.uv; var fv = fm.vertices; int tip = 0;
        for (int i = 0; i < fv.Length; i++) if (fuv[i].y > fuv[tip].y || (Mathf.Approximately(fuv[i].y, fuv[tip].y) && Mathf.Abs(fuv[i].x - 0.5f) < Mathf.Abs(fuv[tip].x - 0.5f))) tip = i;
        dew.transform.position = fnew.TransformPoint(fv[tip]) + new Vector3(0, -0.002f, -0.001f);
        dew.GetComponent<Renderer>().sharedMaterial = Mat("Jar_Dew", "Fidelity/Glow", m =>
        {
            m.SetTexture("_MainTex", Texture2D.whiteTexture); m.SetColor("_Tint", new Color(0.6f, 1f, 0.85f)); m.SetTexture("_EmissionTex", Texture2D.whiteTexture);
            m.SetColor("_Emission", new Color(0.5f, 1f, 0.8f)); m.SetColor("_Rim", new Color(0.2f, 0.6f, 0.5f)); m.SetFloat("_RimPower", 1.5f);
        });
        hero.dew = dew.GetComponent<Renderer>();

        // light on the real desk: breath ring + the pool of mint light it sits in (one card), a wider soft spill (one card)
        var flat = Quaternion.Euler(90, 0, 0);
        hero.ringMat = Mat("Jar_Ring", "Fidelity/Card", m =>
        {
            m.SetTexture("_MainTex", Tex("ring")); m.SetColor("_Color", new Color(0.75f, 1.35f, 1.05f)); m.SetColor("_Color2", new Color(0.03f, 0.15f, 0.09f));
            m.SetFloat("_Src", (float)BlendMode.One); m.SetFloat("_Dst", (float)BlendMode.One); m.SetFloat("_Ring", 1); m.SetFloat("_Fill", 0.5f);
            m.SetFloat("_ZTest", (float)CompareFunction.LessEqual); m.renderQueue = 2990;
        });
        Card("BreathRing", root.transform, hero.ringMat, new Vector3(0, 0.0006f, 0), new Vector2(0.152f, 0.152f), false, flat);
        var spill = CardMat("Jar_Spill", Tex("halo"), new Color(0.05f, 0.20f, 0.13f), Color.black, false, 0, -11);
        Card("DeskSpill", root.transform, spill, new Vector3(0, 0.0004f, 0.01f), new Vector2(0.42f, 0.30f), false, flat);

        // halo cards instead of bloom: a big soft one around the jar, a hot one on the coil
        hero.jarHaloMat = CardMat("Jar_Halo", Tex("halo"), new Color(0.05f, 0.22f, 0.15f), Color.black, false, 0, 20);
        var bills = new List<Transform>();
        bills.Add(Card("JarHalo", root.transform, hero.jarHaloMat, new Vector3(0, 0.07f, 0), new Vector2(0.24f, 0.26f)).transform);
        hero.coilHaloMat = CardMat("Jar_CoilHalo", Tex("halo"), new Color(0.30f, 0.85f, 0.45f) * 0.55f, Color.black, false, 0, 21);
        bills.Add(Card("CoilHalo", root.transform, hero.coilHaloMat, new Vector3(-0.012f, 0.086f, -0.01f), new Vector2(0.05f, 0.05f)).transform);
        var mossHalo = CardMat("Jar_MossHalo", Tex("halo"), new Color(0.06f, 0.25f, 0.12f), Color.black, false, 0, 19);

        // mist rising from under the cork: three alpha cards on a loop
        var mistMat = CardMat("Jar_Mist", Tex("mist"), new Color(0.72f, 0.86f, 0.84f, 0.8f), Color.black, true, 0, 30);
        var mist = new List<Transform>();
        for (int i = 0; i < 5; i++) { var c = Card("Mist" + i, root.transform, mistMat, new Vector3(0, 0.16f + i * 0.03f, 0), new Vector2(0.05f, 0.09f)); mist.Add(c.transform); bills.Add(c.transform); }
        hero.mist = mist.ToArray();

        // drifting gold spores: one particle system, one draw
        var ps = new GameObject("Spores").AddComponent<ParticleSystem>(); ps.transform.SetParent(root.transform, false);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.loop = true; main.duration = 10; main.startLifetime = 9; main.startSpeed = 0.004f; main.startSize = new ParticleSystem.MinMaxCurve(0.0012f, 0.0026f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1.0f, 0.78f, 0.30f), new Color(0.85f, 1.0f, 0.45f)); main.maxParticles = 90; main.simulationSpace = ParticleSystemSimulationSpace.Local; main.useUnscaledTime = false;
        var em = ps.emission; em.rateOverTime = 9;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.11f; sh.radiusThickness = 0.35f; sh.position = new Vector3(0, 0.08f, 0);
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.004f; noise.frequency = 0.6f;
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.y = new ParticleSystem.MinMaxCurve(0.002f, 0.006f);
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
        col.color = g;
        var pr = ps.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = CardMat("Jar_Spore", Tex("spore"), new Color(1.6f, 1.3f, 0.6f), Color.black, false, 0, 40);
        pr.renderMode = ParticleSystemRenderMode.Billboard; pr.shadowCastingMode = ShadowCastingMode.Off;
        ps.randomSeed = 5; ps.useAutoRandomSeed = false;
        hero.spores = ps;
        hero.billboards = bills.ToArray();
        hero.boilMats = new Material[0];
        return hero;
    }

    // ------------------------------------------------------------------ hero 2: the drawn dial
    public static FidelityHero BuildDial(float yaw)
    {
        var root = new GameObject("DrawnDial"); var hero = root.AddComponent<FidelityHero>(); hero.kind = FidelityHero.Kind.DrawnDial;
        var model = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>($"{Root}/Models/drawn_dial.fbx"));
        model.transform.SetParent(root.transform, false); model.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        var b = new Bounds(); foreach (var r in model.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        Debug.Log($"[Fid] dial model bounds {b}");
        Material Toon(string n, Texture2D t, Color lit, Color shade, float outline, Color? spec = null, float step = 0.15f) => Mat(n, "Fidelity/Toon", m =>
        {
            m.SetTexture("_MainTex", t); m.SetColor("_Lit", lit); m.SetColor("_Shade", shade); m.SetFloat("_Outline", outline);
            m.SetColor("_Ink", new Color(0.20f, 0.15f, 0.11f)); m.SetFloat("_Boil", 0.35f); m.SetFloat("_Step", step); m.SetFloat("_Feather", 0.05f);
            m.SetColor("_Spec", spec ?? Color.clear); m.SetFloat("_SpecStep", 0.96f); m.SetFloat("_Grain", 0.06f); m.SetFloat("_ShadowStrength", 0.9f);
        });
        var lit = new Color(1.05f, 1.02f, 0.97f); var shade = new Color(0.82f, 0.77f, 0.80f);
        var top = Toon("Dial_Top", Tex("dial_top"), lit, shade, 0.0f);
        var side = Toon("Dial_Side", Tex("dial_side"), lit, new Color(0.70f, 0.64f, 0.66f), 0.0006f);
        var gnomon = Toon("Dial_Gnomon", Tex("gnomon"), new Color(0.95f, 0.93f, 0.90f), new Color(0.42f, 0.40f, 0.40f), 0.0006f, new Color(1.0f, 0.98f, 0.92f), -0.2f);
        var mt = model.transform;
        SetMat(Find(mt, "DialTop"), top, ShadowCastingMode.On); SetMat(Find(mt, "Bed"), top, ShadowCastingMode.On);
        SetMat(Find(mt, "DialSide"), side, ShadowCastingMode.On); SetMat(Find(mt, "Gnomon"), gnomon, ShadowCastingMode.On);
        Find(mt, "Gnomon").localScale *= 0.72f;

        // plant cards (owner's drawings, matted) + the pinch halo; billboarded so the drawing always faces the eye
        float R = 0.130f; const float topY = 0.0105f;
        Vector3 P(float r, float deg) { var q = Quaternion.Euler(0, yaw, 0) * new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), 0, Mathf.Sin(deg * Mathf.Deg2Rad)) * r * R; return q + new Vector3(0, topY, 0); }
        var cards = new List<Transform>(); var boil = new List<Material> { top, side, gnomon };
        // positions read off the owner's frame: base pixel -> dial-local (x right, z away), in units of R
        Vector3 Q(float x, float z) => Quaternion.Euler(0, yaw, 0) * new Vector3(x * R, 0, z * R) + new Vector3(0, topY + 0.004f, 0);
        (string tex, Vector3 pos, Vector2 size)[] plants = { ("plant_sunrise", Q(-0.55f, -0.20f), new Vector2(0.040f, 0.050f)), ("plant_midday", Q(0.40f, -0.04f), new Vector2(0.039f, 0.058f)), ("plant_dusk", Q(0.60f, -0.40f), new Vector2(0.032f, 0.040f)) };
        Transform middayCard = null;
        foreach (var p in plants)
        {
            var m = CardMat("Dial_" + p.tex, Tex(p.tex), Color.white, Color.black, true, 0.0025f, 5);
            var c = Card(p.tex, root.transform, m, p.pos, p.size, true); cards.Add(c.transform); boil.Add(m);
            if (p.tex == "plant_midday") middayCard = c.transform;
        }
        hero.haloMat = CardMat("Dial_Halo", Tex("halo_line"), new Color(1.6f, 1.25f, 0.55f), new Color(0.55f, 0.40f, 0.12f), false, 0.004f, 8);
        var halo = Card("PinchHalo", middayCard, hero.haloMat, new Vector3(0, -40f / 228f, -0.02f), new Vector2(232f / 152f, 308f / 228f), true);
        halo.transform.localScale = new Vector3(232f / 152f, 308f / 228f, 1);
        hero.halo = halo.GetComponent<Renderer>(); boil.Add(hero.haloMat);
        hero.billboards = cards.ToArray(); hero.boilMats = boil.ToArray();

        // the shadow on the real table + the sun
        var catcher = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(catcher.GetComponent<Collider>());
        catcher.name = "TableShadowCatcher"; catcher.transform.SetParent(root.transform, false); catcher.transform.localRotation = Quaternion.Euler(90, 0, 0);
        catcher.transform.localScale = new Vector3(1.0f, 1.0f, 1); catcher.transform.localPosition = new Vector3(0, 0.0002f, 0);
        var cr = catcher.GetComponent<Renderer>(); cr.sharedMaterial = Mat("Dial_ShadowCatcher", "Fidelity/ShadowCatcher", m => { m.SetColor("_ShadowColor", new Color(0.66f, 0.56f, 0.50f)); m.SetFloat("_Fade", 0.45f); });
        cr.shadowCastingMode = ShadowCastingMode.Off; cr.receiveShadows = true;
        var sun = new GameObject("Sun").AddComponent<Light>(); sun.transform.SetParent(root.transform, false);
        sun.type = LightType.Directional; sun.color = new Color(1f, 0.95f, 0.85f); sun.intensity = 1f; sun.shadows = LightShadows.Soft; sun.shadowStrength = 1;
        sun.shadowBias = 0.002f; sun.shadowNormalBias = 0.2f;
        sun.transform.rotation = Quaternion.LookRotation(new Vector3(0.45f, -0.62f, -0.64f));
        return hero;
    }

    // ------------------------------------------------------------------ framing (matched to the owner's frames)
    // Night moss 1: jar base centre at pixel (915, 830), cork top at y 195 -> 4550 px/m; ring ellipse 0.22 -> ~13 deg elevation.
    static Camera JarCamera(bool plate = true) => MakeCamera(new Vector3(0.0f, 0.175f, -0.44f), new Vector3(0.0f, 0.072f, 0f), 28.2f, new Vector2(0.0f, -0.4f), plate ? Load<Texture2D>($"{Root}/Plates/plate-jar.png") : null);
    // Field notebook 1: dial ellipse centre (785, 615), semi-axes 505 x 315 px -> ~39 deg elevation, 30 deg vertical FOV.
    static Camera DialCamera(bool plate = true) => MakeCamera(new Vector3(0.0f, 0.322f, -0.411f), new Vector3(0, 0.012f, 0), 30f, new Vector2(3.6f, 0.5f), plate ? Load<Texture2D>($"{Root}/Plates/plate-dial.png") : null);
    const float DialYaw = 0f;

    static void NewScene() { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.5f); }

    static RenderTexture _rt; static Texture2D _tex;
    static void Render(Camera cam, string path, int w = 1824, int h = 1024, int msaa = 8, bool alpha = false)
    {
        if (_rt == null || _rt.width != w || _rt.height != h || _rt.antiAliasing != msaa)
        {
            if (_rt) Object.DestroyImmediate(_rt);
            _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = msaa };
            _tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        }
        cam.targetTexture = _rt; cam.aspect = (float)w / h; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = _rt;
        _tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); _tex.Apply(); RenderTexture.active = prev; cam.targetTexture = null;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, _tex.EncodeToPNG());
    }

    static void Settle(FidelityHero h, Camera cam)
    {
        h.Face(cam); h.Apply();
        if (h.spores) { h.spores.Simulate(12f, true, true, true); }
    }

    // ------------------------------------------------------------------ capture
    public static void CaptureAll()
    {
        ShaderUtil.allowAsyncCompilation = false;
        var sw = Stopwatch.StartNew(); var log = new List<string>();
        // jar, mid-breath (ring half full, fiddlehead a third uncoiled, glass fogging on the out-breath)
        NewScene(); var cam = JarCamera(); var jar = BuildJar();
        jar.breath = 0.5f; jar.uncoil = 0.30f; jar.fog = 0.45f; jar.time = 3f; Settle(jar, cam);
        Render(cam, Path.Combine(Renders, "warmup.png"), 320, 180);
        Render(cam, Path.Combine(Renders, "jar-midbreath.png")); log.Add("jar-midbreath");
        // jar, the answer: the frond stays
        jar.breath = 1f; jar.uncoil = 1f; jar.answer = 0.75f; jar.fog = 0.25f; jar.time = 52f; Settle(jar, cam);
        Render(cam, Path.Combine(Renders, "jar-answer.png")); log.Add("jar-answer");
        // object only, no plate (alpha) - for the report's turntable strip
        var plate = cam.transform.Find("PassthroughPlate"); plate.gameObject.SetActive(false);
        cam.backgroundColor = new Color(0.02f, 0.04f, 0.05f);
        Render(cam, Path.Combine(Renders, "jar-answer-noplate.png")); plate.gameObject.SetActive(true);
        // a short loop: one breath and the answer, 24 fps
        var seq = Path.Combine(Renders, "seq-jar"); if (Directory.Exists(seq)) Directory.Delete(seq, true);
        for (int f = 0; f < 96; f++)
        {
            float t = f / 24f;
            if (t < 2.5f) { jar.answer = 0; jar.breath = t / 2.5f; jar.uncoil = 0.3f + 0.45f * t / 2.5f; jar.fog = 0.2f + 0.35f * Mathf.Sin(jar.breath * Mathf.PI); }
            else { jar.answer = 0.5f + (t - 2.5f) / 6f; jar.uncoil = 1; jar.breath = 1; }
            jar.time = t; jar.Face(cam); jar.Apply(); jar.spores.Simulate(1f / 24f, true, false, true);
            Render(cam, Path.Combine(seq, $"f{f:000}.png"), 912, 512, 4);
        }

        // dial, idle and with the pinch halo answering
        NewScene(); cam = DialCamera(); var dial = BuildDial(DialYaw);
        dial.answer = 0; dial.time = 0; Settle(dial, cam);
        Render(cam, Path.Combine(Renders, "warmup.png"), 320, 180);
        Render(cam, Path.Combine(Renders, "dial-idle.png")); log.Add("dial-idle");
        dial.answer = 1; dial.time = 0.4f; Settle(dial, cam);
        Render(cam, Path.Combine(Renders, "dial-halo.png")); log.Add("dial-halo");
        seq = Path.Combine(Renders, "seq-dial"); if (Directory.Exists(seq)) Directory.Delete(seq, true);
        for (int f = 0; f < 72; f++)
        {
            float t = f / 24f; dial.time = t; dial.answer = Mathf.Clamp01((t - 0.5f) * 2f);
            dial.Face(cam); dial.Apply(); Render(cam, Path.Combine(seq, $"f{f:000}.png"), 912, 512, 4);
        }
        Debug.Log($"[Fid] CaptureAll {string.Join(", ", log)} in {sw.ElapsedMilliseconds} ms");
    }

    public static void CaptureDebug()
    {
        ShaderUtil.allowAsyncCompilation = false;
        NewScene(); var cam = MakeCamera(new Vector3(0, 0.12f, -0.22f), new Vector3(0, 0.07f, 0), 30f, Vector2.zero, null);
        cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
        var jar = BuildJar(); jar.uncoil = 0.3f; jar.breath = 0.5f; Settle(jar, cam);
        jar.transform.Find("night_jar/Jar").gameObject.SetActive(false);
        Render(cam, Path.Combine(Renders, "debug-jar-noglass.png"), 1200, 900);
        var mesh = jar.fiddle.sharedMesh; Debug.Log($"[Fid] fiddle bounds {mesh.bounds} verts {mesh.vertexCount}");
        var moss = jar.transform.Find("night_jar/Moss").GetComponent<MeshFilter>().sharedMesh; var n = moss.normals; var v = moss.vertices;
        int top = 0; for (int i = 0; i < v.Length; i++) if (v[i].y > v[top].y) top = i;
        foreach (var r in jar.GetComponentsInChildren<Renderer>(true))
        {
            var m = r.sharedMaterial; var t = m && m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            Debug.Log("[Fid] r " + r.name + " active=" + r.gameObject.activeInHierarchy + " mat=" + (m ? m.name : "-") + " shader=" + (m ? m.shader.name : "-") + " tex=" + (t ? t.name : "-") + " tri=" + (m && m.HasProperty("_Tri") ? m.GetFloat("_Tri") : -1) + " q=" + (m ? m.renderQueue : 0));
        }
        Debug.Log($"[Fid] moss top vertex {v[top]} normal {n[top]}; mesh bounds {moss.bounds}");
    }

    // ------------------------------------------------------------------ measure
    public static void Measure()
    {
        ShaderUtil.allowAsyncCompilation = false;
        var lines = new List<string>();
        void L(string s) { lines.Add(s); Debug.Log("[Fid][measure] " + s); }
        L($"machine: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), {SystemInfo.processorType}, Unity {Application.unityVersion}");
        L("hero,renderers,draws_est,shadow_casters,transparent_draws,triangles,textures,tex_mem_editor_MB,tex_mem_astc6x6_MB,shaders");
        foreach (var which in new[] { "jar", "jar-lean", "dial", "round2-lantern" })
        {
            NewScene();
            Camera cam; GameObject root;
            if (which == "jar" || which == "jar-lean")
            {
                cam = JarCamera(which == "jar-lean"); var h = BuildJar(); h.breath = 0.5f; h.uncoil = 0.3f; h.fog = 0.45f; h.time = 3f;
                if (which == "jar-lean")
                {   // the Quest-lean jar: no wide desk spill, a smaller jar halo, glass back wall off - what the overdraw budget would force
                    h.transform.Find("DeskSpill").gameObject.SetActive(false);
                    h.transform.Find("JarHalo").localScale = new Vector3(0.15f, 0.17f, 1);
                    h.glassMat = null;
                    var lean = new Material(h.transform.Find("night_jar/Jar").GetComponent<Renderer>().sharedMaterial); lean.SetShaderPassEnabled("SRPDefaultUnlit", false);
                    h.transform.Find("night_jar/Jar").GetComponent<Renderer>().sharedMaterial = lean;
                    Settle(h, cam); Render(cam, Path.Combine(Renders, "jar-lean-midbreath.png"));
                    cam.transform.Find("PassthroughPlate").gameObject.SetActive(false);
                }
                else Settle(h, cam);
                root = h.gameObject;
            }
            else if (which == "dial") { cam = DialCamera(false); var h = BuildDial(DialYaw); h.answer = 1; Settle(h, cam); root = h.gameObject; }
            else
            {
                var look = AssetDatabase.LoadAssetAtPath<Terrarium.TerrariumLook>("Assets/Terrarium/Looks/C_NightLantern.asset");
                cam = MakeCamera(new Vector3(0.02f, 0.30f, -0.36f), new Vector3(0, 0.105f, 0), 41f, Vector2.zero, null);
                var v = new GameObject("Terrarium").AddComponent<Terrarium.TerrariumView>(); v.look = look; v.permanentFronds = 3; v.uncoil = 0.55f; v.fog = 0.35f; v.Build();
                for (int i = 0; i < 3; i++) v.Apply(1f / 60f);
                root = v.gameObject; cam.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            }
            var rs = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.enabled && r.gameObject.activeInHierarchy && !(r.name == "PassthroughPlate")).ToArray();
            int draws = 0, trans = 0, casters = 0; long tris = 0; var texs = new HashSet<Texture>(); var shaders = new HashSet<string>();
            foreach (var r in rs)
            {
                var mfl = r.GetComponent<MeshFilter>(); Mesh mesh = r is SkinnedMeshRenderer smr ? smr.sharedMesh : (mfl != null ? mfl.sharedMesh : null);
                int mtri = mesh ? (int)(Enumerable.Range(0, mesh.subMeshCount).Sum(s => (long)mesh.GetIndexCount(s)) / 3) : 0;
                if (r is ParticleSystemRenderer) mtri = ((ParticleSystemRenderer)r).GetComponent<ParticleSystem>().particleCount * 2;
                foreach (var m in r.sharedMaterials.Where(m => m))
                {
                    int passes = m.shader.name == "Fidelity/Glass" || m.shader.name == "Fidelity/Toon" ? 2 : 1;   // two-pass shaders draw twice
                    if (m.shader.name == "Fidelity/Toon" && m.GetFloat("_Outline") <= 0) passes = 1;
                    draws += passes; tris += (long)mtri * passes; shaders.Add(m.shader.name);
                    if (m.renderQueue >= 2500 || m.shader.name.Contains("Glass")) trans += passes;
                    foreach (var id in m.GetTexturePropertyNameIDs()) { var t = m.GetTexture(id); if (t && t is Texture2D && t != Texture2D.whiteTexture) texs.Add(t); }
                }
                if (r.shadowCastingMode != ShadowCastingMode.Off) casters++;
            }
            long mem = texs.Sum(t => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t));
            double astc = texs.Sum(t => { int max = t.name.Contains("dial_top") || t.name.Contains("fern_") ? 1024 : 512; int w = Mathf.Min(t.width, max), hh = Mathf.Min(t.height, max); if (t.width > t.height) hh = Mathf.Max(1, hh * Mathf.Min(t.width, max) / t.width * t.height / Mathf.Max(1, hh)); return Math.Ceiling(w / 6.0) * Math.Ceiling(hh / 6.0) * 16 * 1.333; });
            if (which == "round2-lantern") { mem = 0; astc = 0; }
            L($"{which},{rs.Length},{draws + casters},{casters},{trans},{tris},{texs.Count},{mem / 1048576.0:0.0},{astc / 1048576.0:0.00},{string.Join(" ", shaders.OrderBy(s => s))}");
            L($"{which} textures: {string.Join(" ", texs.Select(t => $"{t.name}:{t.width}x{t.height}"))}");

            // GPU+CPU frame proxy: two Quest-3-sized eye buffers (1680x1760, 4x MSAA) per frame, 240 frames, one sync at the end
            var eye = new RenderTexture(1680, 1760, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; eye.Create();
            cam.targetTexture = eye; cam.aspect = 1680f / 1760f;
            for (int i = 0; i < 10; i++) cam.Render();
            AsyncGPUReadback.Request(eye).WaitForCompletion();
            var sw = Stopwatch.StartNew(); int N = 240;
            for (int i = 0; i < N; i++) { cam.Render(); cam.Render(); }
            AsyncGPUReadback.Request(eye).WaitForCompletion();
            double ms = sw.Elapsed.TotalMilliseconds / N;
            L($"{which} frame proxy (2 eyes 1680x1760 4xMSAA, editor batchmode, this PC): {ms:0.00} ms/frame avg over {N}");
            cam.targetTexture = null; eye.Release();

            // transparent overdraw: swap every transparent material for the counting shader, opaque ones write depth only
            if (which != "round2-lantern") L(Overdraw(which, cam, rs));
        }
        File.WriteAllLines(Path.Combine(FidDir, "logs", "measure.txt"), lines);
    }

    static string Overdraw(string which, Camera cam, Renderer[] rs)
    {
        var od = new Material(Shader.Find("Fidelity/Overdraw"));
        var saved = rs.ToDictionary(r => r, r => r.sharedMaterials);
        foreach (var r in rs)
        {
            var mats = r.sharedMaterials;
            bool transparent = mats.Any(m => m && (m.renderQueue >= 2500 || m.shader.name.Contains("Glass")));
            if (transparent) r.sharedMaterials = mats.Select(m => od).ToArray();   // od is Cull Off: one draw covers what the glass back pass (Cull Front) + front pass (Cull Back) cover together
            else r.enabled = r.enabled; // opaque stays: it writes depth, its colour is overwritten below
        }
        cam.backgroundColor = Color.black;
        var rt = new RenderTexture(912, 512, 24, RenderTextureFormat.ARGBHalf) { antiAliasing = 1 }; cam.targetTexture = rt; cam.aspect = 912f / 512f;
        // opaque colour must not count: render once with opaque hidden from colour by clearing after a depth prepass is not
        // available in a plain camera, so instead we count with opaque present and subtract an opaque-only render
        cam.Render();
        var a = Read(rt);
        foreach (var r in rs) r.sharedMaterials = saved[r];
        foreach (var r in rs) if (r.sharedMaterials.Any(m => m && (m.renderQueue >= 2500 || m.shader.name.Contains("Glass")))) r.enabled = false;
        cam.Render(); var b = Read(rt);
        foreach (var r in rs) r.enabled = true;
        cam.targetTexture = null; rt.Release();
        int covered = 0; double sum = 0; float max = 0; var hist = new int[12];
        var png = new Texture2D(912, 512, TextureFormat.RGB24, false);
        for (int i = 0; i < a.Length; i++)
        {
            // pixels where only opaque geometry is present give the same colour in both renders; the counter is additive
            float layers = Mathf.Round(Mathf.Max(0, a[i].r - b[i].r) * 32f);
            if (layers > 0) { covered++; sum += layers; max = Mathf.Max(max, layers); }
            hist[Mathf.Min(11, (int)layers)]++;
            float k = layers / 10f; png.SetPixel(i % 912, i / 912, new Color(Mathf.Clamp01(k * 2), Mathf.Clamp01(k * 1.2f - 0.2f), Mathf.Clamp01(1 - k * 3) * (layers > 0 ? 0.6f : 0)));
        }
        png.Apply(); File.WriteAllBytes(Path.Combine(FidDir, "renders", $"overdraw-{which}.png"), png.EncodeToPNG());
        return $"{which} transparent overdraw: {covered * 100.0 / a.Length:0.0}% of the frame covered, mean {sum / Math.Max(1, covered):0.00} layers where covered, max {max}, histogram(0..11+) {string.Join("/", hist)}";
    }

    static Color[] Read(RenderTexture rt)
    {
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false); t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); t.Apply();
        RenderTexture.active = prev; var c = t.GetPixels(); Object.DestroyImmediate(t); return c;
    }

    // ------------------------------------------------------------------ headset gallery scenes (passthrough, no plate)
    public static void MakeScenes()
    {
        NewScene();
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = go.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);   // alpha 0: passthrough shows through
        cam.nearClipPlane = 0.02f; cam.farClipPlane = 20f; cam.allowHDR = false;
        var data = go.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = false; data.renderShadows = true;
        var tpd = go.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
        tpd.positionInput = new UnityEngine.InputSystem.InputActionProperty(new UnityEngine.InputSystem.InputAction("pos", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
        tpd.rotationInput = new UnityEngine.InputSystem.InputActionProperty(new UnityEngine.InputSystem.InputAction("rot", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
        // seated: both objects on a virtual desk 0.45 m ahead, 0.30 m below the eye; jar left, dial right
        var jar = BuildJar(); jar.autoPlay = true; jar.transform.position = new Vector3(-0.17f, -0.30f, 0.45f);
        var dial = BuildDial(DialYaw); dial.autoPlay = true; dial.transform.position = new Vector3(0.20f, -0.30f, 0.50f);
        var catcher = dial.transform.Find("TableShadowCatcher"); if (catcher) catcher.localScale = new Vector3(0.7f, 0.7f, 1);
        // Meta passthrough, by reflection so this file compiles without the Meta assemblies; logged if missing
        foreach (var tn in new[] { "OVRManager", "OVRPassthroughLayer" })
        {
            var t = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(tn)).FirstOrDefault(x => x != null);
            if (t == null) { Debug.LogWarning("[Fid] passthrough type not found: " + tn); continue; }
            var c = go.AddComponent(t);
            if (tn == "OVRManager") t.GetField("isInsightPassthroughEnabled")?.SetValue(c, true);
            if (tn == "OVRPassthroughLayer") { var ov = t.GetField("overlayType"); if (ov != null) ov.SetValue(c, Enum.Parse(ov.FieldType, "Underlay")); }
            Debug.Log("[Fid] added " + tn);
        }
        var path = $"{Root}/HeroGallery.unity";
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), path);
        Debug.Log("[Fid] scene " + path);
    }
}
