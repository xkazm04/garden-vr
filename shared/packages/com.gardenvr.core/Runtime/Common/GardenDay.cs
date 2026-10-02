using System;

namespace GardenVR.Core
{
    /// <summary>
    /// One garden day. Index is the number of days since 2000-01-01 of that garden day.
    /// The day rolls at a local boundary (default 03:00, PLAN D3): 01:30 still belongs to the
    /// previous civil date, and 03:00 exactly starts the new one.
    /// </summary>
    public readonly struct GardenDay : IComparable<GardenDay>, IEquatable<GardenDay>
    {
        public static readonly TimeSpan DefaultBoundary = new TimeSpan(3, 0, 0);
        public static readonly DateTime Epoch = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        public readonly int Index;

        public GardenDay(int index) { Index = index; }

        /// <summary>Wall-clock local time. The offset is not consulted; <paramref name="local"/> already carries the civil time.</summary>
        public static GardenDay From(DateTimeOffset local, TimeSpan boundary)
        {
            if (boundary < TimeSpan.Zero || boundary >= TimeSpan.FromDays(1))
                throw new ArgumentOutOfRangeException(nameof(boundary), "boundary must sit inside one civil day");
            var wall = local.DateTime;
            var civil = wall.TimeOfDay < boundary ? wall.Date.AddDays(-1) : wall.Date;
            int index = (int)(civil - Epoch).TotalDays;
            return new GardenDay(index);
        }

        public DateTime CivilDate
        {
            get { return Epoch.AddDays(Index); }
        }

        /// <summary>
        /// The instant this garden day ends: the boundary on the next civil date, in <paramref name="tz"/>.
        /// A spring-forward gap resolves to the transition instant. An ambiguous autumn instant resolves
        /// to the earlier one (the first time the clock shows that wall time).
        /// </summary>
        public DateTimeOffset EndsAt(TimeZoneInfo tz, TimeSpan boundary)
        {
            if (tz == null) throw new ArgumentNullException(nameof(tz));
            if (boundary < TimeSpan.Zero || boundary >= TimeSpan.FromDays(1))
                throw new ArgumentOutOfRangeException(nameof(boundary), "boundary must sit inside one civil day");
            var wall = DateTime.SpecifyKind(CivilDate.AddDays(1).Add(boundary), DateTimeKind.Unspecified);
            return WallToOffset(tz, wall);
        }

        public int CompareTo(GardenDay other) { return Index.CompareTo(other.Index); }
        public bool Equals(GardenDay other) { return Index == other.Index; }
        public override bool Equals(object obj) { return obj is GardenDay other && Equals(other); }
        public override int GetHashCode() { return Index; }
        public override string ToString() { return "GardenDay(" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")"; }

        public static bool operator ==(GardenDay a, GardenDay b) { return a.Index == b.Index; }
        public static bool operator !=(GardenDay a, GardenDay b) { return a.Index != b.Index; }
        public static bool operator <(GardenDay a, GardenDay b) { return a.Index < b.Index; }
        public static bool operator >(GardenDay a, GardenDay b) { return a.Index > b.Index; }

        static DateTimeOffset WallToOffset(TimeZoneInfo tz, DateTime wall)
        {
            if (tz.IsAmbiguousTime(wall))
            {
                // Greater offset is the earlier UTC instant: the first time this wall clock is shown.
                var offsets = tz.GetAmbiguousTimeOffsets(wall);
                var best = offsets[0];
                for (int i = 1; i < offsets.Length; i++)
                    if (offsets[i] > best) best = offsets[i];
                return new DateTimeOffset(wall, best);
            }
            if (tz.IsInvalidTime(wall))
                return FirstInstantAfterGap(tz, wall);
            var utc = TimeZoneInfo.ConvertTimeToUtc(wall, tz);
            return new DateTimeOffset(wall, wall - utc);
        }

        static DateTimeOffset FirstInstantAfterGap(TimeZoneInfo tz, DateTime invalidWall)
        {
            var probe = invalidWall;
            for (int i = 0; i < 6 * 60 * 60; i++)
            {
                probe = DateTime.SpecifyKind(probe.AddSeconds(1), DateTimeKind.Unspecified);
                if (tz.IsInvalidTime(probe)) continue;
                var utc = TimeZoneInfo.ConvertTimeToUtc(probe, tz);
                return new DateTimeOffset(probe, probe - utc);
            }
            throw new InvalidOperationException("garden day boundary falls in a gap with no following valid time");
        }
    }

    /// <summary>Injected time. Production uses <see cref="SystemClock"/>. Tests use <see cref="FixedClock"/>.</summary>
    public interface IClock
    {
        DateTimeOffset Now { get; }
        TimeZoneInfo Zone { get; }
    }

    public sealed class FixedClock : IClock
    {
        public DateTimeOffset Now { get; set; }
        public TimeZoneInfo Zone { get; set; }

        public FixedClock(DateTimeOffset now, TimeZoneInfo zone)
        {
            Now = now;
            Zone = zone ?? TimeZoneInfo.Utc;
        }

        public void Advance(TimeSpan by) { Now = Now.Add(by); }
    }

    public sealed class SystemClock : IClock
    {
        public TimeZoneInfo Zone { get; }

        public SystemClock() : this(null) { }

        public SystemClock(TimeZoneInfo zone) { Zone = zone ?? TimeZoneInfo.Local; }

        public DateTimeOffset Now
        {
            get { return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone); }
        }
    }
}
