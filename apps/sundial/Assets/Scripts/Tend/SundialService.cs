using System;
using System.Collections.Generic;
using System.IO;
using GardenVR.Core;

namespace GardenVR.Sundial
{
    /// <summary>
    /// Wraps a clock so play time and the dev day offset move together.
    /// A <see cref="FixedClock"/> does not move on its own, so play time is added.
    /// A live clock already moves, so only the fast-clock surplus is added.
    /// </summary>
    public sealed class SteppingClock : IClock
    {
        readonly IClock _inner;
        double _extraSeconds;
        int _dayOffset;

        public float TimeScale = 1f;

        public SteppingClock(IClock inner)
        {
            if (inner == null) throw new ArgumentNullException(nameof(inner));
            _inner = inner;
        }

        public TimeZoneInfo Zone { get { return _inner.Zone ?? TimeZoneInfo.Utc; } }

        public DateTimeOffset Now
        {
            get { return _inner.Now.AddDays(_dayOffset).AddSeconds(_extraSeconds); }
        }

        public void AddDays(int days) { _dayOffset += days; }

        public void Step(float dt)
        {
            if (dt < 0f) dt = 0f;
            if (dt > 0.1f) dt = 0.1f;
            if (_inner is FixedClock) _extraSeconds += dt * TimeScale;
            else if (TimeScale > 1f) _extraSeconds += dt * (TimeScale - 1f);
        }
    }

    /// <summary>
    /// Habits, tends and settings in one save file. The state oracle is rebuilt when the minute or the ledger changes.
    /// A fresh file stays empty so the first run can plant one habit per arc. Tests opt into the old dev seed.
    /// </summary>
    public sealed class SundialService
    {
        public const int SchemaVersion = 1;
        public const string DevSeedStep = "dev-seed";

        /// <summary>
        /// Tests that still want the three dev habits on a fresh file set this before the scene loads.
        /// The player leaves it false, so a fresh file starts the first run instead.
        /// </summary>
        public static bool DevSeedOnFresh;

        /// <summary>Applied to a fresh file's settings before the wizard reads them. Null leaves the default.</summary>
        public static bool? FreshReducedMotion;

        readonly SteppingClock _clock;
        readonly SaveStore<SundialSave> _store;
        readonly string _directory;
        readonly bool _readOnly;
        SundialSave _save;
        Ledger _ledger;
        DeferredTend _deferred;
        string _stateJson = "";

        public LoadOutcome Outcome { get; private set; }
        public bool BackupAvailable { get; private set; }
        public string FailedStep { get; private set; }
        public SundialState State { get; private set; }
        public string StateJson { get { return _stateJson; } }
        public bool StateChanged { get; private set; }
        public Ledger Ledger { get { return _ledger; } }
        public IClock Clock { get { return _clock; } }
        public SundialSave Save { get { return _save; } }
        public string PendingHabitId { get; private set; }
        public bool IsPending { get { return _deferred != null && _deferred.IsPending; } }
        public bool FastClock { get { return _clock.TimeScale > 1.5f; } }
        public bool ReducedMotion { get { return _save != null && _save.Settings != null && _save.Settings.ReducedMotion; } }
        public bool Boil { get { return _save == null || _save.Settings == null || _save.Settings.Boil; } }
        public bool Mute { get { return _save != null && _save.Settings != null && _save.Settings.Mute; } }
        public bool Voice { get { return _save != null && _save.Settings != null && _save.Settings.Voice; } }
        public bool Beds { get { return _save != null && _save.Settings != null && _save.Settings.Beds; } }

