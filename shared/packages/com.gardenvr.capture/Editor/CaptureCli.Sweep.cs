using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Capture.Editor
{
    /// <summary>
    /// <c>CaptureCli.Sweep</c>: one Unity launch, one scene open, many variants. Each variant is a state delta and/or
    /// material overrides on runtime copies (<c>new Material(shared)</c>), so nothing under Assets/ is touched.
    /// The base variant is rendered first and last, which measures drift inside the launch.
    /// </summary>
    public static partial class CaptureCli
    {
        sealed class SweepVariant
        {
            public string Id;
            public string Label;
            public string Axis;
            public string Value;
            public string StateDelta;
            public bool IsBase;
            public JsonValue Materials;
            public JsonValue Textures;
        }

        sealed class SweepShot
        {
            public SweepVariant Variant;
            public string File;
            public string State;
            public ImageStats Stats;
            public long Millis;
            public readonly List<string> Overrides = new List<string>();
            public readonly List<KeyValuePair<string, string>> TextureFiles = new List<KeyValuePair<string, string>>();
        }

        sealed class Touched
        {
            public Renderer Renderer;
            public Material[] Original;
        }

        static string DoSweep()
        {
            RequireGraphics();
            ShaderUtil.allowAsyncCompilation = false;
            var launch = Stopwatch.StartNew();
            Args args = Args.Parse();
            string specPath = ResolveExisting(args.Require("sweep"));
            string outDir = Path.GetFullPath(args.Require("out"));
            JsonValue spec = JsonValue.Parse(File.ReadAllText(specPath));

            string scene = SweepString(spec, "scene", args.Optional("scene"));
            if (string.IsNullOrEmpty(scene)) throw new InvalidOperationException("sweep spec needs a scene");
            string framingName = SweepString(spec, "framing", args.Optional("framing"));
            if (string.IsNullOrEmpty(framingName)) throw new InvalidOperationException("sweep spec needs a framing");
            string baseState = SweepString(spec, "state", args.Optional("state")) ?? "";
            string targetId = SweepString(spec, "target", args.Optional("target"));
            string referenceArg = SweepString(spec, "reference", args.Optional("reference"));
            string baseId = SweepString(spec, "base", null);
            if (string.IsNullOrEmpty(baseId)) baseId = "base";
            string prefix = SweepString(spec, "filePrefix", "") ?? "";
            Framing framing = Framings.LoadApp().Get(framingName);
            int width = SweepInt(spec, "w", args.Has("w") ? args.Int("w") : framing.Width);
            int height = SweepInt(spec, "h", args.Has("h") ? args.Int("h") : framing.Height);
            int msaa = SanitizeMsaa(SweepInt(spec, "msaa", args.Has("msaa") ? args.Int("msaa") : 8));
            if (width < 1 || height < 1) throw new InvalidOperationException("width and height must be positive");

            var variants = new List<SweepVariant>();
            variants.Add(new SweepVariant { Id = baseId, Label = baseId, Axis = "base", Value = "first", IsBase = true });
            JsonValue list = spec.Get("variants");
            if (list == null || list.Type != JsonValue.Kind.Array || list.Arr.Count == 0)
                throw new InvalidOperationException("sweep spec needs a non-empty variants array");
            var seen = new HashSet<string> { baseId, baseId + "-last" };
            for (int i = 0; i < list.Arr.Count; i++)
            {
                JsonValue entry = list.Arr[i];
                string id = SweepString(entry, "id", null);
                if (string.IsNullOrEmpty(id)) throw new InvalidOperationException("variant " + i + " has no id");
                if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new InvalidOperationException("variant id is not a file name: " + id);
                if (!seen.Add(id)) throw new InvalidOperationException("duplicate variant id: " + id);
                variants.Add(new SweepVariant
                {
                    Id = id,
                    Label = SweepString(entry, "label", id),
                    Axis = SweepString(entry, "axis", ""),
                    Value = SweepString(entry, "value", ""),
                    StateDelta = SweepString(entry, "state", ""),
                    Materials = entry.Get("materials"),
                    Textures = entry.Get("textures")
                });
            }
            variants.Add(new SweepVariant { Id = baseId + "-last", Label = baseId + "-last", Axis = "base", Value = "last", IsBase = true });

            OpenScene(scene);
            Transform target = ResolveTarget(targetId, true);
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

            Directory.CreateDirectory(outDir);
            var textureCache = new Dictionary<string, Texture2D>();
            var shots = new List<SweepShot>();
            // Scene hosts such as JarView write into shared material assets when a state is applied. Snapshot them
            // first and put them back last, so Unity has nothing to save and git stays clean.
            var guard = new AssetGuard();
            List<string> dirtyAssets = null;
            try
            {
                for (int i = 0; i < variants.Count; i++)
                {
                    SweepVariant variant = variants[i];
                    var timer = Stopwatch.StartNew();
                    string state = MergeState(baseState, variant.StateDelta);
                    // The state is applied every variant, so a delta never leaks into the next one.
                    if (!string.IsNullOrEmpty(state)) ApplyState(CaptureState.Parse(state));

                    var shot = new SweepShot { Variant = variant, State = state, File = prefix + variant.Id + ".png" };
                    var touched = new List<Touched>();
                    var copies = new Dictionary<string, Material>();
                    try
                    {
                        ApplyMaterialOverrides(variant, copies, touched, textureCache, shot);
                        // One discarded render after any change, so late shader or texture work cannot reach the saved frame.
                        UnityEngine.Object.DestroyImmediate(FrameGrab.RenderToTexture(cam, Math.Min(width, 320), Math.Min(height, 180), 1));
                        string outPath = Path.Combine(outDir, shot.File);
                        shot.Stats = RenderSweepShot(cam, width, height, msaa, framing, referenceArg, outPath);
                    }
                    finally
                    {
                        for (int t = 0; t < touched.Count; t++)
                            if (touched[t].Renderer != null) touched[t].Renderer.sharedMaterials = touched[t].Original;
                        foreach (KeyValuePair<string, Material> pair in copies)
                            UnityEngine.Object.DestroyImmediate(pair.Value);
                    }
                    shot.Millis = timer.ElapsedMilliseconds;
                    shots.Add(shot);
                    UnityEngine.Debug.Log("[Capture] Sweep shot " + variant.Id + " sha256=" + shot.Stats.Sha256 + " ms=" + shot.Millis);
                }
            }
            finally
            {
                foreach (KeyValuePair<string, Texture2D> pair in textureCache) UnityEngine.Object.DestroyImmediate(pair.Value);
                if (plate != null) UnityEngine.Object.DestroyImmediate(plate);
                dirtyAssets = guard.Restore();
            }

            if (dirtyAssets.Count > 0)
                UnityEngine.Debug.LogWarning("[Capture] Sweep left assets dirty (not restorable): " + string.Join(", ", dirtyAssets.ToArray()));
            File.WriteAllText(Path.Combine(outDir, "sweep.json"),
                SweepManifest(scene, framingName, baseState, baseId, width, height, msaa, specPath, shots, launch.ElapsedMilliseconds, guard.Restored, dirtyAssets));
            return outDir;
        }

        /// <summary>Snapshot of every Assets/ material that is loaded, taken before any state is applied.</summary>
        sealed class AssetGuard
        {
            readonly List<KeyValuePair<Material, Material>> _snapshots = new List<KeyValuePair<Material, Material>>();
            public int Restored;

            public AssetGuard()
            {
                // Load every material under Assets/, not only the ones the scene has pulled in so far: a host can
                // reach a material the first time a state is applied (JarView and the desk spill, for one).
                string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
                for (int i = 0; i < guids.Length; i++)
                {
                    Material m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[i]));
                    if (m == null) continue;
                    var copy = new Material(m) { hideFlags = HideFlags.HideAndDontSave };
                    _snapshots.Add(new KeyValuePair<Material, Material>(m, copy));
                }
            }

            /// <summary>Puts the materials back, clears their dirty flag, and names any other asset still dirty.</summary>
            public List<string> Restore()
            {
                for (int i = 0; i < _snapshots.Count; i++)
                {
                    Material live = _snapshots[i].Key;
                    Material saved = _snapshots[i].Value;
                    if (live != null)
                    {
                        live.CopyPropertiesFromMaterial(saved);
                        live.renderQueue = saved.renderQueue;
                        EditorUtility.ClearDirty(live);
                        Restored++;
                    }
                    UnityEngine.Object.DestroyImmediate(saved);
                }
                _snapshots.Clear();
                var dirty = new List<string>();
                UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll<UnityEngine.Object>();
                for (int i = 0; i < objects.Length; i++)
                {
                    UnityEngine.Object o = objects[i];
                    if (o == null || !EditorUtility.IsDirty(o) || !AssetDatabase.Contains(o)) continue;
                    string path = AssetDatabase.GetAssetPath(o);
                    if (path != null && path.StartsWith("Assets/", StringComparison.Ordinal) && !path.EndsWith(".unity", StringComparison.Ordinal))
                        dirty.Add(path);
                }
                return dirty;
            }
        }

        static void ApplyMaterialOverrides(SweepVariant variant, Dictionary<string, Material> copies, List<Touched> touched,
            Dictionary<string, Texture2D> textureCache, SweepShot shot)
        {
            var names = new List<string>();
            if (variant.Materials != null && variant.Materials.Type == JsonValue.Kind.Object)
                foreach (string name in variant.Materials.Obj.Keys) if (!names.Contains(name)) names.Add(name);
            if (variant.Textures != null && variant.Textures.Type == JsonValue.Kind.Object)
                foreach (string name in variant.Textures.Obj.Keys) if (!names.Contains(name)) names.Add(name);
            if (names.Count == 0) return;

            Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
            for (int n = 0; n < names.Count; n++)
            {
                string name = names[n];
                Material copy = null;
                int hits = 0;
                for (int r = 0; r < renderers.Length; r++)
                {
                    Renderer renderer = renderers[r];
                    if (renderer == null) continue;
                    Material[] original = renderer.sharedMaterials;
                    Material[] swapped = null;
                    for (int m = 0; m < original.Length; m++)
                    {
                        if (original[m] == null || BaseMaterialName(original[m]) != name) continue;
                        if (copy == null)
                        {
                            copy = new Material(original[m]) { name = name + " (sweep " + variant.Id + ")" };
                            copies[name] = copy;
                        }
                        else if (copy.shader != original[m].shader)
                            throw new InvalidOperationException("material name '" + name + "' is used with two shaders");
                        if (swapped == null) swapped = (Material[])original.Clone();
                        swapped[m] = copy;
                        hits++;
                    }
                    if (swapped == null) continue;
                    touched.Add(new Touched { Renderer = renderer, Original = original });
                    renderer.sharedMaterials = swapped;
                }
                if (copy == null) throw new InvalidOperationException("variant " + variant.Id + ": no renderer uses material '" + name + "'");

                JsonValue floats = variant.Materials != null ? variant.Materials.Get(name) : null;
                if (floats != null)
                {
                    if (floats.Type != JsonValue.Kind.Object) throw new InvalidOperationException("materials." + name + " must be an object");
                    foreach (KeyValuePair<string, JsonValue> pair in floats.Obj)
                    {
                        SetMaterialValue(copy, pair.Key, pair.Value);
                        shot.Overrides.Add(name + "." + pair.Key + "=" + DescribeValue(pair.Value));
                    }
                }
                JsonValue textures = variant.Textures != null ? variant.Textures.Get(name) : null;
                if (textures != null)
                {
                    if (textures.Type != JsonValue.Kind.Object) throw new InvalidOperationException("textures." + name + " must be an object");
                    foreach (KeyValuePair<string, JsonValue> pair in textures.Obj)
                    {
                        if (pair.Value == null || pair.Value.Type != JsonValue.Kind.String || string.IsNullOrEmpty(pair.Value.Str))
                            throw new InvalidOperationException("textures." + name + "." + pair.Key + " must be a file path");
                        string file = ResolveTextureFile(pair.Value.Str);
                        string[] properties = pair.Key.Split(',');
                        for (int p = 0; p < properties.Length; p++)
                        {
                            string property = properties[p].Trim();
                            if (property.Length == 0) continue;
                            SetMaterialTexture(copy, property, file, textureCache);
                            shot.Overrides.Add(name + "." + property + "=" + Path.GetFileName(file));
                        }
                        shot.TextureFiles.Add(new KeyValuePair<string, string>(pair.Value.Str, ImageCheck.Hash(File.ReadAllBytes(file))));
                    }
                }
            }
        }

        static string BaseMaterialName(Material material)
        {
            string name = material.name;
            const string suffix = " (Instance)";
            return name.EndsWith(suffix, StringComparison.Ordinal) ? name.Substring(0, name.Length - suffix.Length) : name;
        }

        static void SetMaterialValue(Material material, string property, JsonValue value)
        {
            Shader shader = material.shader;
            int index = shader.FindPropertyIndex(property);
            if (index < 0) throw new InvalidOperationException("shader " + shader.name + " has no property " + property);
            ShaderPropertyType type = shader.GetPropertyType(index);
            if (type == ShaderPropertyType.Texture) throw new InvalidOperationException(property + " is a texture; use \"textures\"");
            if (value.Type == JsonValue.Kind.Number)
            {
                if (type != ShaderPropertyType.Float && type != ShaderPropertyType.Range && type != ShaderPropertyType.Int)
                    throw new InvalidOperationException(property + " is " + type + ", not a number");
                material.SetFloat(property, (float)value.Number);
                return;
            }
            if (value.Type == JsonValue.Kind.Array)
            {
                int count = value.Arr.Count;
                if (count < 3 || count > 4) throw new InvalidOperationException(property + " needs 3 or 4 numbers");
                var v = new float[4] { 0f, 0f, 0f, 1f };
                for (int i = 0; i < count; i++)
                {
                    if (value.Arr[i].Type != JsonValue.Kind.Number) throw new InvalidOperationException(property + " needs numbers");
                    v[i] = (float)value.Arr[i].Number;
                }
                if (type == ShaderPropertyType.Color) material.SetColor(property, new Color(v[0], v[1], v[2], v[3]));
                else if (type == ShaderPropertyType.Vector) material.SetVector(property, new Vector4(v[0], v[1], v[2], count == 4 ? v[3] : 0f));
                else throw new InvalidOperationException(property + " is " + type + ", not a colour or vector");
                return;
            }
            if (value.Type == JsonValue.Kind.String && type == ShaderPropertyType.Color)
            {
                Color colour;
                if (!ColorUtility.TryParseHtmlString(value.Str, out colour)) throw new InvalidOperationException("bad colour for " + property + ": " + value.Str);
                material.SetColor(property, colour);
                return;
            }
            throw new InvalidOperationException("unsupported value for " + property);
        }

        static void SetMaterialTexture(Material material, string property, string file, Dictionary<string, Texture2D> cache)
        {
            Shader shader = material.shader;
            int index = shader.FindPropertyIndex(property);
            if (index < 0) throw new InvalidOperationException("shader " + shader.name + " has no property " + property);
            if (shader.GetPropertyType(index) != ShaderPropertyType.Texture)
                throw new InvalidOperationException(property + " is not a texture property");
            // The sampler state and colour space follow the texture being replaced, so the override differs only in texels.
            Texture current = material.GetTexture(property);
            bool srgb = current == null || current.isDataSRGB;
            string key = file + "|" + (srgb ? "srgb" : "linear") + "|" + (current == null ? "none" : current.wrapMode + "/" + current.filterMode + "/" + current.anisoLevel);
            Texture2D tex;
            if (!cache.TryGetValue(key, out tex))
            {
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, true, !srgb);
                if (!tex.LoadImage(File.ReadAllBytes(file), false)) throw new InvalidOperationException("could not read texture: " + file);
                tex.name = Path.GetFileNameWithoutExtension(file) + " (sweep)";
                tex.wrapMode = current != null ? current.wrapMode : TextureWrapMode.Repeat;
                tex.filterMode = current != null ? current.filterMode : FilterMode.Bilinear;
                tex.anisoLevel = current != null ? current.anisoLevel : 1;
                tex.Apply(true, false);
                cache[key] = tex;
            }
            material.SetTexture(property, tex);
        }

        static string ResolveTextureFile(string spec)
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string asProject = Path.Combine(project, spec.Replace('/', Path.DirectorySeparatorChar));
            if (!Path.IsPathRooted(spec) && File.Exists(asProject)) return asProject;
            return ResolveExisting(spec);
        }

        static ImageStats RenderSweepShot(Camera cam, int width, int height, int msaa, Framing framing, string referenceArg, string outPath)
        {
            Texture2D shot = FrameGrab.RenderToTexture(cam, width, height, msaa);
            Color32[] pixels = shot.GetPixels32();
            byte[] png;
            if (!string.IsNullOrEmpty(framing.Overlay))
            {
                string overlayPath = ResolveFile(framing.Overlay);
                if (overlayPath == null) throw new InvalidOperationException("overlay not found: " + framing.Overlay);
                pixels = CompositeOverlay(pixels, width, height, overlayPath, referenceArg);
                png = FrameGrab.EncodePixels(pixels, width, height);
                UnityEngine.Object.DestroyImmediate(shot);
            }
            else
            {
                png = shot.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(shot);
            }
            EnsureParent(outPath);
            File.WriteAllBytes(outPath, png);
            ImageStats stats = ImageCheck.Analyze(pixels, width, height, png);
            File.WriteAllText(outPath + ".check.json", ImageCheck.ToJson(stats));
            return stats;
        }

        static string MergeState(string baseState, string delta)
        {
            if (string.IsNullOrWhiteSpace(delta)) return baseState ?? "";
            var merged = CaptureState.Parse(baseState);
            foreach (KeyValuePair<string, string> pair in CaptureState.Parse(delta)) merged[pair.Key] = pair.Value;
            var sb = new StringBuilder();
            foreach (KeyValuePair<string, string> pair in merged)
            {
                if (sb.Length > 0) sb.Append(',');
                sb.Append(pair.Key).Append('=').Append(pair.Value);
            }
            return sb.ToString();
        }

        static string SweepString(JsonValue obj, string key, string fallback)
        {
            JsonValue v = obj == null ? null : obj.Get(key);
            if (v == null || v.IsNull) return fallback;
            if (v.Type == JsonValue.Kind.String) return v.Str;
            if (v.Type == JsonValue.Kind.Number) return CaptureJson.Num(v.Number);
            throw new InvalidOperationException("'" + key + "' must be a string");
        }

        static int SweepInt(JsonValue obj, string key, int fallback)
        {
            JsonValue v = obj == null ? null : obj.Get(key);
            if (v == null || v.IsNull) return fallback;
            if (v.Type != JsonValue.Kind.Number) throw new InvalidOperationException("'" + key + "' must be a number");
            return (int)v.Number;
        }

        static string DescribeValue(JsonValue value)
        {
            if (value.Type == JsonValue.Kind.Number) return CaptureJson.Num(value.Number);
            if (value.Type == JsonValue.Kind.String) return value.Str;
            if (value.Type == JsonValue.Kind.Array)
            {
                var parts = new List<string>();
                for (int i = 0; i < value.Arr.Count; i++) parts.Add(CaptureJson.Num(value.Arr[i].Number));
                return "[" + string.Join(",", parts.ToArray()) + "]";
            }
            return "?";
        }

        static string SweepManifest(string scene, string framing, string baseState, string baseId, int width, int height, int msaa,
            string specPath, List<SweepShot> shots, long launchMillis, int restoredMaterials, List<string> dirtyAssets)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"scene\": ").Append(CaptureJson.Quote(scene)).Append(",\n");
            sb.Append("  \"framing\": ").Append(CaptureJson.Quote(framing)).Append(",\n");
            sb.Append("  \"state\": ").Append(CaptureJson.Quote(baseState)).Append(",\n");
            sb.Append("  \"base\": ").Append(CaptureJson.Quote(baseId)).Append(",\n");
            sb.Append("  \"width\": ").Append(width).Append(",\n");
            sb.Append("  \"height\": ").Append(height).Append(",\n");
            sb.Append("  \"msaa\": ").Append(msaa).Append(",\n");
            sb.Append("  \"spec\": ").Append(CaptureJson.Quote(specPath.Replace('\\', '/'))).Append(",\n");
            sb.Append("  \"launchMillis\": ").Append(launchMillis).Append(",\n");
            sb.Append("  \"restoredMaterials\": ").Append(restoredMaterials).Append(",\n");
            sb.Append("  \"dirtyAssets\": [");
            for (int k = 0; k < dirtyAssets.Count; k++) sb.Append(k > 0 ? ", " : "").Append(CaptureJson.Quote(dirtyAssets[k]));
            sb.Append("],\n");
            sb.Append("  \"shots\": [\n");
            for (int i = 0; i < shots.Count; i++)
            {
                SweepShot s = shots[i];
                sb.Append("    {\n");
                sb.Append("      \"id\": ").Append(CaptureJson.Quote(s.Variant.Id)).Append(",\n");
                sb.Append("      \"label\": ").Append(CaptureJson.Quote(s.Variant.Label)).Append(",\n");
                sb.Append("      \"axis\": ").Append(CaptureJson.Quote(s.Variant.Axis)).Append(",\n");
                sb.Append("      \"value\": ").Append(CaptureJson.Quote(s.Variant.Value)).Append(",\n");
                sb.Append("      \"isBase\": ").Append(s.Variant.IsBase ? "true" : "false").Append(",\n");
                sb.Append("      \"file\": ").Append(CaptureJson.Quote(s.File)).Append(",\n");
                sb.Append("      \"sha256\": ").Append(CaptureJson.Quote(s.Stats.Sha256)).Append(",\n");
                sb.Append("      \"state\": ").Append(CaptureJson.Quote(s.State)).Append(",\n");
                sb.Append("      \"meanLuma\": ").Append(CaptureJson.Num(s.Stats.MeanLuma)).Append(",\n");
                sb.Append("      \"millis\": ").Append(s.Millis).Append(",\n");
                sb.Append("      \"overrides\": [");
                for (int k = 0; k < s.Overrides.Count; k++) sb.Append(k > 0 ? ", " : "").Append(CaptureJson.Quote(s.Overrides[k]));
                sb.Append("],\n");
                sb.Append("      \"textureFiles\": [");
                for (int k = 0; k < s.TextureFiles.Count; k++)
                    sb.Append(k > 0 ? ", " : "").Append("{\"path\": ").Append(CaptureJson.Quote(s.TextureFiles[k].Key))
                      .Append(", \"sha256\": ").Append(CaptureJson.Quote(s.TextureFiles[k].Value)).Append("}");
                sb.Append("]\n");
                sb.Append("    }").Append(i + 1 < shots.Count ? "," : "").Append("\n");
            }
            sb.Append("  ]\n}\n");
            return sb.ToString();
        }
    }
}
