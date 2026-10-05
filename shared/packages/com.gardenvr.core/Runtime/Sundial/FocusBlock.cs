using System;

namespace GardenVR.Core
{
    public enum FocusPhase { Idle, Running, Paused, Complete }

    /// <summary>
    /// Enough of a focus hour to put it back after a quit.
    /// Instants are UTC milliseconds. <see cref="Arc"/> is null when the hour started between 03:00 and 06:00.
    /// </summary>
    public sealed class FocusSnapshot
    {
        public string Phase;
        public long StartedUtcMs;
        public long PausedMs;
        public long PauseUtcMs;
        public string Arc;
        public float StartGnomonDeg;
        public bool EndedEarly;
        public bool TendPending;
        public int ElapsedSeconds;
    }

    /// <summary>
    /// A 25-minute shadow hour. Pinch starts it. Pinch again ends it early, and that still counts.
    /// Active time is the UTC gap from the start, minus every paused gap. A spring-forward or a fall-back
    /// changes the wall clock and does not add or remove minutes. There is no failed phase.
    /// Completion authorises one ritual tend of the arc the hour started in. This type does not write the ledger.
    /// </summary>
    public sealed class FocusBlock
    {
        public const int DurationSeconds = 25 * 60;

        /// <summary>How far the ink shadow travels across the dial over a full hour, in gnomon degrees.</summary>
        public const float SweepDegrees = 90f;

        DateTimeOffset _started;
        DateTimeOffset _pauseAt;
        TimeSpan _paused;
        bool _tendPending;

        public FocusPhase Phase { get; private set; } = FocusPhase.Idle;
        public ArcId? Arc { get; private set; }
        public float StartGnomonDeg { get; private set; }
        public bool EndedEarly { get; private set; }
        public bool Counted { get; private set; }
        public int ElapsedSeconds { get; private set; }
        public float Progress { get; private set; }
        public bool TendAuthorised { get { return _tendPending; } }
        public TendSource TendsAs { get { return TendSource.Ritual; } }
        public DateTimeOffset Started { get { return _started; } }

        /// <summary>
        /// Opens an hour at <paramref name="now"/>. The arc is whoever owns <paramref name="nowMin"/>.
        /// False when an hour is already running or paused. A finished hour can start again.
        /// </summary>
        public bool TryStart(DateTimeOffset now, int nowMin)
        {
            return TryStart(now, nowMin, null);
        }

        /// <summary>
        /// Opens an hour on <paramref name="times"/>. A null schedule uses the plan arcs.
        /// The locked arc follows the user's boundaries. The gnomon is still the clock.
        /// </summary>
        public bool TryStart(DateTimeOffset now, int nowMin, ArcTimes times)
        {
            if (Phase == FocusPhase.Running || Phase == FocusPhase.Paused) return false;
            int minute = nowMin % SundialRules.MinutesPerDay;
            if (minute < 0) minute += SundialRules.MinutesPerDay;
            _started = now;
            _pauseAt = default(DateTimeOffset);
            _paused = TimeSpan.Zero;
            ArcTimes schedule = times ?? ArcTimes.Default;
            Arc = schedule.ArcAt(minute);
            StartGnomonDeg = SundialRules.GnomonAngleDeg(minute);
            EndedEarly = false;
            Counted = false;
            _tendPending = false;
            ElapsedSeconds = 0;
            Progress = 0f;
            Phase = FocusPhase.Running;
            return true;
        }

        /// <summary>Freezes active time. False when the hour is not running.</summary>
        public bool TryPause(DateTimeOffset now)
        {
            if (Phase != FocusPhase.Running) return false;
            Apply(now);
            _pauseAt = now;
            Phase = FocusPhase.Paused;
            return true;
        }

        /// <summary>
        /// Continues. The gap since <see cref="TryPause"/> is not counted.
        /// A resume that already holds a full hour completes it. That completion is not early.
        /// </summary>
        public bool TryResume(DateTimeOffset now)
        {
            if (Phase != FocusPhase.Paused) return false;
            _paused += UtcBetween(_pauseAt, now);
            Phase = FocusPhase.Running;
            Apply(now);
            if (ElapsedSeconds >= DurationSeconds) Finish(false);
            return true;
        }

        /// <summary>
        /// Ends before the full duration. Still counted. A paused hour keeps the time it had already earned.
        /// An hour that has already reached 25 minutes completes in full, not as an early end.
        /// </summary>
        public bool TryEndEarly(DateTimeOffset now)
        {
            if (Phase != FocusPhase.Running && Phase != FocusPhase.Paused) return false;
            if (Phase == FocusPhase.Paused) now = _pauseAt;
            Apply(now);
            Finish(ElapsedSeconds < DurationSeconds);
            return true;
        }