        public SundialService(IClock clock, string directory)
        {
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("directory");
            _directory = directory;
            _clock = new SteppingClock(clock);
            _store = new SaveStore<SundialSave>(new DiskSaveIo(directory), SchemaVersion, CreateFresh, SundialCodec.Read, SundialCodec.Write, null);
            LoadResult<SundialSave> result = _store.Load();
            Outcome = result.Outcome;
            BackupAvailable = result.BackupAvailable;
            FailedStep = result.FailedStep;
            _readOnly = result.ReadOnly;
            if (result.Outcome == LoadOutcome.Failed || result.Doc == null)
            {
                _save = CreateFresh();
                _ledger = new Ledger();
            }
            else
            {
                _save = result.Doc;
                if (_save.Settings == null) _save.Settings = new SundialSettings();
                if (_save.Habits == null) _save.Habits = new List<HabitDef>();
                if (_save.Tends == null) _save.Tends = new List<TendEvent>();
                if (result.Outcome == LoadOutcome.Fresh)
                {
                    if (FreshReducedMotion.HasValue) _save.Settings.ReducedMotion = FreshReducedMotion.Value;
                    if (DevSeedOnFresh)
                    {
                        SeedDev(GardenDay.From(_clock.Now, Boundary));
                        _ledger = new Ledger();
                        Persist();
                    }
                    else
                    {
                        _ledger = new Ledger();
                    }
                }
                else
                {
                    _ledger = new Ledger(_save.Tends);
                }
            }
            _deferred = new DeferredTend(_ledger);
            Recompute();
            StateChanged = true;
        }

        public void Step(float dt)
        {
            _clock.Step(dt);
            TendEvent committed = _deferred.Tick(_clock);
            if (committed != null)
            {
                PendingHabitId = null;
                Persist();
            }
            Recompute();
        }

        /// <summary>
        /// Keeps an in-app ritual at once. There is no undo window: the ledger row is written now.
        /// </summary>
        public TendResult TendRitual(string habitId)
        {
            if (string.IsNullOrEmpty(habitId)) throw new ArgumentException("habitId");
            TendResult result = _ledger.Tend(habitId, Today(), TendSource.Ritual, _clock);
            if (result.Ok && !result.AlreadyKept) Persist();
            Recompute();
            return result;
        }

        /// <summary>Remembers the wizard step and writes the file. A failed load writes nothing.</summary>
        public void SetFirstRunStep(string step)
        {
            if (_save == null) return;
            _save.FirstRunStep = step;
            Persist();
        }

        /// <summary>
        /// Plants one preset on an arc. A second pinch on an arc that already has a habit keeps the first.
        /// </summary>
        public HabitDef PlantPreset(SeedPreset preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            if (_save == null) return null;
            if (_save.Habits == null) _save.Habits = new List<HabitDef>();
            string arcKey = SundialArcs.Key(preset.Group);
            HabitDef existing = HabitForArc(arcKey);
            if (existing != null) return existing;
            var habit = new HabitDef
            {
                Id = string.IsNullOrEmpty(preset.HabitId) ? preset.Key : preset.HabitId,
                PresetKey = preset.Key,
                Group = preset.Group,
                Kind = preset.Kind,
                Slot = SundialArcs.Index(arcKey),
                CreatedDay = Today().Index
            };
            _save.Habits.Add(habit);
            Persist();
            Recompute();
            return habit;
        }

        /// <summary>Copies the newest backup over the live file. The caller reloads to read it.</summary>
        public bool TryRestoreBackup()
        {
            if (!BackupAvailable || string.IsNullOrEmpty(_directory)) return false;
            string live = Path.Combine(_directory, "save.json");
            string prev = Path.Combine(_directory, "save.prev1.json");
            if (!File.Exists(prev)) prev = Path.Combine(_directory, "save.prev2.json");
            if (!File.Exists(prev)) prev = Path.Combine(_directory, "save.snapshot.json");
            if (!File.Exists(prev)) return false;
            File.Copy(prev, live, true);
            return true;
        }

        public void SetReducedMotion(bool on) { EditSettings(s => s.ReducedMotion = on); }

        public void SetMute(bool on) { EditSettings(s => s.Mute = on); }

        public void SetVoice(bool on) { EditSettings(s => s.Voice = on); }

        public void SetBeds(bool on) { EditSettings(s => s.Beds = on); }

