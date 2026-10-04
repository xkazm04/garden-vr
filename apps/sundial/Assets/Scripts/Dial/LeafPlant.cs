using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Sundial
{
    /// <summary>One piece on the parts sheet. Pixel units, y down, as in the sliced atlas (apps/sundial/Art/Scripts/leafplant_s3.py).</summary>
    [Serializable]
    public sealed class LeafPartRect
    {
        public string kind;
        public int index;
        public int[] rect;
        public float[] pivot;
        public int[] content;
        public float[] tip;
        /// <summary>Stem pieces: [y top, y bottom, shaft half width] of the run of rows with no leaf nubs.</summary>
        public float[] bare;
    }

    [Serializable]
    sealed class LeafPartsFile
    {
        public int[] size;
        public LeafPartRect[] parts;
    }

    /// <summary>What the heads of the plant show. Matches <see cref="DialView.ShownBloomCard"/>: none, bud, open.</summary>
    public enum LeafBloom
    {
        None = 0,
        Bud = 1,
        Open = 2
    }

    /// <summary>
    /// Spike S3. One plant built from up to 60 drawn cards cut from one parts sheet: three stems on splines, opposite
    /// leaf pairs along them, flower and bud heads, a soil tuft, one soft contact shadow. The whole assembly is one mesh
    /// and one draw (<c>Fidelity/Leaf</c>), plus one blended quad for the contact shadow.
    /// Every vertex carries a normal transferred from one plant-sized ellipsoid, so the single shade step reads as one
    /// rounded plant. The layout is seeded and deterministic. Local axes are the dial's: x right, y up, z away from the viewer.
    /// </summary>
    public sealed class LeafPlant
    {
        /// <summary>The midday plant's resources, as in T-SUN-045. The others are <see cref="LeafSpecies.MaterialResource"/> and <see cref="LeafSpecies.PartsResource"/>.</summary>
        public const string ResourceMaterial = "LeafPlant/Leaf_Midday";
        public const string ResourceParts = "LeafPlant/midday-parts";

        readonly LeafSpecies _species;
        public LeafSpecies Species { get { return _species; } }

        public int Cards { get; private set; }
        public int Vertices { get; private set; }
        public int Triangles { get; private set; }
        public Transform Root { get { return _root; } }

        readonly Transform _root;
        readonly Material _leafMat;
        readonly Mesh _mesh;
        readonly MeshRenderer _renderer;
        readonly Texture2D _atlas;
        readonly List<LeafPartRect> _leaves = new List<LeafPartRect>();
        readonly List<LeafPartRect> _faceFlowers = new List<LeafPartRect>();
        readonly List<LeafPartRect> _sideFlowers = new List<LeafPartRect>();
        readonly List<LeafPartRect> _buds = new List<LeafPartRect>();
        readonly List<LeafPartRect> _stems = new List<LeafPartRect>();
        readonly List<LeafPartRect> _tufts = new List<LeafPartRect>();
        readonly Vector2 _atlasSize;
        Color32[] _atlasPixels;
        Mesh _shadowMesh;
        Material _shadowMat;
        MeshRenderer _shadowRenderer;
        LeafBloom _built = (LeafBloom)(-1);
        readonly List<Vector3> _verts = new List<Vector3>(700);
        readonly List<Vector3> _normals = new List<Vector3>(700);
        readonly List<Vector2> _uvs = new List<Vector2>(700);
        readonly List<Color> _colors = new List<Color>(700);
        readonly List<int> _tris = new List<int>(2400);
        System.Random _rng;

        /// <summary>The template carries the shader, atlas and look values. This keeps its own instance so no asset is edited.</summary>
        public LeafPlant(Transform parent, Material template, TextAsset parts, Material contactTemplate)
            : this(parent, LeafSpecies.Midday, template, parts, contactTemplate)
        {
        }

        public LeafPlant(Transform parent, LeafSpecies species, Material template, TextAsset parts, Material contactTemplate)
        {
            if (species == null) throw new ArgumentNullException("species");
            _species = species;
            if (template == null) throw new InvalidOperationException("LeafPlant needs the Fidelity/Leaf material");
            if (parts == null) throw new InvalidOperationException("LeafPlant needs the parts json");
            _atlas = template.mainTexture as Texture2D;
            if (_atlas == null) throw new InvalidOperationException("the leaf material has no parts atlas");
            LeafPartsFile file = JsonUtility.FromJson<LeafPartsFile>(parts.text);
            if (file == null || file.parts == null || file.parts.Length == 0) throw new InvalidOperationException("parts json is empty");
            _atlasSize = new Vector2(_atlas.width, _atlas.height);
            foreach (LeafPartRect p in file.parts)
            {
                switch (p.kind)
                {
                    case "leaf": _leaves.Add(p); break;
                    case "flower": _faceFlowers.Add(p); break;
                    case "flowerside": _sideFlowers.Add(p); break;
                    case "bud": _buds.Add(p); break;
                    case "stem": _stems.Add(p); break;
                    case "tuft": _tufts.Add(p); break;
                }
            }
            if (_leaves.Count < 6 || (species.UsesFaceFlowers && _faceFlowers.Count < 1) || _sideFlowers.Count < 1 || _buds.Count < 1 || _stems.Count < 1 || _tufts.Count < 1)
                throw new InvalidOperationException("parts json is missing a kind of piece");

            var go = new GameObject(species.ObjectName);
            go.transform.SetParent(parent, false);
            _root = go.transform;
            _mesh = new Mesh { name = "LeafPlantAssembly." + species.Id, hideFlags = HideFlags.DontSave };
            _leafMat = new Material(template) { name = species.ObjectName + ".instance", hideFlags = HideFlags.DontSave };
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = go.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _leafMat;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            if (contactTemplate != null)
            {
                var sg = new GameObject("LeafPlant.contact");
                sg.transform.SetParent(_root, false);
                _shadowMesh = BuildShadowMesh(species);
                sg.AddComponent<MeshFilter>().sharedMesh = _shadowMesh;
                _shadowMat = new Material(contactTemplate) { name = species.ObjectName + ".contact", hideFlags = HideFlags.DontSave };
                _shadowRenderer = sg.AddComponent<MeshRenderer>();
                _shadowRenderer.sharedMaterial = _shadowMat;
                _shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _shadowRenderer.receiveShadows = false;
            }
        }

        public void SetActive(bool on)
        {
            _root.gameObject.SetActive(on);
        }

        public void SetPose(Vector3 localPosition, float scale)
        {
            _root.localPosition = localPosition;
            _root.localRotation = Quaternion.identity;
            _root.localScale = Vector3.one * scale;
        }

        public void SetLook(float time, Color tint)
        {
            _leafMat.SetFloat("_T", time);
            _leafMat.SetColor("_Color", tint);
        }

        public void SetContact(float alpha)
        {
            if (_shadowMat == null) return;
            Color c = _shadowMat.GetColor("_Color");
            c.a = alpha;
            _shadowMat.SetColor("_Color", c);
        }

        public void Destroy()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root.gameObject);
            if (_mesh != null) UnityEngine.Object.DestroyImmediate(_mesh);
            if (_leafMat != null) UnityEngine.Object.DestroyImmediate(_leafMat);
            if (_shadowMesh != null) UnityEngine.Object.DestroyImmediate(_shadowMesh);
            if (_shadowMat != null) UnityEngine.Object.DestroyImmediate(_shadowMat);
        }

        /// <summary>Rebuilds the one mesh when the heads change. The layout is the same each time.</summary>
        public void Rebuild(LeafBloom bloom)
        {
            if (_built == bloom) return;
            _built = bloom;
            _verts.Clear();
            _normals.Clear();
            _uvs.Clear();
            _colors.Clear();
            _tris.Clear();
            _rng = new System.Random(_species.Seed);
            Cards = 0;

            var stems = new Stem[_species.Stems.Length];
            for (int i = 0; i < stems.Length; i++)
            {
                LeafStemDef d = _species.Stems[i];
                stems[i] = new Stem(d.Base, d.Top, d.Nodes, d.Phase);
            }
            for (int i = 0; i < stems.Length; i++)
                AddStemRibbons(stems[i], i);
            if (_species.Layout == LeafLayout.Basal)
                AddBasalLeaves();
            int small = 0;
            for (int i = 0; i < stems.Length; i++)
                AddLeaves(stems[i], i, ref small);
            AddTufts();
            if (bloom != LeafBloom.None)
                AddHeads(stems, bloom);

            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.SetNormals(_normals);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_tris, 0);
            _mesh.RecalculateBounds();
            Vertices = _verts.Count;
            Triangles = _tris.Count / 3;
        }

        // ---- stems ---------------------------------------------------------------------------------------------

        struct Stem
        {
            public Vector3 Base, Top;
            public int Nodes;
            public float Phase;

            public Stem(Vector3 b, Vector3 t, int nodes, float phase)
            {
                Base = b;
                Top = t;
                Nodes = nodes;
                Phase = phase;
            }

            public Vector3 At(float t)
            {
                Vector3 lean = Top - Base;
                Vector3 p1 = Base + new Vector3(lean.x * 0.12f, lean.y * 0.32f, lean.z * 0.12f);
                Vector3 p2 = Base + new Vector3(lean.x * 0.58f, lean.y * 0.68f, lean.z * 0.58f);
                float u = 1f - t;
                return u * u * u * Base + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * Top;
            }

            public Vector3 Tangent(float t)
            {
                Vector3 a = At(Mathf.Clamp01(t - 0.02f));
                Vector3 b = At(Mathf.Clamp01(t + 0.02f));
                return (b - a).normalized;
            }
        }

        void AddStemRibbons(Stem stem, int index)
        {
            // A straight bare column of a stem piece. The slicer finds the run of rows with no leaf nubs.
            var straight = new List<LeafPartRect>();
            foreach (LeafPartRect st in _stems)
            {
                if (st.tip != null && Mathf.Abs(st.tip[0] - st.pivot[0]) < 6f) straight.Add(st);
            }
            LeafPartRect piece = straight.Count > 0 ? straight[index % straight.Count] : _stems[0];
            const int segments = 8;
            float shaftHalfPx;
            float yTop = piece.content[1] + 5f;
            float yBot = piece.content[1] + 0.45f * piece.content[3];
            if (piece.bare != null && piece.bare.Length >= 3)
            {
                yTop = piece.bare[0] + 2f;
                yBot = piece.bare[1] - 2f;
                shaftHalfPx = piece.bare[2] + 6f;
            }
            else
            {
                shaftHalfPx = 13f;
            }
            float colX0 = (piece.pivot[0] - shaftHalfPx) / _atlasSize.x;
            float colX1 = (piece.pivot[0] + shaftHalfPx) / _atlasSize.x;
            float halfWidth = shaftHalfPx * _species.StemMetresPerPx;
            Vector3 xAxis = Vector3.right;
            Vector3 zAxis = Vector3.forward;
            for (int ribbon = 0; ribbon < _species.StemRibbons; ribbon++)
            {
                int start = _verts.Count;
                for (int k = 0; k <= segments; k++)
                {
                    float t = k / (float)segments;
                    Vector3 c = stem.At(t);
                    Vector3 w = ribbon == 0 ? xAxis : zAxis;
                    float v = 1f - Mathf.Lerp(yBot, yTop, t) / _atlasSize.y;
                    // taper a little toward the tip
                    float hw = halfWidth * Mathf.Lerp(1.15f, 0.8f, t);
                    AddVertex(c - w * hw, new Vector2(colX0, v), Sway(c.y), stem.Phase);
                    AddVertex(c + w * hw, new Vector2(colX1, v), Sway(c.y), stem.Phase);
                }
                for (int k = 0; k < segments; k++)
                {
                    int a = start + k * 2;
                    _tris.Add(a); _tris.Add(a + 2); _tris.Add(a + 1);
                    _tris.Add(a + 1); _tris.Add(a + 2); _tris.Add(a + 3);
                }
                Cards++;
            }
        }

        // ---- leaves --------------------------------------------------------------------------------------------

        /// <summary>The first leaves on the sheet are the big class (sheet order is by area). The last few are the small class.</summary>
        int BigLeafCount
        {
            get { return Mathf.Max(4, _leaves.Count - 3); }
        }

        void AddLeaves(Stem stem, int stemIndex, ref int smallPlaced)
        {
            float az0 = (float)_rng.NextDouble() * Mathf.PI * 2f;
            int big = BigLeafCount;
            for (int k = 0; k < stem.Nodes; k++)
            {
                float t = NodeT(stem, k);
                Vector3 p = stem.At(t);
                Vector3 axis = stem.Tangent(t);
                Vector3 a = Vector3.Cross(axis, Vector3.forward);
                if (a.sqrMagnitude < 1e-4f) a = Vector3.right;
                a.Normalize();
                Vector3 b = Vector3.Cross(axis, a);
                float az = az0 + k * (Mathf.PI * 0.5f) * (1f + 0.15f * ((float)_rng.NextDouble() - 0.5f));
                for (int side = 0; side < 2; side++)
                {
                    float phi = az + side * Mathf.PI;
                    Vector3 radial = a * Mathf.Cos(phi) + b * Mathf.Sin(phi);
                    float pitch = Mathf.Lerp(_species.PitchBase, _species.PitchTop, t) + ((float)_rng.NextDouble() - 0.5f) * 14f;
                    Vector3 dir = (axis * Mathf.Cos(pitch * Mathf.Deg2Rad) + radial * Mathf.Sin(pitch * Mathf.Deg2Rad)).normalized;
                    float size01 = 1f - t;
                    int pick = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(0.5f, big - 1.5f, Mathf.Pow(1f - size01, 0.8f)) + ((float)_rng.NextDouble() - 0.5f) * 2.2f), 0, big - 1);
                    LeafPartRect part = _leaves[pick];
                    float scale = _species.LeafMetresPerPx * Mathf.Lerp(0.80f, 1.12f, size01) * (0.92f + 0.16f * (float)_rng.NextDouble());
                    float roll = ((float)_rng.NextDouble() - 0.5f) * 70f;
                    AddBladeCard(part, p + radial * 0.0006f, dir, scale, roll, _species.LeafDroop, _species.LeafCup, stem.Phase + k * 0.07f + side * 0.03f);
                }
                // The second size class: one small leaf in the gap above this node, between the two pair directions.
                if (k < stem.Nodes - 1 && smallPlaced < _species.SmallLeaves)
                {
                    float tm = 0.5f * (t + NodeT(stem, k + 1));
                    Vector3 pm = stem.At(tm);
                    Vector3 am = stem.Tangent(tm);
                    Vector3 xa = Vector3.Cross(am, Vector3.forward);
                    if (xa.sqrMagnitude < 1e-4f) xa = Vector3.right;
                    xa.Normalize();
                    Vector3 ya = Vector3.Cross(am, xa);
                    float phim = az + Mathf.PI * 0.25f + (smallPlaced % 2) * Mathf.PI;
                    Vector3 radialm = xa * Mathf.Cos(phim) + ya * Mathf.Sin(phim);
                    float pitchm = 62f + ((float)_rng.NextDouble() - 0.5f) * 16f;
                    Vector3 dirm = (am * Mathf.Cos(pitchm * Mathf.Deg2Rad) + radialm * Mathf.Sin(pitchm * Mathf.Deg2Rad)).normalized;
                    int pickm = big + (smallPlaced % Mathf.Max(1, _leaves.Count - big));
                    pickm = Mathf.Clamp(pickm, 0, _leaves.Count - 1);
                    float scalem = _species.LeafMetresPerPx * (0.95f + 0.2f * (float)_rng.NextDouble());
                    float rollm = ((float)_rng.NextDouble() - 0.5f) * 60f;
                    AddBladeCard(_leaves[pickm], pm + radialm * 0.0005f, dirm, scalem, rollm, _species.LeafDroop, _species.LeafCup, stem.Phase + k * 0.07f + 0.05f);
                    smallPlaced++;
                }
            }
        }

        float NodeT(Stem stem, int k)
        {
            return Mathf.Lerp(_species.NodeStart, _species.NodeEnd, stem.Nodes <= 1 ? 0f : k / (float)(stem.Nodes - 1));
        }

        /// <summary>A clump of narrow blades fanned out from the base: steep ones are long, flat ones short, arching outward.</summary>
        void AddBasalLeaves()
        {
            float golden = 2.39996f;
            float az0 = (float)_rng.NextDouble() * Mathf.PI * 2f;
            for (int i = 0; i < _species.BasalLeaves; i++)
            {
                float az = az0 + i * golden;
                float mix = Mathf.Repeat(i * 0.618034f, 1f);
                float pitch = Mathf.Lerp(12f, 66f, mix) + ((float)_rng.NextDouble() - 0.5f) * 8f;
                Vector3 radial = new Vector3(Mathf.Cos(az), 0f, Mathf.Sin(az));
                float off = 0.0015f + 0.005f * (float)_rng.NextDouble();
                Vector3 dir = (Vector3.up * Mathf.Cos(pitch * Mathf.Deg2Rad) + radial * Mathf.Sin(pitch * Mathf.Deg2Rad)).normalized;
                int pick = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(0.5f, _leaves.Count - 1.5f, mix) + ((float)_rng.NextDouble() - 0.5f) * 3f), 0, _leaves.Count - 1);
                float scale = _species.LeafMetresPerPx * (0.80f + 0.30f * (float)_rng.NextDouble());
                float roll = ((float)_rng.NextDouble() - 0.5f) * 50f;
                AddBladeCard(_leaves[pick], radial * off, dir, scale, roll, _species.LeafDroop * (0.5f + mix), _species.LeafCup, 0.1f + 0.012f * i);
            }
        }

        /// <summary>
        /// A card whose pivot is the base of the piece, pointing along <paramref name="dir"/>, drooping under gravity by
        /// <paramref name="droop"/> of its length, and cupped upward by <paramref name="cup"/> of its half width.
        /// </summary>
        void AddBladeCard(LeafPartRect part, Vector3 origin, Vector3 dir, float metresPerPx, float rollDeg, float droop, float cup, float phase)
        {
            Vector3 up = Vector3.up + Vector3.back * 0.30f;
            Vector3 n0 = (up - dir * Vector3.Dot(up, dir));
            if (n0.sqrMagnitude < 1e-4f) n0 = Vector3.back;
            n0.Normalize();
            Vector3 x0 = Vector3.Cross(n0, dir).normalized;
            Quaternion roll = Quaternion.AngleAxis(rollDeg, dir);
            Vector3 xAxis = roll * x0;
            Vector3 nAxis = roll * n0;
            AddGrid(part, 4, 2, (alongPx, acrossPx, halfPx) =>
            {
                float s = alongPx * metresPerPx;
                float across = acrossPx * metresPerPx;
                float half = Mathf.Max(halfPx * metresPerPx, 1e-4f);
                Vector3 pos = origin + dir * s + Vector3.down * (droop * s * s / Mathf.Max(part.content[3] * metresPerPx, 1e-3f))
                              + xAxis * across + nAxis * (cup * half * (1f - (across / half) * (across / half)));
                return pos;
            }, phase);
        }

        // ---- heads, tufts ---------------------------------------------------------------------------------------

        void AddHeads(Stem[] stems, LeafBloom bloom)
        {
            bool open = bloom == LeafBloom.Open;
            int face = 0;
            int side = 0;
            int bud = 0;
            LeafHeadDef[] heads = _species.Heads;
            for (int i = 0; i < heads.Length; i++)
            {
                LeafHeadDef h = heads[i];
                Stem stem = stems[h.Stem];
                Vector3 at = stem.At(h.T);
                Vector3 dir = h.AlongStem ? stem.Tangent(h.T) : h.Dir.normalized;
                float phase = stem.Phase + 0.5f + i * 0.09f;
                if (open)
                {
                    if (h.Face)
                    {
                        LeafPartRect part = _faceFlowers[face++ % _faceFlowers.Count];
                        AddFlowerFace(part, at + dir * 0.003f, new Vector3(dir.x * 0.5f, 0.55f, -0.75f).normalized, _species.FaceFlowerMetresPerPx * h.Scale, ((float)_rng.NextDouble() - 0.5f) * 40f, phase);
                    }
                    else
                    {
                        LeafPartRect part = _sideFlowers[side++ % _sideFlowers.Count];
                        AddBladeCard(part, at, dir, _species.SideFlowerMetresPerPx * h.Scale, ((float)_rng.NextDouble() - 0.5f) * 30f, 0.02f, 0.04f, phase);
                    }
                }
                else
                {
                    LeafPartRect part = _buds[bud++ % _buds.Count];
                    Vector3 up = h.AlongStem ? dir : new Vector3(dir.x * 0.5f, 1f, dir.z * 0.4f).normalized;
                    AddBladeCard(part, at, up, _species.BudMetresPerPx * _species.BudHeadScale, ((float)_rng.NextDouble() - 0.5f) * 30f, 0.06f, 0.05f, phase);
                }
            }
            if (open && _species.ExtraBuds != null)
            {
                // A few buds among the open heads: the plant is still opening.
                foreach (LeafExtraBud e in _species.ExtraBuds)
                    AddBladeCard(_buds[e.BudIndex % _buds.Count], stems[e.Stem].At(e.T), e.Dir.normalized, _species.BudMetresPerPx * e.Scale, e.Roll, 0.06f, 0.05f, e.Phase);
            }
        }

        /// <summary>A face-on flower. The pivot is the middle of the piece. The card turns to <paramref name="facing"/>.</summary>
        void AddFlowerFace(LeafPartRect part, Vector3 centre, Vector3 facing, float metresPerPx, float rollDeg, float phase)
        {
            Vector3 x0 = Vector3.Cross(Vector3.up, facing);
            if (x0.sqrMagnitude < 1e-4f) x0 = Vector3.right;
            x0.Normalize();
            Vector3 y0 = Vector3.Cross(facing, x0).normalized;
            Quaternion roll = Quaternion.AngleAxis(rollDeg, facing);
            Vector3 xAxis = roll * x0;
            Vector3 yAxis = roll * y0;
            AddGrid(part, 3, 3, (alongPx, acrossPx, halfPx) =>
            {
                // The pivot of a face-on flower is the middle of the piece, so the offsets are already centred.
                float s = alongPx * metresPerPx;
                float across = acrossPx * metresPerPx;
                float half = Mathf.Max(halfPx * metresPerPx, 1e-4f);
                float r2 = (across * across + s * s) / (half * half);
                return centre + yAxis * s + xAxis * across + facing * (0.0035f * Mathf.Clamp01(1f - r2 * 0.5f));
            }, phase);
        }

        void AddTufts()
        {
            // Tufts at the base, flat on the soil. Two crossed cards each when the species asks for it.
            float tuft = _species.TuftMetresPerPx;
            for (int i = 0; i < _species.TuftCount; i++)
            {
                LeafPartRect part = _tufts[i % _tufts.Count];
                float a = _species.TuftAngle[i % _species.TuftAngle.Length] * Mathf.Deg2Rad;
                float radius = _species.TuftRadius[i % _species.TuftRadius.Length];
                Vector3 at = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius * 0.8f);
                int cards = _species.TuftCrossed ? 2 : 1;
                for (int c = 0; c < cards; c++)
                {
                    float yaw = (c == 0 ? 0f : 78f) + i * 31f;
                    Vector3 xAxis = Quaternion.AngleAxis(yaw, Vector3.up) * Vector3.right;
                    Vector3 pivotAt = at;
                    AddGrid(part, 2, 2, (alongPx, acrossPx, halfPx) =>
                        pivotAt + Vector3.up * (alongPx * tuft) + xAxis * (acrossPx * tuft), 0.9f + i * 0.05f);
                }
            }
        }

        // ---- geometry ------------------------------------------------------------------------------------------

        delegate Vector3 CardMap(float alongPx, float acrossPx, float halfWidthPx);

        /// <summary>Adds one card as a (columns+1) by (rows+1) grid over the part's rect, positions from the map (pixels from the pivot).</summary>
        void AddGrid(LeafPartRect part, int rows, int columns, CardMap map, float phase)
        {
            int x0 = part.rect[0];
            int y0 = part.rect[1];
            int w = part.rect[2];
            int h = part.rect[3];
            float px = part.pivot[0];
            float py = part.pivot[1];
            int start = _verts.Count;
            float halfPx = w * 0.5f;
            for (int r = 0; r <= rows; r++)
            {
                float y = y0 + h - (r / (float)rows) * h;
                for (int c = 0; c <= columns; c++)
                {
                    float x = x0 + (c / (float)columns) * w;
                    Vector3 pos = map(py - y, x - px, halfPx);
                    var uv = new Vector2(x / _atlasSize.x, 1f - y / _atlasSize.y);
                    AddVertex(pos, uv, Sway(pos.y), phase);
                }
            }
            int stride = columns + 1;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    int a = start + r * stride + c;
                    _tris.Add(a); _tris.Add(a + stride); _tris.Add(a + 1);
                    _tris.Add(a + 1); _tris.Add(a + stride); _tris.Add(a + stride + 1);
                }
            }
            Cards++;
        }

        float Sway(float y)
        {
            return Mathf.Clamp01(y / _species.PlantHeight);
        }

        void AddVertex(Vector3 pos, Vector2 uv, float sway, float phase)
        {
            Vector3 scaled = pos * _species.Scale;
            _verts.Add(scaled);
            _normals.Add(NormalAt(scaled));
            _uvs.Add(uv);
            _colors.Add(new Color(sway, Mathf.Repeat(phase, 1f), 0f, 1f));
        }

        /// <summary>The gradient of the midday plant's ellipsoid at the point. The same for both faces of any card.</summary>
        public static Vector3 EllipsoidNormal(Vector3 p)
        {
            return EllipsoidNormal(LeafSpecies.Midday, p);
        }

        public static Vector3 EllipsoidNormal(LeafSpecies species, Vector3 p)
        {
            Vector3 d = p - species.EllipsoidCentre;
            Vector3 r = species.EllipsoidRadius;
            var n = new Vector3(d.x / (r.x * r.x), d.y / (r.y * r.y), d.z / (r.z * r.z));
            return n.sqrMagnitude < 1e-12f ? Vector3.up : n.normalized;
        }

        Vector3 NormalAt(Vector3 p)
        {
            return EllipsoidNormal(_species, p);
        }

        static Mesh BuildShadowMesh(LeafSpecies species)
        {
            var mesh = new Mesh { name = "LeafPlantContact." + species.Id, hideFlags = HideFlags.DontSave };
            float rx = species.ContactRx;
            float rz = species.ContactRz;
            mesh.SetVertices(new[]
            {
                new Vector3(-rx, 0.004f, -rz), new Vector3(rx, 0.004f, -rz),
                new Vector3(rx, 0.004f, rz), new Vector3(-rx, 0.004f, rz)
            });
            mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
            mesh.SetColors(new[] { Color.white, Color.white, Color.white, Color.white });
            mesh.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---- silhouette for the pinch halo ---------------------------------------------------------------------

        /// <summary>
        /// The assembly as the camera sees it on a camera-facing upright card of <paramref name="cardSize"/> metres whose base
        /// sits at the plant root. The halo mask bakes from this alpha. Rasterised on the CPU from the real triangles and the
        /// atlas alpha, so the halo follows the plant that is drawn.
        /// </summary>
        public Texture2D BakeSilhouette(Vector3 viewForwardLocal, Vector2 cardSize, int width, int height, float rootScale)
        {
            if (!_atlas.isReadable) throw new InvalidOperationException("the parts atlas must be readable for the halo silhouette");
            if (_atlasPixels == null) _atlasPixels = _atlas.GetPixels32();
            Vector3 fwd = viewForwardLocal.normalized;
            Vector3 fh = new Vector3(fwd.x, 0f, fwd.z);
            if (fh.sqrMagnitude < 1e-6f) fh = Vector3.forward;
            fh.Normalize();
            Vector3 toCam = -fh;
            // The card's own +x, as LookRotation turns it: the card shows its texture mirrored to a camera that faces it.
            Vector3 right = Vector3.Cross(Vector3.up, toCam).normalized;
            float denom = Vector3.Dot(fwd, toCam);
            if (Mathf.Abs(denom) < 1e-4f) denom = -1e-4f;
            var uvp = new Vector2[_verts.Count];
            for (int i = 0; i < _verts.Count; i++)
            {
                Vector3 p = _verts[i] * rootScale;
                float t = -Vector3.Dot(p, toCam) / denom;
                Vector3 q = p + fwd * t;
                uvp[i] = new Vector2(Vector3.Dot(q, right) / cardSize.x + 0.5f, q.y / cardSize.y);
            }
            var on = new Color32[width * height];
            var white = new Color32(255, 255, 255, 255);
            for (int tri = 0; tri < _tris.Count; tri += 3)
            {
                int ia = _tris[tri], ib = _tris[tri + 1], ic = _tris[tri + 2];
                RasterTri(on, width, height, uvp[ia], uvp[ib], uvp[ic], _uvs[ia], _uvs[ib], _uvs[ic], white);
            }
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = "LeafPlantSilhouette",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            tex.SetPixels32(on);
            tex.Apply(false, false);
            return tex;
        }

        void RasterTri(Color32[] dst, int w, int h, Vector2 a, Vector2 b, Vector2 c, Vector2 ta, Vector2 tb, Vector2 tc, Color32 value)
        {
            float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)) * w;
            float maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x)) * w;
            float minY = Mathf.Min(a.y, Mathf.Min(b.y, c.y)) * h;
            float maxY = Mathf.Max(a.y, Mathf.Max(b.y, c.y)) * h;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(minX));
            int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(maxX));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(minY));
            int y1 = Mathf.Min(h - 1, Mathf.CeilToInt(maxY));
            Vector2 pa = new Vector2(a.x * w, a.y * h);
            Vector2 pb = new Vector2(b.x * w, b.y * h);
            Vector2 pc = new Vector2(c.x * w, c.y * h);
            float area = (pb.x - pa.x) * (pc.y - pa.y) - (pb.y - pa.y) * (pc.x - pa.x);
            if (Mathf.Abs(area) < 1e-6f) return;
            int aw = _atlas.width;
            int ah = _atlas.height;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float w0 = ((pb.x - px) * (pc.y - py) - (pb.y - py) * (pc.x - px)) / area;
                    float w1 = ((pc.x - px) * (pa.y - py) - (pc.y - py) * (pa.x - px)) / area;
                    float w2 = 1f - w0 - w1;
                    if (w0 < 0f || w1 < 0f || w2 < 0f) continue;
                    Vector2 t = ta * w0 + tb * w1 + tc * w2;
                    int ax = Mathf.Clamp((int)(t.x * aw), 0, aw - 1);
                    int ay = Mathf.Clamp((int)(t.y * ah), 0, ah - 1);
                    if (_atlasPixels[ay * aw + ax].a >= 128) dst[y * w + x] = value;
                }
            }
        }
    }
}
