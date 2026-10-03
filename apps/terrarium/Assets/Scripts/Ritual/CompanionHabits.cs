using System;
using System.Collections.Generic;
using GardenVR.Core;

namespace GardenVR.Terrarium
{
    /// <summary>
    /// The life-habit session. Planting writes at once. A check-in is held for six seconds
    /// and lands in the ledger on expiry, pause, or quit. Undo before that write always works.
    /// </summary>
    public sealed class CompanionHabits
    {
        readonly TerrariumSave _save;
        readonly Ledger _ledger;
        readonly DeferredTend _deferred;
        bool _offered;
        bool _markLate;
        bool _pendingYesterday;
        string _pendingId;
        long _armedAtMs;

        public CompanionHabits(TerrariumSave save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            _save = save;
            if (_save.Habits == null) _save.Habits = new List<HabitDef>();
            if (_save.Tends == null) _save.Tends = new List<TendEvent>();
            _ledger = new Ledger(_save.Tends);
            _deferred = new DeferredTend(_ledger);
        }

        public Ledger Ledger { get { return _ledger; } }
        public bool PacketsOffered { get { return _offered; } }
        public bool IsPending { get { return _deferred.IsPending; } }
        public string PendingId { get { return _deferred.IsPending ? _pendingId : null; } }
        public int LastSemitones { get; private set; }

        /// <summary>The first call is the one that should play seed.appear. Later calls stay quiet.</summary>
        public bool Offer()
        {
            if (_offered) return false;
            _offered = true;
            return true;
        }

        public List<HabitDef> Active()
        {
            var list = new List<HabitDef>();
            for (int i = 0; i < _save.Habits.Count; i++)
            {
                HabitDef habit = _save.Habits[i];
                if (habit != null && !habit.ArchivedDay.HasValue) list.Add(habit);
            }
            return list;
        }

        public bool TryPlant(string preset, int today)
        {
            string label;
            CompanionSpecies species;
            if (!Companions.TryPreset(preset, out label, out species)) return false;
            int active = 0;
            for (int i = 0; i < _save.Habits.Count; i++)
            {
                HabitDef habit = _save.Habits[i];
                if (habit == null || habit.ArchivedDay.HasValue) continue;
                if (habit.Id == preset || habit.PresetKey == preset) return false;
                active++;
            }
            if (active >= Companions.MaxHabits) return false;
            var planted = new HabitDef();
            planted.Id = preset;
            planted.PresetKey = preset;
            planted.Species = species.ToString();
            planted.Kind = HabitKind.LifeCheckIn;
            planted.Slot = active;
            planted.CreatedDay = today;
            _save.Habits.Add(planted);
            return true;
        }

        public int ShownLeaves(string habitId)
        {
            int leaves = Companions.Leaves(_ledger, habitId);
            if (_deferred.IsPending && _pendingId == habitId) leaves++;
            return leaves;
        }

        public float ShownVitality(string habitId, GardenDay today)
        {
            if (_deferred.IsPending && _pendingId == habitId) return 1f;
            return Companions.Vitality(_ledger, habitId, today);
        }

        public bool YesterdayVisible(string habitId, GardenDay today, IClock clock)
        {
            HabitDef habit = Find(habitId);
            if (habit == null || clock == null || clock.Zone == null) return false;
            int yesterday = today.Index - 1;
            if (yesterday < habit.CreatedDay) return false;
            if (_ledger.IsKept(habitId, yesterday)) return false;
            if (clock.Now >= today.EndsAt(clock.Zone, GardenDay.DefaultBoundary)) return false;
            if (_deferred.IsPending && _pendingYesterday && _pendingId == habitId) return false;
            return true;
        }

        public bool TryArm(string habitId, GardenDay day, GardenDay today, bool yesterday, TendSource source, IClock armClock, IClock boundaryClock)
        {
            if (armClock == null) throw new ArgumentNullException(nameof(armClock));
            if (_deferred.IsPending) return false;
            if (Find(habitId) == null) return false;
            if (yesterday)
            {
                if (!YesterdayVisible(habitId, today, boundaryClock)) return false;
                if (day.Index != today.Index - 1) return false;
            }
            else if (day.Index != today.Index || _ledger.IsKept(habitId, day.Index))
            {
                return false;
            }
            LastSemitones = Pitch(Companions.Leaves(_ledger, habitId));
            _markLate = yesterday;
            _pendingYesterday = yesterday;
            _pendingId = habitId;
            _armedAtMs = armClock.Now.ToUnixTimeMilliseconds();
            _deferred.Arm(habitId, day, source, armClock);
            return true;
        }

        public bool TryUndo()
        {
            if (!_deferred.IsPending) return false;
            _deferred.Undo();
            _pendingId = null;
            _markLate = false;
            _pendingYesterday = false;
            return true;
        }

        public bool Tick(IClock clock)
        {
            TendEvent ev = _deferred.Tick(clock);
            if (ev == null && !_deferred.IsPending) return false;
            if (ev == null) return false;
            Publish(ev);
            return true;
        }

        public bool Flush()
        {
            if (!_deferred.IsPending) return false;
            TendEvent ev = _deferred.Flush();
            if (ev == null) return false;
            Publish(ev);
            return true;
        }

        void Publish(TendEvent ev)
        {
            if (_markLate && ev.AtUtcMs == _armedAtMs)
            {
                ev.Late = true;
                ev.Source = TendSource.Backfill;
            }
            _markLate = false;
            _pendingYesterday = false;
            _pendingId = null;
            _save.Tends = new List<TendEvent>(_ledger.Events);
        }

        HabitDef Find(string habitId)
        {
            if (string.IsNullOrEmpty(habitId)) return null;
            for (int i = 0; i < _save.Habits.Count; i++)
            {
                HabitDef habit = _save.Habits[i];
                if (habit != null && !habit.ArchivedDay.HasValue && habit.Id == habitId) return habit;
            }
            return null;
        }

        static int Pitch(int leavesBefore)
        {
            if (leavesBefore <= 0) return 0;
            if (leavesBefore <= 2) return 3;
            return 5;
        }
    }
}
