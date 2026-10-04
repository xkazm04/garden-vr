#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using GardenVR.Capture;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace GardenVR.Terrarium.Editor
{
    /// <summary>
    /// Spike S2 (T-TER-044) tools. One Unity at a time, graphics on, foreground.
    /// <para><c>Bake</c>: renders the refraction strip for variant s2b1. Six 160 px faces from the middle of the cavity
    /// (jar origin + 6 cm up) with the glass hidden, at the JarG1 state, with the JarG1 plate in place, laid out for
    /// <c>SampleRefractStrip</c> in GlassCommon.hlsl (+X, -X, +Y, -Y, +Z, -Z). Writes the PNG to <c>-out</c>.</para>
    /// <para><c>Time</c>: renders the JarG1 frame N times per look into a render texture, synchronised by a read-back, and
    /// reports ms per frame. The scene is small, so this is the PC GPU cost bound from the outside, not a profiler
    /// capture. Writes JSON to <c>-out</c>.</para>
    /// </summary>
    public static class S2Bake
    {
        const int Face = 160;
        const string G1State = "breath=0.5,uncoil=0.3,fog=0.45,time=3";
        const string Scene = "Assets/Scenes/Main.unity";

        public static void Bake() { Guard("Bake", DoBake); }
        public static void Time() { Guard("Time", DoTime); }

        static void Guard(string entry, Func<string> body)
        {
            try
            {
                string output = body();
                Debug.Log("[S2] " + entry + " OK " + output);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[S2] " + entry + " FAIL " + e);
                EditorApplication.Exit(1);
            }
        }

        static string Arg(string name, string fallback)
        {
            string[] argv = Environment.GetCommandLineArgs();
            for (int i = 0; i < argv.Length - 1; i++)
                if (string.Equals(argv[i], "-" + name, StringComparison.OrdinalIgnoreCase)) return argv[i + 1];
            if (fallback == null) throw new InvalidOperationException("missing -" + name);
            return fallback;
        }

        static JarView OpenJar(string state)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("no graphics device; run without -nographics");
            ShaderUtil.allowAsyncCompilation = false;
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            JarView jar = UnityEngine.Object.FindAnyObjectByType<JarView>();
            if (jar == null) throw new InvalidOperationException("no JarView in " + Scene);
            jar.ApplyCaptureState(CaptureState.Parse(state));
            return jar;
        }

        static MeshRenderer GlassRenderer(JarView jar)
        {
            foreach (MeshRenderer r in jar.GetComponentsInChildren<MeshRenderer>(true))
                if (r.name == "Jar") return r;
            throw new InvalidOperationException("jar glass renderer not found");
        }

        static void HideWorldPlates()
        {
            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t != null && (t.name == "PcRoomPlate" || t.name == "PassthroughPlate")) t.gameObject.SetActive(false);
        }

        static Camera NewCamera(string name, float fov, float near, int w, int h)
        {
            var go = new GameObject(name);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = false;
            cam.nearClipPlane = near;
            cam.farClipPlane = 50f;
            cam.fieldOfView = fov;
            cam.allowHDR = false;
            cam.allowMSAA = true;
            cam.useOcclusionCulling = false;
            cam.cullingMask = ~0;
            cam.aspect = w / (float)h;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = true;
            return cam;
        }

        static void DisableCameras(Camera keep)
        {
            foreach (Camera c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude))
                if (c != keep && c.GetComponent<UniversalAdditionalCameraData>() != null) c.enabled = false;
        }

        static string PlatePath(string file)
        {
            string repo = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            string[] candidates =
            {
                Path.Combine(Application.dataPath, "Capture", file),
                Path.Combine(repo, "shared", "assets", "room-plates", file)
            };
            foreach (string c in candidates) if (File.Exists(c)) return c;
            throw new InvalidOperationException("plate not found: " + file);
        }

        /// <summary>The JarG1 pose (same maths as CaptureCli.CreateCamera) and its plate quad, parented to a pose object.</summary>
        static Transform MakeG1Pose(JarView jar, Framing framing, int w, int h, Camera cam)
        {
            Vector3 eye = jar.transform.TransformPoint(framing.Eye);
            Vector3 look = jar.transform.TransformPoint(framing.LookAt);
            Quaternion rot = Quaternion.LookRotation(look - eye, Vector3.up) * Quaternion.Euler(-framing.LensShift.y, framing.LensShift.x, 0f);
            if (cam != null) cam.transform.SetPositionAndRotation(eye, rot);
            var pose = new GameObject("G1Pose").transform;
            pose.SetPositionAndRotation(eye, rot);

            Shader shader = Shader.Find("Fidelity/Plate");
            if (shader == null) throw new InvalidOperationException("shader not found: Fidelity/Plate");
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!tex.LoadImage(File.ReadAllBytes(PlatePath(framing.Plate)))) throw new InvalidOperationException("plate unreadable");
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            var mat = new Material(shader) { name = "S2PlateMat" };
            mat.SetTexture("_MainTex", tex);
            mat.SetFloat("_Exposure", 1f);
            ShaderUtil.CompilePass(mat, 0, true);

            const float d = 2f;
            float worldHeight = 2f * d * Mathf.Tan(framing.Fov * 0.5f * Mathf.Deg2Rad);
            float worldWidth = worldHeight * (w / (float)h);
            var quad = new GameObject("CapturePlate");
            quad.transform.SetParent(pose, false);
            quad.transform.localPosition = new Vector3(0f, 0f, d);
            quad.transform.localRotation = Quaternion.identity;
            quad.transform.localScale = new Vector3(worldWidth, worldHeight, 1f);
            var mesh = new Mesh { name = "S2PlateQuad" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            quad.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = quad.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return pose;
        }

        // Forward and up per strip face. The shader reads u from the camera's right and v from its up (see StripFace).
        static readonly Vector3[] Forward = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        static readonly Vector3[] Up = { Vector3.up, Vector3.up, Vector3.back, Vector3.forward, Vector3.up, Vector3.up };

        static string DoBake()
        {
            string outPath = Path.GetFullPath(Arg("out", null));
            JarView jar = OpenJar(G1State);
            Framing framing = Framings.LoadApp().Get("JarG1");
            HideWorldPlates();
            Camera cam = NewCamera("S2BakeCamera", 90f, 0.004f, Face, Face);
            DisableCameras(cam);
            MakeG1Pose(jar, framing, framing.Width, framing.Height, null);
            GlassRenderer(jar).enabled = false;

            Vector3 probe = jar.transform.TransformPoint(new Vector3(0f, 0.06f, 0f));
            cam.transform.position = probe;
            var strip = new Texture2D(Face * 6, Face, TextureFormat.RGBA32, false, false);
            // One discarded render so async compilation cannot poison the first face.
            UnityEngine.Object.DestroyImmediate(FrameGrab.RenderToTexture(cam, 64, 64, 1));
            for (int f = 0; f < 6; f++)
            {
                cam.transform.rotation = Quaternion.LookRotation(Forward[f], Up[f]);
                Texture2D face = FrameGrab.RenderToTexture(cam, Face, Face, 4);
                strip.SetPixels32(f * Face, 0, Face, Face, face.GetPixels32());
                UnityEngine.Object.DestroyImmediate(face);
            }
            strip.Apply(false, false);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? ".");
            File.WriteAllBytes(outPath, strip.EncodeToPNG());
            Debug.Log("[S2] probe " + probe.ToString("F4") + " jar " + jar.transform.position.ToString("F4"));
            return outPath;
        }

        sealed class Look
        {
            public string Name, State;
            public bool GlassOff;
            public List<double> Runs = new List<double>();
        }

        static string DoTime()
        {
            string outPath = Path.GetFullPath(Arg("out", null));
            int frames = int.Parse(Arg("frames", "240"), CultureInfo.InvariantCulture);
            int scale = int.Parse(Arg("scale", "1"), CultureInfo.InvariantCulture);
            int rounds = int.Parse(Arg("rounds", "5"), CultureInfo.InvariantCulture);
            JarView jar = OpenJar(G1State);
            Framing framing = Framings.LoadApp().Get("JarG1");
            int w = framing.Width * scale, h = framing.Height * scale;
            HideWorldPlates();
            Camera cam = NewCamera("S2TimeCamera", framing.Fov, 0.02f, w, h);
            DisableCameras(cam);
            MakeG1Pose(jar, framing, w, h, cam);
            MeshRenderer glass = GlassRenderer(jar);

            var looks = new List<Look>
            {
                new Look { Name = "noglass", State = G1State, GlassOff = true },
                new Look { Name = "a", State = G1State },
                new Look { Name = "s1", State = G1State + ",variant=s1" },
                new Look { Name = "s2b0", State = G1State + ",variant=s2b0" },
                new Look { Name = "s2b1", State = G1State + ",variant=s2b1" },
                new Look { Name = "s2b2", State = G1State + ",variant=s2b2" }
            };
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
            rt.Create();
            cam.targetTexture = rt;
            var sync = new Texture2D(1, 1, TextureFormat.RGBA32, false);

            for (int round = 0; round < rounds + 1; round++)
            {
                foreach (Look look in looks)
                {
                    jar.ApplyCaptureState(CaptureState.Parse(look.State));
                    glass.enabled = !look.GlassOff;
                    for (int i = 0; i < 12; i++) cam.Render();
                    RenderTexture.active = rt;
                    sync.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);
                    var sw = Stopwatch.StartNew();
                    for (int i = 0; i < frames; i++) cam.Render();
                    sync.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);
                    sw.Stop();
                    RenderTexture.active = null;
                    // Round 0 is a warm-up for every variant.
                    if (round > 0) look.Runs.Add(sw.Elapsed.TotalMilliseconds / frames);
                }
            }
            var json = new StringBuilder();
            json.Append("{\"gpu\":\"").Append(SystemInfo.graphicsDeviceName).Append("\",\"api\":\"").Append(SystemInfo.graphicsDeviceType)
                .Append("\",\"width\":").Append(w).Append(",\"height\":").Append(h).Append(",\"msaa\":8,\"framesPerRun\":").Append(frames)
                .Append(",\"rounds\":").Append(rounds).Append(",\"looks\":[");
            for (int k = 0; k < looks.Count; k++)
            {
                Look look = looks[k];
                look.Runs.Sort();
                double median = look.Runs[look.Runs.Count / 2];
                if (k > 0) json.Append(",");
                json.Append("{\"name\":\"").Append(look.Name).Append("\",\"medianMs\":").Append(median.ToString("F4", CultureInfo.InvariantCulture))
                    .Append(",\"minMs\":").Append(look.Runs[0].ToString("F4", CultureInfo.InvariantCulture))
                    .Append(",\"maxMs\":").Append(look.Runs[look.Runs.Count - 1].ToString("F4", CultureInfo.InvariantCulture)).Append("}");
            }
            json.Append("]}");
            Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? ".");
            File.WriteAllText(outPath, json.ToString());
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            return outPath;
        }
    }
}
#endif
