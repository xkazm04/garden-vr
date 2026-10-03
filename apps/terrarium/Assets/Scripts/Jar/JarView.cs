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
        public Material Glass, Moss, MossCard, Soil, Cork, Fern, FernNew, Fiddle, Seedling, Dew, Flower;
        public Material Ring, Spill, JarHalo, CoilHalo, Mist, Spore;
        public Mesh Quad;
        public Mesh FlowerMesh;
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
        // T-TER-006 lean was 0.21 x 0.15 and 0.15 x 0.17. Those quads blanketed the jar.
        // The desk pool is wider than T-TER-007's pinprick and still sits under the glass,
        // so the fill it adds is the base of the jar, not the whole body.
        // Wide enough to wash the desk under the hands. The hands live in the plate; this halo is what lights them.
        public const float LeanSpillWidth = 0.34f;
        public const float LeanSpillHeight = 0.080f;
        /// <summary>Frond roots on the low moss carpet. The soil bed tops out near 0.024 m.</summary>
        public const float MossBedY = 0.031f;
        public const float LeanHaloWidth = 0.050f;
        public const float LeanHaloHeight = 0.036f;
        // The crozier card sits on the jar halo and the glass. 3.8 cm stacked a third layer
        // across the G1 jar (mean 1.528). 2.5 cm stays just outside the 1.6 cm coil.
        public const float CoilHaloSize = 0.025f;
        // One plume card. Wide at the cork, tucked into it, thinning as it rises.
        public const float PlumeWidth = 0.100f;
        public const float PlumeHeight = 0.132f;
        public const float PlumeCenterY = 0.188f;

        // Resting glow, under the style-bible cap #E8FFF4. Green stays the strongest channel,
        // and it stays under 1 so a highlight can be mint without clipping to neon.
        // The crozier tip and the dew are the bright points; the moss body is only a lift.
        static readonly Color FernEmission = new Color(0.16f, 0.54f, 0.30f);
        static readonly Color FernRim = new Color(0.32f, 0.50f, 0.42f);
        static readonly Color FiddleEmission = new Color(0.46f, 0.95f, 0.55f);
        static readonly Color FiddleRim = new Color(0.48f, 0.70f, 0.52f);
        static readonly Color MossEmission = new Color(0.22f, 0.62f, 0.18f);
        static readonly Color MossRim = new Color(0.20f, 0.48f, 0.18f);
        static readonly Color MossCardEmission = new Color(0.12f, 0.42f, 0.11f);
        static readonly Color MossCardRim = new Color(0.18f, 0.44f, 0.16f);
        static readonly Color FocusMint = new Color(0.32f, 0.66f, 0.40f, 1f);
        static readonly Color FocusWarm = new Color(0.55f, 0.32f, 0.12f, 1f);
        const float FocusRadius = 0.080f;
        static readonly Color GlassRim = new Color(0.48f, 0.72f, 0.78f, 0.32f);
        static readonly Color GlassInner = new Color(0.34f, 0.86f, 0.48f, 1f);
        const float GlassRimPower = 2.40f;
        static readonly Color DewEmission = new Color(0.70f, 0.98f, 0.84f);
        static readonly Color CoilGlow = new Color(0.42f, 0.78f, 0.50f);
        static readonly Color JarGlow = new Color(0.16f, 0.40f, 0.24f);

        [Header("State")]
        [Range(0f, 1f)] public float breath = 0.5f;
        [Range(0f, 1f)] public float uncoil = 0.3f;
        [Range(0f, 1f)] public float answer;
        [Range(0f, 1f)] public float fog = 0.45f;
        [Range(0.6f, 1f)] public float vitality = 1f;
        public float time = 3f;
        /// <summary>Picks which authored frond atlas column each placed frond uses.</summary>
        public int shapeSeed = 4;
        /// <summary>How many first-flower blooms to show. Positions come from <see cref="FlowerPosition"/>.</summary>
        public int flowers;
        /// <summary>Garden day. Seven shows the week: seven fronds, the newest brightest, one quieter, the first flower.</summary>
        public int day;
        /// <summary>Lifetime fronds. Season light and details come only from <see cref="Season"/>. 0 to 6 is week 1, the locked mint.</summary>
        public int lifetimeFronds;
        /// <summary>Half the spores, and no curl. The style bible's reduced-motion row.</summary>
        public bool reducedMotion;
        public BreathPhase phase = BreathPhase.Waiting;
        /// <summary>Seconds since the answer began. Negative means the answer is not playing.</summary>
        public float answerTime = -1f;
        /// <summary>Seconds since a recovery ripple began. Negative means it is not playing. It runs 2.5 s.</summary>
        public float recoveredTime = -1f;
        /// <summary>Spore step this frame. Negative keeps the capture resim from <see cref="time"/>.</summary>
        public float sporeStep = -1f;
        float _corkPuff;

        [Header("Wired by Build")]
        public MeshFilter fiddle;
        public Mesh[] fiddleStates;
        public Renderer newFrond;
        public Renderer dew;
        public Material ringMat;
        public Material glassMat;
        public Material mossMat;
        public Material mossCardMat;
        public Material soilMat;
        public Material seedlingMat;
        public Material coilHaloMat;
        public Material jarHaloMat;
        public Material newFrondMat;
        public Material fernMat;
        public Material fiddleMat;
        public Material mistMat;
        public Material sporeMat;
        public Mesh flowerMesh;
        public Material flowerMat;
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
        int _builtFlowers = -1;
        readonly List<GameObject> _flowerRoots = new List<GameObject>();
        Transform[] _baseBills;
        Material _flowerHalo;
        Material _quietFern;
        Transform[] _weekFronds;
        Transform _spillCard;
        Transform _haloCard;
        bool _g1PosesReady;
        Vector3[] _g1Position;
        Quaternion[] _g1Rotation;
        Vector3[] _g1Scale;
        int _weekApplied = -1;
        bool _hasLook;
        GardenLook _look;
        readonly List<Transform> _recordFronds = new List<Transform>();
        readonly List<Material> _recordMats = new List<Material>();
        int _lookLit = -1;
        bool _lookHeld;
        readonly List<Transform> _dewBeads = new List<Transform>();
        Transform _ripple;
        Material _rippleMat;
        readonly CompanionGarden _companions = new CompanionGarden();
        readonly SeasonDetails _season = new SeasonDetails();
        bool _liveCompanions;
        List<CompanionGarden.Shot> _liveShots = new List<CompanionGarden.Shot>();
        List<CompanionGarden.Shot> _captureShots = new List<CompanionGarden.Shot>();

        public int RecordFrondCount { get; private set; }
        public float ShownLean { get; private set; }
        public int ShownDew { get; private set; }
        public bool FiddleQuiet { get; private set; }
        public bool FiddleWaiting { get; private set; }
        /// <summary>Fronds lit in the look-back, birth order. Negative means the replay is off.</summary>
        public int LookLit { get { return _lookLit; } }
        public bool LookHeld { get { return _lookHeld; } }

        struct GardenLook
        {
            public int[] Days;
            public float Vitality;
            public int Flowers;
            public int Dew;
            public bool Gap;
            public bool Waiting;
            public bool AnimatingNew;
            public bool Recovered;
        }
        // Dew stays on the tip until the frond has settled, then rolls for 1.2 s.
        const float DewStart = 0.72f;
        const float DewRollSeconds = 1.2f;
        const int MistGrid = 8;
        const float MistLoopSeconds = 8f;
        // #F2D27A, the style-bible spore gold. The card multiplies it, so the material stays this hue.
        static readonly Color SporeGold = new Color(0.9490196f, 0.8235294f, 0.4784314f, 1f);

        static readonly Color RingMint = new Color(0.46f, 0.82f, 0.54f);
        static readonly Color RingAnswer = new Color(0.50f, 0.88f, 0.58f);
        static readonly Color RingGold = new Color(0.92f, 0.70f, 0.32f);
        static readonly Color RingPool = new Color(0.12f, 0.32f, 0.18f, 1f);
        static readonly Color SpillMint = new Color(0.20f, 0.48f, 0.28f, 1f);

        // Week 6 endpoints. Week 1 returns the locked colours above, unchanged.
        static readonly Color MossEmissionWarm = new Color(0.36f, 0.50f, 0.12f);
        static readonly Color MossRimWarm = new Color(0.40f, 0.46f, 0.14f);
        static readonly Color MossCardEmissionWarm = new Color(0.30f, 0.44f, 0.12f);
        static readonly Color MossCardRimWarm = new Color(0.36f, 0.42f, 0.14f);
        static readonly Color FernEmissionWarm = new Color(0.36f, 0.50f, 0.18f);
        static readonly Color FernRimWarm = new Color(0.48f, 0.46f, 0.24f);
        static readonly Color FiddleEmissionWarm = new Color(0.58f, 0.82f, 0.34f);
        static readonly Color FiddleRimWarm = new Color(0.62f, 0.58f, 0.28f);
        static readonly Color FocusGold = new Color(0.46f, 0.40f, 0.16f, 1f);
        static readonly Color GlassRimWarm = new Color(0.62f, 0.64f, 0.48f, 0.32f);
        static readonly Color GlassInnerWarm = new Color(0.52f, 0.64f, 0.22f, 1f);
        // Edge absorption, not a fill. Alpha is how far the silhouette darkens.
        static readonly Color GlassVolumeCool = new Color(0.42f, 0.62f, 0.66f, 0.75f);
        static readonly Color GlassVolumeWarm = new Color(0.55f, 0.58f, 0.42f, 0.70f);
        static readonly Color GlassStreakCool = new Color(0.50f, 0.66f, 0.74f, 0.12f);
        static readonly Color GlassStreakWarm = new Color(0.64f, 0.62f, 0.42f, 0.12f);
        static readonly Color DewEmissionWarm = new Color(0.86f, 0.88f, 0.52f);
        static readonly Color CoilGlowWarm = new Color(0.64f, 0.66f, 0.28f);
        static readonly Color JarGlowWarm = new Color(0.36f, 0.32f, 0.12f);
        static readonly Color SpillWarm = new Color(0.42f, 0.36f, 0.14f, 1f);

        public void ApplyCaptureState(IReadOnlyDictionary<string, string> state)
        {
            if (state == null) return;
            bool sawAnswerTime = state.ContainsKey("answerTime");
            bool sawJourney = false;
            int capCompanions = 0;
            int capLeaves = 0;
            bool sawHabits = false;
            string etch = null;
            bool sawLifetime = false;
            int lifetimeOverride = 0;
            foreach (var pair in state)
            {
                if (pair.Key == "journey")
                {
                    ApplyJourney(pair.Value);
                    sawJourney = true;
                    continue;
                }
                if (pair.Key == "etch")
                {
                    etch = pair.Value;
                    continue;
                }
                float value = Parse(pair.Key, pair.Value);
                switch (pair.Key)
                {
                    case "breath": breath = value; break;
                    case "uncoil": uncoil = value; break;
                    case "fog": fog = value; break;
                    case "answer":
                        answer = value;
                        // A completed answer still (G2) has no clock of its own. Show the settled gold end.
                        if (value >= 1f && answerTime < 0f && !sawAnswerTime) answerTime = 2.2f;
                        break;
                    case "answerTime": answerTime = value; break;
                    case "shapeSeed": shapeSeed = (int)value; break;
                    case "flowers": flowers = Mathf.Max(0, (int)value); break;
                    case "day": day = Mathf.Max(0, (int)value); break;
                    case "lifetime":
                        lifetimeOverride = Mathf.Max(0, (int)value);
                        sawLifetime = true;
                        break;
                    case "reducedMotion": reducedMotion = value > 0.5f; break;
                    case "vitality": vitality = value; break;
                    case "time": time = value; break;
                    case "companions":
                        capCompanions = Mathf.Clamp((int)value, 0, Companions.MaxHabits);
                        sawHabits = true;
                        break;
                    case "leaves":
                        capLeaves = Mathf.Max(0, (int)value);
                        sawHabits = true;
                        break;
                    case "looklit":
                        break;
                    case "words":
                        break;
                    default:
                        throw new FormatException("JarView has no state field '" + pair.Key + "'");
                }
            }
            if (!sawJourney) _hasLook = false;
            if (sawLifetime) lifetimeFronds = lifetimeOverride;
            int lookLit = -1;
            bool sawLook = false;
            if (state.ContainsKey("looklit"))
            {
                float parsed;
                if (!float.TryParse(state["looklit"], NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                    throw new FormatException("JarView state looklit is not a number: " + state["looklit"]);
                lookLit = Mathf.Max(0, (int)parsed);
                sawLook = true;
            }
            if (sawLook) PresentLookBack(lookLit, true);
            else ClearLookBack();
            _liveCompanions = false;
            _captureShots = sawHabits
                ? CompanionGarden.CaptureShots(capCompanions, capLeaves)
                : new List<CompanionGarden.Shot>();
            HookCamera();
            Apply();
            if (!string.IsNullOrEmpty(etch))
                EtchedLettering.Present(transform, etch);
            int wordPose = 0;
            if (state.ContainsKey("words"))
            {
                float parsedWords;
                if (!float.TryParse(state["words"], NumberStyles.Float, CultureInfo.InvariantCulture, out parsedWords))
                    throw new FormatException("JarView state words is not a number: " + state["words"]);
                wordPose = Mathf.Max(0, (int)parsedWords);
            }
            OneWordRitual.ClearCapture(transform);
            if (wordPose > 0)
                OneWordRitual.PresentCapture(transform, this, wordPose);
        }

        /// <summary>One cork mist swell. Reduced motion leaves the plume still.</summary>
        public void PuffCork(float seconds)
        {
            if (reducedMotion || seconds <= 0f) return;
            if (seconds > _corkPuff) _corkPuff = seconds;
        }

        /// <summary>The live companions. Capture state replaces this until the next present.</summary>
        public void PresentCompanions(IReadOnlyList<CompanionGarden.Shot> shots)
        {
            _liveCompanions = true;
            _liveShots = shots != null ? new List<CompanionGarden.Shot>(shots) : new List<CompanionGarden.Shot>();
        }

        /// <summary>Pose the jar from the core garden and an injected clock. The art week pose stays on <c>day</c>.</summary>
        void ApplyJourney(string name)
        {
            GardenJourney journey = GardenJourney.Build(name);
            day = 0;
            breath = 0.12f;
            uncoil = 0f;
            fog = name == "missed2" ? 0.04f : 0.1f;
            answer = 0f;
            answerTime = -1f;
            time = 4f;
            recoveredTime = journey.Recovered ? 1.15f : -1f;
            PresentGarden(journey.Garden, journey.Today, false, journey.Recovered);
        }

        /// <summary>The live garden. Frond slots, lean, dew, and the waiting fiddle all come from this record.</summary>
        public void PresentGarden(Garden garden, int today, bool animatingNewFrond, bool recoveredRipple)
        {
            if (garden == null) throw new ArgumentNullException(nameof(garden));
            int count = garden.FrondDays.Count;
            var days = new int[count];
            for (int i = 0; i < count; i++) days[i] = garden.FrondDays[i];
            float life = garden.Vitality(today);
            _look = new GardenLook
            {
                Days = days,
                Vitality = life,
                Flowers = garden.Flowers,
                Dew = garden.DewToday,
                Gap = garden.DaysSinceRitual(today) > 1,
                Waiting = !garden.LastRitualDay.HasValue || garden.LastRitualDay.Value != today,
                AnimatingNew = animatingNewFrond,
                Recovered = recoveredRipple
            };
            _hasLook = true;
            vitality = life;
            flowers = garden.Flowers;
            lifetimeFronds = count;
            RecordFrondCount = count;
            ShownLean = LeanDegrees(life);
            ShownDew = garden.DewToday;
            FiddleQuiet = _look.Gap;
            FiddleWaiting = _look.Waiting;
        }

        /// <summary>Light the first <paramref name="lit"/> fronds. A held frame keeps that count.</summary>
        public void PresentLookBack(int lit, bool held)
        {
            _lookLit = lit < 0 ? -1 : lit;
            _lookHeld = _lookLit >= 0 && held;
        }

        public void ClearLookBack()
        {
            _lookLit = -1;
            _lookHeld = false;
        }

        /// <summary>Green channel of one record frond. 0 when that frond is not built.</summary>
        public float FrondGlow(int index)
        {
            if (index < 0 || index >= _recordMats.Count || _recordMats[index] == null) return 0f;
            return _recordMats[index].GetColor("_Emission").g;
        }

        public float FiddleGlow
        {
            get { return fiddleMat != null ? fiddleMat.GetColor("_Emission").g : 0f; }
        }

        /// <summary>0 at full vitality, 6 degrees at the floor. Never past 6.</summary>
        public static float LeanDegrees(float vitality)
        {
            float t = (1f - Mathf.Clamp(vitality, Garden.VitalityFloor, 1f)) / (1f - Garden.VitalityFloor);
            return Mathf.Clamp(t * 6f, 0f, 6f);
        }

        /// <summary>Index in <see cref="Garden.FrondDays"/>. The inner ring starts at 12.</summary>
        public static Vector3 FrondSlot(int index)
        {
            bool inner = index >= Garden.FrondsBeforeInnerLayer;
            int n = inner ? index - Garden.FrondsBeforeInnerLayer : index;
            float yaw = (n * 137.50776f + (inner ? 11f : 0f)) * Mathf.Deg2Rad;
            float radius = inner ? 0.0125f : 0.027f;
            return new Vector3(Mathf.Cos(yaw) * radius, MossBedY, Mathf.Sin(yaw) * radius);
        }

        public static float FrondSpread(int index)
        {
            return Mathf.Sin(index * 2.399963f) * 26f;
        }

        /// <summary>Week 1 returns <paramref name="cool"/> exactly. Later weeks move toward <paramref name="warm"/>.</summary>
        public static Color SeasonColor(Color cool, Color warm, float warmth)
        {
            if (warmth <= 0f) return cool;
            if (warmth >= 1f) return warm;
            return Color.Lerp(cool, warm, warmth);
        }

        public void Apply()
        {
            ResolveFrondSlots();
            ApplyFrondVariants();
            CacheG1Poses();
            if (day >= 7 && flowers < 1) flowers = 1;
            bool answering = answerTime >= 0f;
            if (answering) ApplyAnswerMotion();
            else ApplyRestMotion();

            float pulse = !answering && answer > 0f ? Mathf.Exp(-Mathf.Pow((answer - 0.75f) / 0.2f, 2f)) : 0f;
            float ripple = 0f;
            float gold = 0f;
            if (answering)
            {
                // Ripple starts with the bead still on the tip, and runs 1.5 s. The ring warms to gold over 0.6 s.
                if (answerTime >= 0.35f && answerTime <= 1.85f)
                    ripple = Mathf.Sin(Mathf.Clamp01((answerTime - 0.35f) / 1.5f) * Mathf.PI);
                gold = Mathf.Clamp01((answerTime - 0.45f) / 0.6f);
            }
            float life = Mathf.Clamp(vitality, 0.6f, 1f);
            float seasonWarmth = Season.Warmth(Mathf.Max(0, lifetimeFronds));
            if (ringMat != null)
            {
                ringMat.SetFloat("_Fill", answering || answer > 0f ? 1f : breath);
                ringMat.SetColor("_Color", answering
                    ? Color.Lerp(RingMint, RingGold, gold)
                    : Color.Lerp(RingMint, RingAnswer, pulse));
                ringMat.SetColor("_Color2", RingPool);
            }
            if (glassMat != null)
            {
                glassMat.SetFloat("_Fog", fog);
                glassMat.SetFloat("_Drops", 1.15f);
                glassMat.SetFloat("_Refract", 36f);
                // Clear pane. The tint is a hair of glass, not a wash. Apply owns these.
                glassMat.SetColor("_Tint", new Color(0.75f, 0.94f, 0.84f, 0.004f));
                glassMat.SetColor("_Rim", SeasonColor(GlassRim, GlassRimWarm, seasonWarmth));
                glassMat.SetFloat("_RimPower", GlassRimPower);
                Color inner = SeasonColor(GlassInner, GlassInnerWarm, seasonWarmth);
                glassMat.SetColor("_Inner", inner);
                // Full through the moss and the crozier, half-gone near the upper pane, out by the lip.
                glassMat.SetVector("_InnerY", new Vector4(0.078f, 0.052f, 0f, 0f));
                glassMat.SetColor("_Volume", SeasonColor(GlassVolumeCool, GlassVolumeWarm, seasonWarmth));
                glassMat.SetVector("_VolumeY", new Vector4(0.038f, 0.072f, 0f, 0f));
                // A near-white streak at 0.46 was a hard column in the pane. Keep the stroke, under the cap.
                glassMat.SetColor("_Streak", SeasonColor(GlassStreakCool, GlassStreakWarm, seasonWarmth));
                glassMat.SetShaderPassEnabled("SRPDefaultUnlit", false);
            }
            float recoveredWave = 0f;
            if (_hasLook && _look.Recovered && recoveredTime >= 0f && recoveredTime <= 2.5f)
                recoveredWave = Mathf.Sin(Mathf.Clamp01(recoveredTime / 2.5f) * Mathf.PI);
            float mossGlow = Mathf.Max(answering ? ripple : pulse, recoveredWave * 1.65f);
            if (mossMat != null)
            {
                mossMat.SetColor("_Emission", SeasonColor(MossEmission, MossEmissionWarm, seasonWarmth) * (1f + 0.45f * mossGlow));
                mossMat.SetColor("_Rim", SeasonColor(MossRim, MossRimWarm, seasonWarmth) * (0.85f + 0.35f * mossGlow));
                // One carpet photo. A repeating tile put a cross of cells through the mound.
                mossMat.SetFloat("_Tri", 0f);
                mossMat.SetFloat("_TopAmount", 0f);
                SetTip(mossMat, 0.018f, 0.048f, 0.55f, 1f);
            }
            if (mossCardMat != null)
            {
                mossCardMat.SetColor("_Emission", SeasonColor(MossCardEmission, MossCardEmissionWarm, seasonWarmth) * (1f + 0.40f * mossGlow));
                mossCardMat.SetColor("_Rim", SeasonColor(MossCardRim, MossCardRimWarm, seasonWarmth));
                SetTip(mossCardMat, 0.020f, 0.052f, 0.28f, 1f);
            }
            float coil = answer > 0f ? 0.70f + 0.30f * pulse : 0.90f + 0.20f * Mathf.Sin(breath * Mathf.PI);
            if (_hasLook && _look.Gap) coil = 0.35f;
            if (_lookLit >= 0) coil = 0.35f;
            if (coilHaloMat != null)
                coilHaloMat.SetColor("_Color", SeasonColor(CoilGlow, CoilGlowWarm, seasonWarmth) * coil);
            if (jarHaloMat != null)
                jarHaloMat.SetColor("_Color", SeasonColor(JarGlow, JarGlowWarm, seasonWarmth) * (1f + 0.25f * pulse));
            if (fernMat != null)
            {
                fernMat.SetColor("_Emission", SeasonColor(FernEmission, FernEmissionWarm, seasonWarmth) * life);
                fernMat.SetColor("_Rim", SeasonColor(FernRim, FernRimWarm, seasonWarmth));
                fernMat.SetFloat("_Edge", 0.16f);
                fernMat.SetFloat("_Trans", 0.75f);
                SetTip(fernMat, 0.030f, 0.110f, 0.30f, 1f);
            }
            if (newFrondMat != null)
            {
                float frondGlow = answering ? 0.25f + 0.35f * ripple : pulse * 0.35f;
                float newest = day >= 7 ? 1.15f : 1f;
                newFrondMat.SetColor("_Emission", SeasonColor(FernEmission, FernEmissionWarm, seasonWarmth) * (1f + frondGlow) * life * newest);
                newFrondMat.SetColor("_Rim", SeasonColor(FernRim, FernRimWarm, seasonWarmth));
                newFrondMat.SetFloat("_Edge", 0.22f);
                newFrondMat.SetFloat("_Trans", 0.75f);
                SetTip(newFrondMat, 0.030f, 0.110f, 0.30f, 1f);
            }
            float fiddleScale = _hasLook && _look.Gap ? 0.28f : (0.9f + 0.25f * life);
            if (fiddleMat != null)
            {
                fiddleMat.SetColor("_Emission", SeasonColor(FiddleEmission, FiddleEmissionWarm, seasonWarmth) * fiddleScale);
                fiddleMat.SetColor("_Rim", SeasonColor(FiddleRim, FiddleRimWarm, seasonWarmth));
                fiddleMat.SetFloat("_Edge", 0.10f);
                SetTip(fiddleMat, 0.034f, 0.096f, 0.10f, 1f);
            }
            if (dew != null && dew.sharedMaterial != null)
            {
                dew.sharedMaterial.SetColor("_Tint", new Color(0.78f, 1f, 0.92f));
                dew.sharedMaterial.SetColor("_Emission", SeasonColor(DewEmission, DewEmissionWarm, seasonWarmth));
            }

            ApplyLeanCards();
            ApplyWeek(life);
            DriveMist();
            DriveSporeLook();
            DriveSpores();
            ApplyFlowers();
            bool warm = day >= 7 || (_hasLook && flowers > 0);
            ApplyFocusedLight(warm ? FocusWarm : SeasonColor(FocusMint, FocusGold, seasonWarmth));
            ApplyGardenLook();
            DriveRipple();
            _companions.Show(transform, _liveCompanions ? _liveShots : _captureShots);
            _season.Show(transform, lifetimeFronds, seasonWarmth, flowerMesh, flowerMat, _quad, jarHaloMat);
            AppendSeasonBillboards();
        }

        /// <summary>
        /// A soft point at the crozier, the new frond, or the first flower. Radius 0 in the shader
        /// is the old flat gradient, so only these jar materials opt in.
        /// </summary>
        void ApplyFocusedLight(Color color)
        {
            Vector3 pos = FocusPoint();
            SetLight(mossMat, pos, color);
            SetLight(mossCardMat, pos, color);
            // Soil stays a dark bed. The crozier light is for the plants, not the loam.
            if (soilMat != null && soilMat.HasProperty("_LightPos"))
                soilMat.SetVector("_LightPos", Vector4.zero);
            SetLight(fernMat, pos, color);
            SetLight(newFrondMat, pos, color);
            SetLight(fiddleMat, pos, color);
            SetLight(seedlingMat, pos, color);
            SetLight(flowerMat, pos, color);
            SetLight(_quietFern, pos, color);
            if (day >= 7 && coilHaloMat != null)
                coilHaloMat.SetColor("_Color", new Color(0.85f, 0.48f, 0.18f));
            Transform coilCard = FindNamed("CoilHalo");
            if (coilCard != null)
            {
                coilCard.position = pos;
                coilCard.localScale = new Vector3(CoilHaloSize, CoilHaloSize, 1f);
            }
        }

        Vector3 FocusPoint()
        {
            bool flowerLit = (day >= 7 || (_hasLook && flowers > 0)) && _flowerRoots != null && _flowerRoots.Count > 0 && _flowerRoots[0] != null;
            if (flowerLit)
                return _flowerRoots[0].transform.position;
            if (answer >= 0.5f && newFrond != null)
            {
                Bounds bounds = newFrond.bounds;
                return new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.35f, bounds.center.z);
            }
            if (fiddle != null)
            {
                Renderer renderer = fiddle.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Bounds bounds = renderer.bounds;
                    return new Vector3(bounds.center.x, Mathf.Lerp(bounds.center.y, bounds.max.y, 0.65f), bounds.center.z);
                }
            }
            return transform.TransformPoint(new Vector3(0.004f, 0.078f, 0f));
        }

        static void SetLight(Material material, Vector3 pos, Color color)
        {
            if (material == null || !material.HasProperty("_LightPos")) return;
            material.SetVector("_LightPos", new Vector4(pos.x, pos.y, pos.z, FocusRadius));
            material.SetColor("_LightColor", color);
        }

        /// <summary>
        /// Emission floor at <paramref name="y0"/> and peak at <paramref name="y1"/>, in metres above this jar.
        /// Equal floor and peak would leave the shader flat. Callers pass a lower floor so the light sits on the tips.
        /// </summary>
        void SetTip(Material material, float y0, float y1, float floor, float peak)
        {
            if (material == null || !material.HasProperty("_Tip")) return;
            float baseY = transform.position.y;
            material.SetVector("_Tip", new Vector4(baseY + y0, baseY + y1, floor, peak));
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
            _flowerRoots.Clear();
            _builtFlowers = -1;

            model.transform.SetParent(transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            glassMat = library.Glass;
            mossMat = library.Moss;
            soilMat = library.Soil;
            seedlingMat = library.Seedling;
            fernMat = library.Fern;
            newFrondMat = library.FernNew;
            fiddleMat = library.Fiddle;
            ringMat = library.Ring;
            coilHaloMat = library.CoilHalo;
            jarHaloMat = library.JarHalo;
            mistMat = library.Mist;
            sporeMat = library.Spore;
            flowerMesh = library.FlowerMesh;
            flowerMat = library.Flower;

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
            fid.localPosition += new Vector3(0.004f, 0.001f, 0f);

            Transform seed = Require(mt, "Seedling");
            SetMat(seed, library.Seedling);
            Transform seedStem = Require(mt, "SeedStem");
            SetMat(seedStem, library.Fiddle);
            seed.localPosition = new Vector3(-0.009f, MossBedY + 0.004f, -0.012f);
            seedStem.localPosition = seed.localPosition;
            seed.localScale = Vector3.one * 0.55f;
            seedStem.localScale = Vector3.one * 0.55f;
            Transform seed2 = Instantiate(seed.gameObject, mt).transform;
            seed2.name = "SeedlingB";
            seed2.localPosition = new Vector3(0.010f, MossBedY + 0.003f, -0.012f);
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
            frond.localPosition = new Vector3(-0.010f, MossBedY, 0.004f);
            frond.localRotation = Quaternion.Euler(-8f, 0f, 29f) * axis;
            frond.localScale = Vector3.one * 0.74f;
            Transform frondR = Instantiate(frond.gameObject, mt).transform;
            frondR.name = "FrondRight";
            frondR.localPosition = new Vector3(0.011f, MossBedY, 0.006f);
            frondR.localRotation = Quaternion.Euler(-8f, 0f, -31f) * axis;
            frondR.localScale = new Vector3(-0.72f, 0.72f, 0.72f);
            Transform fnew = Instantiate(frond.gameObject, mt).transform;
            fnew.name = "FrondNew";
            SetMat(fnew, library.FernNew);
            _frondSlots = new[] { frond, frondR, fnew };
            _shownSeed = int.MinValue;
            ApplyFrondVariants();
            fnew.localPosition = new Vector3(0.002f, MossBedY + 0.002f, -0.004f);
            fnew.localRotation = Quaternion.Euler(-12f, 0f, 6f) * axis;
            fnew.localScale = Vector3.one * 0.95f;
            newFrond = fnew.GetComponent<Renderer>();

            var dewGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyObject(dewGo.GetComponent<Collider>());
            dewGo.name = "Dew";
            dewGo.transform.SetParent(transform, false);
            dewGo.transform.localScale = Vector3.one * 0.0038f;
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
            bills.Add(Card("JarHalo", library.JarHalo, new Vector3(0f, 0.050f, 0f), new Vector2(LeanHaloWidth, LeanHaloHeight), Quaternion.identity).transform);
            bills.Add(Card("CoilHalo", library.CoilHalo, new Vector3(-0.006f, 0.072f, 0.002f), new Vector2(CoilHaloSize, CoilHaloSize), Quaternion.identity).transform);

            // One flipbook card. The bottom sits inside the cork, so the plume leaves the lip.
            // Empty corners still count as a layer, so the card is only as wide as the soft base.
            var plume = Card("Mist0", library.Mist, new Vector3(0f, PlumeCenterY, 0f), new Vector2(PlumeWidth, PlumeHeight), Quaternion.identity);
            mist = new[] { plume.transform };
            bills.Add(plume.transform);
            _baseBills = bills.ToArray();
            billboards = _baseBills;
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
        /// Bible answer: frond settles over 0.8 s. The dew stays on the tip until 0.72 s,
        /// then rolls to the base over 1.2 s, so the settled frond and the bead share a frame.
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
                bool rolling = answerTime >= DewStart;
                dew.enabled = rolling;
                dew.transform.localScale = Vector3.one * 0.0055f;
                if (rolling && newFrond != null)
                {
                    float roll = Mathf.Clamp01((answerTime - DewStart) / DewRollSeconds);
                    float eased = 1f - (1f - roll) * (1f - roll);
                    // The card is taller than the painted leaf. 0.70 is the visible tip, not the empty top.
                    Bounds leaf = newFrond.bounds;
                    float tipY = Mathf.Lerp(leaf.min.y, leaf.max.y, 0.765f);
                    float rootY = Mathf.Lerp(leaf.min.y, leaf.max.y, 0.28f);
                    Vector3 tip = new Vector3(leaf.center.x, tipY, leaf.center.z);
                    Vector3 root = new Vector3(leaf.center.x, rootY, leaf.center.z);
                    dew.transform.position = Vector3.Lerp(tip, root, eased) + new Vector3(0f, 0.0006f, -0.003f);
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

        /// <summary>Golden-angle seats on the moss. The same index always lands in the same place.</summary>
        public static float FlowerYaw(int index)
        {
            return index * 137.50776f + 22f;
        }

        public static Vector3 FlowerPosition(int index)
        {
            float yaw = FlowerYaw(index) * Mathf.Deg2Rad;
            float radius = 0.012f + (index % 3) * 0.005f;
            return new Vector3(Mathf.Cos(yaw) * radius, MossBedY + 0.012f, Mathf.Sin(yaw) * radius);
        }

        void DriveMist()
        {
            float clock = reducedMotion ? 0f : time;
            if (mistMat != null)
            {
                int frames = MistGrid * MistGrid;
                int frame = Mathf.FloorToInt(Mathf.Repeat(clock / MistLoopSeconds, 1f) * frames);
                if (frame >= frames) frame = frames - 1;
                int col = frame % MistGrid;
                int row = frame / MistGrid;
                float cell = 1f / MistGrid;
                // Half a texel, so the bilinear filter stays inside its own frame.
                float inset = 0.5f / 2048f;
                float scale = cell - inset * 2f;
                mistMat.SetTextureScale("_MainTex", new Vector2(scale, scale));
                mistMat.SetTextureOffset("_MainTex", new Vector2(col * cell + inset, row * cell + inset));
                mistMat.SetColor("_Color", new Color(0.84f, 0.93f, 0.90f, 0.80f));
            }
            if (mist == null) return;
            for (int i = 0; i < mist.Length; i++)
            {
                if (mist[i] == null) continue;
                mist[i].localPosition = new Vector3(Mathf.Sin(clock * 0.35f + i) * 0.004f, PlumeCenterY, 0f);
                float swell = 1f;
                if (i == 0 && _corkPuff > 0f)
                    swell = 1f + 0.55f * Mathf.Clamp01(_corkPuff / 0.8f);
                mist[i].localScale = new Vector3(PlumeWidth * swell, PlumeHeight * swell, 1f);
            }
            if (_corkPuff > 0f)
            {
                float step = sporeStep > 0f ? sporeStep : 0.016f;
                _corkPuff -= step;
                if (_corkPuff < 0f) _corkPuff = 0f;
            }
        }

        void ApplyLeanCards()
        {
            if (_spillCard == null) _spillCard = FindNamed("DeskSpill");
            if (_haloCard == null) _haloCard = FindNamed("JarHalo");
            if (_spillCard != null)
            {
                _spillCard.localScale = new Vector3(LeanSpillWidth, LeanSpillHeight, 1f);
                var renderer = _spillCard.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                    renderer.sharedMaterial.SetColor("_Color", SeasonColor(SpillMint, SpillWarm, Season.Warmth(Mathf.Max(0, lifetimeFronds))));
            }
            if (_haloCard != null)
                _haloCard.localScale = new Vector3(LeanHaloWidth, LeanHaloHeight, 1f);
        }

        // The frond card faces the camera. A yaw turns it edge-on, so the week only rolls and spreads.
        static readonly float[] WeekX = { -0.030f, -0.020f, -0.010f, 0.002f, 0.012f, 0.024f, -0.004f };
        static readonly float[] WeekZ = { -0.004f, -0.008f, -0.002f, -0.010f, -0.004f, -0.009f, -0.014f };
        static readonly float[] WeekScale = { 0.66f, 0.48f, 0.60f, 0.46f, 0.62f, 0.50f, 0.78f };
        static readonly float[] WeekRoll = { 32f, 20f, 10f, -4f, -18f, -32f, 0f };
        static readonly float[] WeekMirror = { 1f, -1f, 1f, -1f, 1f, -1f, 1f };

        /// <summary>
        /// The slot list is filled in Build and is not saved on the prefab. Capture reloads the scene,
        /// so the week and the variant swap look the three fronds up by name.
        /// </summary>
        void ResolveFrondSlots()
        {
            if (_frondSlots != null && _frondSlots.Length >= 3 && _frondSlots[0] != null && _frondSlots[2] != null)
                return;
            Transform left = FindNamed("Frond");
            Transform right = FindNamed("FrondRight");
            Transform newest = FindNamed("FrondNew");
            if (left == null || right == null || newest == null) return;
            _frondSlots = new[] { left, right, newest };
            if (newFrond == null) newFrond = newest.GetComponent<Renderer>();
        }

        void CacheG1Poses()
        {
            if (_g1PosesReady || _frondSlots == null || _frondSlots.Length == 0) return;
            _g1Position = new Vector3[_frondSlots.Length];
            _g1Rotation = new Quaternion[_frondSlots.Length];
            _g1Scale = new Vector3[_frondSlots.Length];
            for (int i = 0; i < _frondSlots.Length; i++)
            {
                Transform slot = _frondSlots[i];
                if (slot == null) continue;
                _g1Position[i] = slot.localPosition;
                _g1Rotation[i] = slot.localRotation;
                _g1Scale[i] = slot.localScale;
            }
            _g1PosesReady = true;
        }

        /// <summary>
        /// Day 7 is a glance at the week: seven fronds inside the glass, the newest brightest,
        /// one quieter frond still green, and the first flower. Ritual timing is unchanged.
        /// </summary>
        void ApplyWeek(float life)
        {
            if (day >= 7)
            {
                EnsureWeekFronds();
                PlaceWeek(life);
                if (fiddle != null)
                {
                    var fiddleRenderer = fiddle.GetComponent<Renderer>();
                    if (fiddleRenderer != null) fiddleRenderer.enabled = false;
                }
                if (newFrond != null) newFrond.enabled = true;
                _weekApplied = 7;
                return;
            }
            if (_weekApplied < 7) return;
            RestoreG1Poses();
            if (_weekFronds != null)
            {
                for (int i = 0; i < _weekFronds.Length; i++)
                {
                    if (_weekFronds[i] != null) _weekFronds[i].gameObject.SetActive(false);
                }
            }
            _weekApplied = 0;
        }

        void EnsureWeekFronds()
        {
            if (_frondSlots == null || _frondSlots.Length == 0 || _frondSlots[0] == null) return;
            if (_weekFronds != null && _weekFronds.Length == 4 && _weekFronds[0] != null) return;
            _weekFronds = new Transform[4];
            Transform parent = _frondSlots[0].parent;
            for (int i = 0; i < 4; i++)
            {
                var copy = Instantiate(_frondSlots[0].gameObject, parent);
                copy.name = "FrondWeek" + i;
                _weekFronds[i] = copy.transform;
                if (fernMat != null) SetMat(copy.transform, fernMat);
            }
        }

        void PlaceWeek(float life)
        {
            if (_frondSlots == null || _frondSlots.Length < 3 || _weekFronds == null) return;
            var all = new[]
            {
                _frondSlots[0], _frondSlots[1],
                _weekFronds[0], _weekFronds[1], _weekFronds[2], _weekFronds[3],
                _frondSlots[2]
            };
            Transform source = FindMesh(transform, "FrondV0");
            Quaternion axis = source != null ? source.localRotation : Quaternion.identity;
            Material quiet = QuietFern(life);
            for (int i = 0; i < all.Length; i++)
            {
                Transform slot = all[i];
                if (slot == null) continue;
                slot.gameObject.SetActive(true);
                float scale = WeekScale[i];
                slot.localPosition = new Vector3(WeekX[i], MossBedY + 0.002f, WeekZ[i]);
                slot.localRotation = Quaternion.Euler(-8f, 0f, WeekRoll[i]) * axis;
                slot.localScale = new Vector3(WeekMirror[i] * scale, scale, scale);
                Material material = fernMat;
                if (i == 1) material = quiet;
                else if (i == 6) material = newFrondMat;
                if (material != null) SetMat(slot, material);
                if (_frondVariants != null && _frondVariants.Length == 3 && i != 6)
                {
                    int mod = (shapeSeed + i) % _frondVariants.Length;
                    if (mod < 0) mod += _frondVariants.Length;
                    var filter = slot.GetComponent<MeshFilter>();
                    if (filter != null && _frondVariants[mod] != null) filter.sharedMesh = _frondVariants[mod];
                }
                var renderer = slot.GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = true;
            }
        }

        Material QuietFern(float life)
        {
            if (_quietFern == null && fernMat != null)
                _quietFern = new Material(fernMat) { name = "Jar_FernQuiet" };
            if (_quietFern != null)
                _quietFern.SetColor("_Emission", SeasonColor(FernEmission, FernEmissionWarm, Season.Warmth(Mathf.Max(0, lifetimeFronds))) * 0.55f * life);
                SetLight(_quietFern, FocusPoint(), day >= 7 ? FocusWarm : FocusMint);
            return _quietFern;
        }

        void RestoreG1Poses()
        {
            if (!_g1PosesReady || _frondSlots == null) return;
            for (int i = 0; i < _frondSlots.Length; i++)
            {
                Transform slot = _frondSlots[i];
                if (slot == null || _g1Position == null || i >= _g1Position.Length) continue;
                slot.localPosition = _g1Position[i];
                slot.localRotation = _g1Rotation[i];
                slot.localScale = _g1Scale[i];
            }
            if (_frondSlots.Length > 0 && fernMat != null) SetMat(_frondSlots[0], fernMat);
            if (_frondSlots.Length > 1 && fernMat != null) SetMat(_frondSlots[1], fernMat);
            if (_frondSlots.Length > 2 && newFrondMat != null) SetMat(_frondSlots[2], newFrondMat);
        }

        static Transform FindNamed(Transform root, string name)
        {
            if (root == null) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name) return all[i];
            }
            return null;
        }

        Transform FindNamed(string name)
        {
            return FindNamed(transform, name);
        }

        void DriveSporeLook()
        {
            if (sporeMat != null)
            {
                // One second of brighter gold while the answer plays, then back.
                float boost = 0f;
                if (answerTime >= 0f && answerTime <= 1f)
                    boost = Mathf.Sin(Mathf.Clamp01(answerTime) * Mathf.PI);
                sporeMat.SetColor("_Color", SporeGold * (1.85f + 0.9f * boost));
            }
            if (spores == null) return;
            var emission = spores.emission;
            emission.rateOverTime = reducedMotion ? 2f : 4f;
            var noise = spores.noise;
            noise.enabled = !reducedMotion;
            var main = spores.main;
            main.maxParticles = reducedMotion ? 20 : 40;
            main.startSize = new ParticleSystem.MinMaxCurve(reducedMotion ? 0.0014f : 0.0018f, reducedMotion ? 0.0024f : 0.0034f);
        }

        void ApplyFlowers()
        {
            if (flowerMesh == null || flowerMat == null) return;
            flowerMat.SetColor("_Emission", new Color(0.42f, 0.24f, 0.08f));
            flowerMat.SetColor("_Tint", new Color(0.95f, 0.82f, 0.55f));
            if (_builtFlowers == flowers) return;
            for (int i = _flowerRoots.Count - 1; i >= 0; i--)
                DestroyObject(_flowerRoots[i]);
            _flowerRoots.Clear();
            int count = Mathf.Max(0, flowers);
            for (int k = 0; k < count; k++)
            {
                var go = new GameObject("Flower" + k);
                go.transform.SetParent(transform, false);
                // The bell opens on +Y. A negative pitch turns that opening toward the JarG1 eye.
                // Positive pitch turns the opening away, so the frame sees outer petals rather than the throat.
                // Steep pitch points the throat away from the eye. The frame sees the outer petals.
                go.transform.localPosition = k == 0 ? new Vector3(0.014f, MossBedY + 0.012f, 0.002f) : FlowerPosition(k);
                go.transform.localRotation = Quaternion.Euler(96f, -18f + k * 12f, 8f);
                go.transform.localScale = Vector3.one * 0.42f;
                go.AddComponent<MeshFilter>().sharedMesh = flowerMesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = flowerMat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                Material haloMat = FlowerHaloMaterial();
                if (haloMat != null && _quad != null)
                {
                    var halo = Card("FlowerHalo" + k, haloMat, Vector3.zero, new Vector2(0.014f, 0.014f), Quaternion.identity);
                    halo.transform.SetParent(go.transform, false);
                    halo.transform.localPosition = new Vector3(0f, 0.012f, -0.004f);
                    halo.transform.localRotation = Quaternion.identity;
                    halo.transform.localScale = new Vector3(0.028f, 0.028f, 1f);
                }
                _flowerRoots.Add(go);
            }
            _builtFlowers = count;
            var bills = new List<Transform>();
            if (_baseBills != null)
            {
                for (int i = 0; i < _baseBills.Length; i++)
                    if (_baseBills[i] != null) bills.Add(_baseBills[i]);
            }
            for (int i = 0; i < _flowerRoots.Count; i++)
            {
                Transform root = _flowerRoots[i] != null ? _flowerRoots[i].transform : null;
                if (root == null) continue;
                for (int c = 0; c < root.childCount; c++)
                {
                    Transform child = root.GetChild(c);
                    if (child.name.StartsWith("FlowerHalo", System.StringComparison.Ordinal)) bills.Add(child);
                }
            }
            billboards = bills.ToArray();
        }

        Material FlowerHaloMaterial()
        {
            if (_flowerHalo != null) return _flowerHalo;
            if (jarHaloMat == null) return null;
            _flowerHalo = new Material(jarHaloMat) { name = "FlowerHalo" };
            _flowerHalo.SetColor("_Color", SporeGold * 0.42f);
            _flowerHalo.SetFloat("_Falloff", 1.3f);
            _flowerHalo.SetVector("_Focus", new Vector4(0.5f, 0.5f, 0.46f, 0f));
            return _flowerHalo;
        }

        void DriveSpores()
        {
            if (spores == null) return;
            AlignSporeVelocity();
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

        bool _sporeVelocityAligned;

        /// <summary>
        /// Unity 6 rejects a velocity module whose axes are not the same curve mode.
        /// The saved jar has Y as two constants and X/Z as constants, which logs an error on Simulate.
        /// </summary>
        void AlignSporeVelocity()
        {
            if (_sporeVelocityAligned || spores == null) return;
            var velocity = spores.velocityOverLifetime;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.01f, 0.03f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            _sporeVelocityAligned = true;
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
            main.startLifetime = 8f;
            // Sphere emission is radial. Keep that tiny, and let the Y velocity be the 1-3 cm/s rise.
            main.startSpeed = 0.002f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.0018f, 0.0034f);
            main.startColor = Color.white;
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.useUnscaledTime = false;
            var emission = ps.emission;
            emission.rateOverTime = 4f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.038f;
            shape.radiusThickness = 0.55f;
            shape.position = new Vector3(0f, 0.050f, 0f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.004f;
            noise.frequency = 0.6f;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.01f, 0.03f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
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

        void ApplyGardenLook()
        {
            if (!_hasLook || day >= 7)
            {
                HideRecord();
                SuppressArtPair(false);
                return;
            }
            ResolveFrondSlots();
            int count = _look.Days == null ? 0 : _look.Days.Length;
            bool animateLast = _look.AnimatingNew && count > 0;
            int placed = animateLast ? count - 1 : count;
            EnsureRecord(placed);
            float lean = LeanDegrees(_look.Vitality);
            ShownLean = lean;
            for (int i = 0; i < _recordFronds.Count; i++)
            {
                Transform slot = _recordFronds[i];
                if (slot == null) continue;
                bool on = i < placed;
                slot.gameObject.SetActive(on);
                if (!on) continue;
                PlaceRecord(slot, i, placed, lean);
            }
            SuppressArtPair(true);
            if (newFrond != null && !animateLast) newFrond.enabled = false;
            if (fiddle != null)
            {
                Renderer fiddleRenderer = fiddle.GetComponent<Renderer>();
                if (fiddleRenderer != null)
                    fiddleRenderer.enabled = _look.Waiting && !_look.AnimatingNew;
            }
            PlaceDew(placed);
        }

        void PlaceRecord(Transform slot, int index, int placed, float lean)
        {
            bool inner = index >= Garden.FrondsBeforeInnerLayer;
            float scale = inner ? 0.40f : 0.56f + (index % 3) * 0.05f;
            if (index == placed - 1 && _look.Vitality > 0.99f) scale += 0.14f;
            float mirror = index % 2 == 0 ? 1f : -1f;
            slot.localPosition = FrondSlot(index);
            Transform meshSource = FindMesh(transform, "FrondV0");
            Quaternion axis = meshSource != null ? meshSource.localRotation : Quaternion.identity;
            Quaternion rest = Quaternion.Euler(-8f, 0f, FrondSpread(index)) * axis;
            slot.localRotation = Quaternion.Euler(lean, 0f, 0f) * rest;
            slot.localScale = new Vector3(mirror * scale, scale, scale);
            if (_frondVariants != null && _frondVariants.Length == 3)
            {
                int mod = (shapeSeed + index) % _frondVariants.Length;
                if (mod < 0) mod += _frondVariants.Length;
                MeshFilter filter = slot.GetComponent<MeshFilter>();
                if (filter != null && _frondVariants[mod] != null) filter.sharedMesh = _frondVariants[mod];
            }
            float glow = _look.Vitality;
            if (index == placed - 1 && _look.Vitality > 0.99f) glow *= 1.12f;
            if (_lookLit >= 0)
            {
                // Birth order. The newest lit frond is the brightest. The ones still waiting stay green and quiet.
                if (index < _lookLit) glow = index == _lookLit - 1 ? 1.18f : 0.92f;
                else glow = 0.34f;
            }
            Material material = index < _recordMats.Count ? _recordMats[index] : null;
            if (material != null) material.SetColor("_Emission", SeasonColor(FernEmission, FernEmissionWarm, Season.Warmth(Mathf.Max(0, lifetimeFronds))) * glow);
            Renderer renderer = slot.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = true;
        }

        void EnsureRecord(int count)
        {
            Transform source = FindNamed("Frond");
            if (source == null && _frondSlots != null && _frondSlots.Length > 0) source = _frondSlots[0];
            if (source == null) return;
            while (_recordFronds.Count < count)
            {
                int index = _recordFronds.Count;
                GameObject copy = Instantiate(source.gameObject, source.parent);
                copy.name = "RecordFrond" + index.ToString(CultureInfo.InvariantCulture);
                copy.SetActive(true);
                Material material = null;
                Renderer renderer = copy.GetComponent<Renderer>();
                Material shared = fernMat != null ? fernMat : (renderer != null ? renderer.sharedMaterial : null);
                if (shared != null)
                {
                    material = new Material(shared) { name = "RecordFern" + index.ToString(CultureInfo.InvariantCulture) };
                    SetMat(copy.transform, material);
                }
                _recordMats.Add(material);
                _recordFronds.Add(copy.transform);
            }
        }

        void PlaceDew(int placed)
        {
            int count = Mathf.Max(0, _look.Dew);
            ShownDew = count;
            Material shared = dew != null ? dew.sharedMaterial : null;
            Transform parent = _recordFronds.Count > 0 && _recordFronds[0] != null ? _recordFronds[0].parent : transform;
            Vector3 anchor = placed > 0 && placed - 1 < _recordFronds.Count && _recordFronds[placed - 1] != null
                ? _recordFronds[placed - 1].localPosition
                : new Vector3(0f, 0.05f, 0f);
            while (_dewBeads.Count < count)
            {
                var bead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Collider collider = bead.GetComponent<Collider>();
                if (collider != null) DestroyObject(collider);
                bead.name = "DewBead" + _dewBeads.Count.ToString(CultureInfo.InvariantCulture);
                bead.transform.SetParent(parent, false);
                if (shared != null) SetMat(bead.transform, shared);
                _dewBeads.Add(bead.transform);
            }
            for (int i = 0; i < _dewBeads.Count; i++)
            {
                Transform bead = _dewBeads[i];
                if (bead == null) continue;
                bool on = i < count;
                bead.gameObject.SetActive(on);
                if (!on) continue;
                float side = (i - (count - 1) * 0.5f) * 0.006f;
                bead.localPosition = anchor + new Vector3(side, 0.028f + i * 0.0015f, -0.004f);
                bead.localScale = Vector3.one * 0.0042f;
            }
        }

        void HideRecord()
        {
            for (int i = 0; i < _recordFronds.Count; i++)
            {
                if (_recordFronds[i] != null) _recordFronds[i].gameObject.SetActive(false);
            }
            for (int i = 0; i < _dewBeads.Count; i++)
            {
                if (_dewBeads[i] != null) _dewBeads[i].gameObject.SetActive(false);
            }
        }

        void SuppressArtPair(bool suppress)
        {
            SetNamedActive("Frond", !suppress);
            SetNamedActive("FrondRight", !suppress);
        }

        void SetNamedActive(string name, bool active)
        {
            Transform found = FindNamed(name);
            if (found != null && found.gameObject.activeSelf != active)
                found.gameObject.SetActive(active);
        }

        void DriveRipple()
        {
            bool on = _hasLook && _look.Recovered && recoveredTime >= 0f && recoveredTime <= 2.5f;
            if (!on)
            {
                if (_ripple != null) _ripple.gameObject.SetActive(false);
                return;
            }
            EnsureRipple();
            if (_ripple == null) return;
            _ripple.gameObject.SetActive(true);
            _ripple.position = NewestFrondWorld();
            float u = Mathf.Clamp01(recoveredTime / 2.5f);
            float size = Mathf.Lerp(0.028f, 0.18f, u);
            _ripple.localScale = new Vector3(size, size, 1f);
            if (_rippleMat != null)
                _rippleMat.SetColor("_Color", new Color(0.28f, 0.52f, 0.36f) * (1.15f * (1f - u)));
            AddBillboard(_ripple);
        }

        void EnsureRipple()
        {
            if (_ripple != null) return;
            Transform halo = FindNamed("JarHalo");
            if (halo == null) return;
            GameObject copy = Instantiate(halo.gameObject, transform);
            copy.name = "RecoveredRipple";
            _ripple = copy.transform;
            Renderer renderer = copy.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                _rippleMat = new Material(renderer.sharedMaterial) { name = "RecoveredRipple" };
                renderer.sharedMaterial = _rippleMat;
            }
            copy.SetActive(false);
        }

        Vector3 NewestFrondWorld()
        {
            for (int i = _recordFronds.Count - 1; i >= 0; i--)
            {
                Transform slot = _recordFronds[i];
                if (slot != null && slot.gameObject.activeInHierarchy) return slot.position;
            }
            if (newFrond != null && newFrond.enabled) return newFrond.transform.position;
            return transform.TransformPoint(new Vector3(0f, 0.05f, 0f));
        }

        void AddBillboard(Transform card)
        {
            if (card == null) return;
            if (billboards != null)
            {
                for (int i = 0; i < billboards.Length; i++)
                {
                    if (billboards[i] == card) return;
                }
            }
            var list = new List<Transform>();
            if (billboards != null)
            {
                for (int i = 0; i < billboards.Length; i++)
                {
                    if (billboards[i] != null) list.Add(billboards[i]);
                }
            }
            list.Add(card);
            billboards = list.ToArray();
        }

        void OnDestroy()
        {
            for (int i = 0; i < _recordMats.Count; i++) DestroyObject(_recordMats[i]);
            DestroyObject(_rippleMat);
        }

        void AppendSeasonBillboards()
        {
            IReadOnlyList<Transform> extra = _season.Halos;
            if (extra == null || extra.Count == 0) return;
            var list = new List<Transform>();
            if (billboards != null)
            {
                for (int i = 0; i < billboards.Length; i++)
                    if (billboards[i] != null) list.Add(billboards[i]);
            }
            for (int i = 0; i < extra.Count; i++)
                if (extra[i] != null) list.Add(extra[i]);
            billboards = list.ToArray();
        }

        static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
