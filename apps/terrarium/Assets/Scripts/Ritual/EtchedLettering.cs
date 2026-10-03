using System.Collections.Generic;
using GardenVR.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Terrarium
{
    /// <summary>
    /// Frosted lettering on the jar and the desk. One OFL face, no card behind the glyphs.
    /// The mesh sits in the scene and takes the etch colour, so the jar's light is what you read by.
    /// </summary>
    public static class EtchedLettering
    {
        public const string FontResource = "Etch/CormorantGaramond SDF";
        public const string FrostResource = "Etch/EtchedFrost";
        // On the desk in front of the jar. The shoulder glass is full of fronds, so the line sits in the light the jar casts.
        public static readonly Vector3 GlassPos = new Vector3(0f, 0.020f, -0.092f);
        public const float WordsWidth = 0.16f;
        public const float WordsCap = 0.013f;

        /// <summary>Latin set used by the first run, the voice lines, and the settings pebbles.</summary>
        public const string Glyphs =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
            " .,\'\"!?/;:-";

        static Material _frost;

        public static TextMeshPro Place(Transform parent, string name, string text, Vector3 localPos, Quaternion localRot, float maxWorldWidth, float worldCap)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = Vector3.one;
            var tmp = go.AddComponent<TextMeshPro>();
            Prepare(tmp);
            Fit(tmp, text, maxWorldWidth, worldCap);
            return tmp;
        }

        public static void Prepare(TextMeshPro tmp)
        {
            if (tmp == null) return;
            TMP_FontAsset font = EtchContrast.Font();
            if (font != null) tmp.font = font;
            Material frost = Frost(font);
            if (frost != null) tmp.fontSharedMaterial = frost;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 36f;
            tmp.lineSpacing = 0f;
            tmp.extraPadding = true;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.rectTransform.sizeDelta = new Vector2(400f, 40f);
            tmp.color = EtchContrast.SceneInk;
            var renderer = tmp.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        /// <summary>
        /// Scales the mesh so one line's cap height is <paramref name="worldCap"/> metres,
        /// then shrinks it if the line is wider than <paramref name="maxWorldWidth"/>.
        /// </summary>
        public static void Fit(TextMeshPro tmp, string text, float maxWorldWidth, float worldCap)
        {
            if (tmp == null) return;
            tmp.text = text ?? "";
            tmp.color = EtchContrast.SceneInk;
            tmp.transform.localScale = Vector3.one;
            tmp.ForceMeshUpdate(true, true);
            var renderer = tmp.GetComponent<Renderer>();
            if (renderer == null) return;
            int lines = LineCount(tmp.text);
            float height = renderer.bounds.size.y;
            float cap = height / Mathf.Max(1, lines);
            if (cap < 0.00001f) return;
            float scale = worldCap / cap;
            float width = renderer.bounds.size.x * scale;
            if (maxWorldWidth > 0.0001f && width > maxWorldWidth && renderer.bounds.size.x > 0.00001f)
                scale = maxWorldWidth / renderer.bounds.size.x;
            tmp.transform.localScale = new Vector3(scale, scale, scale);
        }

        public static void SetText(GameObject go, string text, float maxWorldWidth, float worldCap)
        {
            if (go == null) return;
            var tmp = go.GetComponent<TMP_Text>() as TextMeshPro;
            if (tmp == null) tmp = go.GetComponentInChildren<TextMeshPro>(true);
            if (tmp != null)
            {
                Fit(tmp, text, maxWorldWidth, worldCap);
                return;
            }
            var mesh = go.GetComponent<TextMesh>();
            if (mesh == null) mesh = go.GetComponentInChildren<TextMesh>(true);
            if (mesh != null) mesh.text = text ?? "";
        }

        /// <summary>Active etched lines in the open scene. Inactive pebbles stay out.</summary>
        public static string[] VisibleLines()
        {
            TextMeshPro[] found = Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Exclude);
            var lines = new List<string>();
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] == null || string.IsNullOrEmpty(found[i].text)) continue;
                lines.Add(found[i].text);
            }
            return lines.ToArray();
        }

        public static string Read(GameObject go)
        {
            if (go == null) return null;
            var tmp = go.GetComponent<TMP_Text>();
            if (tmp == null) tmp = go.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) return tmp.text;
            var mesh = go.GetComponent<TextMesh>();
            if (mesh == null) mesh = go.GetComponentInChildren<TextMesh>(true);
            return mesh != null ? mesh.text : null;
        }

        public static Material Frost(TMP_FontAsset font)
        {
            if (_frost != null) return _frost;
            Material saved = Resources.Load<Material>(FrostResource);
            if (saved != null)
            {
                _frost = saved;
                return _frost;
            }
            Material source = font != null ? font.material : null;
            if (source == null) return null;
            _frost = new Material(source) { name = "EtchedFrost" };
            ApplyFrost(_frost);
            return _frost;
        }

        public static void ApplyFrost(Material mat)
        {
            if (mat == null) return;
            mat.SetColor("_FaceColor", Color.white);
            mat.SetFloat("_FaceDilate", 0.16f);
            mat.SetColor("_OutlineColor", new Color(0.82f, 1f, 0.94f, 0.92f));
            mat.SetFloat("_OutlineWidth", 0.20f);
            mat.SetFloat("_OutlineSoftness", 0.18f);
            mat.SetColor("_UnderlayColor", new Color(0.03f, 0.12f, 0.08f, 0.82f));
            mat.SetFloat("_UnderlayOffsetX", 0f);
            mat.SetFloat("_UnderlayOffsetY", -0.28f);
            mat.SetFloat("_UnderlayDilate", 0.18f);
            mat.SetFloat("_UnderlaySoftness", 0.16f);
            mat.EnableKeyword("OUTLINE_ON");
            mat.EnableKeyword("UNDERLAY_ON");
            mat.DisableKeyword("UNDERLAY_INNER");
            mat.SetFloat("_CullMode", 0f);
            // After the plant glow, so a frond cannot paint over the letters.
            mat.renderQueue = 3400;
        }

        /// <summary>Poses the lettering a capture can shoot. The same mesh the ritual uses.</summary>
        public static void Present(Transform jar, string mode)
        {
            if (jar == null || string.IsNullOrEmpty(mode)) return;
            ClearCapture(jar);
            BoxPaceMarks.Clear(BoxPaceMarks.FindRing(jar));
            if (mode == "hold")
            {
                Place(jar, "EtchedWords", EtchContrast.HoldLine + "\nSpace or mouse",
                    GlassPos, Quaternion.identity, WordsWidth, WordsCap);
                Ghost(jar, true);
            }
            else if (mode == "answer")
            {
                Place(jar, "EtchedWords", EtchContrast.AnswerLine,
                    GlassPos, Quaternion.identity, WordsWidth, WordsCap);
            }
            else if (mode == "settings")
            {
                SettingsPebbles.PresentOpen(jar);
            }
            else if (mode == "settings-box")
            {
                var preset = new RitualSettings();
                preset.BoxPace = true;
                SettingsPebbles.PresentOpen(jar, preset);
            }
        }

        public static Renderer Ghost(Transform parent, bool showing)
        {
            Texture2D open = Resources.Load<Texture2D>("Ghost/hand-open");
            Texture2D pinch = Resources.Load<Texture2D>("Ghost/hand-pinch");
            if (open == null || parent == null) return null;
            Material mat = GhostHandLook.Create(open, pinch, 0f);
            if (mat == null) return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "GhostHand";
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            go.transform.SetParent(parent, false);
            GhostHandLook.Pose(go.transform);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = showing;
            return renderer;
        }

        static void ClearCapture(Transform jar)
        {
            for (int i = jar.childCount - 1; i >= 0; i--)
            {
                Transform child = jar.GetChild(i);
                if (child.name == "EtchedWords" || child.name == "GhostHand" || child.name == "SettingsPebbles")
                    Object.DestroyImmediate(child.gameObject);
            }
        }

        static int LineCount(string text)
        {
            int lines = 1;
            if (string.IsNullOrEmpty(text)) return lines;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n') lines++;
            }
            return lines;
        }
    }
}
