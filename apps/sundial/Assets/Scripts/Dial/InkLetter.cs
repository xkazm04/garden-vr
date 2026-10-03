using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Sundial
{
    /// <summary>
    /// Patrick Hand on a cream note. The face is the OFL file in Resources/FirstRunHand.
    /// Notes are generated: ink outline, soft shadow, no grey box. Callers tint white so the ink stays ink.
    /// The shared card, quad, and note textures are not destroyed by a component.
    /// </summary>
    public static class InkLetter
    {
        public const string ResourceName = "FirstRunHand";

        public static readonly Color Ink = new Color(0.165f, 0.149f, 0.133f, 1f);
        public static readonly Color Paper = new Color(0.953f, 0.933f, 0.886f, 1f);
        public static readonly Color Glow = new Color(0.961f, 0.780f, 0.416f, 1f);

        static Font _hand;
        static Material _card;
        static Mesh _quad;
        static Texture2D _ask;
        static readonly Dictionary<string, Texture2D> Notes = new Dictionary<string, Texture2D>();

        public static Font Hand
        {
            get
            {
                if (_hand == null) _hand = Resources.Load<Font>(ResourceName);
                return _hand;
            }
        }

        public static void Apply(TextMesh mesh, string text, int fontSize, float characterSize)
        {
            if (mesh == null) return;
            mesh.text = text ?? "";
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = fontSize;
            mesh.characterSize = characterSize;
            mesh.color = Ink;
            Font font = Hand;
            if (font == null) return;
            font.RequestCharactersInTexture(mesh.text, fontSize, FontStyle.Normal);
            mesh.font = font;
            Renderer renderer = mesh.GetComponent<MeshRenderer>();
            if (renderer == null) return;
            renderer.sharedMaterial = font.material;
            if (font.material != null) font.material.renderQueue = 3100;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        public static GameObject AttachNote(Transform parent, Vector2 scale)
        {
            var go = new GameObject("paper");
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localPosition = new Vector3(0f, 0f, 0.002f);
            go.transform.localScale = new Vector3(Mathf.Max(0.01f, scale.x), Mathf.Max(0.01f, scale.y), 1f);
            go.AddComponent<MeshFilter>().sharedMesh = Quad();
            var renderer = go.AddComponent<MeshRenderer>();
            Material card = Card();
            if (card != null) renderer.sharedMaterial = card;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Tint(renderer, Note(scale.x / Mathf.Max(0.01f, scale.y)), Color.white);
            return go;
        }

        public static Texture2D Note(float aspect)
        {
            if (aspect < 0.35f) aspect = 0.35f;
            if (aspect > 8f) aspect = 8f;
            int height = 128;
            int width = Mathf.Clamp(Mathf.RoundToInt(height * aspect), 64, 640);
            string key = width + "x" + height;
            Texture2D cached;
            if (Notes.TryGetValue(key, out cached) && cached != null) return cached;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.name = "InkNote " + key;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color[width * height];
            float margin = Mathf.Max(8f, Mathf.Min(width, height) * 0.14f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float page = PageDist(px, py, margin, width, height);
                    float shadow = PageDist(px - width * 0.025f, py - height * 0.055f, margin, width, height);
                    float shade = Mathf.Clamp01((-shadow) / (margin * 0.9f));
                    shade = shade * shade * 0.42f;
                    float grain = Hash(x, y) * 0.012f;
                    Color paper = new Color(Paper.r - grain, Paper.g - grain * 0.8f, Paper.b - grain * 0.5f, 1f);
                    Color ink = Ink;
                    float stroke = Mathf.Clamp(Mathf.Min(width, height) * 0.018f, 1.7f, 2.8f);
                    float inkA = 1f - Mathf.Clamp01(Mathf.Abs(page) / stroke);
                    inkA = inkA * inkA;
                    Color color = new Color(0f, 0f, 0f, 0f);
                    if (page > 0f && shade > 0.01f)
                        color = new Color(0.10f, 0.09f, 0.08f, shade);
                    if (page <= 0f)
                        color = paper;
                    if (inkA > 0.04f)
                        color = Color.Lerp(color, ink, Mathf.Clamp01(inkA));
                    pixels[y * width + x] = color;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            Notes[key] = tex;
            return tex;
        }

        /// <summary>Yesterday's quiet ask. A pale square, an ink outline, a soft gold glow. No glyph.</summary>
        public static Texture2D AskMark()
        {
            if (_ask != null) return _ask;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.name = "InkAsk";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color[n * n];
            float mid = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = x - mid;
                    float dy = y - mid;
                    float ang = Mathf.Atan2(dy, dx);
                    float wob = 0.7f * Mathf.Sin(ang * 5f);
                    float half = 16f + wob;
                    float ax = Mathf.Abs(dx);
                    float ay = Mathf.Abs(dy);
                    float box = Mathf.Max(ax, ay) - half;
                    float glow = Mathf.Clamp01(1f - (Mathf.Max(ax, ay) - half) / 14f);
                    glow = glow * glow * 0.55f;
                    Color color = new Color(Glow.r, Glow.g, Glow.b, glow);
                    if (box <= 0f)
                        color = new Color(0.937f, 0.910f, 0.855f, 1f);
                    float edge = 1f - Mathf.Clamp01(Mathf.Abs(box) / 1.8f);
                    if (edge > 0.05f)
                        color = Color.Lerp(color, Ink, edge * edge);
                    pixels[y * n + x] = color;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            _ask = tex;
            return tex;
        }

        public static void Tint(Renderer renderer, Texture2D texture, Color color)
        {
            if (renderer == null) return;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            if (texture != null) block.SetTexture("_MainTex", texture);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }

        public static Material Card()
        {
            if (_card != null) return _card;
            Shader shader = Shader.Find("Fidelity/Card");
            if (shader == null) return null;
            _card = new Material(shader) { name = "InkNoteCard" };
            _card.SetFloat("_Src", 5f);
            _card.SetFloat("_Dst", 10f);
            _card.SetFloat("_Ring", 0f);
            _card.SetFloat("_Coverage", 0f);
            _card.SetFloat("_Mask", 0f);
            _card.SetFloat("_ZWrite", 0f);
            _card.SetColor("_Color", Color.white);
            return _card;
        }

        public static Mesh Quad()
        {
            if (_quad != null) return _quad;
            _quad = new Mesh { name = "InkNoteQuad" };
            _quad.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            _quad.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
            };
            _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _quad.RecalculateNormals();
            _quad.RecalculateBounds();
            return _quad;
        }

        static float PageDist(float x, float y, float margin, int w, int h)
        {
            float dx = x - w * 0.5f;
            float dy = y - h * 0.5f;
            float ang = Mathf.Atan2(dy, dx);
            float wob = 1.3f * Mathf.Sin(ang * 5f) + 0.7f * Mathf.Sin(ang * 9f + 1.2f);
            float hw = w * 0.5f - margin + wob;
            float hh = h * 0.5f - margin + wob * 0.55f;
            float rad = Mathf.Min(hw, hh) * 0.16f;
            if (rad < 2f) rad = 2f;
            float qx = Mathf.Abs(dx) - (hw - rad);
            float qy = Mathf.Abs(dy) - (hh - rad);
            float ox = Mathf.Max(qx, 0f);
            float oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - rad;
        }

        static float Hash(int x, int y)
        {
            uint n = (uint)(x * 374761393 + y * 668265263);
            n = (n ^ (n >> 13)) * 1274126177u;
            return ((n ^ (n >> 16)) & 255) / 255f;
        }
    }
}
