using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Sundial
{
    [Serializable]
    public sealed class Halo2MaskInfo
    {
        public string key;
        public int concavities;
        public float gapMinG1Px;
        public float ellipseU, ellipseV, ellipseA;
    }

    [Serializable]
    sealed class Halo2File
    {
        public float pitchDeg;
        public float xHalf, vTop, vBottom, cardW, cardH;
        public int canvasW, canvasH;
        public Halo2MaskInfo[] masks;
    }

    /// <summary>
    /// Spike S5. The pinch halo as one smooth closed curve around the plant plus a ground ellipse under it. The curve is
    /// baked offline (apps/sundial/Art/Scripts/halo_s5.py) into the same R core / G falloff mask <c>Fidelity/Card</c> reads
    /// through <c>_Silhouette</c>, one per plant card, on a canvas that is wider than the card and reaches below it.
    /// <para>
    /// The canvas is a drawing in card-plane metres, square in screen pixels at the DialG1 pitch. It is drawn on one mesh
    /// of two pieces: the part above the card base on the card plane (curved like the plant card, so it sits on the plant),
    /// and the part below it on a flat quad lying on the soil, mapped so both halves meet at the card base seamlessly.
    /// The ground part keeps the depth test, so the plants in front of the ring still cover it. One draw, one material.
    /// </para>
    /// Local axes are the halo card's: x across, y up, z toward the viewer, in card units, so the mesh is built once.
    /// </summary>
    public sealed class Halo2
    {
        public const string ResourceDir = "Halo2/";
        public const string ResourceJson = "Halo2/halo2";
        /// <summary>Height of the ground half above the card base, metres. The plant bases sit in the soil, a little above it.</summary>
        public const float GroundLift = 0.0062f;
        /// <summary>The plant card bulge toward the viewer, in card units (DialSetup.FillCard).</summary>
        const float Bulge = 0.11f;
        const int Columns = 24;
        const int GroundRows = 6;

        public int Vertices { get; private set; }
        public int Triangles { get; private set; }
        public Renderer Renderer { get { return _renderer; } }
        public Mesh Mesh { get { return _mesh; } }
        public float PitchDeg { get { return _file.pitchDeg; } }
        public bool Missing { get; private set; }

        readonly GameObject _go;
        readonly MeshRenderer _renderer;
        readonly Mesh _mesh;
        readonly Halo2File _file;
        readonly Dictionary<string, Texture2D> _masks = new Dictionary<string, Texture2D>();
        readonly Dictionary<string, Halo2MaskInfo> _info = new Dictionary<string, Halo2MaskInfo>();
        readonly HashSet<string> _warned = new HashSet<string>();
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

        /// <param name="parent">The halo card. The pieces follow its place, turn and scale.</param>
        public Halo2(Transform parent, Material material, TextAsset data)
        {
            if (parent == null) throw new InvalidOperationException("Halo2 needs the halo card to hang on");
            if (material == null) throw new InvalidOperationException("Halo2 needs the halo material");
            if (data == null) throw new InvalidOperationException("Halo2 needs Resources/Halo2/halo2.json (run halo_s5.py bake)");
            _file = JsonUtility.FromJson<Halo2File>(data.text);
            if (_file == null || _file.masks == null || _file.canvasW < 16 || _file.cardW <= 0f)
                throw new InvalidOperationException("halo2.json is malformed");
            for (int i = 0; i < _file.masks.Length; i++) _info[_file.masks[i].key] = _file.masks[i];

            _go = new GameObject("PinchHalo2");
            _go.transform.SetParent(parent, false);
            _mesh = BuildMesh(_file, GroundLift / Mathf.Max(parent.localScale.y, 1e-4f));
            _fitScaleY = Mathf.Max(parent.localScale.y, 1e-4f);
            _mesh.name = "Halo2Mesh";
            _mesh.hideFlags = HideFlags.DontSave;
            _go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = _go.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            Vertices = _mesh.vertexCount;
            Triangles = _mesh.triangles.Length / 3;
        }

        public void SetActive(bool on)
        {
            _go.SetActive(on);
        }

        /// <summary>The key of the mask for a card, with the bloom overlay when one is drawn on it.</summary>
        public static string KeyFor(Texture2D plant, Texture2D bloom)
        {
            if (plant == null) return null;
            return bloom == null ? plant.name : plant.name + "__" + bloom.name;
        }

        public bool TryMask(string key, out Texture2D mask, out Halo2MaskInfo info)
        {
            mask = null;
            info = null;
            if (string.IsNullOrEmpty(key)) return false;
            if (!_info.TryGetValue(key, out info)) return false;
            if (!_masks.TryGetValue(key, out mask) || mask == null)
            {
                mask = Resources.Load<Texture2D>(ResourceDir + key);
                _masks[key] = mask;
            }
            return mask != null;
        }

        public void Warn(string message)
        {
            if (_warned.Add(message)) Debug.LogWarning("[Halo2] " + message);
        }

        /// <summary>Points the one draw at a mask. Returns false (and draws nothing) when there is none.</summary>
        public bool Show(string key)
        {
            Texture2D mask;
            Halo2MaskInfo info;
            Missing = !TryMask(key, out mask, out info);
            if (Missing) return false;
            _renderer.GetPropertyBlock(_block);
            _block.SetTexture("_MainTex", mask);
            _renderer.SetPropertyBlock(_block);
            return true;
        }

        /// <summary>
        /// Where the ground ellipse sits on the soil and how big it is, in the halo card's local frame: the point the ellipse
        /// centre lands on at the nominal pitch, and its radius as a ground circle (card units across, metres at hero scale).
        /// </summary>
        public Vector3 GroundCentreLocal(Halo2MaskInfo info)
        {
            float lx = info.ellipseU / _file.cardW;
            float ly = info.ellipseV / _file.cardH;
            float tan = Mathf.Tan(_file.pitchDeg * Mathf.Deg2Rad);
            float lift = _groundLiftLocal;
            return new Vector3(lx, lift, BulgeAt(lx) + (lift - ly) / tan);
        }

        float _groundLiftLocal;
        float _fitScaleY = -1f;
        int _groundBase;
        Vector3[] _vertexArray;

        /// <summary>
        /// The ground half lies GroundLift above the base in metres, which is a different number of card units for every card
        /// size. Re-lays it when the card scale changes.
        /// </summary>
        public void Fit(float scaleY)
        {
            scaleY = Mathf.Max(scaleY, 1e-4f);
            if (Mathf.Abs(scaleY - _fitScaleY) < 1e-5f) return;
            _fitScaleY = scaleY;
            _groundLiftLocal = GroundLift / scaleY;
            float tan = Mathf.Tan(_file.pitchDeg * Mathf.Deg2Rad);
            float bottomV = _file.vBottom / _file.cardH;
            float halfU = _file.xHalf / _file.cardW;
            int k = _groundBase;
            for (int j = 0; j <= GroundRows; j++)
            {
                float ly = bottomV * j / GroundRows;
                for (int i = 0; i <= Columns; i++)
                {
                    float lx = Mathf.Lerp(-halfU, halfU, i / (float)Columns);
                    _vertexArray[k++] = new Vector3(lx, _groundLiftLocal, BulgeAt(lx) + (_groundLiftLocal - ly) / tan);
                }
            }
            _mesh.vertices = _vertexArray;
            _mesh.RecalculateBounds();
        }

        public float RadiusCardUnits(Halo2MaskInfo info)
        {
            return info.ellipseA / _file.cardW;
        }

        public static float BulgeAt(float lx)
        {
            float u = Mathf.Clamp(lx, -0.5f, 0.5f);
            return Mathf.Cos(u * Mathf.PI) * Bulge;
        }

        public void Destroy()
        {
            // The masks are shared Resources assets; only the mesh and the object are ours.
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            if (_mesh != null) UnityEngine.Object.DestroyImmediate(_mesh);
        }

        Mesh BuildMesh(Halo2File f, float liftLocal)
        {
            _groundLiftLocal = liftLocal;
            float tan = Mathf.Tan(f.pitchDeg * Mathf.Deg2Rad);
            float halfU = f.xHalf / f.cardW;
            float range = f.vTop - f.vBottom;
            float topV = f.vTop / f.cardH;
            float bottomV = f.vBottom / f.cardH;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // Upper piece: the card plane, curved like the plant card so the ring hangs on the plant.
            int upperRows = 3;
            for (int j = 0; j <= upperRows; j++)
            {
                float ly = topV * j / upperRows;
                for (int i = 0; i <= Columns; i++)
                {
                    float lx = Mathf.Lerp(-halfU, halfU, i / (float)Columns);
                    verts.Add(new Vector3(lx, ly, BulgeAt(lx)));
                    uvs.Add(new Vector2(i / (float)Columns, (ly * f.cardH - f.vBottom) / range));
                }
            }
            AddGrid(tris, 0, Columns + 1, upperRows);

            // Ground piece: starts where the card base is seen from the nominal camera and runs toward it. A point that
            // lies Δ above the base and d in front of it appears Δ - d tan(pitch) above the base on the card plane.
            int gBase = verts.Count;
            _groundBase = gBase;
            for (int j = 0; j <= GroundRows; j++)
            {
                float ly = bottomV * j / GroundRows;
                for (int i = 0; i <= Columns; i++)
                {
                    float lx = Mathf.Lerp(-halfU, halfU, i / (float)Columns);
                    float lz = BulgeAt(lx) + (liftLocal - ly) / tan;
                    verts.Add(new Vector3(lx, liftLocal, lz));
                    uvs.Add(new Vector2(i / (float)Columns, (ly * f.cardH - f.vBottom) / range));
                }
            }
            AddGrid(tris, gBase, Columns + 1, GroundRows);

            var mesh = new Mesh();
            _vertexArray = verts.ToArray();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            var colors = new Color[verts.Count];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            mesh.colors = colors;
            var normals = new Vector3[verts.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.forward;
            mesh.normals = normals;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddGrid(List<int> tris, int start, int stride, int rows)
        {
            int cols = stride - 1;
            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i < cols; i++)
                {
                    int a = start + j * stride + i;
                    tris.Add(a);
                    tris.Add(a + stride);
                    tris.Add(a + 1);
                    tris.Add(a + 1);
                    tris.Add(a + stride);
                    tris.Add(a + stride + 1);
                }
            }
        }
    }

    /// <summary>
    /// Spike S5, the E2 path, stubbed. The 3D leaf plant of S3 is a cloud of leaf cards, so its halo wants an analytic hull:
    /// the union of a few bounding spheres per leaf cluster, seen from the camera as circles, smoothed with a smooth minimum
    /// so it stays one blob. This is the signed distance of that union in the view plane (negative inside). Nothing feeds
    /// it yet: <see cref="LeafPlant"/> does not publish its clusters, and while the assembly is drawn the dial falls back to
    /// look A's halo, which traces the silhouette the assembly casts. Wire it by handing the cluster circles to
    /// <see cref="Rasterise"/> and the result to the same bake the offline masks use.
    /// </summary>
    public static class HaloSphereUnion
    {
        /// <param name="circles">x, y = centre in the view plane, z = radius.</param>
        /// <param name="blend">Smooth-minimum width in the same units; 0 is a plain union.</param>
        public static float Sdf(Vector2 p, IList<Vector3> circles, float blend)
        {
            if (circles == null || circles.Count == 0) return float.PositiveInfinity;
            float d = float.PositiveInfinity;
            for (int i = 0; i < circles.Count; i++)
            {
                Vector3 c = circles[i];
                float di = Vector2.Distance(p, new Vector2(c.x, c.y)) - c.z;
                d = i == 0 ? di : SmoothMin(d, di, blend);
            }
            return d;
        }

        static float SmoothMin(float a, float b, float k)
        {
            if (k <= 1e-6f) return Mathf.Min(a, b);
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        /// <summary>The inside of the union on a w by h grid, one view-plane unit per cell: the input of the hull bake.</summary>
        public static bool[] Rasterise(IList<Vector3> circles, int w, int h, float blend)
        {
            var on = new bool[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    on[y * w + x] = Sdf(new Vector2(x + 0.5f, y + 0.5f), circles, blend) <= 0f;
            return on;
        }
    }
}
