using System;
using System.Collections.Generic;
using System.Globalization;

namespace GardenVR.Core
{
    public enum HabitKind { InAppRitual, LifeCheckIn }

    /// <summary>
    /// A habit both apps share. <see cref="Group"/> is the sundial arc key.
    /// <see cref="Species"/> is the terrarium companion. Either may be left unset.
    /// </summary>
    public sealed class HabitDef
    {
        public string Id;
        public string PresetKey;
        public string Group;
        public string Species;
        public HabitKind Kind;
        public int Slot;

        /// <summary>
        /// Sundial row inside the arc: 0, 1 or 2. Terrarium leaves this at 0.
        /// A saved habit with no row is row 0, the only row the one-habit dial used.
        /// </summary>
        public int Row;

        public int CreatedDay;
        public int? ArchivedDay;

        /// <summary>The user's own name for the habit. Null means the preset's label.</summary>
        public string Name;
        /// <summary>Null means the preset's zone (<see cref="HabitProfiles.ZoneOf"/>).</summary>
        public LifeZone? Zone;
        /// <summary>Null means daily (<see cref="HabitProfiles.ScheduleOf"/>).</summary>
        public HabitSchedule Schedule;
        /// <summary>A quantity to reach in a day, 2 or more. Null means a yes-or-no habit.</summary>
        public int? Target;
        /// <summary>What <see cref="Target"/> counts, for example "glasses". Null when there is no target.</summary>
        public string Unit;
    }

    public enum TendSource { Pinch, Poke, Ritual, Backfill }

    /// <summary>Append-only. Undo sets <see cref="UndoneAtUtcMs"/> and never removes the row.</summary>
    public sealed class TendEvent
    {
        public string Id;
        public string HabitId;
        public string Tz;
        public int Day;
        public long AtUtcMs;
        public TendSource Source;
        public bool Late;
        public long? UndoneAtUtcMs;
    }

    public sealed class TendResult
    {
        public readonly bool Ok;
        public readonly bool AlreadyKept;
        public readonly TendEvent Event;
        public readonly string Reason;

        TendResult(bool ok, bool alreadyKept, TendEvent ev, string reason)
        {
            Ok = ok;
            AlreadyKept = alreadyKept;
            Event = ev;
            Reason = reason;
        }

        public static TendResult Accepted(TendEvent ev) { return new TendResult(true, false, ev, null); }
        public static TendResult Duplicate(TendEvent ev) { return new TendResult(true, true, ev, "already-kept"); }
        public static TendResult Refused(string reason) { return new TendResult(false, false, null, reason); }
    }

    /// <summary>
    /// The habit record. Tends are idempotent per habit and garden day.
    /// A day after the clock's own garden day is refused, so setting the clock back cannot create a future day.
    /// </summary>
    public sealed class Ledger
    {
        readonly List<TendEvent> _events = new List<TendEvent>();
        int _seq;

        public Ledger() { }

        public Ledger(IEnumerable<TendEvent> events)
        {
            if (events == null) return;
            foreach (var e in events)
            {
                if (e != null) _events.Add(e);
            }
        }

        public IReadOnlyList<TendEvent> Events { get { return _events; } }

        public TendResult Tend(string habitId, GardenDay day, TendSource src, IClock clock)
        {
            return Tend(habitId, day, src, clock, GardenDay.DefaultBoundary);
        }

        public TendResult Tend(string habitId, GardenDay day, TendSource src, IClock clock, TimeSpan boundary)
        {
            Require(habitId, clock);
            var nowDay = GardenDay.From(clock.Now, boundary);
            if (day.Index > nowDay.Index) return TendResult.Refused("future");
            var existing = FindLive(habitId, day.Index);
            if (existing != null) return TendResult.Duplicate(existing);
            return TendResult.Accepted(Append(habitId, day.Index, src, false, clock));
        }