        /// <summary>
        /// Moves a running hour to <paramref name="now"/>. A pause holds still. A finished hour holds still.
        /// A clock that steps backward does not unwind the ink or the count.
        /// </summary>
        public void Observe(DateTimeOffset now)
        {
            if (Phase != FocusPhase.Running) return;
            Apply(now);
            if (ElapsedSeconds >= DurationSeconds) Finish(false);
        }

        /// <summary>True once, when a finished hour has an arc and the ledger has not taken it yet.</summary>
        public bool ConsumeTend()
        {
            if (!_tendPending) return false;
            _tendPending = false;
            return true;
        }

        public FocusSnapshot Capture()
        {
            var snap = new FocusSnapshot();
            snap.Phase = Phase.ToString();
            snap.StartedUtcMs = Phase == FocusPhase.Idle ? 0L : UtcMs(_started);
            snap.PausedMs = (long)_paused.TotalMilliseconds;
            if (snap.PausedMs < 0L) snap.PausedMs = 0L;
            snap.PauseUtcMs = Phase == FocusPhase.Paused ? UtcMs(_pauseAt) : 0L;
            snap.Arc = Arc.HasValue ? Arc.Value.ToString() : null;
            snap.StartGnomonDeg = StartGnomonDeg;
            snap.EndedEarly = EndedEarly;
            snap.TendPending = _tendPending;
            snap.ElapsedSeconds = ElapsedSeconds;
            return snap;
        }

        public static FocusBlock Restore(FocusSnapshot snap)
        {
            var block = new FocusBlock();
            if (snap == null || string.IsNullOrEmpty(snap.Phase) || snap.Phase == "Idle") return block;
            FocusPhase phase;
            if (!Enum.TryParse(snap.Phase, out phase) || phase == FocusPhase.Idle) return block;
            // Running or Paused with no start instant is not an hour in progress; a 1970 start would finish it at once.
            if (phase != FocusPhase.Complete && snap.StartedUtcMs <= 0L) return block;
            block.Phase = phase;
            block._started = DateTimeOffset.FromUnixTimeMilliseconds(snap.StartedUtcMs);
            long pausedMs = snap.PausedMs < 0L ? 0L : snap.PausedMs;
            block._paused = TimeSpan.FromMilliseconds(pausedMs);
            if (phase == FocusPhase.Paused)
            {
                block._pauseAt = snap.PauseUtcMs > 0L
                    ? DateTimeOffset.FromUnixTimeMilliseconds(snap.PauseUtcMs)
                    : block._started;
            }
            if (!string.IsNullOrEmpty(snap.Arc))
            {
                ArcId arc;
                if (Enum.TryParse(snap.Arc, out arc)) block.Arc = arc;
            }
            block.StartGnomonDeg = snap.StartGnomonDeg;
            block.EndedEarly = snap.EndedEarly && phase == FocusPhase.Complete;
            block._tendPending = snap.TendPending && phase == FocusPhase.Complete && block.Arc.HasValue;
            int elapsed = snap.ElapsedSeconds;
            if (elapsed < 0) elapsed = 0;
            if (elapsed > DurationSeconds) elapsed = DurationSeconds;
            block.ElapsedSeconds = elapsed;
            block.Progress = elapsed / (float)DurationSeconds;
            block.Counted = phase == FocusPhase.Complete;
            return block;
        }

        void Apply(DateTimeOffset now)
        {
            TimeSpan active = UtcBetween(_started, now) - _paused;
            if (active < TimeSpan.Zero) active = TimeSpan.Zero;
            if (active > TimeSpan.FromSeconds(DurationSeconds)) active = TimeSpan.FromSeconds(DurationSeconds);
            int seconds = (int)active.TotalSeconds;
            if (seconds < ElapsedSeconds) seconds = ElapsedSeconds;
            if (seconds > DurationSeconds) seconds = DurationSeconds;
            ElapsedSeconds = seconds;
            float next = seconds / (float)DurationSeconds;
            if (next > Progress) Progress = next;
        }

        void Finish(bool early)
        {
            Phase = FocusPhase.Complete;
            EndedEarly = early;
            Counted = true;
            if (Arc.HasValue) _tendPending = true;
        }

        static TimeSpan UtcBetween(DateTimeOffset from, DateTimeOffset to)
        {
            TimeSpan span = to.UtcDateTime - from.UtcDateTime;
            if (span < TimeSpan.Zero) return TimeSpan.Zero;
            return span;
        }

        static long UtcMs(DateTimeOffset instant)
        {
            return new DateTimeOffset(instant.UtcDateTime, TimeSpan.Zero).ToUnixTimeMilliseconds();
        }
    }
}
