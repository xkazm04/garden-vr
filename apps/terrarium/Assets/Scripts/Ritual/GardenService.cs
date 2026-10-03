using System;
using System.Collections.Generic;
using System.IO;
using GardenVR.Core;
using UnityEngine;

namespace GardenVR.Terrarium
{
    /// <summary>
    /// The garden, the clock, and the save file. Tests inject <see cref="ClockOverride"/> and
    /// <see cref="DirectoryOverride"/> before the scene loads. A failed load never becomes a first run.
    /// </summary>
    public sealed class GardenService
    {
        public const string SaveFolderName = "terrarium";

        public static string DirectoryOverride;
        public static IClock ClockOverride;
        /// <summary>Tests set this before the scene loads. A fresh garden otherwise starts with motion on.</summary>
        public static bool? ReducedMotionOverride;

        public static void ResetOverrides()
        {
            DirectoryOverride = null;
            ClockOverride = null;
            ReducedMotionOverride = null;
        }

        public static string DefaultDirectory()
        {
            return Path.Combine(Application.persistentDataPath, SaveFolderName);
        }

        readonly IClock _clock;
        readonly SaveStore<TerrariumSave> _store;
        readonly string _directory;
        int _devDays;
        double _habitSeconds;
        bool _readOnly;
        TerrariumSave _save;
        CompanionHabits _habits;

        public LoadOutcome Outcome { get; private set; }
        public bool ReadOnly { get { return _readOnly; } }
        public bool RestoreOffered { get { return Outcome == LoadOutcome.Failed; } }
        public bool BackupAvailable { get; private set; }
        public Garden Garden { get; private set; }
        public TerrariumSave Document { get { return _save; } }
        public IClock Clock { get { return _clock; } }

        public RitualSettings Settings
        {
            get { return _save != null && _save.Settings != null ? _save.Settings : new RitualSettings(); }
        }

        public DateTimeOffset Now
        {
            get { return _clock.Now.AddDays(_devDays); }
        }

        public int TodayIndex
        {
            get { return GardenDay.From(Now, GardenDay.DefaultBoundary).Index; }
        }

        public GardenService(IClock clock, string directory)
        {
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("directory");
            _clock = clock;
            _directory = directory;
            _store = new SaveStore<TerrariumSave>(
                new DiskSaveIo(directory),
                TerrariumSaveMigrations.CurrentSchema,
                () => new TerrariumSave(),
                TerrariumSave.Read,
                TerrariumSave.Write,
                TerrariumSaveMigrations.Steps);
            LoadResult<TerrariumSave> result = _store.Load();
            Outcome = result.Outcome;
            _readOnly = result.ReadOnly;
            BackupAvailable = result.BackupAvailable;
            if (result.Outcome == LoadOutcome.Failed || result.Doc == null)
            {
                _save = new TerrariumSave();
                Garden = new Garden();
                BindHabits();
                ApplyMotionOverride();
                return;
            }
            _save = result.Doc;
            if (_save.Settings == null) _save.Settings = new RitualSettings();
            if (_save.Habits == null) _save.Habits = new List<HabitDef>();
            if (_save.Tends == null) _save.Tends = new List<TendEvent>();
            if (_save.FrondDays == null) _save.FrondDays = new int[0];
            Garden = Garden.FromSave(_save);
            BindHabits();
            ApplyMotionOverride();
        }

        /// <summary>
        /// Remembers the tour id. The file is created when the jar arrives, not during the fade.
        /// A failed or newer-schema load does not touch the file.
        /// </summary>
        public void SetFirstRunStep(string step)
        {
            if (_save == null || _readOnly || Outcome == LoadOutcome.Failed) return;
            if (step == _save.FirstRunStep) return;
            _save.FirstRunStep = step;
            if (FirstRunSteps.AtLeast(step, FirstRunSteps.Arrive))
                Persist();
        }

        void ApplyMotionOverride()
        {
            if (!ReducedMotionOverride.HasValue || _save == null) return;
            if (_save.Settings == null) _save.Settings = new RitualSettings();
            _save.Settings.ReducedMotion = ReducedMotionOverride.Value;
        }

        public Ledger HabitLedger { get { return _habits != null ? _habits.Ledger : null; } }
        public bool PacketsOffered { get { return _habits != null && _habits.PacketsOffered; } }
        public string PendingHabitId { get { return _habits != null ? _habits.PendingId : null; } }
        public int LastPluckSemitones { get { return _habits != null ? _habits.LastSemitones : 0; } }

        public List<HabitDef> ActiveHabits()
        {
            return _habits != null ? _habits.Active() : new List<HabitDef>();
        }

        public int ShownLeaves(string habitId)
        {
            return _habits != null ? _habits.ShownLeaves(habitId) : 0;
        }

        public float HabitVitality(string habitId)
        {
            if (_habits == null) return 1f;
            return _habits.ShownVitality(habitId, new GardenDay(TodayIndex));
        }

        public bool YesterdayVisible(string habitId)
        {
            if (_habits == null) return false;
            return _habits.YesterdayVisible(habitId, new GardenDay(TodayIndex), BoundaryClock());
        }

        /// <summary>First offer returns true so the caller can play seed.appear. The choice is not saved.</summary>
        public bool OfferSeedPackets()
        {
            if (_habits == null || _readOnly || RestoreOffered) return false;
            return _habits.Offer();
        }

        public bool TryPlantHabit(string preset)
        {
            if (_habits == null || _readOnly || RestoreOffered) return false;
            if (!_habits.TryPlant(preset, TodayIndex)) return false;
            Persist();
            return true;
        }