        void EditSettings(Action<SundialSettings> edit)
        {
            if (_save == null || edit == null) return;
            if (_save.Settings == null) _save.Settings = new SundialSettings();
            edit(_save.Settings);
            Persist();
        }

        public void Arm(string habitId)
        {
            if (string.IsNullOrEmpty(habitId)) throw new ArgumentException("habitId");
            if (_deferred.IsPending) throw new InvalidOperationException("a tend is already waiting to commit");
            _deferred.Arm(habitId, Today(), TendSource.Pinch, _clock);
            PendingHabitId = habitId;
        }

        public bool Cancel()
        {
            if (!_deferred.IsPending) return false;
            _deferred.Undo();
            PendingHabitId = null;
            return true;
        }

        public TendEvent Flush()
        {
            TendEvent committed = _deferred.Flush();
            PendingHabitId = null;
            if (committed != null) Persist();
            Recompute();
            return committed;
        }

        public void ToggleFast()
        {
            _clock.TimeScale = FastClock ? 1f : 60f;
        }

        /// <summary>
        /// Moves the dev day. A step backward is refused when a live tend would land after the new today,
        /// because that would hide a day the ledger already kept.
        /// </summary>
        public bool TryShiftDay(int delta)
        {
            if (delta == 0) return true;
            if (delta < 0)
            {
                GardenDay today = GardenDay.From(_clock.Now.AddDays(delta), Boundary);
                IReadOnlyList<TendEvent> events = _ledger.Events;
                for (int i = 0; i < events.Count; i++)
                {
                    TendEvent ev = events[i];
                    if (ev != null && !ev.UndoneAtUtcMs.HasValue && ev.Day > today.Index)
                        return false;
                }
            }
            _clock.AddDays(delta);
            Recompute();
            return true;
        }

        public HabitDef HabitForArc(string arcKey)
        {
            if (_save == null || _save.Habits == null || string.IsNullOrEmpty(arcKey)) return null;
            for (int i = 0; i < _save.Habits.Count; i++)
            {
                HabitDef habit = _save.Habits[i];
                if (habit == null || habit.ArchivedDay.HasValue) continue;
                if (SundialArcs.Key(habit.Group) == arcKey) return habit;
            }
            return null;
        }

        public PlantState PlantFor(HabitDef habit)
        {
            if (habit == null || State == null || State.Plants == null) return null;
            for (int i = 0; i < State.Plants.Count; i++)
            {
                PlantState plant = State.Plants[i];
                if (plant != null && plant.HabitId == habit.Id) return plant;
            }
            return null;
        }

        /// <summary>
        /// Yesterday can be logged once, before today's boundary. The flag on the plant does not
        /// see the clock, so the boundary is checked here, the same instant <see cref="SundialRules.Backfill"/> uses.
        /// </summary>
        public bool BackfillOffered(HabitDef habit)
        {
            if (habit == null) return false;
            PlantState plant = PlantFor(habit);
            if (plant == null || !plant.CanBackfillYesterday) return false;
            GardenDay today = Today();
            TimeZoneInfo zone = _clock.Zone ?? TimeZoneInfo.Utc;
            return _clock.Now < today.EndsAt(zone, GardenDay.DefaultBoundary);
        }

        /// <summary>Logs yesterday late, once, with the clock's real timestamp. A refusal writes nothing.</summary>
        public TendResult BackfillYesterday(HabitDef habit)
        {
            if (habit == null) throw new ArgumentNullException(nameof(habit));
            TendResult result = SundialRules.Backfill(habit, _ledger, Today(), _clock);
            if (result != null && result.Ok) Persist();
            Recompute();
            return result;
        }

        public int LiveCount(string habitId)
        {
            int count = 0;
            IReadOnlyList<TendEvent> events = _ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                TendEvent ev = events[i];
                if (ev != null && ev.HabitId == habitId && !ev.UndoneAtUtcMs.HasValue) count++;
            }
            return count;
        }

