using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Sundial
{
    [Serializable]
    public sealed class SoilPebble
    {
        public float x, z, y, r, sy, rot;
        public float[] c;
    }

    [Serializable]
    sealed class SoilMoundFile
    {
        public float span;
        public int grid;
        // T-SUN-051. Optional, for a painting that is not square: gridZ cells over zMin..zMax (default: grid cells over -span..span).
        public int gridZ;
        public float zMin, zMax;
        // Optional bed box in baked metres (half width, near and far z). The layout maps it onto its bed ellipse's box.
        public float bedX, bedZ0, bedZ1;
        public float moundRadius;
        public float peak;
        public float[] heights;
        public SoilPebble[] pebbles;
    }

    /// <summary>
    /// T-SUN-052. Asks the mound to be the layout's bed: the baked circle is stretched to the bed ellipse (<see cref="DialLayout"/>) and
    /// shifted toward the viewer, the painting stretching with it (the UV stays the baked one), and the pebbles scale up.
    /// </summary>
    public sealed class SoilBed
    {
        public readonly float FaceRadius;
        public SoilBed(float faceRadius) { FaceRadius = faceRadius; }
    }

    /// <summary>
    /// Spike S4. The soil as a raised heightfield mound (about 8 mm at the crown, a ragged lip that rises from the paper)
    /// with a top-down painting projected planar from above, and a handful of low pebbles merged into the same mesh.
    /// One mesh, one renderer, one draw (<c>Fidelity/SoilMound</c>). The heightfield and the pebbles are baked and seeded by
    /// apps/sundial/Art/Scripts/soilmound_s4.py, so the build is deterministic. Local axes are the dial's: x right, y up,
    /// z away from the viewer. The mesh UV is (x, z) across the painted span, which is what makes the projection exact.
    /// </summary>
    public sealed class SoilMound
    {
        public const string ResourceMaterial = "SoilMound/Soil_Mound";
        public const string ResourceData = "SoilMound/soil-mound";
        const int Stacks = 4;
        const int Slices = 8;

        public int Vertices { get; private set; }
        public int Triangles { get; private set; }
        public int Pebbles { get; private set; }
        /// <summary>Highest point above the paper, metres.</summary>
        public float Crown { get; private set; }
        public Transform Root { get { return _root; } }
        public MeshRenderer Renderer { get { return _renderer; } }

        readonly Transform _root;
        readonly Mesh _mesh;
        readonly Material _material;
        readonly MeshRenderer _renderer;

        public SoilMound(Transform parent, Material template, TextAsset data, float faceY, SoilBed bed = null)
        {
            if (template == null) throw new InvalidOperationException("SoilMound needs the Fidelity/SoilMound material");
            if (data == null) throw new InvalidOperationException("SoilMound needs the heightfield json");
            SoilMoundFile file = JsonUtility.FromJson<SoilMoundFile>(data.text);
            if (file != null && file.gridZ <= 0) file.gridZ = file.grid;
            if (file != null && file.zMax <= file.zMin) { file.zMin = -file.span; file.zMax = file.span; }
            if (file == null || file.heights == null || file.grid < 8 || file.gridZ < 8 || file.heights.Length != (file.grid + 1) * (file.gridZ + 1))
                throw new InvalidOperationException("soil-mound.json is malformed");

            var go = new GameObject("SoilMound");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, faceY, 0f);
            _root = go.transform;
            _mesh = new Mesh { name = "SoilMound", hideFlags = HideFlags.DontSave };
            _material = new Material(template) { name = "SoilMound.instance", hideFlags = HideFlags.DontSave };
            Build(file, bed);
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = go.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = true;
        }

        static readonly int WashTex = Shader.PropertyToID("_WashTex");
        static readonly int WashU = Shader.PropertyToID("_WashU");
        static readonly int WashV = Shader.PropertyToID("_WashV");
        static readonly int WashOn = Shader.PropertyToID("_WashOn");

        /// <summary>
        /// T-SUN-051. The wash the bed's edge bleeds into is the face's own texture, read in the shader at the dial position:
        /// face u = a x + b z + c and v = d x + e z + f (dial-local metres, the mound root shares the dial's x and z). A null texture
        /// turns the bleed off and the edge fades to the fallback cream.
        /// </summary>
        public void SetWash(Texture texture, Vector3 u, Vector3 v)
        {
            if (_material == null) return;
            bool on = texture != null;
            _material.SetFloat(WashOn, on ? 1f : 0f);
            if (on)
            {
                _material.SetTexture(WashTex, texture);
                _material.SetVector(WashU, new Vector4(u.x, u.y, u.z, 0f));
                _material.SetVector(WashV, new Vector4(v.x, v.y, v.z, 0f));
            }
        }

        public void SetActive(bool on)
        {
            _root.gameObject.SetActive(on);
        }

        public void Destroy()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root.gameObject);
            if (_mesh != null) UnityEngine.Object.DestroyImmediate(_mesh);
            if (_material != null) UnityEngine.Object.DestroyImmediate(_material);
        }

        void Build(SoilMoundFile f, SoilBed bed)
        {
            float sx = 1f, sz = 1f, oz = 0f, pebbleScale = 1f;
            if (bed != null)
            {
                if (f.bedX > 0f && f.bedZ1 > f.bedZ0) DialLayout.BedBoxMap(bed.FaceRadius, f.bedX, f.bedZ0, f.bedZ1, out sx, out sz, out oz);
                else DialLayout.BedMap(bed.FaceRadius, f.moundRadius, out sx, out sz, out oz);
                pebbleScale = DialLayout.PebbleScale;
            }
            int n = f.grid;
            int nz = f.gridZ;
            int w = n + 1;
            float step = 2f * f.span / n;
            float stepZ = (f.zMax - f.zMin) / nz;
            const float floor = 0.0003f;
            var verts = new List<Vector3>(w * w);
            var uvs = new List<Vector2>(w * w);
            var normals = new List<Vector3>(w * w);
            var colors = new List<Color>(w * w);
            var tris = new List<int>(n * nz * 6);

            // A cell is kept when any corner is painted. Corners with no paint sit at the paper line.
            var used = new bool[n * nz];
            var needed = new bool[w * (nz + 1)];
            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    bool any = f.heights[j * w + i] >= 0f || f.heights[j * w + i + 1] >= 0f
                        || f.heights[(j + 1) * w + i] >= 0f || f.heights[(j + 1) * w + i + 1] >= 0f;
                    if (!any) continue;
                    used[j * n + i] = true;
                    needed[j * w + i] = needed[j * w + i + 1] = needed[(j + 1) * w + i] = needed[(j + 1) * w + i + 1] = true;
                }
            }
            var map = new int[w * (nz + 1)];
            for (int j = 0; j <= nz; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    int k = j * w + i;
                    map[k] = -1;
                    if (!needed[k]) continue;
                    float x = -f.span + i * step;
                    float z = f.zMin + j * stepZ;
                    float h = Mathf.Max(f.heights[k], floor);
                    map[k] = verts.Count;
                    verts.Add(new Vector3(x * sx, h, z * sz + oz));
                    uvs.Add(new Vector2((x + f.span) / (2f * f.span), (z - f.zMin) / (f.zMax - f.zMin)));
                    // Central difference of the heightfield. Edge samples fall back to one side.
                    float hl = Height(f, i - 1, j, floor), hr = Height(f, i + 1, j, floor);
                    float hd = Height(f, i, j - 1, floor), hu = Height(f, i, j + 1, floor);
                    normals.Add(new Vector3((hl - hr) / sx, 2f * step, (hd - hu) / sz * (step / stepZ)).normalized);
                    colors.Add(new Color(1f, 1f, 1f, 0f));
                    if (h > Crown) Crown = h;
                }
            }
            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    if (!used[j * n + i]) continue;
                    int a = map[j * w + i], b = map[j * w + i + 1], c = map[(j + 1) * w + i], d = map[(j + 1) * w + i + 1];
                    // Upward facing winding (+y). The shader is Cull Off, so this only keeps the normals honest.
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }

            // Pebbles: a squashed ellipsoid, half sunk, same mesh. Vertex alpha 1 marks them for the shader.
            if (f.pebbles != null)
            {
                foreach (SoilPebble p in f.pebbles)
                {
                    AddPebble(p, verts, uvs, normals, colors, tris, f, sx, sz, oz, pebbleScale);
                    Pebbles++;
                }
            }

            _mesh.SetVertices(verts);
            _mesh.SetNormals(normals);
            _mesh.SetUVs(0, uvs);
            _mesh.SetColors(colors);
            _mesh.SetTriangles(tris, 0);
            _mesh.RecalculateBounds();
            Vertices = verts.Count;
            Triangles = tris.Count / 3;
        }

        static float Height(SoilMoundFile f, int i, int j, float floor)
        {
            int w = f.grid + 1;
            i = Mathf.Clamp(i, 0, f.grid);
            j = Mathf.Clamp(j, 0, f.gridZ);
            return Mathf.Max(f.heights[j * w + i], floor);
        }

        static void AddPebble(SoilPebble p, List<Vector3> verts, List<Vector2> uvs, List<Vector3> normals, List<Color> colors, List<int> tris, SoilMoundFile f,
            float sx, float sz, float oz, float pebbleScale)
        {
            int first = verts.Count;
            Quaternion spin = Quaternion.Euler(0f, p.rot, 0f);
            var centre = new Vector3(p.x, p.y + p.r * p.sy * 0.25f, p.z);
            var radius = new Vector3(p.r, p.r * p.sy, p.r * 0.82f);
            Color tint = p.c != null && p.c.Length >= 3 ? new Color(p.c[0], p.c[1], p.c[2], 1f) : new Color(0.75f, 0.70f, 0.62f, 1f);
            for (int s = 0; s <= Stacks; s++)
            {
                float phi = Mathf.PI * s / Stacks;
                float ring = Mathf.Sin(phi);
                float y = Mathf.Cos(phi);
                for (int k = 0; k < Slices; k++)
                {
                    float theta = 2f * Mathf.PI * k / Slices;
                    var unit = new Vector3(ring * Mathf.Cos(theta), y, ring * Mathf.Sin(theta));
                    Vector3 local = new Vector3(unit.x * radius.x, unit.y * radius.y, unit.z * radius.z);
                    Vector3 off = spin * local;
                    Vector3 pos = new Vector3(centre.x * sx, centre.y, centre.z * sz + oz) + off * pebbleScale;
                    Vector3 sample = centre + off;
                    var nrm = spin * new Vector3(unit.x / radius.x, unit.y / radius.y, unit.z / radius.z);
                    verts.Add(pos);
                    normals.Add(nrm.normalized);
                    uvs.Add(new Vector2((sample.x + f.span) / (2f * f.span), (sample.z - f.zMin) / (f.zMax - f.zMin)));
                    colors.Add(tint);
                }
            }
            for (int s = 0; s < Stacks; s++)
            {
                for (int k = 0; k < Slices; k++)
                {
                    int a = first + s * Slices + k;
                    int b = first + s * Slices + (k + 1) % Slices;
                    int c = first + (s + 1) * Slices + k;
                    int d = first + (s + 1) * Slices + (k + 1) % Slices;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            }
        }
    }
}
