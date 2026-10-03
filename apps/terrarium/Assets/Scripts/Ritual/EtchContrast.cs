using System.Globalization;
using TMPro;
using UnityEngine;

namespace GardenVR.Terrarium
{
    /// <summary>
    /// Etched words use <c>etch.text</c> (#BFF5DD at 70%) on <c>night.room</c> (#0E1A1C).
    /// Contrast is the WCAG ratio of the rendered pixels, not the stylesheet colours.
    /// </summary>
    public static class EtchContrast
    {
        public static readonly Color Ink = new Color(0xBF / 255f, 0xF5 / 255f, 0xDD / 255f, 0.70f);
        public static readonly Color Night = new Color(0x0E / 255f, 0x1A / 255f, 0x1C / 255f, 1f);
        public const float MinimumRatio = 4.5f;
        public const string HoldLine = "Hold to breathe in.";
        public const string ReleaseLine = "and let go.";

        static TMP_FontAsset _font;

        public static TMP_FontAsset Font()
        {
            if (_font != null) return _font;
            // Liberation Sans ships with the TextMesh Pro essentials. The built-in runtime font has no face data.
            TMP_Settings settings = TMP_Settings.LoadDefaultSettings();
            if (settings != null)
                _font = TMP_Settings.defaultFontAsset;
            if (_font == null)
                _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            return _font;
        }

        public static float Linear(float channel)
        {
            if (channel <= 0.04045f) return channel / 12.92f;
            return Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
        }

        public static float Luminance(Color color)
        {
            return 0.2126f * Linear(color.r) + 0.7152f * Linear(color.g) + 0.0722f * Linear(color.b);
        }

        public static float Ratio(Color a, Color b)
        {
            float la = Luminance(a);
            float lb = Luminance(b);
            float hi = la > lb ? la : lb;
            float lo = la > lb ? lb : la;
            return (hi + 0.05f) / (lo + 0.05f);
        }

        /// <summary>
        /// Renders the hold line with TextMeshPro over night.room and returns the contrast of the brightest
        /// glyph pixel against a background sample. <paramref name="capture"/> is the frame (caller destroys it).
        /// </summary>
        public static float MeasureRendered(out Texture2D capture, out int hits)
        {
            capture = null;
            hits = 0;
            const int width = 640;
            const int height = 360;
            var root = new GameObject("EtchContrastRig");
            Camera cam = null;
            RenderTexture rt = null;
            try
            {
                var textGo = new GameObject("EtchSample");
                textGo.transform.SetParent(root.transform, false);
                textGo.transform.localPosition = Vector3.zero;
                textGo.transform.localRotation = Quaternion.identity;
                textGo.transform.localScale = Vector3.one * 0.12f;
                var tmp = textGo.AddComponent<TextMeshPro>();
                TMP_FontAsset font = Font();
                if (font == null) return 0f;
                tmp.font = font;
                tmp.text = HoldLine;
                tmp.color = Ink;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = 9f;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.rectTransform.sizeDelta = new Vector2(12f, 2f);
                tmp.ForceMeshUpdate(true, true);
                string shader = tmp.fontSharedMaterial != null && tmp.fontSharedMaterial.shader != null
                    ? tmp.fontSharedMaterial.shader.name : "none";
                Debug.Log("[Etch] font=" + (font.name ?? "")
                    + " chars=" + tmp.textInfo.characterCount.ToString(CultureInfo.InvariantCulture)
                    + " shader=" + shader);

                var camGo = new GameObject("EtchCamera");
                camGo.transform.SetParent(root.transform, false);
                camGo.transform.position = new Vector3(0f, 0f, -2f);
                camGo.transform.rotation = Quaternion.identity;
                cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Night;
                cam.orthographic = true;
                cam.orthographicSize = 0.55f;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 8f;
                cam.allowHDR = false;
                cam.allowMSAA = false;
                cam.cullingMask = ~0;

                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                rt.Create();
                cam.targetTexture = rt;
                cam.Render();

                capture = new Texture2D(width, height, TextureFormat.RGB24, false);
                RenderTexture.active = rt;
                capture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                capture.Apply();
                RenderTexture.active = null;

                Color background = capture.GetPixel(4, 4);
                float best = -1f;
                Color ink = background;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color pixel = capture.GetPixel(x, y);
                        float delta = Mathf.Abs(pixel.r - background.r) + Mathf.Abs(pixel.g - background.g) + Mathf.Abs(pixel.b - background.b);
                        if (delta < 0.06f) continue;
                        hits++;
                        float lum = Luminance(pixel);
                        if (lum > best)
                        {
                            best = lum;
                            ink = pixel;
                        }
                    }
                }
                float ratio = hits > 0 ? Ratio(ink, background) : 0f;
                Debug.Log("[Etch] contrast=" + ratio.ToString("0.00", CultureInfo.InvariantCulture)
                    + " hits=" + hits.ToString(CultureInfo.InvariantCulture)
                    + " ink=" + ColorUtility.ToHtmlStringRGB(ink)
                    + " bg=" + ColorUtility.ToHtmlStringRGB(background));
                return ratio;
            }
            finally
            {
                if (cam != null) cam.targetTexture = null;
                if (rt != null)
                {
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }
                Object.DestroyImmediate(root);
            }
        }
    }
}
