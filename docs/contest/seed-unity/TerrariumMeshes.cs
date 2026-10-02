using System.Collections.Generic;
using UnityEngine;

namespace Terrarium
{
    /// <summary>Procedural geometry: the jar is a lathe, the fern is a curvature-integrated rachis with leaflets.</summary>
    public static class TerrariumMeshes
    {
        // ---------- jar (lathe of a 2D profile: (radius, height) in metres) ----------
        public static readonly Vector2[] JarProfile =
        {
            new Vector2(0.000f, 0.000f), new Vector2(0.062f, 0.000f), new Vector2(0.070f, 0.006f), new Vector2(0.072f, 0.020f),
            new Vector2(0.072f, 0.150f), new Vector2(0.068f, 0.172f), new Vector2(0.056f, 0.188f), new Vector2(0.047f, 0.196f),
            new Vector2(0.046f, 0.206f), new Vector2(0.050f, 0.210f), new Vector2(0.050f, 0.216f), new Vector2(0.045f, 0.218f),
        };

        public static Mesh Lathe(Vector2[] profile, int segments = 72, string name = "Lathe")
        {
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var tris = new List<int>();
            int rings = profile.Length;
            for (int s = 0; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                for (int r = 0; r < rings; r++)
                {
                    var p = profile[r];
                    verts.Add(dir * p.x + Vector3.up * p.y);
                    Vector2 t = profile[Mathf.Min(r + 1, rings - 1)] - profile[Mathf.Max(r - 1, 0)];
                    var n2 = new Vector2(t.y, -t.x).normalized;
                    norms.Add((dir * n2.x + Vector3.up * n2.y).normalized);
                }
            }
            for (int s = 0; s < segments; s++)
                for (int r = 0; r < rings - 1; r++)
                {
                    int i0 = s * rings + r, i1 = (s + 1) * rings + r;
                    tris.Add(i0); tris.Add(i0 + 1); tris.Add(i1);
                    tris.Add(i1); tris.Add(i0 + 1); tris.Add(i1 + 1);
                }
            var m = new Mesh { name = name };
            m.SetVertices(verts); m.SetNormals(norms); m.SetTriangles(tris, 0); m.RecalculateBounds();
            return m;
        }

        // ---------- fern frond ----------
        public struct FrondShape
        {
            public float Length;      // metres
            public float Uncoil;      // 0 = tight fiddlehead, 1 = open frond
            public float Droop;       // 0..1 extra arch when the garden is resting (missed days)
            public float LeafScale;   // leaflet size multiplier
            public int Segments;
        }

        /// <summary>Samples the rachis centreline in the frond's local XY plane (+Y up, +X forward).</summary>
        public static void Rachis(FrondShape f, List<Vector3> pts, List<float> along)
        {
            pts.Clear(); along.Clear();
            int n = Mathf.Max(8, f.Segments);
            float L = f.Length, ds = L / n;
            float su = Mathf.Lerp(0.18f, 1.0f, f.Uncoil) * L;             // unrolled length
            float coiledLen = Mathf.Max(1e-4f, L - su);
            float turns = 2.4f * Mathf.Sqrt(1f - f.Uncoil);                  // spiral turns still wound
            float archK = (0.9f + 2.2f * f.Droop) / L;                       // gentle arch of the open part
            float c = 2f * (turns * 2f * Mathf.PI) / (coiledLen * coiledLen);
            float theta = Mathf.Deg2Rad * (84f - 10f * f.Droop);            // start almost vertical
            var p = Vector3.zero;
            for (int i = 0; i <= n; i++)
            {
                float s = i * ds;
                pts.Add(p); along.Add(s / L);
                float k = archK + (s > su ? c * (s - su) : 0f);
                theta -= k * ds;                                             // curl forward and down
                p += new Vector3(Mathf.Cos(theta), Mathf.Sin(theta), 0) * ds;
            }
        }

        static readonly List<Vector3> _pts = new List<Vector3>();
        static readonly List<float> _along = new List<float>();

