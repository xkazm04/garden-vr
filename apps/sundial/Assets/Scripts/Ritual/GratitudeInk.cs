using UnityEngine;

namespace GardenVR.Sundial
{
    /// <summary>
    /// Fountain-pen drawings for the five midday symbols. Strokes only, no letters.
    /// Coordinates are -1..1 with y up. The same routine paints the row and the inked mark.
    /// </summary>
    public static class GratitudeInk
    {
        public static readonly Color Ink = new Color(0.165f, 0.149f, 0.133f, 1f);

        public static Texture2D Make(int symbol, bool chosen, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = "Gratitude" + symbol;
            Paint(tex, symbol, chosen);
            return tex;
        }

        public static void Paint(Texture2D tex, int symbol, bool chosen)
        {
            if (tex == null) return;
            int n = tex.width;
            var pixels = new Color[n * n];
            var pen = new Pen
            {
                Pixels = pixels,
                N = n,
                Width = chosen ? 0.20f : 0.15f,
                Alpha = chosen ? 1f : 0.90f,
                Symbol = symbol
            };
            Draw(pen, symbol);
            tex.SetPixels(pixels);
            tex.Apply(false, false);
        }

        static void Draw(Pen pen, int symbol)
        {
            if (symbol == 0) Sun(pen);
            else if (symbol == 1) Leaf(pen);
            else if (symbol == 2) Cup(pen);
            else if (symbol == 3) Star(pen);
            else Hearth(pen);
        }

        static void Sun(Pen pen)
        {
            pen.Circle(0f, 0f, 0.34f);
            for (int i = 0; i < 8; i++)
            {
                float ang = i * Mathf.PI * 0.25f;
                float c = Mathf.Cos(ang);
                float s = Mathf.Sin(ang);
                pen.Line(c * 0.50f, s * 0.50f, c * 0.82f, s * 0.82f);
            }
        }

        static void Leaf(Pen pen)
        {
            pen.Line(0f, -0.62f, 0f, 0.70f);
            const int steps = 28;
            for (int i = 0; i < steps; i++)
            {
                float t0 = i / (float)steps;
                float t1 = (i + 1) / (float)steps;
                Edge(pen, t0, t1, 1f);
                Edge(pen, t0, t1, -1f);
            }
            pen.Line(-0.22f, 0.05f, 0f, 0.16f);
            pen.Line(0.24f, -0.16f, 0f, -0.04f);
        }

        static void Edge(Pen pen, float t0, float t1, float side)
        {
            float y0, x0, y1, x1;
            LeafPoint(t0, side, out x0, out y0);
            LeafPoint(t1, side, out x1, out y1);
            pen.Line(x0, y0, x1, y1);
        }

        static void LeafPoint(float t, float side, out float x, out float y)
        {
            y = Mathf.Lerp(-0.48f, 0.62f, t);
            float belly = Mathf.Sin(t * Mathf.PI);
            float shoulder = 0.55f + 0.45f * Mathf.Sin(t * Mathf.PI);
            x = side * 0.46f * belly * shoulder;
        }

        static void Cup(Pen pen)
        {
            pen.Line(-0.40f, 0.28f, 0.34f, 0.28f);
            pen.Line(-0.30f, 0.28f, -0.30f, -0.08f);
            pen.Line(0.30f, 0.28f, 0.30f, -0.08f);
            pen.Arc(0f, -0.08f, 0.30f, Mathf.PI, Mathf.PI * 2f);
            pen.Arc(0.30f, 0.02f, 0.20f, -1.05f, 1.05f);
            pen.Line(-0.50f, -0.52f, 0.50f, -0.52f);
        }

        static void Star(Pen pen)
        {
            var x = new float[5];
            var y = new float[5];
            for (int i = 0; i < 5; i++)
            {
                float ang = Mathf.PI * 0.5f + i * Mathf.PI * 2f / 5f;
                x[i] = Mathf.Cos(ang) * 0.74f;
                y[i] = Mathf.Sin(ang) * 0.74f;
            }
            for (int i = 0; i < 5; i++)
            {
                int j = (i + 2) % 5;
                pen.Line(x[i], y[i], x[j], y[j]);
            }
        }