        /// <summary>Marks the row when the clock is still inside the window (inclusive). Default 6 seconds.</summary>
        public bool Undo(string tendId, IClock clock, double windowSeconds = 6)
        {
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            if (windowSeconds < 0) throw new ArgumentOutOfRangeException(nameof(windowSeconds));
            if (string.IsNullOrEmpty(tendId)) return false;
            TendEvent found = null;
            for (int i = 0; i < _events.Count; i++)
            {
                if (_events[i].Id == tendId) { found = _events[i]; break; }
            }
            if (found == null || found.UndoneAtUtcMs.HasValue) return false;
            long now = clock.Now.ToUnixTimeMilliseconds();
            long limit = (long)Math.Round(windowSeconds * 1000.0);
            if (now - found.AtUtcMs > limit) return false;
            found.UndoneAtUtcMs = now;
            return true;
        }

        /// <summary>
        /// Logs yesterday only, once, and only before today's boundary.
        /// <see cref="TendEvent.Late"/> is set. The timestamp is the clock's real instant, not a fabricated one.
        /// </summary>
        public TendResult Backfill(string habitId, GardenDay day, GardenDay today, IClock clock)
        {
            return Backfill(habitId, day, today, clock, GardenDay.DefaultBoundary);
        }

        public TendResult Backfill(string habitId, GardenDay day, GardenDay today, IClock clock, TimeSpan boundary)
        {
            Require(habitId, clock);
            if (clock.Zone == null) throw new ArgumentException("clock has no zone");
            if (day.Index != today.Index - 1) return TendResult.Refused("not-yesterday");
            if (clock.Now >= today.EndsAt(clock.Zone, boundary)) return TendResult.Refused("after-boundary");
            var nowDay = GardenDay.From(clock.Now, boundary);
            if (day.Index > nowDay.Index) return TendResult.Refused("future");
            var existing = FindLive(habitId, day.Index);
            if (existing != null) return TendResult.Refused("already-kept");
            return TendResult.Accepted(Append(habitId, day.Index, TendSource.Backfill, true, clock));
        }

        /// <summary>
        /// Logs a missed day from the catch-up window: a day from <paramref name="depth"/> days before
        /// <paramref name="today"/> up to yesterday, once, and only before today's boundary. <see cref="TendEvent.Late"/>
        /// is set and the timestamp is the clock's real instant. With a depth of 1 it accepts what <see cref="Backfill"/>
        /// accepts.
        /// </summary>
        public TendResult BackfillWithin(string habitId, GardenDay day, GardenDay today, int depth, IClock clock, TimeSpan boundary)
        {
            Require(habitId, clock);
            if (depth < 1) throw new ArgumentOutOfRangeException(nameof(depth));
            if (clock.Zone == null) throw new ArgumentException("clock has no zone");
            if (day.Index >= today.Index || day.Index < today.Index - depth) return TendResult.Refused("outside-window");
            if (clock.Now >= today.EndsAt(clock.Zone, boundary)) return TendResult.Refused("after-boundary");
            var nowDay = GardenDay.From(clock.Now, boundary);
            if (day.Index > nowDay.Index) return TendResult.Refused("future");
            var existing = FindLive(habitId, day.Index);
            if (existing != null) return TendResult.Refused("already-kept");
            return TendResult.Accepted(Append(habitId, day.Index, TendSource.Backfill, true, clock));
        }

        public bool IsKept(string habitId, int day) { return FindLive(habitId, day) != null; }

        public int KeptDays(string habitId) { return KeptDaysFrom(habitId, int.MinValue); }

        /// <summary>Live days in the inclusive window ending at <paramref name="today"/>. A 7-day window starts six days earlier.</summary>
        public int KeptDaysInWindow(string habitId, GardenDay today, int days)
        {
            if (string.IsNullOrEmpty(habitId)) throw new ArgumentException("habitId is null or empty", nameof(habitId));
            if (days < 1) throw new ArgumentOutOfRangeException(nameof(days));
            int start = today.Index - (days - 1);
            var seen = new HashSet<int>();
            for (int i = 0; i < _events.Count; i++)
            {
                var e = _events[i];
                if (e.HabitId != habitId || e.UndoneAtUtcMs.HasValue) continue;
                if (e.Day < start || e.Day > today.Index) continue;
                seen.Add(e.Day);
            }
            return seen.Count;
        }

