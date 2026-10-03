using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GardenVR.Core;
using GardenVR.Input;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Sundial
{
    /// <summary>
    /// The first launch. The dial draws itself, the shadow sweeps, three packets open onto one habit each,
    /// the due plant is tended, and the dusk ritual is offered. Steps advance on a finished drawing or on
    /// a real pinch. A quit resumes at the saved step. A failed load shows the restore prompt and never starts.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(30)]
    public sealed class FirstRunWizard : MonoBehaviour
    {
        public const float AppearSeconds = 1.2f;
        public const float SweepSeconds = 1.5f;
        public const float CaptionSeconds = 0.9f;
        public const float RiseSeconds = 0.8f;
        public const float DropSeconds = 0.9f;
        public const string RestoreId = "prompt.restore";

        /// <summary>While this is set, the dusk offer stays hidden so the first tend happens first.</summary>
        public static bool SuppressBreathOffer;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly Color Pencil = new Color(0.541f, 0.506f, 0.471f, 1f);
        static readonly Color Gold = new Color(0.961f, 0.780f, 0.416f, 0.55f);

        SundialController _controller;
        SundialService _service;
        DialView _view;
        IHandIntentSource _source;
        bool _subscribed;
        bool _running;
        bool _built;
        bool _open;
        string _step;
        string _lookId;
        float _stepTime;
        float _appTime;
        float _firstTendAt = -1f;
        float _ritualDoneAt = -1f;

        Material _card;
        Texture2D _white;
        Texture2D _ring;
        Texture2D _glow;
        Mesh _upright;
        Mesh _flat;
        GameObject _caption;
        GameObject _construction;
        GameObject _glowGo;
        GameObject _restore;
        readonly GameObject[] _packets = new GameObject[3];
        readonly Texture2D[] _packetTex = new Texture2D[3];
        readonly GameObject[] _seeds = new GameObject[3];
        readonly List<GameObject> _chips = new List<GameObject>();

        public string Step { get { return _step; } }
        public bool Running { get { return _running; } }
        /// <summary>Palm dismiss freezes the drawing. The step and the clocks stay where they were.</summary>
        public bool Held;
        public bool RestoreVisible { get { return _restore != null && _restore.activeSelf; } }
        public float FirstTendAt { get { return _firstTendAt; } }
        public float RitualDoneAt { get { return _ritualDoneAt; } }
        public float AppTime { get { return _appTime; } }

        public void SetSource(IHandIntentSource source)
        {
            if (_source == source && _subscribed) return;
            Unsubscribe();
            _source = source;
            if (isActiveAndEnabled && _source != null)
            {
                _source.Intent += OnIntent;
                _subscribed = true;
            }
        }

        /// <summary>Seed packets and the restore chip belong to the wizard, not the tend.</summary>
        public bool Handles(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (!_running && _service != null && _service.Outcome == LoadOutcome.Failed)
                return id == RestoreId;
            if (!_running) return false;
            return id.StartsWith("seed.", StringComparison.Ordinal);
        }

        /// <summary>During the journey only the due plant's pinch counts, and only on the tend step.</summary>
        public bool AllowsPlantTend(string arc)
        {
            if (!_running) return true;
            if (_step != FirstRunSteps.FirstTend) return false;
            return arc == DueArc();
        }

        void Awake()
        {
            _controller = GetComponent<SundialController>();
            _view = GetComponent<DialView>();
            _service = _controller != null ? _controller.Service : null;
            if (_service == null || _view == null) return;
            if (_service.Outcome == LoadOutcome.Failed)
            {
                _running = false;
                SuppressBreathOffer = false;
                return;
            }
            if (!ShouldStart()) return;
            _running = true;
            string saved = _service.Save != null ? _service.Save.FirstRunStep : null;
            if (FirstRunSteps.IsResume(saved))
            {
                _step = saved;
                if (FirstRunSteps.Index(saved) > FirstRunSteps.Index(FirstRunSteps.Sweep))
                    _controller.SnapSweep = true;
                else
                    _controller.HoldSweep = true;
                Debug.Log("[FirstRun] step=" + saved + " t=0.00");
            }
            else
            {
                _controller.HoldSweep = true;
                _view.gnomonDeg = 0f;
                Enter(FirstRunSteps.Appear);
            }
            _view.appear = AppearNow();
            _view.showPlants = PlantsNow();
            SuppressBreathOffer = FirstRunSteps.Index(_step) < FirstRunSteps.Index(FirstRunSteps.BreathsOffer);
        }

        void OnEnable()
        {
            if (_source != null && !_subscribed)
            {
                _source.Intent += OnIntent;
                _subscribed = true;
            }
        }

        void OnDisable() { Unsubscribe(); }

        void OnDestroy()
        {
            Unsubscribe();
            SuppressBreathOffer = false;
            if (_card != null) Destroy(_card);
            if (_white != null) Destroy(_white);
            if (_ring != null) Destroy(_ring);
            if (_glow != null) Destroy(_glow);
            if (_upright != null) Destroy(_upright);
            if (_flat != null) Destroy(_flat);
            for (int i = 0; i < _packetTex.Length; i++)
            {
                if (_packetTex[i] != null) Destroy(_packetTex[i]);
            }
        }

        void Update()
        {
            if (!Application.isPlaying || _service == null) return;
            if (Held) return;
            float dt = Time.deltaTime;
            if (dt < 0f) dt = 0f;
            if (dt > 0.1f) dt = 0.1f;
            _appTime += dt;

            if (_service.Outcome == LoadOutcome.Failed)
            {
                EnsureChrome();
                if (_restore != null && !_restore.activeSelf) _restore.SetActive(true);
                _running = false;
                SuppressBreathOffer = false;
                return;
            }
            if (!_running) return;

            _stepTime += dt;
            AdvanceCinematics();
            WatchRitual();
            EnsureChrome();
            ApplyVisuals();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            if (_caption != null && _caption.activeSelf)
                FaceAway(cam, _caption.transform, _caption.transform.position);
            for (int i = 0; i < _packets.Length; i++)
            {
                if (_packets[i] != null && _packets[i].activeSelf)
                    FaceToward(cam, _packets[i].transform, _packets[i].transform.position);
            }
            for (int i = 0; i < _chips.Count; i++)
            {
                if (_chips[i] != null && _chips[i].activeSelf)
                    FaceAway(cam, _chips[i].transform, _chips[i].transform.position);
            }
            if (_restore != null && _restore.activeSelf)
                FaceAway(cam, _restore.transform, _restore.transform.position);
            for (int i = 0; i < _seeds.Length; i++)
            {
                if (_seeds[i] != null && _seeds[i].activeSelf)
                    FaceToward(cam, _seeds[i].transform, _seeds[i].transform.position);
            }
        }

        void OnIntent(HandIntent intent)
        {
            if (_service != null && _service.Outcome == LoadOutcome.Failed)
            {
                if (intent.Kind == HandIntentKind.Pinch && intent.TargetId == RestoreId)
                    _service.TryRestoreBackup();
                return;
            }
            if (!_running) return;
            if (intent.Kind == HandIntentKind.Look)
            {
                _lookId = intent.TargetId;
                return;
            }
            if (intent.Kind != HandIntentKind.Pinch) return;
            string id = intent.TargetId;
            string pickArc = FirstRunSteps.PickArc(_step);
            if (pickArc != null)
            {
                if (id == SeedCatalog.PacketId(pickArc))
                {
                    if (!_open && _controller != null)
                    {
                        int index = ArcIndex(pickArc);
                        Transform at = index >= 0 && _packets[index] != null ? _packets[index].transform : transform;
                        _controller.Play(SundialController.CuePacket, at);
                    }
                    _open = true;
                    return;
                }
                SeedPreset preset = PresetFromId(id);
                if (preset != null && preset.Arc == pickArc)
                {
                    _service.PlantPreset(preset);
                    _open = false;
                    ClearChips();
                    string next = FirstRunSteps.Next(_step);
                    if (next != null) Enter(next);
                    return;
                }
            }
            if (_step == FirstRunSteps.FirstTend && id == "plant." + DueArc() && _service.IsPending)
            {
                _firstTendAt = _appTime;
                Debug.Log("[FirstRun] firstTendAt=" + _appTime.ToString("0.00", Inv));
                Enter(FirstRunSteps.BreathsOffer);
            }
        }

        bool ShouldStart()
        {
            if (_service.Outcome == LoadOutcome.Failed) return false;
            string step = _service.Save != null ? _service.Save.FirstRunStep : null;
            if (step == SundialService.DevSeedStep || step == FirstRunSteps.Done) return false;
            if (_service.Outcome == LoadOutcome.Fresh && string.IsNullOrEmpty(step)) return true;
            return FirstRunSteps.IsResume(step);
        }

        void AdvanceCinematics()
        {
            for (int n = 0; n < 8; n++)
            {
                float need = Duration(_step);
                if (need < 0f) return;
                if (_stepTime + 0.0001f < need) return;
                string next = FirstRunSteps.Next(_step);
                if (next == null) return;
                Enter(next);
            }
        }

        float Duration(string step)
        {
            if (FirstRunSteps.WaitsForIntent(step) || string.IsNullOrEmpty(step)) return -1f;
            bool reduced = _service != null && _service.ReducedMotion;
            if (step == FirstRunSteps.Appear) return reduced ? 0f : AppearSeconds;
            if (step == FirstRunSteps.Sweep) return reduced ? 0f : SweepSeconds;
            if (step == FirstRunSteps.Caption) return reduced ? 0f : CaptionSeconds;
            if (step == FirstRunSteps.Packets) return reduced ? 0f : RiseSeconds;
            if (step == FirstRunSteps.Drop) return reduced ? 0f : DropSeconds;
            return -1f;
        }

        void Enter(string step)
        {
            _step = step;
            _stepTime = 0f;
            _open = false;
            ClearChips();
            if (_service != null) _service.SetFirstRunStep(step);
            Debug.Log("[FirstRun] step=" + step + " t=" + _appTime.ToString("0.00", Inv));
            if (step == FirstRunSteps.Appear && _controller != null)
                _controller.NotifyDialAppear();
            if (step == FirstRunSteps.Sweep && _controller != null)
                _controller.ReleaseSweep();
            if (step == FirstRunSteps.Caption && _controller != null)
                _controller.PlayFirstRunLine();
            if (step == FirstRunSteps.Drop && _controller != null)
                _controller.Play(SundialController.CueSeed, transform);
            if (step == FirstRunSteps.Done)
            {
                _running = false;
                SuppressBreathOffer = false;
            }
        }

        void WatchRitual()
        {
            DuskRitualController dusk = GetComponent<DuskRitualController>();
            if (dusk == null) return;
            if (_step == FirstRunSteps.BreathsOffer && dusk.Active)
                Enter(FirstRunSteps.Breaths);
            if (dusk.Answered && _step != FirstRunSteps.Done
                && FirstRunSteps.Index(_step) >= FirstRunSteps.Index(FirstRunSteps.BreathsOffer))
            {
                _ritualDoneAt = _appTime;
                Debug.Log("[FirstRun] ritualDoneAt=" + _appTime.ToString("0.00", Inv));
                Enter(FirstRunSteps.Done);
            }
        }

        float AppearNow()
        {
            if (_step == FirstRunSteps.Appear && _service != null && !_service.ReducedMotion)
                return Mathf.Clamp01(_stepTime / AppearSeconds);
            if (FirstRunSteps.Index(_step) <= FirstRunSteps.Index(FirstRunSteps.Appear) && _step == FirstRunSteps.Appear)
                return _service != null && _service.ReducedMotion ? 1f : Mathf.Clamp01(_stepTime / AppearSeconds);
            return 1f;
        }

        bool PlantsNow()
        {
            return FirstRunSteps.Index(_step) > FirstRunSteps.Index(FirstRunSteps.Drop);
        }

        void ApplyVisuals()
        {
            if (_view == null) return;
            _view.appear = AppearNow();
            _view.showPlants = PlantsNow();
            SuppressBreathOffer = FirstRunSteps.Index(_step) < FirstRunSteps.Index(FirstRunSteps.BreathsOffer);
            PlacePackets();
            PlaceCaption();
            PlaceConstruction();
            PlaceGlow();
            PlaceSeeds();
            if (_open) PlaceChips();
        }

        string DueArc()
        {
            if (_service != null && _service.State != null && _service.State.Arc.HasValue)
            {
                string key = SundialArcs.Key(_service.State.Arc.Value.ToString());
                if (!string.IsNullOrEmpty(key)) return key;
            }
            return "midday";
        }

        static SeedPreset PresetFromId(string id)
        {
            const string prefix = "seed.";
            if (string.IsNullOrEmpty(id) || !id.StartsWith(prefix, StringComparison.Ordinal)) return null;
            string rest = id.Substring(prefix.Length);
            int dot = rest.IndexOf('.');
            if (dot <= 0 || dot >= rest.Length - 1) return null;
            return SeedCatalog.Find(rest.Substring(0, dot), rest.Substring(dot + 1));
        }

        void EnsureChrome()
        {
            if (_built) return;
            _built = true;
            Shader shader = Shader.Find("Fidelity/Card");
            if (shader != null)
            {
                _card = new Material(shader) { name = "FirstRunCard" };
                _card.SetFloat("_Src", 5f);
                _card.SetFloat("_Dst", 10f);
                _card.SetFloat("_Ring", 0f);
                _card.SetFloat("_Coverage", 0f);
                _card.SetFloat("_ZWrite", 0f);
                _card.SetFloat("_Sparkle", 0f);
                _card.SetColor("_Color", Color.white);
            }
            _white = Solid(Color.white);
            _ring = Circle(false);
            _glow = Circle(true);
            _upright = UprightQuad();
            _flat = FlatQuad();
            Font hand = InkLetter.Hand;
            if (hand != null)
            {
                var chars = new System.Text.StringBuilder();
                chars.Append(SeedCatalog.DayCaption);
                chars.Append(SeedCatalog.RestoreTitle);
                chars.Append(SeedCatalog.RestoreAction);
                for (int i = 0; i < SeedCatalog.All.Length; i++)
                {
                    chars.Append(SeedCatalog.All[i].Title);
                    chars.Append(SeedCatalog.All[i].Cue);
                }
                hand.RequestCharactersInTexture(chars.ToString(), 64, FontStyle.Normal);
            }
            string[] arcs = { "morning", "midday", "winddown" };
            for (int i = 0; i < arcs.Length; i++)
            {
                _packetTex[i] = LoadPng("packet-" + arcs[i] + ".png");
                _packets[i] = MakeQuad("seed." + arcs[i], _upright, _packetTex[i] != null ? _packetTex[i] : _white, Color.white, SeedCatalog.PacketId(arcs[i]));
                _packets[i].SetActive(false);
                _seeds[i] = MakeQuad("seedling." + arcs[i], _upright, _white, new Color(0.541f, 0.420f, 0.275f, 1f), null);
                _seeds[i].SetActive(false);
            }
            _caption = MakeText("DayCaption", SeedCatalog.DayCaption, 56, 0.0042f, null);
            InkLetter.AttachNote(_caption.transform, new Vector2(0.24f, 0.050f));
            _caption.SetActive(false);
            _construction = MakeQuad("PencilConstruction", _flat, _ring, Pencil, null);
            _construction.SetActive(false);
            _glowGo = MakeQuad("ArcGlow", _flat, _glow, Gold, null);
            _glowGo.SetActive(false);
            if (_service != null && _service.Outcome == LoadOutcome.Failed)
                BuildRestore();
        }

        void BuildRestore()
        {
            if (_restore != null) return;
            _restore = new GameObject("RestorePrompt");
            _restore.transform.SetParent(transform, false);
            InkLetter.AttachNote(_restore.transform, new Vector2(0.20f, 0.086f));
            var title = MakeText("title", SeedCatalog.RestoreTitle, 48, 0.0024f, null);
            title.transform.SetParent(_restore.transform, false);
            title.transform.localPosition = new Vector3(0f, 0.012f, -0.001f);
            var action = MakeText("action", SeedCatalog.RestoreAction, 42, 0.002f, RestoreId);
            action.transform.SetParent(_restore.transform, false);
            action.transform.localPosition = new Vector3(0f, -0.014f, -0.001f);
            _restore.SetActive(true);
        }

        void PlacePackets()
        {
            bool show = FirstRunSteps.Index(_step) >= FirstRunSteps.Index(FirstRunSteps.Packets);
            float rise = 1f;
            if (_step == FirstRunSteps.Packets && _service != null && !_service.ReducedMotion)
                rise = Mathf.Clamp01(_stepTime / RiseSeconds);
            if (_step == FirstRunSteps.Drop && _service != null && !_service.ReducedMotion)
                rise = 1f - Mathf.Clamp01(_stepTime / DropSeconds);
            if (FirstRunSteps.Index(_step) > FirstRunSteps.Index(FirstRunSteps.Drop))
                show = false;
            float radius = _view != null && _view.faceRadius > 0.05f ? _view.faceRadius : 0.15f;
            float y0 = (_view != null ? _view.faceY : 0.01f) - 0.07f;
            float y1 = (_view != null ? _view.faceY : 0.01f) + 0.02f;
            for (int i = 0; i < 3; i++)
            {
                if (_packets[i] == null) continue;
                if (_packets[i].activeSelf != show) _packets[i].SetActive(show);
                if (!show) continue;
                float x = (i - 1) * radius * 0.70f;
                float z = -radius * (i == 1 ? 1.08f : 0.95f);
                _packets[i].transform.localPosition = new Vector3(x, Mathf.Lerp(y0, y1, rise), z);
                _packets[i].transform.localScale = new Vector3(0.046f, 0.062f, 1f);
                bool looked = _lookId == SeedCatalog.PacketId(ArcName(i));
                Tint(_packets[i], looked ? new Color(1f, 0.96f, 0.82f, 1f) : Color.white);
            }
        }

        void PlaceChips()
        {
            string arc = FirstRunSteps.PickArc(_step);
            if (string.IsNullOrEmpty(arc))
            {
                ClearChips();
                return;
            }
            if (_chips.Count == 0)
            {
                List<SeedPreset> presets = SeedCatalog.ForArc(arc);
                for (int i = 0; i < presets.Count; i++)
                {
                    SeedPreset preset = presets[i];
                    string line = preset.Title + "\n" + preset.Cue;
                    GameObject chip = new GameObject("chip." + preset.Key);
                    chip.transform.SetParent(transform, false);
                    InkLetter.AttachNote(chip.transform, new Vector2(0.20f, 0.052f));
                    var text = MakeText("label", line, 40, 0.00155f, SeedCatalog.PresetId(preset));
                    text.transform.SetParent(chip.transform, false);
                    text.transform.localPosition = new Vector3(0f, 0f, -0.001f);
                    _chips.Add(chip);
                }
            }
            int packet = ArcIndex(arc);
            Vector3 origin = packet >= 0 && _packets[packet] != null
                ? _packets[packet].transform.position
                : transform.position;
            Camera cam = Camera.main;
            Vector3 toward = cam != null ? (cam.transform.position - origin) : Vector3.back;
            toward.y = 0f;
            if (toward.sqrMagnitude < 1e-6f) toward = Vector3.back;
            toward.Normalize();
            for (int i = 0; i < _chips.Count; i++)
            {
                _chips[i].transform.position = origin + Vector3.up * (0.05f + i * 0.056f) + toward * 0.03f;
            }
        }

        void PlaceCaption()
        {
            if (_caption == null || _view == null) return;
            bool show = FirstRunSteps.Index(_step) >= FirstRunSteps.Index(FirstRunSteps.Caption);
            if (_caption.activeSelf != show) _caption.SetActive(show);
            if (!show) return;
            float radius = _view.faceRadius > 0.05f ? _view.faceRadius : 0.15f;
            _caption.transform.localPosition = new Vector3(0f, _view.faceY + 0.028f, radius * 0.15f);
            var mesh = _caption.GetComponent<TextMesh>();
            if (mesh == null) return;
            string full = SeedCatalog.DayCaption;
            if (_step == FirstRunSteps.Caption && _service != null && !_service.ReducedMotion)
            {
                int count = Mathf.Clamp(Mathf.CeilToInt(full.Length * Mathf.Clamp01(_stepTime / CaptionSeconds)), 0, full.Length);
                mesh.text = count <= 0 ? "" : full.Substring(0, count);
            }
            else
            {
                mesh.text = full;
            }
        }

        void PlaceConstruction()
        {
            if (_construction == null || _view == null) return;
            float appear = _view.appear;
            bool show = _running && _step == FirstRunSteps.Appear && appear < 0.55f;
            if (_construction.activeSelf != show) _construction.SetActive(show);
            if (!show) return;
            float radius = _view.faceRadius > 0.05f ? _view.faceRadius : 0.15f;
            _construction.transform.localPosition = new Vector3(0f, _view.faceY + 0.004f, 0f);
            _construction.transform.localRotation = Quaternion.identity;
            _construction.transform.localScale = new Vector3(radius * 2.05f, 1f, radius * 2.05f);
            float fade = appear < 0.33f ? 1f : Mathf.Clamp01(1f - (appear - 0.33f) / 0.22f);
            Tint(_construction, new Color(Pencil.r, Pencil.g, Pencil.b, fade));
        }

        void PlaceGlow()
        {
            if (_glowGo == null || _view == null) return;
            bool show = FirstRunSteps.Index(_step) >= FirstRunSteps.Index(FirstRunSteps.FirstTend);
            if (_glowGo.activeSelf != show) _glowGo.SetActive(show);
            if (!show) return;
            int arc = ArcIndex(DueArc());
            if (arc < 0) arc = 1;
            Transform plant = _view.transform.Find("plant." + ArcName(arc));
            Vector3 pos = plant != null ? plant.position : transform.position;
            pos.y = transform.position.y + _view.faceY + 0.003f;
            _glowGo.transform.position = pos;
            _glowGo.transform.rotation = Quaternion.identity;
            float breathe = _service != null && _service.ReducedMotion ? 1f : 0.85f + 0.15f * Mathf.Sin(_appTime * 2.4f);
            float size = _view.faceRadius * 0.55f * breathe;
            _glowGo.transform.localScale = new Vector3(size, 1f, size);
        }

        void PlaceSeeds()
        {
            bool drop = _step == FirstRunSteps.Drop;
            float u = 1f;
            if (drop && _service != null && !_service.ReducedMotion)
                u = Mathf.Clamp01(_stepTime / DropSeconds);
            for (int i = 0; i < 3; i++)
            {
                if (_seeds[i] == null) continue;
                if (_seeds[i].activeSelf != drop) _seeds[i].SetActive(drop);
                if (!drop) continue;
                Vector3 from = _packets[i] != null ? _packets[i].transform.position : transform.position;
                Transform plant = transform.Find("plant." + ArcName(i));
                Vector3 to = plant != null ? plant.position : transform.position;
                _seeds[i].transform.position = Vector3.Lerp(from, to, u * u * (3f - 2f * u));
                _seeds[i].transform.localScale = Vector3.one * 0.012f;
            }
        }

        void ClearChips()
        {
            for (int i = 0; i < _chips.Count; i++)
            {
                if (_chips[i] != null) Destroy(_chips[i]);
            }
            _chips.Clear();
        }

        GameObject MakeQuad(string name, Mesh mesh, Texture2D texture, Color color, string id)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            if (mesh != null) go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (_card != null) renderer.sharedMaterial = _card;
            Tint(go, color, texture);
            if (id != null)
            {
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(1.1f, 1.1f, 0.2f);
                var target = go.AddComponent<IntentTarget>();
                target.Id = id;
            }
            return go;
        }

        GameObject MakeText(GameObject parent, string name, string text, int fontSize, float characterSize, string id)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent.transform, false);
            else go.transform.SetParent(transform, false);
            return FinishText(go, text, fontSize, characterSize, id);
        }

        GameObject MakeText(string name, string text, int fontSize, float characterSize, string id)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return FinishText(go, text, fontSize, characterSize, id);
        }

        GameObject FinishText(GameObject go, string text, int fontSize, float characterSize, string id)
        {
            var mesh = go.AddComponent<TextMesh>();
            InkLetter.Apply(mesh, text, fontSize, characterSize);
            var renderer = mesh.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            if (id != null)
            {
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(0.16f, 0.03f, 0.01f);
                var target = go.AddComponent<IntentTarget>();
                target.Id = id;
            }
            return go;
        }

        static void Tint(GameObject go, Color color)
        {
            Tint(go, color, null);
        }

        static void Tint(GameObject go, Color color, Texture2D texture)
        {
            if (go == null) return;
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null) return;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            if (texture != null) block.SetTexture("_MainTex", texture);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }

        static void FaceToward(Camera cam, Transform mark, Vector3 world)
        {
            mark.position = world;
            Vector3 to = cam.transform.position - world;
            if (to.sqrMagnitude < 1e-8f) return;
            mark.rotation = Quaternion.LookRotation(to, Vector3.up);
        }

        static void FaceAway(Camera cam, Transform mark, Vector3 world)
        {
            mark.position = world;
            Vector3 away = world - cam.transform.position;
            if (away.sqrMagnitude < 1e-8f) return;
            mark.rotation = Quaternion.LookRotation(away, Vector3.up);
        }

        static Texture2D LoadPng(string file)
        {
            string path = Path.Combine(Application.dataPath, "Art", "Textures", file);
            if (!File.Exists(path)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(path)))
            {
                Destroy(tex);
                return null;
            }
            tex.name = file;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        static Texture2D Solid(Color color)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var pixels = new Color[4];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        static Texture2D Circle(bool soft)
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var pixels = new Color[n * n];
            float mid = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - mid) / mid;
                    float dy = (y - mid) / mid;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha;
                    if (soft) alpha = Mathf.Clamp01(1f - d);
                    else alpha = (d > 0.72f && d < 0.84f) || (d > 0.48f && d < 0.56f) || (d > 0.22f && d < 0.28f) ? 1f : 0f;
                    pixels[y * n + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        static Mesh UprightQuad()
        {
            var mesh = new Mesh { name = "FirstRunUpright" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh FlatQuad()
        {
            var mesh = new Mesh { name = "FirstRunFlat" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, 0.5f),
                new Vector3(-0.5f, 0f, 0.5f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static string ArcName(int index)
        {
            if (index == 0) return "morning";
            if (index == 1) return "midday";
            return "winddown";
        }

        static int ArcIndex(string arc)
        {
            if (arc == "morning") return 0;
            if (arc == "midday") return 1;
            if (arc == "winddown") return 2;
            return -1;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            if (_source != null) _source.Intent -= OnIntent;
            _subscribed = false;
        }
    }
}
