using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GardenVR.Audio;
using GardenVR.Core;
using GardenVR.Input;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Sundial
{
    /// <summary>
    /// Glance and pinch. Look paints the halo after the provider's dwell (scripted looks dwell here).
    /// Pinch arms a deferred tend, plays the tock, pulses the plant and fills today's tile.
    /// The undo mark cancels inside 6 s. Palm open puts the dial away and brings it back,
    /// unless it lands on a morning stretch mark, which the stretch ritual claims.
    /// A dismiss still commits a waiting tend. Focus loss pauses without hiding the dial.
    /// App code never reads a keyboard or a mouse.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public sealed class SundialController : MonoBehaviour
    {
        public const float DwellSeconds = 0.15f;
        public const float SweepSeconds = 1.5f;
        public const float PulseSeconds = 0.7f;
        public const float TileFillSeconds = 0.3f;
        public const string CueTock = "tend.tock";
        public const string CueInk = "tile.ink";
        public const string CueUndo = "tend.undo";
        public const string CueHatch = "tile.hatch";
        public const string CueFlutter = "bloom.flutter";
        public const string CueAppear = "dial.appear";
        public const string CueRoom = "amb.kitchen";
        public const string CuePacket = "packet.open";
        public const string CueSeed = "seed.drop";
        public const string CueFirst = "vo.sun.first.01";
        public const string PromptYesId = "prompt.backfill.yes";
        public const string PromptNoId = "prompt.backfill.no";
        public const string PromptQuestion = "Did it happen?";
        public const string PromptYesLine = "Yes, it happened";
        public const string PromptNoLine = "Not this time";

        public static string SaveDirectoryOverride;
        public static IClock ClockOverride;

        DialView _view;
        SundialService _service;
        FirstRunWizard _wizard;
        SettingsTabs _settings;
        Vector3 _dialHome;
        bool _dialHomeReady;
        IHandIntentSource _source;
        KeyboardMouseIntentSource _keyboard;
        bool _subscribed;
        readonly List<string> _cues = new List<string>();
        AudioCueService _audio;
        readonly ArcBeds _beds = new ArcBeds();
        bool _kitchen;
        bool _dialCue;

        string _dwellId;
        float _dwellTime;
        bool _lookedThisFrame;
        string _lookArc;

        string _pendingArc;
        string _pendingHabitId;
        int _pendingRow;
        bool _filling;
        bool _fillDone;
        float _fillT;
        bool _pulsing;
        float _pulseT;
        int _pulseArc;

        bool _sweeping;
        bool _sweepBegun;
        float _swept;
        bool _readyLogged;
        /// <summary>The first run holds the shadow at 06:00 until the sweep step.</summary>
        public bool HoldSweep;
        /// <summary>A resumed journey shows the shadow where it is, with no replay.</summary>
        public bool SnapSweep;
        readonly int[] _stageFloor = { -1, -1, -1, -1, -1, -1, -1, -1, -1 };

        GameObject _undo;
        readonly GameObject[] _asks = new GameObject[3];
        readonly GameObject[] _extraAsks = new GameObject[6];
        readonly GameObject[] _more = new GameObject[3];
        GameObject _prompt;
        string _promptArc;
        int _promptRow;
        Material _inkMat;
        Texture2D _ring;
        Mesh _quad;
        Mesh _chipQuad;
        GUIStyle _overlayStyle;

        public SundialService Service { get { return _service; } }
        public FirstRunWizard Wizard { get { return _wizard; } }
        public SettingsTabs Settings { get { return _settings; } }
        /// <summary>Palm open has put the dial under the desk. A second palm brings it back.</summary>
        public bool Dismissed { get; private set; }
        public IHandIntentSource Source { get { return _source; } }
        public SundialState State { get { return _service == null ? null : _service.State; } }
        public string StateJson { get { return _service == null ? "" : _service.StateJson; } }
        public Ledger Ledger { get { return _service == null ? null : _service.Ledger; } }
        public DialView View { get { return _view; } }
        public LoadOutcome Outcome { get { return _service == null ? LoadOutcome.Failed : _service.Outcome; } }
        public bool UndoVisible { get { return _undo != null && _undo.activeSelf; } }
        public bool PromptVisible { get { return _prompt != null && _prompt.activeSelf; } }
        public string PromptArc { get { return PromptVisible ? _promptArc : null; } }

        public bool AskVisible(string arc)
        {
            int index = SundialArcs.Index(arc);
            return index >= 0 && _asks[index] != null && _asks[index].activeSelf;
        }
        public bool LookHaloOn { get { return _view != null && _view.halo > 0.5f; } }
        public bool FastClock { get { return _service != null && _service.FastClock; } }
        public bool OverlayVisible { get; private set; }
        public IReadOnlyList<string> Cues { get { return _cues; } }
        public AudioCueService Audio { get { EnsureAudio(); return _audio; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!Application.isPlaying) return;
            DialView view = UnityEngine.Object.FindAnyObjectByType<DialView>();
            if (view == null) return;
            if (UnityEngine.Object.FindAnyObjectByType<SundialController>() == null)
                view.gameObject.AddComponent<SundialController>();
            if (UnityEngine.Object.FindAnyObjectByType<DuskRitualController>() == null)
                view.gameObject.AddComponent<DuskRitualController>();
            if (UnityEngine.Object.FindAnyObjectByType<StretchRitualController>() == null)
                view.gameObject.AddComponent<StretchRitualController>();
            if (UnityEngine.Object.FindAnyObjectByType<GratitudeRitualController>() == null)
                view.gameObject.AddComponent<GratitudeRitualController>();
            if (UnityEngine.Object.FindAnyObjectByType<FocusBlockController>() == null)
                view.gameObject.AddComponent<FocusBlockController>();
            if (UnityEngine.Object.FindAnyObjectByType<WeekDialController>() == null)
                view.gameObject.AddComponent<WeekDialController>();
            if (UnityEngine.Object.FindAnyObjectByType<RimScrubController>() == null)
                view.gameObject.AddComponent<RimScrubController>();
            if (UnityEngine.Object.FindAnyObjectByType<ArcTimesController>() == null)
                view.gameObject.AddComponent<ArcTimesController>();
        }

        public void SetSource(IHandIntentSource source)
        {
            Unsubscribe();
            _source = source;
            _keyboard = source as KeyboardMouseIntentSource;
            if (isActiveAndEnabled) Subscribe();
            RimScrubController rim = GetComponent<RimScrubController>();
            if (rim != null) rim.SetSource(source);
            ArcTimesController arcs = GetComponent<ArcTimesController>();
            if (arcs != null) arcs.SetSource(source);
            if (_wizard != null) _wizard.SetSource(source);
            DuskRitualController dusk = GetComponent<DuskRitualController>();
            if (dusk != null) dusk.SetSource(source);
            StretchRitualController stretch = GetComponent<StretchRitualController>();
            if (stretch != null) stretch.SetSource(source);
            GratitudeRitualController gratitude = GetComponent<GratitudeRitualController>();
            if (gratitude != null) gratitude.SetSource(source);
            FocusBlockController focus = GetComponent<FocusBlockController>();
            if (focus != null) focus.SetSource(source);
            WeekDialController week = GetComponent<WeekDialController>();
            if (week != null) week.SetSource(source);
        }

        /// <summary>
        /// Focus loss. A waiting tend is written. A breath in progress holds still.
        /// A shadow hour pauses and does not fail. The dial stays on the desk.
        /// Palm open is the gesture that puts it away.
        /// </summary>
        public void NotifyFocusLost()
        {
            CommitEarly();
            DuskRitualController dusk = GetComponent<DuskRitualController>();
            if (dusk != null) dusk.NotifyFocusLost();
            FocusBlockController focus = GetComponent<FocusBlockController>();
            if (focus != null) focus.NotifyFocusLost();
        }

        public bool ShiftDay(int delta)
        {
            if (_service == null) return false;
            return _service.TryShiftDay(delta);
        }

        public void HandleDev(DevCommand command)
        {
            if (_service == null) return;
            if (_service.Scrubbing && command != DevCommand.StateOverlay) return;
            if (command == DevCommand.StateOverlay) OverlayVisible = !OverlayVisible;
            else if (command == DevCommand.NextDay) _service.TryShiftDay(1);
            else if (command == DevCommand.PreviousDay) _service.TryShiftDay(-1);
            else if (command == DevCommand.ClockFast) _service.ToggleFast();
        }

        public int LiveCount(string habitId)
        {
            return _service == null ? 0 : _service.LiveCount(habitId);
        }

        public int CueCount(string id)
        {
            int count = 0;
            for (int i = 0; i < _cues.Count; i++)
            {
                if (_cues[i] == id) count++;
            }
            return count;
        }

        void Awake()
        {
            _view = GetComponent<DialView>();
            if (_view == null) _view = UnityEngine.Object.FindAnyObjectByType<DialView>();
            string directory = string.IsNullOrEmpty(SaveDirectoryOverride)
                ? Path.Combine(Application.persistentDataPath, "sundial")
                : SaveDirectoryOverride;
            IClock clock = ClockOverride ?? new SystemClock();
            _service = new SundialService(clock, directory);
            EnsureAudio();
            PushAudio();
            if (GetComponent<FirstRunWizard>() == null)
                _wizard = gameObject.AddComponent<FirstRunWizard>();
            else
                _wizard = GetComponent<FirstRunWizard>();
            if (GetComponent<SettingsTabs>() == null)
                _settings = gameObject.AddComponent<SettingsTabs>();
            else
                _settings = GetComponent<SettingsTabs>();
            RememberDialHome();
            if (_view != null)
            {
                _view.halo = 0f;
                PushView();
                BeginSweep();
                _view.Apply();
            }
        }

        void Start()
        {
            if (!Application.isPlaying) return;
            if (_source == null)
            {
                KeyboardMouseIntentSource keyboard = UnityEngine.Object.FindAnyObjectByType<KeyboardMouseIntentSource>();
                if (keyboard != null) SetSource(keyboard);
            }
            BeginSweep();
        }

        void OnEnable() { Subscribe(); }

        void OnDisable() { Unsubscribe(); }

        void OnApplicationQuit() { CommitEarly(); }

        void OnDestroy()
        {
            CommitEarly();
            if (_inkMat != null) Destroy(_inkMat);
            if (_ring != null) Destroy(_ring);
            if (_quad != null) Destroy(_quad);
            if (_chipQuad != null) Destroy(_chipQuad);
        }

        void Update()
        {
            if (!Application.isPlaying || _service == null || _view == null) return;
            if (!_readyLogged)
            {
                _readyLogged = true;
                string ready = Time.realtimeSinceStartup.ToString("0.000", CultureInfo.InvariantCulture);
                Debug.Log("[Sundial] first-interactive t=" + ready);
                if (!Application.isEditor)
                {
                    try
                    {
                        string folder = Path.GetDirectoryName(Application.dataPath);
                        if (!string.IsNullOrEmpty(folder))
                            File.WriteAllText(Path.Combine(folder, "first-interactive.txt"), ready);
                    }
                    catch (Exception)
                    {
                        // The player log line is the measurement. A locked folder must not stop the dial.
                    }
                }
            }
            float dt = ClampStep(Time.deltaTime);
            _service.Step(dt);
            if (_pendingArc != null && !_service.IsPending)
                ClearTendVisual();

            if (!(_source is KeyboardMouseIntentSource))
            {
                if (_lookedThisFrame) _dwellTime += dt;
                if (_lookedThisFrame && _dwellTime >= DwellSeconds)
                    ShowLookHalo(_dwellId);
            }

            UpdatePulse(dt);
            UpdateFill(dt);
            PushView();
            UpdateAudio();
            RefreshAsks();
            RefreshMore();
            UpdateSweep(dt);
            _view.time += dt;
            _view.Apply();
        }

        void LateUpdate()
        {
            if (!_lookedThisFrame)
            {
                _dwellId = null;
                _dwellTime = 0f;
            }
            _lookedThisFrame = false;
            PlaceRecordMarks();
        }

        void OnGUI()
        {
            if (!OverlayVisible || _service == null) return;
            if (_overlayStyle == null)
            {
                _overlayStyle = new GUIStyle(GUI.skin.label);
                _overlayStyle.fontSize = 14;
                _overlayStyle.wordWrap = true;
                _overlayStyle.normal.textColor = new Color(0.16f, 0.15f, 0.13f, 1f);
                Font hand = InkLetter.Hand;
                if (hand != null) _overlayStyle.font = hand;
            }
            GUI.Label(new Rect(16f, 16f, 920f, 420f), _service.StateJson + "\n" + TileCaption(), _overlayStyle);
        }

        string TileCaption()
        {
            if (_service == null || _service.Save == null || _service.Save.Habits == null) return "";
            var text = new StringBuilder();
            for (int i = 0; i < _service.Save.Habits.Count; i++)
            {
                HabitDef habit = _service.Save.Habits[i];
                PlantState plant = _service.PlantFor(habit);
                if (habit == null || plant == null || plant.Window == null) continue;
                if (text.Length > 0) text.Append('\n');
                text.Append(habit.Id);
                text.Append(": ");
                int count = plant.Window.Length < 7 ? plant.Window.Length : 7;
                for (int slot = 0; slot < count; slot++)
                {
                    if (slot > 0) text.Append(' ');
                    text.Append(TileLabels.For(plant.Window[slot]));
                }
            }
            return text.ToString();
        }

        void OnIntent(HandIntent intent)
        {
            if (_service != null && _service.Scrubbing)
            {
                if (intent.Kind == HandIntentKind.Look) OnLook(intent.TargetId);
                return;
            }
            if (intent.Kind == HandIntentKind.Look) OnLook(intent.TargetId);
            else if (intent.Kind == HandIntentKind.Pinch) OnPinch(intent.TargetId);
            else if (intent.Kind == HandIntentKind.Poke) OnPoke(intent.TargetId);
            else if (intent.Kind == HandIntentKind.PalmOpen)
            {
                StretchRitualController stretch = GetComponent<StretchRitualController>();
                if (stretch != null && stretch.ClaimsPalm(intent)) return;
                CommitEarly();
                SetDismissed(!Dismissed);
            }
        }

        void OnLook(string id)
        {
            _lookedThisFrame = true;
            if (_source is KeyboardMouseIntentSource)
            {
                ShowLookHalo(id);
                return;
            }
            if (id != _dwellId)
            {
                _dwellId = id;
                _dwellTime = 0f;
            }
        }

        void ShowLookHalo(string id)
        {
            string arc;
            int row;
            if (!SundialArcs.TryPlant(id, out arc, out row) || _view == null) return;
            int slot = SundialArcs.Index(arc) * 3 + row;
            if (_lookArc == arc && _view.haloSlot == slot && _view.halo > 0.5f) return;
            _lookArc = arc;
            _view.halo = 1f;
            _view.haloTarget = arc;
            _view.haloSlot = slot;
        }

        void OnPoke(string id)
        {
            if (_settings != null && _settings.HandlePoke(id)) return;
            TryBackfillGesture(id);
        }

        void RememberDialHome()
        {
            if (_dialHomeReady) return;
            _dialHome = transform.localPosition;
            _dialHomeReady = true;
        }

        void SetDismissed(bool away)
        {
            RememberDialHome();
            Dismissed = away;
            transform.localPosition = away ? _dialHome + new Vector3(0f, -3f, 0f) : _dialHome;
            if (_wizard != null) _wizard.Held = away;
        }

        void OnPinch(string id)
        {
            if (_wizard != null && _wizard.Handles(id)) return;
            if (TryBackfillGesture(id)) return;
            string undoArc;
            if (SundialArcs.TryUndo(id, out undoArc))
            {
                TryUndo(undoArc);
                return;
            }
            if (TryAnother(id)) return;
            string arc;
            int row;
            if (!SundialArcs.TryPlant(id, out arc, out row)) return;
            if (_wizard != null && !_wizard.AllowsPlantTend(arc)) return;
            HabitDef habit = _service.HabitAt(arc, row);
            PlantState plant = _service.PlantFor(habit);
            if (habit == null || plant == null || plant.Window == null || plant.Window.Length < 7) return;
            // An in-app ritual, including the wind-down breaths, is not a quick pinch.
            if (habit.Kind == HabitKind.InAppRitual) return;
            // A second pinch the same day, or a pinch while one tend is still waiting, leaves the picture alone.
            if (_service.IsPending) return;
            if (plant.Window[6] != TileState.Today) return;

            _service.Arm(habit.Id);
            Play(CueTock, At(SundialArcs.PlantId(arc, row)));
            Play(CueInk, At(SundialArcs.TileId(arc, row, 6)));
            Play(CueFlutter, At(SundialArcs.PlantId(arc, row)));
            _pendingArc = arc;
            _pendingRow = row;
            _pendingHabitId = habit.Id;
            _pulseArc = SundialArcs.Index(arc);
            _pulsing = true;
            _pulseT = 0f;
            _view.pulse = 0.001f;
            _view.pulseArc = _pulseArc;
            _view.pulseSlot = _pulseArc * 3 + row;
            _fillT = 0f;
            _fillDone = _service.ReducedMotion;
            _filling = !_service.ReducedMotion;
            ShowUndo(arc, row);
        }

        bool TryAnother(string id)
        {
            if (string.IsNullOrEmpty(id) || !id.StartsWith("more.", StringComparison.Ordinal)) return false;
            string arc = id.Substring("more.".Length);
            if (SundialArcs.Index(arc) < 0) return false;
            if (_wizard != null && _wizard.Running) return true;
            if (_view != null && _view.weekPage) return true;
            if (_service == null || _service.Save == null) return true;
            SeedPreset preset = SeedCatalog.NextFree(arc, _service.Save.Habits);
            if (preset == null) return true;
            HabitDef planted = _service.TryAdmit(preset);
            if (planted != null) Play(CueSeed, At(SundialArcs.PlantId(arc, planted.Row)));
            return true;
        }

        void TryUndo(string arc)
        {
            if (!_service.IsPending || _pendingArc != arc) return;
            if (!_service.Cancel()) return;
            Play(CueUndo, At(SundialArcs.TileId(arc, _pendingRow, 6)));
            ClearTendVisual();
        }

        void CommitEarly()
        {
            if (_service == null) return;
            _service.Flush();
            ClearTendVisual();
        }

        void ClearTendVisual()
        {
            _pendingArc = null;
            _pendingHabitId = null;
            _pendingRow = 0;
            _filling = false;
            _fillDone = false;
            _pulsing = false;
            if (_view != null) _view.pulse = 0f;
            HideUndo();
        }

        void UpdatePulse(float dt)
        {
            if (!_pulsing || _view == null) return;
            _pulseT += dt;
            float u = Mathf.Clamp01(_pulseT / PulseSeconds);
            _view.pulse = u <= 0f ? 0.001f : u;
            _view.pulseArc = _pulseArc;
            if (u >= 1f)
            {
                _pulsing = false;
                _view.pulse = 0f;
            }
        }

        void UpdateFill(float dt)
        {
            if (!_filling) return;
            _fillT += dt;
            if (_fillT >= TileFillSeconds)
            {
                _filling = false;
                _fillDone = true;
            }
        }

        /// <summary>The sweep step calls this. A held sweep stays at 06:00 until then.</summary>
        public void ReleaseSweep()
        {
            HoldSweep = false;
            _sweepBegun = false;
            BeginSweep();
        }

        void BeginSweep()
        {
            if (HoldSweep || _sweepBegun || _view == null || _service == null || _service.State == null) return;
            _sweepBegun = true;
            _swept = 0f;
            if (_wizard == null || !_wizard.Running)
                NotifyDialAppear();
            if (_service.ReducedMotion || SnapSweep)
            {
                _sweeping = false;
                _view.gnomonDeg = _service.State.GnomonDeg;
            }
            else
            {
                _sweeping = true;
                _view.gnomonDeg = 0f;
            }
        }

        void UpdateSweep(float dt)
        {
            if (_view == null || _service == null || _service.State == null) return;
            float target = _service.State.GnomonDeg;
            if (HoldSweep)
            {
                _view.gnomonDeg = 0f;
                return;
            }
            if (!_sweeping)
            {
                _view.gnomonDeg = target;
                return;
            }
            _swept += dt;
            float u = Mathf.Clamp01(_swept / SweepSeconds);
            _view.gnomonDeg = Mathf.Lerp(0f, target, u);
            if (u >= 1f)
            {
                _sweeping = false;
                _view.gnomonDeg = target;
            }
        }

        void PushView()
        {
            SundialState state = _service.State;
            if (state == null) return;
            _view.EnsureTiles();
            _view.EnsurePlantArrays();
            if (_view.tiles == null || _view.tiles.Length != DialView.TileCount)
                _view.tiles = new int[DialView.TileCount];
            if (_view.tileFill == null || _view.tileFill.Length != DialView.TileCount)
                _view.tileFill = new float[DialView.TileCount];
            for (int i = 0; i < _view.tiles.Length; i++)
            {
                _view.tiles[i] = DialView.HiddenTile;
                _view.tileFill[i] = 1f;
            }
            for (int i = 0; i < _view.plantOn.Length; i++)
            {
                _view.plantOn[i] = false;
                _view.plantDue[i] = false;
                _view.plantSpecies[i] = -1;
            }

            int due = -1;
            if (_service.Save != null && _service.Save.Habits != null)
            {
                for (int i = 0; i < _service.Save.Habits.Count; i++)
                {
                    HabitDef habit = _service.Save.Habits[i];
                    PlantState plant = _service.PlantFor(habit);
                    if (habit == null || plant == null || plant.Window == null) continue;
                    int arc = SundialArcs.Index(SundialArcs.Key(habit.Group));
                    int row = SundialRules.RowOf(_service.Save.Habits, habit);
                    if (arc < 0 || row < 0) continue;
                    int plantIndex = arc * 3 + row;
                    int stage = (int)plant.Stage;
                    // A miss never shrinks the drawing. The card stays on the fullest stage this session has shown.
                    if (_stageFloor[plantIndex] < 0 || stage > _stageFloor[plantIndex]) _stageFloor[plantIndex] = stage;
                    else stage = _stageFloor[plantIndex];
                    float bloom = plant.Bloom == Bloom.Open ? 2f : plant.Bloom == Bloom.Bud ? 1f : 0f;
                    _view.plantStage[plantIndex] = stage;
                    _view.plantOn[plantIndex] = true;
                    _view.plantDue[plantIndex] = plant.DueNow;
                    _view.plantSpecies[plantIndex] = SundialSpecies.Index(habit.Species);
                    if (row == 0)
                    {
                        if (arc == 0) { _view.stageMorning = stage; _view.bloomMorning = bloom; }
                        else if (arc == 1) { _view.stageMidday = stage; _view.bloomMidday = bloom; }
                        else { _view.stageWinddown = stage; _view.bloomWinddown = bloom; }
                    }
                    int count = plant.Window.Length < 7 ? plant.Window.Length : 7;
                    for (int day = 0; day < count; day++)
                        _view.tiles[DialView.TileIndex(arc, row, day)] = SundialArcs.TileDigit(plant.Window[day]);
                    if (_pendingHabitId != null && habit.Id == _pendingHabitId && (_filling || _fillDone))
                    {
                        int today = DialView.TileIndex(arc, row, 6);
                        _view.tiles[today] = SundialArcs.TileDigit(TileState.Kept);
                        float flood = _service.ReducedMotion ? 1f : Mathf.Clamp01(_fillT / TileFillSeconds);
                        _view.tileFill[today] = _fillDone ? 1f : flood;
                    }
                    if (plant.DueNow) due = arc;
                }
            }
            _view.waiting = due >= 0 ? 1f : 0f;
            if (due >= 0) _view.waitingTarget = SundialArcs.FromIndex(due);
            // Reduced motion stops the boil. The saved boil flag stays, so turning it off brings the line back.
            _view.boil = _service.Boil && !_service.ReducedMotion;
            _view.reducedMotion = _service.ReducedMotion;
        }

        /// <summary>Turns the guide and the arc beds on for a mixdown run. Both stay off until the save says otherwise.</summary>
        public void UseVoiceAndBeds()
        {
            if (_service == null) return;
            _service.SetVoice(true);
            _service.SetBeds(true);
            UpdateAudio();
        }

        /// <summary>The dial drawing itself. Once per session. The first run calls this when the page appears.</summary>
        public void NotifyDialAppear()
        {
            if (_dialCue) return;
            _dialCue = true;
            Play(CueAppear, transform);
        }

        /// <summary>The first-run line, and only when the voice guide is on. The ink caption is already on the page.</summary>
        public void PlayFirstRunLine()
        {
            if (_service == null || !_service.Voice) return;
            Play(CueFirst, null);
        }

        public bool Play(string id, Transform at)
        {
            if (string.IsNullOrEmpty(id)) return false;
            PushAudio();
            bool played = _audio != null && _audio.Play(id, at, 0f);
            if (played || Remember(id))
            {
                _cues.Add(id);
                Debug.Log("[Sundial] cue " + id);
            }
            return played;
        }

        void EnsureAudio()
        {
            if (_audio != null) return;
            _audio = GetComponent<AudioCueService>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioCueService>();
        }

        void PushAudio()
        {
            if (_service == null) return;
            EnsureAudio();
            if (_audio == null) return;
            if (_audio.Mute != _service.Mute) _audio.Mute = _service.Mute;
            _audio.VoiceGuide = _service.Voice;
            _audio.Beds = _service.Beds;
        }

        void UpdateAudio()
        {
            PushAudio();
            if (_audio == null || _service == null) return;
            if (!_kitchen && !_service.Mute && _audio.Play(CueRoom, null, 0f))
            {
                _kitchen = true;
                _cues.Add(CueRoom);
                Debug.Log("[Sundial] cue " + CueRoom);
            }
            ArcId? arc = _service.State == null ? (ArcId?)null : _service.State.Arc;
            string bed = _beds.Sync(_audio, _service.Beds && !_service.Mute, arc);
            if (!string.IsNullOrEmpty(bed))
                Play(bed, null);
        }

        static bool Remember(string id)
        {
            if (id == CueRoom) return false;
            if (id.StartsWith("bed.", StringComparison.Ordinal)) return false;
            return true;
        }

        Transform At(string name)
        {
            Transform found = FindNamed(name);
            return found != null ? found : transform;
        }

        void ShowUndo(string arc, int row)
        {
            HideUndo();
            Transform tile = FindNamed(SundialArcs.TileId(arc, row, 6));
            _undo = MakeMark("undo." + arc, _ring, Color.white, 0.012f);
            var box = _undo.AddComponent<BoxCollider>();
            box.size = new Vector3(0.016f, 0.012f, 0.016f);
            var target = _undo.AddComponent<IntentTarget>();
            target.Id = "undo." + arc;
            if (tile != null)
            {
                _undo.transform.position = tile.position + Vector3.up * 0.008f;
                _undo.transform.rotation = tile.rotation;
            }
        }

        GameObject MakeMark(string name, Texture2D texture, Color color, float size)
        {
            EnsureInk();
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            if (_quad == null || _inkMat == null) return go;
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _inkMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var block = new MaterialPropertyBlock();
            if (texture != null) block.SetTexture("_MainTex", texture);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
            go.transform.localScale = new Vector3(size, 1f, size);
            return go;
        }

        void EnsureInk()
        {
            if (_inkMat != null) return;
            Shader shader = Shader.Find("Fidelity/Card");
            if (shader == null) return;
            _inkMat = new Material(shader);
            _inkMat.SetFloat("_Src", 5f);
            _inkMat.SetFloat("_Dst", 10f);
            _inkMat.SetFloat("_Ring", 0f);
            _inkMat.SetFloat("_Coverage", 0f);
            _inkMat.SetFloat("_Mask", 0f);
            _ring = Ring();
            _quad = FlatQuad();
        }

        static Texture2D Ring()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var pixels = new Color[n * n];
            var ink = new Color(0.165f, 0.149f, 0.133f, 1f);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = x - (n - 1) * 0.5f;
                    float dy = y - (n - 1) * 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    pixels[y * n + x] = d > 8f && d < 13f ? ink : new Color(0f, 0f, 0f, 0f);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        static Mesh FlatQuad()
        {
            var mesh = new Mesh { name = "TendMark" };
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

        void HideUndo()
        {
            if (_undo == null) return;
            _undo.SetActive(false);
            Destroy(_undo);
            _undo = null;
        }

        bool TryBackfillGesture(string id)
        {
            if (id == PromptYesId && PromptVisible)
            {
                ConfirmBackfill();
                return true;
            }
            if (id == PromptNoId && PromptVisible)
            {
                ClosePrompt();
                return true;
            }
            string arc;
            int row;
            if (!TryAsk(id, out arc, out row)) return false;
            OpenPrompt(arc, row);
            return true;
        }

        static bool TryAsk(string id, out string arc, out int row)
        {
            arc = null;
            row = 0;
            const string marker = ".yesterday.ask";
            if (string.IsNullOrEmpty(id) || !id.StartsWith("tile.", StringComparison.Ordinal) || !id.EndsWith(marker, StringComparison.Ordinal))
                return false;
            string middle = id.Substring("tile.".Length, id.Length - "tile.".Length - marker.Length);
            if (middle.EndsWith(".", StringComparison.Ordinal))
                middle = middle.Substring(0, middle.Length - 1);
            int dot = middle.IndexOf('.');
            string arcPart = dot < 0 ? middle : middle.Substring(0, dot);
            if (SundialArcs.Index(arcPart) < 0) return false;
            arc = arcPart;
            if (dot < 0) return true;
            string tail = middle.Substring(dot + 1);
            if (tail.Length == 2 && tail[0] == 'r' && (tail[1] == '1' || tail[1] == '2'))
            {
                row = tail[1] - '0';
                return true;
            }
            return false;
        }

        void OpenPrompt(string arc, int row)
        {
            if (_service == null || string.IsNullOrEmpty(arc)) return;
            HabitDef habit = row <= 0 ? _service.HabitForArc(arc) : _service.HabitAt(arc, row);
            if (!_service.BackfillOffered(habit)) return;
            EnsurePrompt();
            _promptArc = arc;
            _promptRow = row;
            if (_prompt != null) _prompt.SetActive(true);
        }

        void ClosePrompt()
        {
            _promptArc = null;
            _promptRow = 0;
            if (_prompt != null) _prompt.SetActive(false);
        }

        void ConfirmBackfill()
        {
            string arc = _promptArc;
            int row = _promptRow;
            HabitDef habit = null;
            if (_service != null && !string.IsNullOrEmpty(arc))
                habit = row <= 0 ? _service.HabitForArc(arc) : _service.HabitAt(arc, row);
            TendResult result = habit == null ? null : _service.BackfillYesterday(habit);
            ClosePrompt();
            if (result != null && result.Ok) Play(CueHatch, At(SundialArcs.TileId(arc, row, 5)));
        }

        void RefreshAsks()
        {
            if (_service == null) return;
            for (int arc = 0; arc < 3; arc++)
            {
                string key = SundialArcs.FromIndex(arc);
                for (int row = 0; row < 3; row++)
                {
                    HabitDef habit = row == 0 ? _service.HabitForArc(key) : _service.HabitAt(key, row);
                    bool show = _service.BackfillOffered(habit);
                    GameObject ask = AskObject(arc, row);
                    if (show && ask == null)
                    {
                        ask = MakeAsk(key, row);
                        SetAsk(arc, row, ask);
                    }
                    ask = AskObject(arc, row);
                    if (ask == null) continue;
                    if (ask.activeSelf != show) ask.SetActive(show);
                }
            }
            HabitDef prompted = null;
            if (!string.IsNullOrEmpty(_promptArc))
                prompted = _promptRow <= 0 ? _service.HabitForArc(_promptArc) : _service.HabitAt(_promptArc, _promptRow);
            if (PromptVisible && !_service.BackfillOffered(prompted))
                ClosePrompt();
        }

        GameObject AskObject(int arc, int row)
        {
            if (row <= 0) return _asks[arc];
            return _extraAsks[arc * 2 + (row - 1)];
        }

        void SetAsk(int arc, int row, GameObject ask)
        {
            if (row <= 0) _asks[arc] = ask;
            else _extraAsks[arc * 2 + (row - 1)] = ask;
        }

        void PlaceRecordMarks()
        {
            if (!Application.isPlaying) return;
            Camera cam = Camera.main;
            for (int arc = 0; arc < 3; arc++)
            {
                for (int row = 0; row < 3; row++)
                {
                    GameObject ask = AskObject(arc, row);
                    if (ask == null || !ask.activeSelf) continue;
                    Transform tile = FindNamed(SundialArcs.TileId(SundialArcs.FromIndex(arc), row, 5));
                    Vector3 pos = tile != null ? tile.position + Vector3.up * 0.008f : transform.position;
                    FaceCamera(ask.transform, pos, cam);
                }
            }
            PlaceMoreMarks(cam);
            if (_prompt == null || !_prompt.activeSelf || string.IsNullOrEmpty(_promptArc)) return;
            Transform yesterday = FindNamed(SundialArcs.TileId(_promptArc, _promptRow, 5));
            Vector3 at = yesterday != null ? yesterday.position + Vector3.up * 0.034f : transform.position + Vector3.up * 0.05f;
            FaceCamera(_prompt.transform, at, cam);
        }

        static void FaceCamera(Transform mark, Vector3 worldPos, Camera cam)
        {
            mark.position = worldPos;
            if (cam == null) return;
            Vector3 away = worldPos - cam.transform.position;
            if (away.sqrMagnitude < 1e-8f) return;
            mark.rotation = Quaternion.LookRotation(away, Vector3.up);
        }

        GameObject MakeAsk(string arc, int row)
        {
            string name = row <= 0
                ? "tile." + arc + ".yesterday.ask"
                : "tile." + arc + ".r" + row + ".yesterday.ask";
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            EnsureInk();
            var mark = new GameObject("mark");
            mark.transform.SetParent(go.transform, false);
            mark.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            mark.transform.localPosition = new Vector3(0f, 0f, 0.001f);
            mark.transform.localScale = new Vector3(0.016f, 0.016f, 1f);
            if (_chipQuad == null) _chipQuad = VerticalQuad();
            mark.AddComponent<MeshFilter>().sharedMesh = _chipQuad;
            var renderer = mark.AddComponent<MeshRenderer>();
            if (_inkMat != null) renderer.sharedMaterial = _inkMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            InkLetter.Tint(renderer, InkLetter.AskMark(), Color.white);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.018f, 0.018f, 0.008f);
            var target = go.AddComponent<IntentTarget>();
            target.Id = go.name;
            return go;
        }

        void RefreshMore()
        {
            if (_service == null || _service.Save == null) return;
            for (int arc = 0; arc < 3; arc++)
            {
                string key = SundialArcs.FromIndex(arc);
                bool show = ShowMore(key);
                if (show && _more[arc] == null) _more[arc] = MakeMore(key);
                if (_more[arc] == null) continue;
                if (_more[arc].activeSelf != show) _more[arc].SetActive(show);
            }
        }

        bool ShowMore(string arc)
        {
            if (_wizard != null && _wizard.Running) return false;
            if (_view != null && _view.weekPage) return false;
            if (_service.Scrubbing || Dismissed) return false;
            int live = _service.LiveInArc(arc);
            if (live < 1 || live >= SundialRules.MaxHabitsPerArc) return false;
            return SeedCatalog.NextFree(arc, _service.Save.Habits) != null;
        }

        GameObject MakeMore(string arc)
        {
            var go = new GameObject("more." + arc);
            go.transform.SetParent(transform, false);
            EnsureInk();
            var paper = new GameObject("chip");
            paper.transform.SetParent(go.transform, false);
            paper.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            paper.transform.localScale = new Vector3(0.046f, 0.016f, 1f);
            if (_chipQuad == null) _chipQuad = VerticalQuad();
            paper.AddComponent<MeshFilter>().sharedMesh = _chipQuad;
            var renderer = paper.AddComponent<MeshRenderer>();
            if (_inkMat != null) renderer.sharedMaterial = _inkMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            InkLetter.Tint(renderer, InkLetter.Note(0.046f / 0.016f), Color.white);
            var label = new GameObject("label");
            label.transform.SetParent(go.transform, false);
            label.transform.localPosition = new Vector3(0f, 0f, 0.001f);
            AddInkLine(label, "Another", 42, 0.0022f);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.05f, 0.018f, 0.008f);
            var target = go.AddComponent<IntentTarget>();
            target.Id = go.name;
            return go;
        }

        void PlaceMoreMarks(Camera cam)
        {
            for (int arc = 0; arc < 3; arc++)
            {
                GameObject chip = _more[arc];
                if (chip == null || !chip.activeSelf) continue;
                Transform plant = FindNamed(SundialArcs.PlantId(SundialArcs.FromIndex(arc), 0));
                Vector3 pos = plant != null
                    ? plant.position + Vector3.up * (plant.lossyScale.y * 1.05f)
                    : transform.position + Vector3.up * 0.08f;
                FaceCamera(chip.transform, pos, cam);
            }
        }

        void EnsurePrompt()
        {
            if (_prompt != null) return;
            EnsureInk();
            _prompt = new GameObject("BackfillPrompt");
            _prompt.transform.SetParent(transform, false);

            var paper = new GameObject("chip");
            paper.transform.SetParent(_prompt.transform, false);
            paper.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            paper.transform.localPosition = new Vector3(0f, 0f, 0.002f);
            paper.transform.localScale = new Vector3(0.132f, 0.078f, 1f);
            if (_chipQuad == null) _chipQuad = VerticalQuad();
            paper.AddComponent<MeshFilter>().sharedMesh = _chipQuad;
            var paperRenderer = paper.AddComponent<MeshRenderer>();
            paperRenderer.shadowCastingMode = ShadowCastingMode.Off;
            paperRenderer.receiveShadows = false;
            if (_inkMat != null) paperRenderer.sharedMaterial = _inkMat;
            InkLetter.Tint(paperRenderer, InkLetter.Note(0.132f / 0.078f), Color.white);

            Line(_prompt.transform, "BackfillQuestion", PromptQuestion, null, 0.016f);
            Line(_prompt.transform, PromptYesId, PromptYesLine, PromptYesId, 0f);
            Line(_prompt.transform, PromptNoId, PromptNoLine, PromptNoId, -0.016f);
            _prompt.SetActive(false);
        }

        void Line(Transform parent, string name, string text, string id, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            AddInkLine(go, text, 46, 0.0025f);
            if (id == null) return;
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.09f, 0.014f, 0.008f);
            var target = go.AddComponent<IntentTarget>();
            target.Id = id;
        }

        TextMesh AddInkLine(GameObject go, string text, int fontSize, float characterSize)
        {
            var mesh = go.GetComponent<TextMesh>();
            if (mesh == null) mesh = go.AddComponent<TextMesh>();
            InkLetter.Apply(mesh, text, fontSize, characterSize);
            return mesh;
        }

        static Mesh VerticalQuad()
        {
            var mesh = new Mesh { name = "PaperChip" };
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

        Transform FindNamed(string name)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name == name) return child;
            }
            return null;
        }

        void Subscribe()
        {
            if (_source == null || _subscribed) return;
            _source.Intent += OnIntent;
            _source.SystemPause += OnSystemPause;
            if (_keyboard != null) _keyboard.DevCommandRaised += HandleDev;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            if (_source != null)
            {
                _source.Intent -= OnIntent;
                _source.SystemPause -= OnSystemPause;
            }
            if (_keyboard != null) _keyboard.DevCommandRaised -= HandleDev;
            _subscribed = false;
        }

        void OnSystemPause() { NotifyFocusLost(); }

        static float ClampStep(float dt)
        {
            if (dt < 0f) return 0f;
            if (dt > 0.1f) return 0.1f;
            return dt;
        }
    }
}