        public bool TryArmHabit(string habitId, bool yesterday, TendSource source, out int semitones)
        {
            semitones = 0;
            if (_habits == null || _readOnly || RestoreOffered) return false;
            var today = new GardenDay(TodayIndex);
            var day = yesterday ? new GardenDay(today.Index - 1) : today;
            if (!_habits.TryArm(habitId, day, today, yesterday, source, HabitClock(), BoundaryClock())) return false;
            semitones = _habits.LastSemitones;
            return true;
        }

        public bool TryUndoHabit()
        {
            if (_habits == null || _readOnly || RestoreOffered) return false;
            return _habits.TryUndo();
        }

        /// <summary>Advances a frozen test clock. A running clock already moves, so it is not stepped twice.</summary>
        public void StepHabits(float dt)
        {
            if (_habits == null || _readOnly || RestoreOffered) return;
            if (dt < 0f) dt = 0f;
            if (dt > 0.1f) dt = 0.1f;
            if (_clock is FixedClock) _habitSeconds += dt;
            if (_habits.Tick(HabitClock())) Persist();
        }

        public void FlushHabits()
        {
            if (_habits == null || _readOnly || Outcome == LoadOutcome.Failed) return;
            if (_habits.Flush()) Persist();
        }

        public GrowthAnswer CompleteRitual()
        {
            if (RestoreOffered)
                throw new InvalidOperationException("the save needs a restore before the next ritual");
            if (_readOnly)
                throw new InvalidOperationException("this save was written by a newer version and is read-only");
            GrowthAnswer answer = Garden.CompleteRitual(TodayIndex);
            CopyGrowth();
            Persist();
            return answer;
        }

        public void ApplySettings(RitualSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (_save == null) return;
            _save.Settings = settings;
            Persist();
        }

        /// <summary>Writes the live garden. A failed or newer-schema load does not touch the file.</summary>
        public void Save()
        {
            CopyGrowth();
            Persist();
        }

        /// <summary>
        /// Moves the injected clock by whole days. A day before <see cref="Garden.LastRitualDay"/> is refused,
        /// the same rule <see cref="Garden.CompleteRitual"/> throws on. Nothing is shown when it refuses.
        /// </summary>
        public bool TryShiftDay(int delta)
        {
            if (delta == 0) return true;
            DateTimeOffset next = Now.AddDays(delta);
            int today = GardenDay.From(next, GardenDay.DefaultBoundary).Index;
            if (Garden != null && !Garden.AcceptsDay(today)) return false;
            FixedClock fixedClock = _clock as FixedClock;
            if (fixedClock != null && _devDays == 0)
                fixedClock.Now = fixedClock.Now.AddDays(delta);
            else
                _devDays += delta;
            return true;
        }

        public bool TryRestore()
        {
            if (!RestoreOffered) return false;
            string path = FirstBackup();
            if (path == null) return false;
            TerrariumSave doc;
            try { doc = TerrariumSave.FromJson(File.ReadAllText(path)); }
            catch (Exception) { return false; }
            if (doc.SchemaVersion > TerrariumSaveMigrations.CurrentSchema) return false;
            try { _store.Save(doc); }
            catch (Exception) { return false; }
            Adopt(doc);
            return true;
        }

        void Adopt(TerrariumSave doc)
        {
            _save = doc;
            if (_save.Settings == null) _save.Settings = new RitualSettings();
            if (_save.Habits == null) _save.Habits = new List<HabitDef>();
            if (_save.Tends == null) _save.Tends = new List<TendEvent>();
            if (_save.FrondDays == null) _save.FrondDays = new int[0];
            Garden = Garden.FromSave(_save);
            Outcome = LoadOutcome.Loaded;
            _readOnly = false;
            BackupAvailable = true;
            _habitSeconds = 0;
            BindHabits();
        }

        void BindHabits()
        {
            if (_save.Habits == null) _save.Habits = new List<HabitDef>();
            if (_save.Tends == null) _save.Tends = new List<TendEvent>();
            _habits = new CompanionHabits(_save);
        }

        IClock HabitClock()
        {
            DateTimeOffset now = _clock is FixedClock ? Now.AddSeconds(_habitSeconds) : Now;
            return new FixedClock(now, _clock.Zone ?? TimeZoneInfo.Utc);
        }

        IClock BoundaryClock()
        {
            return new FixedClock(Now, _clock.Zone ?? TimeZoneInfo.Utc);
        }

        string FirstBackup()
        {
            string prev1 = Path.Combine(_directory, SaveStore<TerrariumSave>.Prev1Name);
            if (File.Exists(prev1)) return prev1;
            string prev2 = Path.Combine(_directory, SaveStore<TerrariumSave>.Prev2Name);
            if (File.Exists(prev2)) return prev2;
            string snap = Path.Combine(_directory, SaveStore<TerrariumSave>.SnapshotName);
            if (File.Exists(snap)) return snap;
            return null;
        }

        void CopyGrowth()
        {
            if (Garden == null || _save == null) return;
            TerrariumSave growth = Garden.ToSave();
            _save.FrondDays = growth.FrondDays;
            _save.DewToday = growth.DewToday;
            _save.LastRitualDay = growth.LastRitualDay;
            _save.Returns = growth.Returns;
            _save.RitualsCompleted = growth.RitualsCompleted;
        }

        void Persist()
        {
            if (_store == null || _readOnly || Outcome == LoadOutcome.Failed || _save == null) return;
            if (_save.SchemaVersion < 1) _save.SchemaVersion = TerrariumSaveMigrations.CurrentSchema;
            _store.Save(_save);
        }
    }
}