        static void Hearth(Pen pen)
        {
            pen.Line(-0.40f, -0.55f, -0.40f, 0.02f);
            pen.Line(0.40f, -0.55f, 0.40f, 0.02f);
            pen.Line(-0.40f, -0.55f, 0.40f, -0.55f);
            pen.Line(-0.52f, 0.02f, 0f, 0.58f);
            pen.Line(0f, 0.58f, 0.52f, 0.02f);
            pen.Line(-0.12f, -0.55f, -0.12f, -0.12f);
            pen.Line(0.12f, -0.55f, 0.12f, -0.12f);
            pen.Line(-0.12f, -0.12f, 0.12f, -0.12f);
            pen.Line(0.16f, 0.22f, 0.16f, 0.46f);
            pen.Line(0.16f, 0.46f, 0.34f, 0.46f);
            pen.Line(0.34f, 0.46f, 0.34f, 0.10f);
        }

        sealed class Pen
        {
            public Color[] Pixels;
            public int N;
            public float Width;
            public float Alpha;
            public int Symbol;

            public void Line(float x0, float y0, float x1, float y1)
            {
                float dx = x1 - x0;
                float dy = y1 - y0;
                float length = Mathf.Sqrt(dx * dx + dy * dy);
                int steps = Mathf.Max(2, Mathf.CeilToInt(length / 0.035f));
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    if (Mathf.Sin(t * 23f + Symbol * 1.7f + x0) > 0.94f) continue;
                    float wobble = 0.014f * Mathf.Sin(t * 11f + Symbol * 2.1f);
                    float nx = -dy;
                    float ny = dx;
                    float nlen = Mathf.Sqrt(nx * nx + ny * ny);
                    if (nlen > 1e-4f)
                    {
                        nx = nx / nlen * wobble;
                        ny = ny / nlen * wobble;
                    }
                    Dot(x0 + dx * t + nx, y0 + dy * t + ny);
                }
            }

            public void Circle(float cx, float cy, float radius)
            {
                Arc(cx, cy, radius, 0f, Mathf.PI * 2f);
            }

            public void Arc(float cx, float cy, float radius, float a0, float a1)
            {
                float span = a1 - a0;
                int steps = Mathf.Max(8, Mathf.CeilToInt(Mathf.Abs(span) * radius / 0.05f));
                float px = cx + Mathf.Cos(a0) * radius;
                float py = cy + Mathf.Sin(a0) * radius;
                for (int i = 1; i <= steps; i++)
                {
                    float a = a0 + span * (i / (float)steps);
                    float x = cx + Mathf.Cos(a) * radius;
                    float y = cy + Mathf.Sin(a) * radius;
                    Line(px, py, x, y);
                    px = x;
                    py = y;
                }
            }

            void Dot(float x, float y)
            {
                float u = (x + 1f) * 0.5f;
                float v = (y + 1f) * 0.5f;
                int cx = Mathf.RoundToInt(u * (N - 1));
                int cy = Mathf.RoundToInt(v * (N - 1));
                int rad = Mathf.CeilToInt(Width * 0.5f * N * 0.5f) + 1;
                float reach = Width * 0.5f * N * 0.5f;
                if (reach < 0.8f) reach = 0.8f;
                for (int oy = -rad; oy <= rad; oy++)
                {
                    int py = cy + oy;
                    if (py < 0 || py >= N) continue;
                    for (int ox = -rad; ox <= rad; ox++)
                    {
                        int px = cx + ox;
                        if (px < 0 || px >= N) continue;
                        float dist = Mathf.Sqrt(ox * ox + oy * oy);
                        float a = Alpha * Mathf.Clamp01(1f - dist / reach);
                        if (a <= 0f) continue;
                        int i = py * N + px;
                        Color have = Pixels[i];
                        if (a > have.a)
                            Pixels[i] = new Color(Ink.r, Ink.g, Ink.b, a);
                    }
                }
            }
        }
    }
}