        /// <summary>
        /// Distinct live days on or after <paramref name="firstDay"/>, with no upper bound.
        /// A day after today still counts, so setting the wall clock back cannot lower it. An undone tend does not count.
        /// </summary>
        public int KeptDaysFrom(string habitId, int firstDay)
        {
            if (string.IsNullOrEmpty(habitId)) throw new ArgumentException("habitId is null or empty", nameof(habitId));
            var seen = new HashSet<int>();
            for (int i = 0; i < _events.Count; i++)
            {
                var e = _events[i];
                if (e.HabitId == habitId && !e.UndoneAtUtcMs.HasValue && e.Day >= firstDay) seen.Add(e.Day);
            }
            return seen.Count;
        }

        TendEvent FindLive(string habitId, int day)
        {
            for (int i = 0; i < _events.Count; i++)
            {
                var e = _events[i];
                if (e.HabitId == habitId && e.Day == day && !e.UndoneAtUtcMs.HasValue) return e;
            }
            return null;
        }

        TendEvent Append(string habitId, int day, TendSource source, bool late, IClock clock)
        {
            _seq++;
            var e = new TendEvent();
            e.Id = "tend-" + clock.Now.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)
                + "-" + _seq.ToString(CultureInfo.InvariantCulture);
            e.HabitId = habitId;
            e.Tz = clock.Zone == null ? "" : clock.Zone.Id;
            e.Day = day;
            e.AtUtcMs = clock.Now.ToUnixTimeMilliseconds();
            e.Source = source;
            e.Late = late;
            _events.Add(e);
            return e;
        }

        static void Require(string habitId, IClock clock)
        {
            if (string.IsNullOrEmpty(habitId)) throw new ArgumentException("habitId is null or empty", nameof(habitId));
            if (clock == null) throw new ArgumentNullException(nameof(clock));
        }
    }

    /// <summary>
    /// Holds one tend for <see cref="WindowSeconds"/> before it is written.
    /// Undo cancels that pending write and does not fail. Expiry and <see cref="Flush"/> commit it.
    /// Flush is the pause and teardown path: leaving writes the tend.
    /// </summary>
    public sealed class DeferredTend
    {
        public const double WindowSeconds = 6;

        readonly Ledger _ledger;
        bool _pending;
        string _habitId;
        GardenDay _day;
        TendSource _source;
        DateTimeOffset _armedAt;
        TimeZoneInfo _zone;

        public DeferredTend(Ledger ledger)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            _ledger = ledger;
        }

        public bool IsPending { get { return _pending; } }

        public void Arm(string habitId, GardenDay day, TendSource source, IClock clock)
        {
            if (string.IsNullOrEmpty(habitId)) throw new ArgumentException("habitId is null or empty", nameof(habitId));
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            if (_pending) throw new InvalidOperationException("a tend is already waiting to commit");
            _pending = true;
            _habitId = habitId;
            _day = day;
            _source = source;
            _armedAt = clock.Now;
            _zone = clock.Zone ?? TimeZoneInfo.Utc;
        }

        /// <summary>Commits when the held tend is at least <see cref="WindowSeconds"/> old. Otherwise returns null.</summary>
        public TendEvent Tick(IClock clock)
        {
            if (!_pending) return null;
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            long age = clock.Now.ToUnixTimeMilliseconds() - _armedAt.ToUnixTimeMilliseconds();
            long limit = (long)Math.Round(WindowSeconds * 1000.0);
            if (age < limit) return null;
            return Commit();
        }

        public TendEvent Flush()
        {
            if (!_pending) return null;
            return Commit();
        }

        /// <summary>Cancels a pending tend. Always succeeds: there is nothing to fail, the write has not happened.</summary>
        public bool Undo()
        {
            _pending = false;
            return true;
        }

        TendEvent Commit()
        {
            _pending = false;
            var result = _ledger.Tend(_habitId, _day, _source, new FixedClock(_armedAt, _zone));
            return result.Event;
        }
    }
}
