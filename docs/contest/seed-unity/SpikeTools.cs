using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Terrarium.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Terrarium.EditorTools
{
    /// <summary>
    /// Everything the spike does in -batchmode, so an agent can run it with no editor window:
    ///   Setup        URP asset, three looks (materials), the scene, build settings
    ///   CaptureLooks stills of each art direction from the real URP renderer
    ///   CaptureRitual a full ritual driven by SimulatedHand, frame by frame, plus a CSV timeline
    ///   (XR config and the Windows player build live in SpikeXR.cs)
    /// Output goes to ../captures (outside Assets).
    /// </summary>
    public static class SpikeTools
    {
        const string LookDir = "Assets/Terrarium/Looks";
        const string ScenePath = "Assets/Scenes/Terrarium.unity";
        static string Captures => Path.GetFullPath(Path.Combine(Application.dataPath, "../../captures"));

        // ------------------------------------------------------------------ setup
        [MenuItem("Terrarium/Setup")]
        public static void Setup()
        {
            var sw = Stopwatch.StartNew();
            EnsureUrp();
            var looks = MakeLooks();
            MakeScene(looks[0]);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Spike] Setup done in {sw.ElapsedMilliseconds} ms; looks: {string.Join(", ", looks.Select(l => l.displayName))}");
        }

        static void EnsureUrp()
        {
            Directory.CreateDirectory("Assets/Settings");
            var rpPath = "Assets/Settings/TerrariumURP.asset";
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(rpPath);
            if (urp == null)
            {
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, "Assets/Settings/TerrariumURP_Renderer.asset");
                urp = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(urp, rpPath);
            }
            urp.supportsHDR = true;
            urp.msaaSampleCount = 4;
            urp.shadowDistance = 1.5f;
            urp.renderScale = 1f;
            var so = new SerializedObject(urp);
            void Set(string prop, Action<SerializedProperty> f) { var sp = so.FindProperty(prop); if (sp != null) f(sp); else Debug.LogWarning("[Spike] URP field not found: " + prop); }
            Set("m_MainLightShadowmapResolution", sp => sp.intValue = 4096);
            Set("m_SoftShadowsSupported", sp => sp.boolValue = true);
            Set("m_ShadowCascadeCount", sp => sp.intValue = 1);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);
            if (PlayerSettings.colorSpace != ColorSpace.Linear) { PlayerSettings.colorSpace = ColorSpace.Linear; Debug.Log("[Spike] colour space -> Linear (was Gamma: washed-out ACES)"); }
            GraphicsSettings.defaultRenderPipeline = urp;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            Debug.Log($"[Spike] URP asset bound: {rpPath} (pipeline now {GraphicsSettings.currentRenderPipeline?.GetType().Name})");
        }

        static Material Mat(string look, string name, Shader shader, Action<Material> init)
        {
            Directory.CreateDirectory(LookDir);
            var path = $"{LookDir}/{look}_{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            init(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Shader Lit => Shader.Find("Universal Render Pipeline/Lit");
        static Shader Glass => Shader.Find("Terrarium/Glass");
        static Shader Ink => Shader.Find("Terrarium/Ink");

        static Action<Material> LitInit(Color c, float smooth, Color? emission = null, bool twoSided = false) => m =>
        {
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0f);
            if (twoSided) { m.SetFloat("_Cull", 0f); m.doubleSidedGI = true; }
            if (emission.HasValue)
            {
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetKeyword(new UnityEngine.Rendering.LocalKeyword(m.shader, "_EMISSION"), true);
            }
            else { m.SetKeyword(new UnityEngine.Rendering.LocalKeyword(m.shader, "_EMISSION"), false); m.SetColor("_EmissionColor", Color.black); }
        };

        static Action<Material> InkInit(Color fill, Color shade, float outline = 0.0011f, float hatch = 0.35f) => m =>
        {
            m.SetColor("_Color", fill); m.SetColor("_Shade", shade); m.SetColor("_Ink", Hex("#2a241c"));
            m.SetFloat("_Outline", outline); m.SetFloat("_Hatch", hatch);
        };

        static Action<Material> GlassInit(Color tint, Color rim, float rimPower, Color streak, Color fog) => m =>
        {
            m.SetColor("_Tint", tint); m.SetColor("_RimColor", rim); m.SetFloat("_RimPower", rimPower);
            m.SetColor("_StreakColor", streak); m.SetColor("_FogColor", fog); m.SetFloat("_Fog", 0f); m.SetColor("_Glow", Color.clear);
        };

        public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        public static TerrariumLook[] MakeLooks()
        {
            // A - Morning Glasshouse: the round-1 palette made physical (moss #3f6b32, fern #6e9b3d, sprout #a9c45a, wood #b48a60)
            var a = Look("A_MorningGlasshouse", "Morning Glasshouse",
                "Naturalistic. Lit fern and soil under a warm low sun, glass carried by a fresnel rim and one window streak. The round-1 canvas palette, made physical.");
            a.glass = Mat("A", "Glass", Glass, GlassInit(new Color(0.86f, 0.95f, 0.93f, 0.05f), new Color(0.93f, 1f, 0.98f, 0.55f), 3.0f, new Color(1, 1, 1, 0.42f), new Color(0.96f, 0.97f, 0.97f, 0.62f)));
            a.soil = Mat("A", "Soil", Lit, LitInit(Hex("#3a2a1e"), 0.12f));
            a.pebble = Mat("A", "Pebble", Lit, LitInit(Hex("#9c968a"), 0.35f));
            a.moss = Mat("A", "Moss", Lit, LitInit(Hex("#3f6b32"), 0.08f));
            a.frond = Mat("A", "Frond", Lit, LitInit(Hex("#5e8c34"), 0.42f, null, true));
            a.frondActive = Mat("A", "FrondActive", Lit, LitInit(Hex("#8fb54a"), 0.48f, null, true));
            a.dew = Mat("A", "Dew", Glass, GlassInit(new Color(0.9f, 1f, 1f, 0.35f), new Color(1, 1, 1, 0.95f), 1.6f, new Color(1, 1, 1, 0.9f), new Color(1, 1, 1, 0.6f)));
            a.table = Mat("A", "Table", Lit, LitInit(Hex("#8a6a4e"), 0.38f));
            a.cork = Mat("A", "Cork", Lit, LitInit(Hex("#a77a4f"), 0.1f));
            a.flower = Mat("A", "Flower", Lit, LitInit(Hex("#f2d9d0"), 0.3f, null, true));
            a.background = Hex("#e9e2d3"); a.keyColor = Hex("#fff1dc"); a.keyIntensity = 1.9f; a.keyEuler = new Vector3(34, -38, 0);
            a.ambientSky = Hex("#b9c4c8"); a.ambientEquator = Hex("#a69d8e"); a.ambientGround = Hex("#4a3b2e");
            a.answerGlow = Hex("#ffd38a"); a.answerGlowIntensity = 0.012f;
            a.bloomIntensity = 0.35f; a.bloomThreshold = 0.95f; a.vignette = 0.16f; a.acesTonemap = true; a.postExposure = 0.35f; a.corkLid = false;

            // B - Herbarium Plate: ink outline + two-tone paper fills, like a pressed-fern plate
            var b = Look("B_HerbariumPlate", "Herbarium Plate",
                "Graphic. Inverted-hull ink outlines, two-tone paper fills and screen-space hatching in shade: a botanical plate that holds still and reads at any distance.");
            b.glass = Mat("B", "Glass", Glass, GlassInit(new Color(0.97f, 0.95f, 0.88f, 0.10f), new Color(0.16f, 0.14f, 0.11f, 0.85f), 5.0f, new Color(1f, 1f, 0.97f, 0.65f), new Color(0.99f, 0.98f, 0.94f, 0.7f)));
            b.soil = Mat("B", "Soil", Ink, InkInit(Hex("#7a5a40"), Hex("#5b3f2b")));
            b.pebble = Mat("B", "Pebble", Ink, InkInit(Hex("#d8d0bf"), Hex("#b5ab96")));
            b.moss = Mat("B", "Moss", Ink, InkInit(Hex("#8fae5c"), Hex("#6a8a45")));
            b.frond = Mat("B", "Frond", Ink, InkInit(Hex("#7fa64a"), Hex("#557a33"), 0.0007f));
            b.frondActive = Mat("B", "FrondActive", Ink, InkInit(Hex("#b9cf6a"), Hex("#88a64a"), 0.0007f));
            b.dew = Mat("B", "Dew", Ink, InkInit(Hex("#e3f6f5"), Hex("#7fcfd6"), 0.0006f, 0f));
            b.table = Mat("B", "Table", Ink, InkInit(Hex("#efe6d2"), Hex("#ddd0b5"), 0.0015f, 0.15f));
            b.cork = Mat("B", "Cork", Ink, InkInit(Hex("#c79a64"), Hex("#a77a4f")));
            b.flower = Mat("B", "Flower", Ink, InkInit(Hex("#e7a08c"), Hex("#c4705c"), 0.0006f));
            b.background = Hex("#f8f4ea"); b.keyColor = Color.white; b.keyIntensity = 1.2f; b.keyEuler = new Vector3(50, -30, 0);
            b.ambientSky = Color.white; b.ambientEquator = Color.white; b.ambientGround = Color.white;
            b.answerGlow = Hex("#d99b3f"); b.answerGlowIntensity = 0.0f;
            b.bloomIntensity = 0f; b.vignette = 0f; b.acesTonemap = false; b.corkLid = true;

            // C - Night Lantern: the wind-down ritual; the fern is the light source in a dark room
            var c = Look("C_NightLantern", "Night Lantern",
                "Atmospheric. The room drops to dusk and the fern carries its own light: emissive veins, a cyan rim on the glass, bloom on every breath. Made for the evening ritual.");
            c.glass = Mat("C", "Glass", Glass, GlassInit(new Color(0.20f, 0.45f, 0.50f, 0.05f), new Color(0.50f, 0.95f, 1f, 0.7f), 2.4f, new Color(0.7f, 1f, 1f, 0.35f), new Color(0.55f, 0.85f, 0.9f, 0.45f)));
            c.soil = Mat("C", "Soil", Lit, LitInit(Hex("#120d0b"), 0.2f));
            c.pebble = Mat("C", "Pebble", Lit, LitInit(Hex("#2a2f36"), 0.5f, new Color(0.02f, 0.06f, 0.08f)));
            c.moss = Mat("C", "Moss", Lit, LitInit(Hex("#0f2a1c"), 0.1f, new Color(0.05f, 0.35f, 0.22f) * 0.8f));
            c.frond = Mat("C", "Frond", Lit, LitInit(Hex("#0e3324"), 0.5f, new Color(0.10f, 0.85f, 0.55f) * 0.9f, true));
            c.frondActive = Mat("C", "FrondActive", Lit, LitInit(Hex("#154a36"), 0.5f, new Color(0.45f, 1.0f, 0.85f) * 2.2f, true));
            c.dew = Mat("C", "Dew", Lit, LitInit(Hex("#bff6ff"), 0.9f, new Color(0.6f, 1f, 1f) * 4f));
            c.table = Mat("C", "Table", Lit, LitInit(Hex("#1d1813"), 0.55f));
            c.cork = Mat("C", "Cork", Lit, LitInit(Hex("#2a1e14"), 0.1f));
            c.flower = Mat("C", "Flower", Lit, LitInit(Hex("#ffd2a8"), 0.3f, new Color(1f, 0.55f, 0.25f) * 2.5f, true));
            c.background = Hex("#070b12"); c.keyColor = Hex("#7f9cff"); c.keyIntensity = 0.35f; c.keyEuler = new Vector3(55, 150, 0);
            c.ambientSky = Hex("#101826"); c.ambientEquator = Hex("#0b1018"); c.ambientGround = Hex("#050608");
            c.answerGlow = Hex("#7fcfd6"); c.answerGlowIntensity = 0.03f;
            c.bloomIntensity = 1.4f; c.bloomThreshold = 0.7f; c.vignette = 0.32f; c.acesTonemap = true; c.postExposure = 0.2f; c.corkLid = false;

            foreach (var l in new[] { a, b, c }) EditorUtility.SetDirty(l);
            AssetDatabase.SaveAssets();
            return new[] { a, b, c };
        }

        static TerrariumLook Look(string file, string name, string intent)
        {
            Directory.CreateDirectory(LookDir);
            var path = $"{LookDir}/{file}.asset";
            var l = AssetDatabase.LoadAssetAtPath<TerrariumLook>(path);
            if (l == null) { l = ScriptableObject.CreateInstance<TerrariumLook>(); AssetDatabase.CreateAsset(l, path); }
            l.displayName = name; l.intent = intent;
            return l;
        }

        public static TerrariumLook[] LoadLooks() =>
            new[] { "A_MorningGlasshouse", "B_HerbariumPlate", "C_NightLantern" }
                .Select(n => AssetDatabase.LoadAssetAtPath<TerrariumLook>($"{LookDir}/{n}.asset")).ToArray();

        static void MakeScene(TerrariumLook look)
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = MakeCamera(look, xrTracked: true);
            cam.transform.position = new Vector3(0, 0.40f, -0.40f);
            cam.transform.LookAt(new Vector3(0, 0.10f, 0));
            var terr = new GameObject("Terrarium");
            var view = terr.AddComponent<TerrariumView>(); view.look = look;
            var driver = terr.AddComponent<TerrariumDriver>(); driver.view = view;
            var opType = Type.GetType("Terrarium.XR.TerrariumOperatorTools, Terrarium.XR");
            if (opType != null) terr.AddComponent(opType); else Debug.LogWarning("[Spike] Terrarium.XR not compiled; scene has no Operator tool");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[Spike] Scene saved: {ScenePath}");
        }

        public static Camera MakeCamera(TerrariumLook look, bool xrTracked)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = look.background;
            cam.nearClipPlane = 0.02f; cam.farClipPlane = 20f; cam.fieldOfView = 38f; cam.allowHDR = true;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; data.antialiasingQuality = AntialiasingQuality.High;
            if (xrTracked)
            {
                var tpd = go.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
                tpd.positionInput = new UnityEngine.InputSystem.InputActionProperty(new UnityEngine.InputSystem.InputAction("pos", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
                tpd.rotationInput = new UnityEngine.InputSystem.InputActionProperty(new UnityEngine.InputSystem.InputAction("rot", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
            }
            return cam;
        }

        // ------------------------------------------------------------------ capture
        static (Camera cam, TerrariumView view) Stage(TerrariumLook look, Vector3 camPos, Vector3 target, float fov)
        {
            var lookName = look.name;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            look = AssetDatabase.LoadAssetAtPath<TerrariumLook>($"{LookDir}/{lookName}.asset"); // NewScene unloads assets held only by C#
            var cam = MakeCamera(look, xrTracked: false);
            cam.transform.position = camPos; cam.transform.LookAt(target); cam.fieldOfView = fov;
            var view = new GameObject("Terrarium").AddComponent<TerrariumView>();
            view.look = look;
            return (cam, view);
        }

        static RenderTexture _rt; static Texture2D _tex;
        static long Render(Camera cam, string path, int w, int h)
        {
            var sw = Stopwatch.StartNew();
            if (_rt == null || _rt.width != w || _rt.height != h)
            {
                if (_rt) Object.DestroyImmediate(_rt);
                _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                _tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            }
            cam.targetTexture = _rt;
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = _rt;
            _tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); _tex.Apply();
            RenderTexture.active = prev; cam.targetTexture = null;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, _tex.EncodeToPNG());
            return sw.ElapsedMilliseconds;
        }

        static void Settle(TerrariumView v, int frames = 3) { for (int i = 0; i < frames; i++) v.Apply(1f / 60f); }

        /// <summary>Async shader compilation draws placeholders on a variant's first frames; captures must not race it.</summary>
        static void NoAsyncShaders() { ShaderUtil.allowAsyncCompilation = false; }

        public static void CaptureAll()
        {
            NoAsyncShaders();
            CaptureLooks();
            foreach (var w in new[] { "A", "C" }) CaptureRitualLook(w, 24);
        }

        public static void SetupAndCaptureLooks() { Setup(); CaptureLooks(); }

        public static void DebugLight()
        {
            var dir = Path.Combine(Captures, "debug");
            for (int k = 0; k < 4; k++)
            {
                var look = LoadLooks()[0];
                var (cam, v) = Stage(look, new Vector3(0.02f, 0.30f, -0.36f), new Vector3(0, 0.105f, 0), 36f);
                v.permanentFronds = 3; v.uncoil = 0.55f; v.Build(); Settle(v);
                var root = v.transform.Find("TerrariumRoot");
                if (k == 1) root.Find("Post").gameObject.SetActive(false);
                if (k == 2) root.Find("KeyLight").gameObject.SetActive(false);
                if (k == 3) { RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.black; RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom; }
                Debug.Log($"[Spike] debug {k}: ambient={RenderSettings.ambientMode} sky={RenderSettings.ambientSkyColor} skybox={RenderSettings.skybox} refl={RenderSettings.defaultReflectionMode} colorSpace={PlayerSettings.colorSpace} lights={string.Join(",", Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Select(l => l.name + ":" + l.intensity + (l.enabled && l.gameObject.activeInHierarchy ? "" : "(off)")))}");
                Render(cam, Path.Combine(dir, $"dbg{k}.png"), 800, 500);
            }
        }

        [MenuItem("Terrarium/Capture Looks")]
        public static void CaptureLooks()
        {
            NoAsyncShaders();
            var looks = LoadLooks();
            foreach (var l in looks)
                foreach (var m in new[] { l.soil, l.frond, l.table })
                    Debug.Log($"[Spike] mat {m.name}: shader={m.shader.name} base={(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString() : "-")} kw=[{string.Join(",", m.shaderKeywords)}] enabledKw=[{string.Join(",", m.enabledKeywords.Select(k => k.name))}]");
            var dir = Path.Combine(Captures, "looks");
            var log = new List<string> { "look,shot,ms" };
            var hero = new Vector3(0.02f, 0.30f, -0.36f); var tgt = new Vector3(0, 0.105f, 0);
            foreach (var lookName in looks.Select(l => l.name).ToArray())
            {
                var look = AssetDatabase.LoadAssetAtPath<TerrariumLook>($"{LookDir}/{lookName}.asset");
                string key = lookName.Split('_')[0];
                // 1. the hero: mid-ritual, three permanent fronds, a fiddlehead mid-inhale, glass lightly fogged
                var (cam, v) = Stage(look, hero, tgt, 41f);
                v.permanentFronds = 3; v.uncoil = 0.55f; v.fog = 0.35f; v.Build(); Settle(v);
                Render(cam, Path.Combine(Captures, "warmup.png"), 320, 200); // compile every variant once before the real shot
                log.Add($"{key},hero,{Render(cam, Path.Combine(dir, $"{key}-hero.png"), 1600, 1000)}");
                // 2. the answer: ritual done, frond fully open, glow pulse, dew bead on the frond
                v.uncoil = 1f; v.fog = 0.8f; v.PlayAnswer(); for (int i = 0; i < 80; i++) v.Apply(1f / 60f);
                log.Add($"{key},answer,{Render(cam, Path.Combine(dir, $"{key}-answer.png"), 1600, 1000)}");
                // 3. day 7: seven fronds and the first flower
                look = AssetDatabase.LoadAssetAtPath<TerrariumLook>($"{LookDir}/{lookName}.asset");
                var (cam7, v7) = Stage(look, new Vector3(0.0f, 0.36f, -0.42f), new Vector3(0, 0.10f, 0), 34f);
                v7.permanentFronds = 7; v7.flowers = 1; v7.uncoil = 0f; v7.Build(); Settle(v7);
                log.Add($"{key},day7,{Render(cam7, Path.Combine(dir, $"{key}-day7.png"), 1600, 1000)}");
            }
            // the missed-day droop and its recovery, in the look the report recommends
            looks = LoadLooks();
            var (camD, vd) = Stage(looks[0], new Vector3(0.0f, 0.36f, -0.42f), new Vector3(0, 0.10f, 0), 34f);
            vd.permanentFronds = 5; vd.vitality = 0.6f; vd.uncoil = 0f; vd.Build(); Settle(vd);
            log.Add($"A,droop,{Render(camD, Path.Combine(dir, "A-droop.png"), 1600, 1000)}");
            vd.vitality = 1f; Settle(vd);
            log.Add($"A,recovered,{Render(camD, Path.Combine(dir, "A-recovered.png"), 1600, 1000)}");
            // seated point of view: the jar at 45 cm in a wide headset-like frustum
            looks = LoadLooks();
            var (camP, vp) = Stage(looks[0], new Vector3(0.0f, 0.42f, -0.45f), new Vector3(0, 0.06f, 0.05f), 90f);
            vp.permanentFronds = 3; vp.uncoil = 0.3f; vp.Build(); Settle(vp);
            log.Add($"A,seated-pov,{Render(camP, Path.Combine(dir, "A-seated-pov.png"), 1600, 1000)}");
            File.WriteAllLines(Path.Combine(dir, "capture-times.csv"), log);
            Debug.Log("[Spike] CaptureLooks wrote " + (log.Count - 1) + " PNGs to " + dir + "\n" + string.Join("\n", log));
        }

        /// <summary>
        /// The hero moment end to end: SimulatedHand (scripted pinch strength with jitter and ramps) drives the real
        /// BreathSession, which drives the real view; every frame is rendered by URP and written as PNG. Look by -look A|B|C.
        /// </summary>
        [MenuItem("Terrarium/Capture Ritual")]
        public static void CaptureRitual()
        {
            var args = Environment.GetCommandLineArgs();
            int li = Array.IndexOf(args, "-look");
            string which = li >= 0 && li + 1 < args.Length ? args[li + 1] : "A";
            int fpsIdx = Array.IndexOf(args, "-fps");
            int fps = fpsIdx >= 0 ? int.Parse(args[fpsIdx + 1]) : 24;
            NoAsyncShaders();
            CaptureRitualLook(which, fps);
        }

        static void CaptureRitualLook(string which, int fps)
        {
            var look = LoadLooks().First(l => l.name.StartsWith(which));
            var dir = Path.Combine(Captures, "ritual-" + which);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);

            var (cam, v) = Stage(look, new Vector3(0.02f, 0.30f, -0.36f), new Vector3(0, 0.105f, 0), 36f);
            v.permanentFronds = 2; v.Build();
            var hand = new SimulatedHand(seed: 3).Rest(1.5f).Breaths(6, 4f, 4f);
            var session = new BreathSession();
            var garden = new Garden(); garden.CompleteRitual(9768); garden.CompleteRitual(9769);
            var csv = new List<string> { "frame,t,strength,tracked,pinch,phase,breaths,uncoil,fog" };
            float dt = 1f / fps; int sub = Mathf.Max(1, Mathf.RoundToInt(72f / fps)); float sdt = dt / sub;
            float t = 0; int frame = 0; bool answered = false; float answerEnd = float.MaxValue;
            var sw = Stopwatch.StartNew(); long renderMs = 0;
            while (t < answerEnd && t < 120f)
            {
                PinchSample s = default;
                for (int k = 0; k < sub; k++) { s = hand.Sample(t + k * sdt); session.Update(sdt, s); }
                t += dt;
                if (session.Phase == BreathPhase.Complete && !answered)
                {
                    answered = true; var ans = garden.CompleteRitual(9770);
                    v.PlayAnswer(); answerEnd = t + 4.5f;
                    Debug.Log($"[Spike] ritual complete at t={t:0.00}s newFrond={ans.NewFrond}");
                }
                v.uncoil = session.Uncoil; v.fog = session.Fog;
                if (answered && !v.AnswerPlaying) { v.permanentFronds = garden.Fronds; v.uncoil = 0; }
                v.Apply(dt);
                renderMs += Render(cam, Path.Combine(dir, $"f{frame:0000}.png"), 960, 600);
                csv.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1:0.000},{2:0.000},{3},{4},{5},{6},{7:0.000},{8:0.000}",
                    frame, t, s.Strength, s.Tracked, session.Pinch, session.Phase, session.Breaths, session.Uncoil, session.Fog));
                frame++;
            }
            File.WriteAllLines(Path.Combine(dir, "timeline.csv"), csv);
            File.WriteAllLines(Path.Combine(dir, "events.txt"), session.Events.Select(e => e.ToString()));
            Debug.Log($"[Spike] CaptureRitual look={which} frames={frame} simulated={t:0.0}s wall={sw.ElapsedMilliseconds}ms render+encode={renderMs}ms -> {dir}");
        }
    }
}
