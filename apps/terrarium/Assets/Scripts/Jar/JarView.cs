using System;
using System.Collections.Generic;
using System.Globalization;
using GardenVR.Capture;
using GardenVR.Core;
using GardenVR.Input;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Terrarium
{
    /// <summary>
    /// Materials and the card quad <see cref="JarView.Build"/> hangs on the round-3 night-jar FBX.
    /// JarSetup saves these as assets so the prefab keeps them.
    /// </summary>
    public sealed class JarLibrary
    {
        public Material Glass, Moss, MossCard, Soil, Cork, Fern, FernNew, Fiddle, Seedling, Dew;
        public Material Ring, Spill, JarHalo, CoilHalo, Mist, Spore;
        public Mesh Quad;
    }

    /// <summary>
    /// Night jar on the desk. Look comes only from state: breath, uncoil, fog, answer, vitality, time.
    /// The mapping matches FidelityHero.ApplyJar. The lean round-3 variant is the default: a smaller desk
    /// spill, a smaller jar halo, and the glass back pass off.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class JarView : MonoBehaviour, ICaptureState
    {
        // Round-3 full spill is 0.42 x 0.30 and the full jar halo is 0.24 x 0.26.
        // Lean keeps a small pool (the style still wants a desk light) and the halo size the cost pass measured.
        public const float LeanSpillWidth = 0.21f;
        public const float LeanSpillHeight = 0.15f;
        public const float LeanHaloWidth = 0.15f;
        public const float LeanHaloHeight = 0.17f;

        static readonly Color FernEmission = new Color(0.35f, 1.0f, 0.62f);
        static readonly Color FiddleEmission = new Color(0.30f, 0.65f, 0.22f);

        [Header("State")]
        [Range(0f, 1f)] public float breath = 0.5f;
        [Range(0f, 1f)] public float uncoil = 0.3f;
        [Range(0f, 1f)] public float answer;
        [Range(0f, 1f)] public float fog = 0.45f;
        [Range(0.6f, 1f)] public float vitality = 1f;
        public float time = 3f;
        /// <summary>Picks which authored frond atlas column each placed frond uses.</summary>
        public int shapeSeed = 4;
        public BreathPhase phase = BreathPhase.Waiting;
        /// <summary>Seconds since the answer began. Negative means the answer is not playing.</summary>
        public float answerTime = -1f;
        /// <summary>Spore step this frame. Negative keeps the capture resim from <see cref="time"/>.</summary>
        public float sporeStep = -1f;

        [Header("Wired by Build")]
        public MeshFilter fiddle;
        public Mesh[] fiddleStates;
        public Renderer newFrond;
        public Renderer dew;
        public Material ringMat;
        public Material glassMat;
        public Material mossMat;
        public Material mossCardMat;
        public Material coilHaloMat;
        public Material jarHaloMat;
        public Material newFrondMat;
        public Material fernMat;
        public Material fiddleMat;
        public Transform[] mist;
        public Transform[] billboards;
        public ParticleSystem spores;

        Mesh _live;
        Vector3[][] _shapes;
        Vector3[] _scratch;
        bool _hooked;
        Mesh[] _frondVariants;
        Transform[] _frondSlots;
        int _shownSeed = int.MinValue;
        bool _frondScaleCached;
        Vector3 _frondBaseScale;
        bool _dewPathReady;
        Vector3 _dewTipLocal;
        Vector3 _dewBaseLocal;
        bool _sporesLive;

        static readonly Color RingMint = new Color(0.75f, 1.35f, 1.05f);
        static readonly Color RingAnswer = new Color(1.3f, 1.8f, 1.4f);
        static readonly Color RingGold = new Color(1.55f, 1.25f, 0.62f);

        public void ApplyCaptureState(IReadOnlyDictionary<string, string> state)
        {
            if (state == null) return;
            foreach (var pair in state)
            {
                float value = Parse(pair.Key, pair.Value);
                switch (pair.Key)
                {
                    case "breath": breath = value; break;
                    case "uncoil": uncoil = value; break;
                    case "fog": fog = value; break;
                    case "answer":
                        answer = value;
                        // A completed answer still (G2) has no clock of its own. Show the settled gold end.
                        if (value >= 1f && answerTime < 0f) answerTime = 2.2f;
                        break;
                    case "shapeSeed": shapeSeed = (int)value; break;
                    case "vitality": vitality = value; break;
                    case "time": time = value; break;
                    default:
                        throw new FormatException("JarView has no state field '" + pair.Key + "'");
                }
            }
            HookCamera();
            Apply();
        }

        public void Apply()
        {
            ApplyFrondVariants();
            bool answering = answerTime >= 0f;
            if (answering) ApplyAnswerMotion();
            else ApplyRestMotion();

            float pulse = !answering && answer > 0f ? Mathf.Exp(-Mathf.Pow((answer - 0.75f) / 0.2f, 2f)) : 0f;
            float ripple = 0f;
            float gold = 0f;
            if (answering)
            {
                // Moss ripple runs 1.5 s starting at 0.6 s. The ring closes gold over 0.6 s from the same moment and stays.
                if (answerTime >= 0.6f && answerTime <= 2.1f)
                    ripple = Mathf.Sin(Mathf.Clamp01((answerTime - 0.6f) / 1.5f) * Mathf.PI);
                gold = Mathf.Clamp01((answerTime - 0.6f) / 0.6f);
            }
            float life = Mathf.Clamp(vitality, 0.6f, 1f);
            if (ringMat != null)
            {
                ringMat.SetFloat("_Fill", answering || answer > 0f ? 1f : breath);
                ringMat.SetColor("_Color", answering
                    ? Color.Lerp(RingMint, RingGold, gold)
                    : Color.Lerp(RingMint, RingAnswer, pulse));
            }
            if (glassMat != null)
            {
                glassMat.SetFloat("_Fog", fog);
                glassMat.SetShaderPassEnabled("SRPDefaultUnlit", false);
            }
            float mossGlow = answering ? ripple : pulse;
            if (mossMat != null)
                mossMat.SetColor("_Emission", new Color(0.035f, 0.11f, 0.055f) * (1f + (answering ? 1.6f : 1.1f) * mossGlow));
            if (mossCardMat != null)
                mossCardMat.SetColor("_Emission", new Color(0.05f, 0.16f, 0.08f) * (1f + (answering ? 1.4f : 1.0f) * mossGlow));
            if (coilHaloMat != null)
                coilHaloMat.SetColor("_Color", new Color(0.30f, 0.85f, 0.45f) * (answer > 0f ? 0.45f + 0.5f * pulse : 0.55f + 0.25f * Mathf.Sin(breath * Mathf.PI)));
            if (jarHaloMat != null)
                jarHaloMat.SetColor("_Color", new Color(0.05f, 0.22f, 0.15f) * (1f + 0.6f * pulse + 0.25f * Mathf.Sin(breath * Mathf.PI)));
            if (fernMat != null) fernMat.SetColor("_Emission", FernEmission * 0.75f * life);
            if (newFrondMat != null)
            {
                float frondGlow = answering ? 0.35f + 0.45f * ripple : pulse;
                newFrondMat.SetColor("_Emission", FernEmission * (0.9f + 0.6f * frondGlow) * life);
            }
            if (fiddleMat != null) fiddleMat.SetColor("_Emission", FiddleEmission * life);

            if (mist != null)
            {
                for (int i = 0; i < mist.Length; i++)
                {
                    if (mist[i] == null) continue;
                    float ph = Mathf.Repeat(time * 0.08f + i / (float)mist.Length, 1f);
                    mist[i].localPosition = new Vector3(Mathf.Sin(i * 2.1f + time * 0.3f) * 0.006f, 0.165f + ph * 0.10f, 0f);
                    mist[i].localScale = new Vector3(0.022f + ph * 0.05f, 0.06f + ph * 0.07f, 1f);
                }
            }
            DriveSpores();
        }

        /// <summary>
        /// Builds the round-3 hierarchy under this object from an instantiated night_jar model.
        /// Caller owns the materials and the card mesh (saved assets). Idempotent: a second call replaces children.
        /// </summary>
        public void Build(GameObject model, JarLibrary library)
        {
            if (model == null) throw new InvalidOperationException("jar model is missing");
            if (library == null || library.Quad == null) throw new InvalidOperationException("jar library is missing a card quad");
            _quad = library.Quad;

            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyObject(transform.GetChild(i).gameObject);

            model.transform.SetParent(transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            glassMat = library.Glass;
            mossMat = library.Moss;
            fernMat = library.Fern;
            newFrondMat = library.FernNew;
            fiddleMat = library.Fiddle;
            ringMat = library.Ring;
            coilHaloMat = library.CoilHalo;
            jarHaloMat = library.JarHalo;

            Transform mt = model.transform;
            SetMat(Require(mt, "Jar"), library.Glass);
            SetMat(Require(mt, "Moss"), library.Moss);
            Transform skirt = Require(mt, "MossSkirt");
            SetMat(skirt, library.MossCard);
            mossCardMat = library.MossCard;
            SetMat(Require(mt, "Soil"), library.Soil);
            SetMat(Require(mt, "Cork"), library.Cork);

            Transform fid = Require(mt, "Fiddle0");
            SetMat(fid, library.Fiddle);
            fiddle = fid.GetComponent<MeshFilter>();
            fiddleStates = new[]
            {
                MeshOf(Require(mt, "Fiddle0")),
                MeshOf(Require(mt, "Fiddle25")),
                MeshOf(Require(mt, "Fiddle50")),
                MeshOf(Require(mt, "Fiddle75")),
                MeshOf(Require(mt, "Fiddle100"))
            };
            foreach (var name in new[] { "Fiddle25", "Fiddle50", "Fiddle75", "Fiddle100" })
                Require(mt, name).gameObject.SetActive(false);
            fid.localScale = Vector3.one * 0.9f;
            fid.localPosition += new Vector3(0.004f, 0.004f, 0f);

            Transform seed = Require(mt, "Seedling");
            SetMat(seed, library.Seedling);
            Transform seedStem = Require(mt, "SeedStem");
            SetMat(seedStem, library.Fiddle);
            seed.localPosition = new Vector3(-0.009f, 0.045f, -0.012f);
            seedStem.localPosition = seed.localPosition;
            seed.localScale = Vector3.one * 0.55f;
            seedStem.localScale = Vector3.one * 0.55f;
            Transform seed2 = Instantiate(seed.gameObject, mt).transform;
            seed2.name = "SeedlingB";
            seed2.localPosition = new Vector3(0.010f, 0.044f, -0.012f);
            seed2.localRotation = Quaternion.Euler(0f, 70f, 0f) * seed.localRotation;
            seed2.localScale = Vector3.one * 0.45f;

            Transform v0 = Require(mt, "FrondV0");
            Transform v1 = Require(mt, "FrondV1");
            Transform v2 = Require(mt, "FrondV2");
            _frondVariants = new[] { MeshOf(v0), MeshOf(v1), MeshOf(v2) };
            SetMat(v0, library.Fern);
            Transform frond = Instantiate(v0.gameObject, mt).transform;
            frond.name = "Frond";
            v0.gameObject.SetActive(false);
            v1.gameObject.SetActive(false);
            v2.gameObject.SetActive(false);
            SetMat(frond, library.Fern);
            Quaternion axis = frond.localRotation;
            frond.localPosition = new Vector3(-0.010f, 0.040f, 0.004f);
            frond.localRotation = Quaternion.Euler(-8f, 0f, 29f) * axis;
            frond.localScale = Vector3.one * 0.74f;
            Transform frondR = Instantiate(frond.gameObject, mt).transform;
            frondR.name = "FrondRight";
            frondR.localPosition = new Vector3(0.011f, 0.040f, 0.006f);
            frondR.localRotation = Quaternion.Euler(-8f, 0f, -31f) * axis;
            frondR.localScale = new Vector3(-0.72f, 0.72f, 0.72f);
            Transform fnew = Instantiate(frond.gameObject, mt).transform;
            fnew.name = "FrondNew";
            SetMat(fnew, library.FernNew);
            _frondSlots = new[] { frond, frondR, fnew };
            _shownSeed = int.MinValue;
            ApplyFrondVariants();
            fnew.localPosition = new Vector3(0.002f, 0.042f, -0.004f);
            fnew.localRotation = Quaternion.Euler(-12f, 0f, 6f) * axis;
            fnew.localScale = Vector3.one * 0.95f;
            newFrond = fnew.GetComponent<Renderer>();

            var dewGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyObject(dewGo.GetComponent<Collider>());
            dewGo.name = "Dew";
            dewGo.transform.SetParent(transform, false);
            dewGo.transform.localScale = Vector3.one * 0.0032f;
            Mesh frondMesh = fnew.GetComponent<MeshFilter>().sharedMesh;
            if (frondMesh == null || !frondMesh.isReadable) throw new InvalidOperationException("frond mesh is not readable");
            Vector2[] fuv = frondMesh.uv;
            Vector3[] fv = frondMesh.vertices;
            int tip = 0;
            for (int i = 0; i < fv.Length; i++)
            {
                bool higher = fuv[i].y > fuv[tip].y;
                bool sameAndCloser = Mathf.Approximately(fuv[i].y, fuv[tip].y) && Mathf.Abs(fuv[i].x - 0.5f) < Mathf.Abs(fuv[tip].x - 0.5f);
                if (higher || sameAndCloser) tip = i;
            }
            dewGo.transform.position = fnew.TransformPoint(fv[tip]) + new Vector3(0f, -0.002f, -0.001f);
            SetMat(dewGo.transform, library.Dew);
            dew = dewGo.GetComponent<Renderer>();

            var flat = Quaternion.Euler(90f, 0f, 0f);
            var bills = new List<Transform>();
            Card("BreathRing", library.Ring, new Vector3(0f, 0.0006f, 0f), new Vector2(0.152f, 0.152f), flat);
            Card("DeskSpill", library.Spill, new Vector3(0f, 0.0004f, 0.01f), new Vector2(LeanSpillWidth, LeanSpillHeight), flat);
            bills.Add(Card("JarHalo", library.JarHalo, new Vector3(0f, 0.07f, 0f), new Vector2(LeanHaloWidth, LeanHaloHeight), Quaternion.identity).transform);
            bills.Add(Card("CoilHalo", library.CoilHalo, new Vector3(-0.012f, 0.086f, -0.01f), new Vector2(0.05f, 0.05f), Quaternion.identity).transform);

            var mistList = new List<Transform>();
            for (int i = 0; i < 5; i++)
            {
                var c = Card("Mist" + i, library.Mist, new Vector3(0f, 0.16f + i * 0.03f, 0f), new Vector2(0.05f, 0.09f), Quaternion.identity);
                mistList.Add(c.transform);
                bills.Add(c.transform);
            }
            mist = mistList.ToArray();
            billboards = bills.ToArray();
            spores = BuildSpores(library.Spore);

            AddCollider(Require(mt, "Jar").gameObject);
            AddCollider(Require(mt, "Cork").gameObject);
            EnsureTarget(gameObject, "jar", Require(mt, "Jar").GetComponent<Collider>());
            EnsureTarget(Require(mt, "Cork").gameObject, "jar.cork", Require(mt, "Cork").GetComponent<Collider>());

            _live = null;
            _shapes = null;
        }

        public void Face(Camera cam)
        {
            if (cam == null || billboards == null) return;
            for (int i = 0; i < billboards.Length; i++)
            {
                Transform b = billboards[i];
                if (b == null) continue;
                Vector3 away = b.position - cam.transform.position;
                if (away.sqrMagnitude < 1e-8f) continue;
                b.rotation = Quaternion.LookRotation(away, cam.transform.up);
            }
        }

        void OnEnable()
        {
            HookCamera();
        }

        void OnDisable()
        {
            if (!_hooked) return;
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            _hooked = false;
        }

        void HookCamera()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            _hooked = true;
        }

        void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (cam == null || cam.cameraType != CameraType.Game) return;
            Face(cam);
        }

        void BlendFiddle()
        {
            if (fiddle == null || fiddleStates == null || fiddleStates.Length < 2) return;
            Mesh first = fiddleStates[0];
            if (first == null || !first.isReadable) return;
            int n = first.vertexCount;
            for (int s = 1; s < fiddleStates.Length; s++)
            {
                if (fiddleStates[s] == null || fiddleStates[s].vertexCount != n || !fiddleStates[s].isReadable)
                {
                    fiddle.sharedMesh = first;
                    return;
                }
            }
            if (_live == null || _shapes == null || _shapes.Length != fiddleStates.Length || _scratch == null || _scratch.Length != n)
            {
                _live = Instantiate(first);
                _live.name = "FiddleLive";
                _shapes = new Vector3[fiddleStates.Length][];
                for (int s = 0; s < fiddleStates.Length; s++) _shapes[s] = fiddleStates[s].vertices;
                _scratch = new Vector3[n];
                fiddle.sharedMesh = _live;
            }
            float u = Mathf.Clamp01(uncoil) * (fiddleStates.Length - 1);
            int i0 = Mathf.Min((int)u, fiddleStates.Length - 2);
            float k = u - i0;
            Vector3[] a = _shapes[i0];
            Vector3[] b = _shapes[i0 + 1];
            for (int i = 0; i < n; i++) _scratch[i] = Vector3.LerpUnclamped(a[i], b[i], k);
            _live.vertices = _scratch;
            _live.RecalculateNormals();
            _live.RecalculateBounds();
        }

        void ApplyFrondVariants()
        {
            if (_frondSlots == null || _frondSlots.Length == 0) return;
            if (_frondVariants == null || _frondVariants.Length != 3 || _frondVariants[0] == null)
            {
                Transform a = FindMesh(transform, "FrondV0");
                Transform b = FindMesh(transform, "FrondV1");
                Transform c = FindMesh(transform, "FrondV2");
                if (a == null || b == null || c == null) return;
                _frondVariants = new[] { MeshOf(a), MeshOf(b), MeshOf(c) };
            }
            if (_shownSeed == shapeSeed) return;
            _shownSeed = shapeSeed;
            for (int i = 0; i < _frondSlots.Length; i++)
            {
                Transform slot = _frondSlots[i];
                if (slot == null) continue;
                int mod = (shapeSeed + i) % _frondVariants.Length;
                if (mod < 0) mod += _frondVariants.Length;
                var filter = slot.GetComponent<MeshFilter>();
                if (filter != null) filter.sharedMesh = _frondVariants[mod];
            }
            _dewPathReady = false;
        }

        static Transform FindMesh(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name && all[i].GetComponent<MeshFilter>() != null) return all[i];
            }
            return null;
        }

        void ApplyRestMotion()
        {
            BlendFiddle();
            if (fiddle != null)
            {
                var fiddleRenderer = fiddle.GetComponent<Renderer>();
                if (fiddleRenderer != null) fiddleRenderer.enabled = answer < 0.5f;
            }
            if (newFrond != null) newFrond.enabled = answer >= 0.5f;
            if (dew != null) dew.enabled = answer >= 0.5f;
        }

        /// <summary>
        /// Bible answer: frond settles over 0.8 s, dew rolls tip to base for 1.2 s from 0.4 s.
        /// </summary>
        void ApplyAnswerMotion()
        {
            BlendFiddle();
            if (fiddle != null)
            {
                var fiddleRenderer = fiddle.GetComponent<Renderer>();
                if (fiddleRenderer != null) fiddleRenderer.enabled = false;
            }
            if (newFrond != null)
            {
                newFrond.enabled = true;
                if (!_frondScaleCached)
                {
                    _frondBaseScale = newFrond.transform.localScale;
                    _frondScaleCached = true;
                }
                float settle = Mathf.Clamp01(answerTime / 0.8f);
                float eased = 1f - (1f - settle) * (1f - settle);
                newFrond.transform.localScale = _frondBaseScale * Mathf.Lerp(0.92f, 1f, eased);
            }
            if (dew != null)
            {
                EnsureDewPath();
                bool rolling = answerTime >= 0.4f;
                dew.enabled = rolling;
                if (rolling && _dewPathReady && newFrond != null)
                {
                    float roll = Mathf.Clamp01((answerTime - 0.4f) / 1.2f);
                    float eased = 1f - (1f - roll) * (1f - roll);
                    Vector3 tip = newFrond.transform.TransformPoint(_dewTipLocal);
                    Vector3 root = newFrond.transform.TransformPoint(_dewBaseLocal);
                    dew.transform.position = Vector3.Lerp(tip, root, eased);
                }
            }
        }

        void EnsureDewPath()
        {
            if (_dewPathReady || newFrond == null) return;
            var filter = newFrond.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || !mesh.isReadable || mesh.vertexCount == 0 || mesh.uv == null || mesh.uv.Length != mesh.vertexCount)
                return;
            Vector3[] vertices = mesh.vertices;
            Vector2[] uv = mesh.uv;
            int tip = 0;
            int root = 0;
            for (int i = 1; i < vertices.Length; i++)
            {
                if (uv[i].y > uv[tip].y) tip = i;
                if (uv[i].y < uv[root].y) root = i;
            }
            _dewTipLocal = vertices[tip];
            _dewBaseLocal = vertices[root];
            _dewPathReady = true;
        }

        void DriveSpores()
        {
            if (spores == null) return;
            if (sporeStep >= 0f)
            {
                if (!_sporesLive)
                {
                    spores.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    spores.useAutoRandomSeed = false;
                    spores.randomSeed = 5;
                    spores.Simulate(12f, true, true, true);
                    _sporesLive = true;
                }
                if (sporeStep > 0f) spores.Simulate(sporeStep, true, false, false);
                return;
            }
            _sporesLive = false;
            spores.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            spores.useAutoRandomSeed = false;
            spores.randomSeed = 5;
            // Round-3 Settle warmed the system for 12 s so the cloud is full. time then carries it forward,
            // which is what moves the spores between a mid-breath still and a later one.
            float seconds = 12f + Mathf.Max(0f, time);
            spores.Simulate(seconds, true, true, true);
        }

        ParticleSystem BuildSpores(Material material)
        {
            var go = new GameObject("Spores");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 10f;
            main.startLifetime = 9f;
            main.startSpeed = 0.004f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.0012f, 0.0026f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1.0f, 0.78f, 0.30f), new Color(0.85f, 1.0f, 0.45f));
            main.maxParticles = 90;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.useUnscaledTime = false;
            var emission = ps.emission;
            emission.rateOverTime = 9f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.11f;
            shape.radiusThickness = 0.35f;
            shape.position = new Vector3(0f, 0.08f, 0f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.004f;
            noise.frequency = 0.6f;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(0.002f, 0.006f);
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.2f),
                    new GradientAlphaKey(1f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = gradient;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ps.useAutoRandomSeed = false;
            ps.randomSeed = 5;
            return ps;
        }

        GameObject Card(string name, Material material, Vector3 pos, Vector2 size, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.transform.localRotation = rotation;
            return go;
        }

        Mesh _quad;

        static void SetMat(Transform t, Material material)
        {
            var renderer = t.GetComponent<Renderer>();
            if (renderer == null) throw new InvalidOperationException(t.name + " has no renderer");
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static Mesh MeshOf(Transform t)
        {
            var filter = t.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) throw new InvalidOperationException(t.name + " has no mesh");
            if (!filter.sharedMesh.isReadable) throw new InvalidOperationException(t.name + " mesh is not readable");
            return filter.sharedMesh;
        }

        static Transform Require(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name && all[i].GetComponent<MeshFilter>() != null) return all[i];
            }
            throw new InvalidOperationException("night jar is missing mesh " + name);
        }

        static void AddCollider(GameObject go)
        {
            var filter = go.GetComponent<MeshFilter>();
            var collider = go.GetComponent<MeshCollider>();
            if (collider == null) collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = false;
        }

        static void EnsureTarget(GameObject go, string id, Collider collider)
        {
            var target = go.GetComponent<IntentTarget>();
            if (target == null) target = go.AddComponent<IntentTarget>();
            target.Id = id;
            // hitCollider is serialized. The property falls back to a collider on this object.
            // The jar id lives on the root, so the glass collider is on a child and must be assigned in the editor.
            if (collider != null && collider.gameObject == go) return;
        }

        static float Parse(string key, string text)
        {
            float value;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                throw new FormatException("JarView state " + key + " is not a number: " + text);
            return value;
        }

        static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