        public static void BuildFrond(Mesh mesh, FrondShape f, float baseRadius = 0.0022f)
        {
            Rachis(f, _pts, _along);
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var tris = new List<int>(); var cols = new List<Color>();
            const int sides = 6;
            int n = _pts.Count;
            // tube along the rachis
            for (int i = 0; i < n; i++)
            {
                Vector3 t = (i < n - 1 ? _pts[i + 1] - _pts[i] : _pts[i] - _pts[i - 1]).normalized;
                Vector3 b = Vector3.forward; Vector3 nn = Vector3.Cross(b, t).normalized;
                float r = Mathf.Lerp(baseRadius, baseRadius * 0.28f, _along[i]);
                for (int s = 0; s < sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    Vector3 dir = nn * Mathf.Cos(a) + b * Mathf.Sin(a);
                    verts.Add(_pts[i] + dir * r); norms.Add(dir); cols.Add(new Color(_along[i], 0, 0, 1));
                }
            }
            for (int i = 0; i < n - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a0 = i * sides + s, a1 = i * sides + (s + 1) % sides, b0 = a0 + sides, b1 = a1 + sides;
                    tris.Add(a0); tris.Add(b0); tris.Add(a1); tris.Add(a1); tris.Add(b0); tris.Add(b1);
                }
            // leaflets (pinnae): pairs along the rachis, open only on the unrolled part, folded inside the coil
            float su = Mathf.Lerp(0.18f, 1.0f, f.Uncoil);
            int pairs = 22;
            for (int k = 1; k <= pairs; k++)
            {
                float u = k / (float)(pairs + 1);
                int idx = Mathf.Clamp(Mathf.RoundToInt(u * (n - 1)), 1, n - 2);
                float open = Mathf.Clamp01((su - u) / 0.22f + 0.15f);
                float profile = Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.8f)) * (1.05f - 0.55f * u);
                float len = f.Length * 0.30f * profile * f.LeafScale * Mathf.Lerp(0.18f, 1f, open);
                if (len < 0.0015f) continue;
                Vector3 t = (_pts[idx + 1] - _pts[idx - 1]).normalized;
                Vector3 up = Vector3.Cross(Vector3.forward, t).normalized;  // in-plane normal, "above" the rachis
                for (int side = -1; side <= 1; side += 2)
                {
                    // sweep from folded along the rachis (closed) to spread sideways and slightly forward (open)
                    Vector3 spread = (Vector3.forward * side * 0.92f + t * 0.38f - up * 0.12f).normalized;
                    Vector3 folded = (t * 0.8f + up * 0.5f + Vector3.forward * side * 0.15f).normalized;
                    Vector3 dir = Vector3.Slerp(folded, spread, open);
                    AddLeaf(verts, norms, tris, cols, _pts[idx], dir, up, len, len * 0.40f, u);
                }
            }
            mesh.Clear();
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetColors(cols); mesh.SetTriangles(tris, 0); mesh.RecalculateBounds();
        }

        static void AddLeaf(List<Vector3> v, List<Vector3> nr, List<int> t, List<Color> c, Vector3 root, Vector3 dir, Vector3 up, float len, float width, float along)
        {
            // a pointed ellipse, 6 stations, slightly cupped
            Vector3 side = Vector3.Cross(dir, up).normalized;
            Vector3 normal = Vector3.Cross(side, dir).normalized;
            int start = v.Count; const int st = 6;
            for (int i = 0; i <= st; i++)
            {
                float x = i / (float)st;
                float w = Mathf.Sin(Mathf.PI * Mathf.Pow(x, 0.75f)) * width * 0.5f;
                Vector3 centre = root + dir * (x * len) - normal * (x * x * len * 0.12f);
                v.Add(centre + side * w + normal * w * 0.25f); v.Add(centre - side * w + normal * w * 0.25f);
                nr.Add(normal); nr.Add(normal);
                c.Add(new Color(along, x, 1, 1)); c.Add(new Color(along, x, 1, 1));
            }
            for (int i = 0; i < st; i++)
            {
                int a = start + i * 2;
                t.Add(a); t.Add(a + 2); t.Add(a + 1); t.Add(a + 1); t.Add(a + 2); t.Add(a + 3);
            }
        }

        /// <summary>Point on the frond centreline at fraction u (local space) - used to roll the dew bead.</summary>
        public static Vector3 PointOnFrond(FrondShape f, float u)
        {
            Rachis(f, _pts, _along);
            float x = Mathf.Clamp01(u) * (_pts.Count - 1);
            int i = Mathf.Min((int)x, _pts.Count - 2);
            return Vector3.Lerp(_pts[i], _pts[i + 1], x - i);
        }
    }
}