        public GardenDay Today()
        {
            return GardenDay.From(_clock.Now, Boundary);
        }

        TimeSpan Boundary
        {
            get
            {
                int minutes = _save != null && _save.Settings != null ? _save.Settings.BoundaryMin : 180;
                if (minutes < 0 || minutes >= 24 * 60) minutes = 180;
                return TimeSpan.FromMinutes(minutes);
            }
        }

        void SeedDev(GardenDay today)
        {
            _save.Habits = new List<HabitDef>
            {
                Habit("water", "water", "morning", HabitKind.LifeCheckIn, 0, today.Index),
                Habit("top3", "top3", "midday", HabitKind.LifeCheckIn, 1, today.Index),
                Habit("breaths", "breaths", "wind-down", HabitKind.InAppRitual, 2, today.Index)
            };
            _save.FirstRunStep = DevSeedStep;
            _save.HabitExtra = new Dictionary<string, Dictionary<string, JsonValue>>();
        }

        static HabitDef Habit(string id, string preset, string group, HabitKind kind, int slot, int created)
        {
            return new HabitDef
            {
                Id = id,
                PresetKey = preset,
                Group = group,
                Kind = kind,
                Slot = slot,
                CreatedDay = created
            };
        }

        void Persist()
        {
            if (_readOnly || _store == null || Outcome == LoadOutcome.Failed) return;
            _save.SchemaVersion = SchemaVersion;
            var tends = new List<TendEvent>();
            IReadOnlyList<TendEvent> events = _ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i] != null) tends.Add(events[i]);
            }
            _save.Tends = tends;
            _store.Save(_save);
        }

        void Recompute()
        {
            int nowMin = (int)_clock.Now.TimeOfDay.TotalMinutes;
            if (nowMin < 0) nowMin = 0;
            if (nowMin > 1439) nowMin = 1439;
            SundialState next = SundialState.Capture(_save.Habits, _ledger, Today(), nowMin);
            string json = next.ToJson();
            StateChanged = json != _stateJson;
            State = next;
            _stateJson = json;
        }

        static SundialSave CreateFresh()
        {
            return new SundialSave
            {
                SchemaVersion = SchemaVersion,
                Habits = new List<HabitDef>(),
                Tends = new List<TendEvent>(),
                Settings = new SundialSettings(),
                FirstRunStep = null
            };
        }
    }

    /// <summary>Arc keys used by plant and undo target ids.</summary>
    public static class SundialArcs
    {
        public static string Key(string group)
        {
            ArcId arc;
            if (!SundialRules.TryArc(group, out arc)) return null;
            if (arc == ArcId.Morning) return "morning";
            if (arc == ArcId.Midday) return "midday";
            return "winddown";
        }

        public static int Index(string key)
        {
            if (key == "morning") return 0;
            if (key == "midday") return 1;
            if (key == "winddown") return 2;
            return -1;
        }

        public static string FromIndex(int index)
        {
            if (index == 0) return "morning";
            if (index == 1) return "midday";
            if (index == 2) return "winddown";
            return null;
        }

        /// <summary>Shader digit. 0 before, 1 kept, 2 late, 3 missed, 4 today.</summary>
        public static int TileDigit(TileState state)
        {
            switch (state)
            {
                case TileState.Before: return 0;
                case TileState.Kept: return 1;
                case TileState.Late: return 2;
                case TileState.Missed: return 3;
                case TileState.Today: return 4;
                default: return 0;
            }
        }

        public static bool TryPlant(string id, out string arc)
        {
            arc = null;
            if (string.IsNullOrEmpty(id) || !id.StartsWith("plant.", StringComparison.Ordinal)) return false;
            arc = id.Substring(6);
            return Index(arc) >= 0;
        }

        public static bool TryUndo(string id, out string arc)
        {
            arc = null;
            if (string.IsNullOrEmpty(id) || !id.StartsWith("undo.", StringComparison.Ordinal)) return false;
            arc = id.Substring(5);
            return Index(arc) >= 0;
        }
    }
}
