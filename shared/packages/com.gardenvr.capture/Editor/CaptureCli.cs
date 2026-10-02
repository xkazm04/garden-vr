using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GardenVR.Capture.Editor
{
    /// <summary>
    /// Batchmode entry points. Each logs "[Capture] &lt;entry&gt; OK &lt;out&gt;" or "[Capture] &lt;entry&gt; FAIL &lt;reason&gt;"
    /// and exits non-zero on failure.
    /// </summary>
    public static class CaptureCli
    {
        public static void Shot() { Guard("Shot", DoShot); }
        public static void Crops() { Guard("Crops", DoCrops); }
        public static void Diff() { Guard("Diff", DoDiff); }
        public static void Measure() { Guard("Measure", DoMeasure); }

        static void Guard(string entry, Func<string> body)
        {
            try
            {
                string output = body();
                Debug.Log("[Capture] " + entry + " OK " + output);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                string reason = (e.Message ?? e.GetType().Name).Replace("\r", " ").Replace("\n", " ");
                Debug.LogError("[Capture] " + entry + " FAIL " + reason);
                EditorApplication.Exit(1);
            }
        }

        static string DoShot()
        {
            RequireGraphics();
            ShaderUtil.allowAsyncCompilation = false;
            Args args = Args.Parse();
            string scene = args.Require("scene");
            string framingName = args.Require("framing");
            string outPath = Path.GetFullPath(args.Require("out"));
            Framing framing = Framings.LoadApp().Get(framingName);
            int width = args.Has("w") ? args.Int("w") : framing.Width;
            int height = args.Has("h") ? args.Int("h") : framing.Height;
            int msaa = args.Has("msaa") ? args.Int("msaa") : 8;
            if (width < 1 || height < 1) throw new InvalidOperationException("width and height must be positive");
            msaa = SanitizeMsaa(msaa);

            OpenScene(scene);
            string stateRaw = args.Optional("state");
            if (!string.IsNullOrEmpty(stateRaw)) ApplyState(CaptureState.Parse(stateRaw));
            Transform target = ResolveTarget(args.Optional("target"), true);
            if (!string.IsNullOrEmpty(framing.Plate)) DisableWorldPlates();

            Camera cam = CreateCamera(framing, target, width, height);
            Texture2D plate = null;
            if (!string.IsNullOrEmpty(framing.Plate))
            {
                string platePath = ResolveFile(framing.Plate);
                if (platePath == null) throw new InvalidOperationException("plate not found: " + framing.Plate);
                plate = LoadPlate(platePath);
                AttachPlate(cam, plate, framing.Fov, width, height);
            }

            // One discarded render so async shader compilation cannot poison the saved frame.
            UnityEngine.Object.DestroyImmediate(FrameGrab.RenderToTexture(cam, Math.Min(width, 320), Math.Min(height, 180), 1));

            Texture2D shot = FrameGrab.RenderToTexture(cam, width, height, msaa);
            Color32[] pixels = shot.GetPixels32();
            byte[] png;
            string overlaySpec = args.Optional("overlay");
            if (string.IsNullOrEmpty(overlaySpec)) overlaySpec = framing.Overlay;
            if (!string.IsNullOrEmpty(overlaySpec))
            {
                string overlayPath = ResolveFile(overlaySpec);
                if (overlayPath == null) throw new InvalidOperationException("overlay not found: " + overlaySpec);
                pixels = CompositeOverlay(pixels, width, height, overlayPath, args.Optional("reference"));
                png = FrameGrab.EncodePixels(pixels, width, height);
                UnityEngine.Object.DestroyImmediate(shot);
            }
            else
            {
                png = shot.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(shot);
            }
            if (plate != null) UnityEngine.Object.DestroyImmediate(plate);

            EnsureParent(outPath);
            File.WriteAllBytes(outPath, png);
            ImageStats stats = ImageCheck.Analyze(pixels, width, height, png);
            File.WriteAllText(outPath + ".check.json", ImageCheck.ToJson(stats));

            string referenceArg = args.Optional("reference");
            if (!string.IsNullOrEmpty(referenceArg))
            {
                string referencePath = ResolveFile(referenceArg);
                if (referencePath == null) throw new InvalidOperationException("reference not found: " + referenceArg);
                RgbaImage reference = PngIO.Load(referencePath);
                var render = new RgbaImage(width, height, pixels);
                string stateLabel = string.IsNullOrEmpty(stateRaw) ? "none" : stateRaw;
                RgbaImage sbs = SideBySide.Compose(reference, render, Path.GetFileName(referencePath), Path.GetFileName(outPath), framingName, stateLabel);
                FrameGrab.WritePixels(Stem(outPath, ".sbs.png"), sbs.Pixels, sbs.Width, sbs.Height);
            }
            return outPath;
        }

        static string DoCrops()
        {
            Args args = Args.Parse();
            RgbaImage a = PngIO.Load(ResolveExisting(args.Require("a")));
            RgbaImage b = PngIO.Load(ResolveExisting(args.Require("b")));
            List<RectInt> regions = ImageCrops.ParseRegions(args.Require("regions"));
            RgbaImage stacked = ImageCrops.Stack(a, b, regions);
            string outPath = Path.GetFullPath(args.Require("out"));
            FrameGrab.WritePixels(outPath, stacked.Pixels, stacked.Width, stacked.Height);
            return outPath;
        }

        static string DoDiff()
        {
            Args args = Args.Parse();
            RgbaImage a = PngIO.Load(ResolveExisting(args.Require("a")));
            RgbaImage b = PngIO.Load(ResolveExisting(args.Require("b")));
            RgbaImage mask = default(RgbaImage);
            string maskArg = args.Optional("mask");
            if (!string.IsNullOrEmpty(maskArg)) mask = PngIO.Load(ResolveExisting(maskArg));
            DiffResult diff = ImageDiff.Compare(a, b, mask);
            string outPath = Path.GetFullPath(args.Require("out"));
            EnsureParent(outPath);
            File.WriteAllText(outPath, CaptureJson.Diff(diff));
            return outPath;
        }

        static string DoMeasure()
        {
            RequireGraphics();
            ShaderUtil.allowAsyncCompilation = false;
            Args args = Args.Parse();
            OpenScene(args.Require("scene"));
            string stateRaw = args.Optional("state");
            if (!string.IsNullOrEmpty(stateRaw)) ApplyState(CaptureState.Parse(stateRaw));
            string targetId = args.Require("target");
            Transform target = ResolveTarget(targetId, false);
            string framingName = args.Optional("framing");
            if (string.IsNullOrEmpty(framingName)) framingName = "SeatedPOV";
            Framing framing = Framings.LoadApp().Get(framingName);
            int width = args.Has("w") ? args.Int("w") : framing.Width;
            int height = args.Has("h") ? args.Int("h") : framing.Height;
            if (width < 1 || height < 1) throw new InvalidOperationException("width and height must be positive");

            DisableWorldPlates();
            Camera cam = CreateCamera(framing, target, width, height);
            Shader overdraw = Shader.Find("Fidelity/Overdraw");
            if (overdraw == null) throw new InvalidOperationException("shader not found: Fidelity/Overdraw");
            var warmMat = new Material(overdraw);
            ShaderUtil.CompilePass(warmMat, 0, true);
            UnityEngine.Object.DestroyImmediate(warmMat);
            UnityEngine.Object.DestroyImmediate(FrameGrab.RenderToTexture(cam, Math.Min(width, 320), Math.Min(height, 180), 1));

            List<Renderer> renderers = SceneRenderers();
            BudgetReport report = BudgetMeasure.Count(renderers);
            report.Framing = framingName;
            report.Target = targetId;
            string outPath = Path.GetFullPath(args.Require("out"));
            string pngPath = Stem(outPath, ".overdraw.png");
            Renderer[] targetRenderers = target.GetComponentsInChildren<Renderer>(false);
            BudgetMeasure.RenderOverdraw(cam, renderers, targetRenderers, width, height, pngPath, report);
            EnsureParent(outPath);
            File.WriteAllText(outPath, CaptureJson.Budget(report));
            return outPath;
        }

        static Camera CreateCamera(Framing framing, Transform target, int width, int height)
        {
            var go = new GameObject("CaptureCamera");
            var cam = go.AddComponent<Camera>();
            cam.enabled = true;
            cam.orthographic = false;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 50f;
            cam.fieldOfView = framing.Fov;
            cam.allowHDR = false;
            cam.allowMSAA = true;
            cam.useOcclusionCulling = false;
            cam.cullingMask = ~0;
            cam.aspect = width / (float)height;
            bool seated = framing.Name == "SeatedPOV" && string.IsNullOrEmpty(framing.Plate);
            cam.clearFlags = seated ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = true;

            Transform eye = null;
            if (framing.Name == "SeatedPOV" && target == null)
            {
                GameObject eyeObject = GameObject.Find("EyeCamera");
                if (eyeObject != null) eye = eyeObject.transform;
            }
            if (eye != null)
            {
                cam.transform.SetPositionAndRotation(eye.position, eye.rotation);
                Camera eyeCam = eye.GetComponent<Camera>();
                if (eyeCam != null && seated)
                {
                    cam.clearFlags = eyeCam.clearFlags;
                    cam.backgroundColor = eyeCam.backgroundColor;
                }
            }
            else
            {
                Vector3 eyePos = target != null ? target.TransformPoint(framing.Eye) : framing.Eye;
                Vector3 look = target != null ? target.TransformPoint(framing.LookAt) : framing.LookAt;
                Vector3 forward = look - eyePos;
                if (forward.sqrMagnitude < 1e-8f) throw new InvalidOperationException("framing look-at equals the eye");
                // Same order as FidelityTools.MakeCamera: LookAt, then a right-multiplied Euler offset.
                cam.transform.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(-framing.LensShift.y, framing.LensShift.x, 0f);
                cam.transform.position = eyePos;
            }
            DisableOtherCameras(cam);
            return cam;
        }

        static void AttachPlate(Camera cam, Texture2D plate, float fovDeg, int pixelWidth, int pixelHeight)
        {
            Shader shader = Shader.Find("Fidelity/Plate");
            if (shader == null) throw new InvalidOperationException("shader not found: Fidelity/Plate");
            var mat = new Material(shader) { name = "CapturePlateMat" };
            mat.SetTexture("_MainTex", plate);
            mat.SetFloat("_Exposure", 1f);
            ShaderUtil.CompilePass(mat, 0, true);

            // The quad fills the vertical FOV at distance d, so each texel lands on its pixel. Point filtering keeps it there.
            const float d = 2f;
            float worldHeight = 2f * d * Mathf.Tan(fovDeg * 0.5f * Mathf.Deg2Rad);
            float worldWidth = worldHeight * (pixelWidth / (float)pixelHeight);
            var go = new GameObject("CapturePlate");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, d);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(worldWidth, worldHeight, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = PlateQuad();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static Mesh PlateQuad()
        {
            var mesh = new Mesh { name = "CapturePlateQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        static Texture2D LoadPlate(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!tex.LoadImage(bytes)) throw new InvalidOperationException("could not read plate: " + path);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.anisoLevel = 0;
            tex.name = Path.GetFileNameWithoutExtension(path);
            return tex;
        }

        static Color32[] CompositeOverlay(Color32[] dest, int width, int height, string overlayPath, string referenceArg)
        {
            RgbaImage overlay = PngIO.Load(overlayPath);
            if (overlay.Width != width || overlay.Height != height)
                throw new InvalidOperationException("overlay size " + overlay.Width + "x" + overlay.Height + " does not match the shot " + width + "x" + height);
            bool partialAlpha = false;
            for (int i = 0; i < overlay.Pixels.Length; i++)
            {
                byte a = overlay.Pixels[i].a;
                if (a > 5 && a < 250) { partialAlpha = true; break; }
            }
            if (partialAlpha)
            {
                for (int i = 0; i < dest.Length; i++)
                {
                    Color32 o = overlay.Pixels[i];
                    float a = o.a / 255f;
                    dest[i] = new Color32(
                        (byte)(o.r * a + dest[i].r * (1f - a)),
                        (byte)(o.g * a + dest[i].g * (1f - a)),
                        (byte)(o.b * a + dest[i].b * (1f - a)),
                        255);
                }
                return dest;
            }
            if (string.IsNullOrEmpty(referenceArg))
                throw new InvalidOperationException("opaque overlay is a matte and needs -reference");
            string referencePath = ResolveFile(referenceArg);
            if (referencePath == null) throw new InvalidOperationException("reference not found: " + referenceArg);
            RgbaImage reference = PngIO.Load(referencePath);
            if (reference.Width != width || reference.Height != height)
                throw new InvalidOperationException("reference size does not match the shot");
            for (int i = 0; i < dest.Length; i++)
            {
                if (overlay.Pixels[i].r >= 128)
                    dest[i] = new Color32(reference.Pixels[i].r, reference.Pixels[i].g, reference.Pixels[i].b, 255);
            }
            return dest;
        }

        static void ApplyState(IReadOnlyDictionary<string, string> state)
        {
            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);
            int hosts = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                var host = behaviours[i] as ICaptureState;
                if (host == null) continue;
                host.ApplyCaptureState(state);
                hosts++;
            }
            if (hosts == 0) throw new InvalidOperationException("no ICaptureState in the scene for -state");
        }

        static Transform ResolveTarget(string id, bool optional)
        {
            if (string.IsNullOrEmpty(id))
            {
                if (optional) return null;
                throw new InvalidOperationException("missing -target");
            }
            Transform found = FindTarget(id);
            if (found == null) throw new InvalidOperationException("target not found: " + id);
            return found;
        }

        static Transform FindTarget(string id)
        {
            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour.GetType().Name != "IntentTarget") continue;
                string value = ReadId(behaviour);
                if (value == id) return behaviour.transform;
            }
            GameObject named = GameObject.Find(id);
            if (named != null) return named.transform;
            Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < transforms.Length; i++)
                if (transforms[i] != null && transforms[i].name == id) return transforms[i];
            return null;
        }

        static string ReadId(MonoBehaviour behaviour)
        {
            var type = behaviour.GetType();
            var field = type.GetField("Id") ?? type.GetField("id") ?? type.GetField("ID");
            if (field != null && field.FieldType == typeof(string)) return (string)field.GetValue(behaviour);
            var property = type.GetProperty("Id") ?? type.GetProperty("id");
            if (property != null && property.PropertyType == typeof(string)) return (string)property.GetValue(behaviour, null);
            return null;
        }

        static void DisableWorldPlates()
        {
            Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null) continue;
                if (t.name == "PcRoomPlate" || t.name == "PassthroughPlate")
                {
                    t.gameObject.SetActive(false);
                    continue;
                }
                Component[] comps = t.GetComponents<Component>();
                for (int c = 0; c < comps.Length; c++)
                {
                    if (comps[c] != null && comps[c].GetType().Name == "PcRoomPlate")
                        t.gameObject.SetActive(false);
                }
            }
        }

        static void DisableOtherCameras(Camera keep)
        {
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
            for (int i = 0; i < cameras.Length; i++)
                if (cameras[i] != keep) cameras[i].enabled = false;
        }

        static List<Renderer> SceneRenderers()
        {
            var list = new List<Renderer>();
            Renderer[] found = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            for (int i = 0; i < found.Length; i++)
            {
                Renderer renderer = found[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                string name = renderer.name;
                if (name == "CapturePlate" || name == "PcRoomPlate" || name == "PassthroughPlate") continue;
                list.Add(renderer);
            }
            return list;
        }

        static void OpenScene(string scene)
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string rel;
            if (Path.IsPathRooted(scene))
            {
                string full = Path.GetFullPath(scene);
                if (!full.StartsWith(project, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("scene is outside the project: " + scene);
                rel = full.Substring(project.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/');
            }
            else rel = scene.Replace('\\', '/');
            string abs = Path.Combine(project, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(abs)) throw new InvalidOperationException("scene not found: " + scene);
            EditorSceneManager.OpenScene(rel, OpenSceneMode.Single);
        }

        static void RequireGraphics()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("no graphics device; run this entry without -nographics");
        }

        static int SanitizeMsaa(int msaa)
        {
            if (msaa == 1 || msaa == 2 || msaa == 4 || msaa == 8) return msaa;
            throw new InvalidOperationException("msaa must be 1, 2, 4 or 8");
        }

        static string ResolveExisting(string file)
        {
            string path = ResolveFile(file);
            if (path == null) throw new InvalidOperationException("file not found: " + file);
            return path;
        }

        static string ResolveFile(string file)
        {
            if (string.IsNullOrEmpty(file)) return null;
            if (Path.IsPathRooted(file) && File.Exists(file)) return Path.GetFullPath(file);
            string repo = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            string[] candidates =
            {
                Path.Combine(Application.dataPath, "Capture", file),
                Path.Combine(repo, "shared", "assets", "room-plates", file),
                Path.Combine(repo, "shared", "assets", "art-reference", file),
                Path.Combine(repo, file.Replace('/', Path.DirectorySeparatorChar)),
                Path.GetFullPath(file)
            };
            for (int i = 0; i < candidates.Length; i++)
                if (File.Exists(candidates[i])) return candidates[i];
            return null;
        }

        static string Stem(string path, string suffix)
        {
            return Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path) + suffix);
        }

        static void EnsureParent(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        }

        sealed class Args
        {
            readonly Dictionary<string, string> _map;

            Args(Dictionary<string, string> map) { _map = map; }

            public bool Has(string key) { return _map.ContainsKey(key); }

            public string Optional(string key)
            {
                string value;
                return _map.TryGetValue(key, out value) ? value : null;
            }

            public string Require(string key)
            {
                string value = Optional(key);
                if (string.IsNullOrEmpty(value) || value == "true") throw new InvalidOperationException("missing -" + key);
                return value;
            }

            public int Int(string key)
            {
                int number;
                if (!int.TryParse(Optional(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                    throw new InvalidOperationException("expected an integer for -" + key);
                return number;
            }

            public static Args Parse()
            {
                string[] argv = Environment.GetCommandLineArgs();
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < argv.Length; i++)
                {
                    string arg = argv[i];
                    if (string.IsNullOrEmpty(arg) || arg[0] != '-' || arg == "-") continue;
                    string name = arg.StartsWith("--") ? arg.Substring(2) : arg.Substring(1);
                    int eq = name.IndexOf('=');
                    if (eq > 0)
                    {
                        map[name.Substring(0, eq)] = name.Substring(eq + 1);
                        continue;
                    }
                    if (i + 1 < argv.Length && !string.IsNullOrEmpty(argv[i + 1]) && argv[i + 1][0] != '-')
                        map[name] = argv[++i];
                    else
                        map[name] = "true";
                }
                return new Args(map);
            }
        }
    }
}
