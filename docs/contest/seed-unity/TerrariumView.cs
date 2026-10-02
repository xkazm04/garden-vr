using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Terrarium
{
    /// <summary>
    /// The jar on the table. Pure presentation: it is told the breath state and the garden record and draws them.
    /// Built procedurally so a look swap, a headless capture and the XR player all use the same object.
    /// Origin = the jar's footprint on the table (the spatial anchor in the MR build).
    /// </summary>
    public sealed class TerrariumView : MonoBehaviour
    {
        public TerrariumLook look;
        public bool ownsLighting = true;

        // state pushed in
        public float uncoil, fog, vitality = 1f;
        public int permanentFronds, flowers;

        Transform _root, _frondsRoot;
        MeshFilter _activeFrond; Mesh _activeMesh;
        Material _glassInstance;
        Transform _dew; Light _glow, _key;
        Volume _volume;
        readonly List<(Transform t, Mesh m, TerrariumMeshes.FrondShape s)> _fronds = new List<(Transform, Mesh, TerrariumMeshes.FrondShape)>();
        readonly List<GameObject> _flowers = new List<GameObject>();
        int _builtFronds = -1, _builtFlowers = -1; float _builtVitality = -1;
        float _answerT = -1f;

        public const float JarRadius = 0.072f;
        static Vector3 SlotPosition(int i)
        {
            if (i == 0) return new Vector3(0.004f, 0.034f, 0.006f);
            float a = i * 137.508f * Mathf.Deg2Rad;
            float r = 0.012f + 0.006f * Mathf.Sqrt(i);
            return new Vector3(Mathf.Cos(a) * r, 0.034f, Mathf.Sin(a) * r);
        }
        static float SlotYaw(int i) => i * 137.508f + 20f;
        static float SlotLength(int i) => 0.118f + 0.022f * Mathf.Sin(i * 2.17f + 0.4f);

        public void Build()
        {
            if (_root) Kill(_root.gameObject);
            _fronds.Clear(); _flowers.Clear(); _builtFronds = -1; _builtFlowers = -1;
            _root = new GameObject("TerrariumRoot").transform; _root.SetParent(transform, false);

            // table (a passthrough stand-in in screenshots; in MR the real table is under the anchor)
            var table = Prim(PrimitiveType.Cube, "Table", look.table, _root);
            table.localScale = new Vector3(1.6f, 0.04f, 1.0f); table.localPosition = new Vector3(0, -0.02f, 0.18f);

            // soil, pebbles, moss inside the jar
            var soil = Prim(PrimitiveType.Cylinder, "Soil", look.soil, _root);
            soil.localScale = new Vector3(0.136f, 0.016f, 0.136f); soil.localPosition = new Vector3(0, 0.018f, 0);
            var drain = Prim(PrimitiveType.Cylinder, "Drainage", look.pebble, _root);
            drain.localScale = new Vector3(0.138f, 0.006f, 0.138f); drain.localPosition = new Vector3(0, 0.006f, 0);
            var rng = new System.Random(11);
            for (int i = 0; i < 9; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2, r = 0.02f + (float)rng.NextDouble() * 0.04f;
                var p = Prim(PrimitiveType.Sphere, "Pebble" + i, look.pebble, _root);
                float s = 0.010f + (float)rng.NextDouble() * 0.010f;
                p.localScale = new Vector3(s, s * 0.55f, s * 0.85f);
                p.localPosition = new Vector3(Mathf.Cos(a) * r, 0.035f, Mathf.Sin(a) * r);
                p.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0);
            }
            for (int i = 0; i < 7; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2, r = 0.03f + (float)rng.NextDouble() * 0.03f;
                var m = Prim(PrimitiveType.Sphere, "Moss" + i, look.moss, _root);
                float s = 0.018f + (float)rng.NextDouble() * 0.016f;
                m.localScale = new Vector3(s, s * 0.45f, s);
                m.localPosition = new Vector3(Mathf.Cos(a) * r, 0.036f, Mathf.Sin(a) * r);
            }

            _frondsRoot = new GameObject("Fronds").transform; _frondsRoot.SetParent(_root, false);

            var active = new GameObject("ActiveFrond");
            active.transform.SetParent(_root, false);
            _activeFrond = active.AddComponent<MeshFilter>();
            active.AddComponent<MeshRenderer>().sharedMaterial = look.frondActive ? look.frondActive : look.frond;
            _activeMesh = new Mesh { name = "ActiveFrondMesh" }; _activeMesh.MarkDynamic();
            _activeFrond.sharedMesh = _activeMesh;

            _dew = Prim(PrimitiveType.Sphere, "DewBead", look.dew, _root);
            _dew.localScale = Vector3.one * 0.0065f; _dew.gameObject.SetActive(false);

            // glass last (transparent), plus an optional cork
            var jar = new GameObject("Jar");
            jar.transform.SetParent(_root, false);
            jar.AddComponent<MeshFilter>().sharedMesh = TerrariumMeshes.Lathe(TerrariumMeshes.JarProfile, 96, "JarGlass");
            _glassInstance = new Material(look.glass) { name = look.glass.name + " (instance)" };
            jar.AddComponent<MeshRenderer>().sharedMaterial = _glassInstance;
            if (look.corkLid && look.cork)
            {
                var cork = Prim(PrimitiveType.Cylinder, "Cork", look.cork, _root);
                cork.localScale = new Vector3(0.088f, 0.014f, 0.088f); cork.localPosition = new Vector3(0, 0.226f, 0);
            }

            var glowGo = new GameObject("AnswerGlow"); glowGo.transform.SetParent(_root, false);
            glowGo.transform.localPosition = new Vector3(0, 0.11f, 0);
            _glow = glowGo.AddComponent<Light>(); _glow.type = LightType.Point; _glow.range = 0.45f; _glow.color = look.answerGlow; _glow.intensity = 0;

            if (ownsLighting) BuildLighting();
            Apply(0f);
        }

        void BuildLighting()
        {
            var keyGo = new GameObject("KeyLight"); keyGo.transform.SetParent(_root, false);
            keyGo.transform.rotation = Quaternion.Euler(look.keyEuler);
            _key = keyGo.AddComponent<Light>(); _key.type = LightType.Directional; _key.color = look.keyColor; _key.intensity = look.keyIntensity;
            _key.shadows = LightShadows.Soft; _key.shadowStrength = 0.75f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = look.ambientSky; RenderSettings.ambientEquatorColor = look.ambientEquator; RenderSettings.ambientGroundColor = look.ambientGround;
            RenderSettings.fog = false;

            var volGo = new GameObject("Post"); volGo.transform.SetParent(_root, false);
            _volume = volGo.AddComponent<Volume>(); _volume.isGlobal = true; _volume.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(look.bloomIntensity); bloom.threshold.Override(look.bloomThreshold); bloom.scatter.Override(0.72f);
            var tm = profile.Add<Tonemapping>(true); tm.mode.Override(look.acesTonemap ? TonemappingMode.ACES : TonemappingMode.Neutral);
            var ca = profile.Add<ColorAdjustments>(true); ca.postExposure.Override(look.postExposure); ca.saturation.Override(look.saturation); ca.contrast.Override(look.contrast);
            var vg = profile.Add<Vignette>(true); vg.intensity.Override(look.vignette); vg.smoothness.Override(0.5f);
            _volume.sharedProfile = profile;
        }

        /// <summary>Start the garden's answer: the frond settles, a dew bead rolls down it, the jar glows once.</summary>
        public void PlayAnswer() { _answerT = 0f; }
        public bool AnswerPlaying => _answerT >= 0f;

        public void Apply(float dt)
        {
            if (!_root) return;
            if (_builtFronds != permanentFronds || !Mathf.Approximately(_builtVitality, vitality)) RebuildPermanent();
            if (_builtFlowers != flowers) RebuildFlowers();

            // the frond being grown right now sits in the next slot
            int slot = permanentFronds;
            var shape = new TerrariumMeshes.FrondShape { Length = SlotLength(slot), Uncoil = uncoil, Droop = 1f - vitality, LeafScale = 1f, Segments = 48 };
            TerrariumMeshes.BuildFrond(_activeMesh, shape);
            var at = _activeFrond.transform;
            at.localPosition = SlotPosition(slot);
            at.localRotation = Quaternion.Euler(0, SlotYaw(slot), 0) * Quaternion.Euler(-8f * uncoil, 0, 0);
            _activeFrond.gameObject.SetActive(uncoil > 0.001f || !AnswerPlaying);

            _glassInstance.SetFloat("_Fog", fog);

            if (_answerT >= 0f)
            {
                _answerT += dt;
                float roll = Mathf.Clamp01((_answerT - 0.6f) / 2.6f);
                _dew.gameObject.SetActive(_answerT > 0.3f && roll < 1f);
                float ease = roll * roll * (3 - 2 * roll);
                var local = TerrariumMeshes.PointOnFrond(shape, Mathf.Lerp(0.92f, 0.05f, ease)) + new Vector3(0, 0.004f, 0);
                _dew.position = at.TransformPoint(local);
                float pulse = Mathf.Exp(-Mathf.Pow((_answerT - 1.2f) / 0.7f, 2));
                _glow.intensity = look.answerGlowIntensity * pulse;
                var g = look.answerGlow; g.a = 0.5f * pulse;
                _glassInstance.SetColor("_Glow", g);
                if (_answerT > 4f) { _answerT = -1f; _glow.intensity = 0; _glassInstance.SetColor("_Glow", Color.clear); _dew.gameObject.SetActive(false); }
            }
        }

        void RebuildPermanent()
        {
            foreach (var f in _fronds) { Kill(f.t.gameObject); }
            _fronds.Clear();
            for (int i = 0; i < permanentFronds; i++)
            {
                var go = new GameObject("Frond" + i);
                go.transform.SetParent(_frondsRoot, false);
                var shape = new TerrariumMeshes.FrondShape { Length = SlotLength(i), Uncoil = 1f, Droop = 1f - vitality, LeafScale = 1f, Segments = 40 };
                var mesh = new Mesh { name = "Frond" + i };
                TerrariumMeshes.BuildFrond(mesh, shape);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = look.frond;
                go.transform.localPosition = SlotPosition(i);
                go.transform.localRotation = Quaternion.Euler(0, SlotYaw(i), 0) * Quaternion.Euler(-8f, 0, 0);
                _fronds.Add((go.transform, mesh, shape));
            }
            _builtFronds = permanentFronds; _builtVitality = vitality;
        }

        void RebuildFlowers()
        {
            foreach (var f in _flowers) Kill(f);
            _flowers.Clear();
            for (int k = 0; k < flowers; k++)
            {
                var head = new GameObject("Flower" + k);
                head.transform.SetParent(_root, false);
                float a = (k * 97f + 40f) * Mathf.Deg2Rad;
                head.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.045f, 0.040f, Mathf.Sin(a) * 0.045f);
                for (int p = 0; p < 5; p++)
                {
                    var petal = Prim(PrimitiveType.Sphere, "Petal" + p, look.flower, head.transform);
                    float pa = p / 5f * Mathf.PI * 2f;
                    petal.localScale = new Vector3(0.008f, 0.0025f, 0.005f);
                    petal.localPosition = new Vector3(Mathf.Cos(pa) * 0.0055f, 0.004f, Mathf.Sin(pa) * 0.0055f);
                    petal.localRotation = Quaternion.Euler(0, -pa * Mathf.Rad2Deg, 0);
                }
                var centre = Prim(PrimitiveType.Sphere, "Centre", look.dew, head.transform);
                centre.localScale = Vector3.one * 0.0042f; centre.localPosition = new Vector3(0, 0.0055f, 0);
                _flowers.Add(head);
            }
            _builtFlowers = flowers;
        }

        static Transform Prim(PrimitiveType t, string name, Material m, Transform parent)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            var col = go.GetComponent<Collider>(); if (col) Kill(col);
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void Kill(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }
    }
}
