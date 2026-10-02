using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GardenVR.Capture
{
    /// <summary>
    /// A named camera pose relative to a target root.
    /// LensShift is the FidelityTools screen offset in degrees, applied after LookAt as
    /// Quaternion.Euler(-lensShift.y, lensShift.x, 0). It is not Camera.lensShift.
    /// </summary>
    public sealed class Framing
    {
        public string Name;
        public Vector3 Eye;
        public Vector3 LookAt;
        public float Fov;
        public Vector2 LensShift;
        public string Plate;
        public string Overlay;
        public int Width = 1824;
        public int Height = 1024;

        public Framing Clone()
        {
            return (Framing)MemberwiseClone();
        }
    }

    public sealed class FramingCatalog
    {
        readonly Dictionary<string, Framing> _map;

        internal FramingCatalog(Dictionary<string, Framing> map)
        {
            _map = map;
        }

        public bool TryGet(string name, out Framing framing)
        {
            Framing found;
            if (name != null && _map.TryGetValue(name, out found))
            {
                framing = found.Clone();
                return true;
            }
            framing = null;
            return false;
        }

        public Framing Get(string name)
        {
            Framing framing;
            if (!TryGet(name, out framing))
                throw new InvalidOperationException("unknown framing: " + name);
            return framing;
        }

        public IEnumerable<string> Names { get { return _map.Keys; } }
    }

    public static class Framings
    {
        public const int DefaultWidth = 1824;
        public const int DefaultHeight = 1024;

        /// <summary>Built-ins, then Assets/Capture/framings.json overrides and additions. A missing file is built-ins only.</summary>
        public static FramingCatalog LoadApp()
        {
            string path = Path.Combine(Application.dataPath, "Capture", "framings.json");
            if (!File.Exists(path)) return Parse("");
            return Parse(File.ReadAllText(path));
        }

        public static FramingCatalog Parse(string json)
        {
            var map = BuiltInMap();
            if (string.IsNullOrWhiteSpace(json)) return new FramingCatalog(map);
            JsonValue root = JsonValue.Parse(json);
            if (root.Type != JsonValue.Kind.Object) throw new FormatException("framings json must be an object");
            JsonValue overrides = root.Get("overrides");
            if (overrides != null && !overrides.IsNull)
            {
                if (overrides.Type != JsonValue.Kind.Object) throw new FormatException("overrides must be an object");
                foreach (var pair in overrides.Obj)
                {
                    bool existed = map.ContainsKey(pair.Key);
                    Framing basis = existed ? map[pair.Key] : Blank(pair.Key);
                    map[pair.Key] = Apply(basis, pair.Value, existed);
                }
            }
            JsonValue additions = root.Get("additions");
            if (additions != null && !additions.IsNull)
            {
                if (additions.Type != JsonValue.Kind.Array) throw new FormatException("additions must be an array");
                for (int i = 0; i < additions.Arr.Count; i++)
                {
                    JsonValue item = additions.Arr[i];
                    JsonValue nameValue = item == null ? null : item.Get("name");
                    string name = nameValue != null && nameValue.Type == JsonValue.Kind.String ? nameValue.Str : null;
                    if (string.IsNullOrEmpty(name)) throw new FormatException("addition missing name");
                    bool existed = map.ContainsKey(name);
                    Framing basis = existed ? map[name] : Blank(name);
                    map[name] = Apply(basis, item, existed);
                }
            }
            return new FramingCatalog(map);
        }

        static Dictionary<string, Framing> BuiltInMap()
        {
            var map = new Dictionary<string, Framing>();
            // FidelityTools.JarCamera: eye (0, 0.175, -0.44), look (0, 0.072, 0), 28.2 deg, offset (0, -0.4).
            map["JarG1"] = new Framing
            {
                Name = "JarG1",
                Eye = new Vector3(0f, 0.175f, -0.44f),
                LookAt = new Vector3(0f, 0.072f, 0f),
                Fov = 28.2f,
                LensShift = new Vector2(0f, -0.4f),
                Plate = "plate-jar.png",
                Width = DefaultWidth,
                Height = DefaultHeight
            };
            // FidelityTools.DialCamera: eye (0, 0.322, -0.411), look (0, 0.012, 0), 30 deg, offset (3.6, 0.5).
            map["DialG1"] = new Framing
            {
                Name = "DialG1",
                Eye = new Vector3(0f, 0.322f, -0.411f),
                LookAt = new Vector3(0f, 0.012f, 0f),
                Fov = 30f,
                LensShift = new Vector2(3.6f, 0.5f),
                Plate = "plate-dial.png",
                Width = DefaultWidth,
                Height = DefaultHeight
            };
            // Seated rig from GardenBootstrap: eye (0, 1.15, 0), pitch 28 deg down, 90 deg FOV, no plate.
            // Shot uses the scene EyeCamera pose when SeatedPOV is requested without -target.
            float pitch = 28f * Mathf.Deg2Rad;
            Vector3 eye = new Vector3(0f, 1.15f, 0f);
            map["SeatedPOV"] = new Framing
            {
                Name = "SeatedPOV",
                Eye = eye,
                LookAt = eye + new Vector3(0f, -Mathf.Sin(pitch), Mathf.Cos(pitch)),
                Fov = 90f,
                LensShift = Vector2.zero,
                Plate = null,
                Width = DefaultWidth,
                Height = DefaultHeight
            };
            return map;
        }

        static Framing Blank(string name)
        {
            return new Framing
            {
                Name = name,
                Fov = 60f,
                Width = DefaultWidth,
                Height = DefaultHeight
            };
        }

        static Framing Apply(Framing basis, JsonValue obj, bool existed)
        {
            if (obj == null || obj.Type != JsonValue.Kind.Object) throw new FormatException("framing must be an object");
            Framing framing = basis.Clone();
            if (obj.Has("name") && !obj.Get("name").IsNull) framing.Name = RequiredString(obj.Get("name"), "name");
            if (obj.Has("eye")) framing.Eye = Vec3(obj.Get("eye"), "eye");
            if (obj.Has("lookAt")) framing.LookAt = Vec3(obj.Get("lookAt"), "lookAt");
            if (obj.Has("fov")) framing.Fov = (float)RequiredNumber(obj.Get("fov"), "fov");
            if (obj.Has("lensShift")) framing.LensShift = Vec2(obj.Get("lensShift"), "lensShift");
            if (obj.Has("plate")) framing.Plate = OptionalString(obj.Get("plate"));
            if (obj.Has("overlay")) framing.Overlay = OptionalString(obj.Get("overlay"));
            if (obj.Has("width")) framing.Width = (int)RequiredNumber(obj.Get("width"), "width");
            if (obj.Has("height")) framing.Height = (int)RequiredNumber(obj.Get("height"), "height");
            if (!existed && (!obj.Has("eye") || !obj.Has("lookAt") || !obj.Has("fov")))
                throw new FormatException("new framing " + framing.Name + " needs eye, lookAt and fov");
            if (framing.Width <= 0 || framing.Height <= 0)
                throw new FormatException("framing " + framing.Name + " needs a positive resolution");
            return framing;
        }

        static string OptionalString(JsonValue value)
        {
            if (value == null || value.IsNull) return null;
            if (value.Type != JsonValue.Kind.String) throw new FormatException("expected a string or null");
            return string.IsNullOrEmpty(value.Str) ? null : value.Str;
        }

        static string RequiredString(JsonValue value, string label)
        {
            if (value == null || value.Type != JsonValue.Kind.String || string.IsNullOrEmpty(value.Str))
                throw new FormatException(label + " must be a string");
            return value.Str;
        }

        static double RequiredNumber(JsonValue value, string label)
        {
            if (value == null || value.Type != JsonValue.Kind.Number) throw new FormatException(label + " must be a number");
            return value.Number;
        }

        static Vector3 Vec3(JsonValue value, string label)
        {
            if (value == null || value.Type != JsonValue.Kind.Array || value.Arr == null || value.Arr.Count != 3)
                throw new FormatException(label + " must be [x, y, z]");
            return new Vector3((float)RequiredNumber(value.Arr[0], label), (float)RequiredNumber(value.Arr[1], label), (float)RequiredNumber(value.Arr[2], label));
        }

        static Vector2 Vec2(JsonValue value, string label)
        {
            if (value == null || value.Type != JsonValue.Kind.Array || value.Arr == null || value.Arr.Count != 2)
                throw new FormatException(label + " must be [x, y]");
            return new Vector2((float)RequiredNumber(value.Arr[0], label), (float)RequiredNumber(value.Arr[1], label));
        }
    }
}
