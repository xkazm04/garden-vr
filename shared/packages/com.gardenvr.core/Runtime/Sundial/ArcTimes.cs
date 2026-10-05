using System;

namespace GardenVR.Core
{
    /// <summary>
    /// The start of one arc. Dusk is where wind-down opens. Wind-down still ends at the garden-day
    /// boundary, which this drag does not move.
    /// </summary>
    public enum ArcEdge
    {
        Morning,
        Midday,
        Dusk
    }

    /// <summary>
    /// The user's morning, midday and dusk boundaries, in minutes after local midnight.
    /// A drag snaps to <see cref="SnapMin"/> minutes. Each arc stays at least <see cref="MinArcMin"/>
    /// minutes, and nothing moves the 03:00 roll-over. The gnomon stays a clock: 0 degrees at 06:00,
    /// 15 degrees an hour. Due state reads <see cref="ArcAt"/>.
    /// </summary>
    public sealed class ArcTimes
    {
        public const int SnapMin = 15;
        public const int MinArcMin = 60;

        public int DayBoundaryMin;
        public int MorningMin;
        public int MiddayMin;
        public int DuskMin;

        public static ArcTimes Default
        {
            get { return DefaultWith(SundialRules.DayBoundaryMin); }
        }

        public int Minute(ArcEdge edge)
        {
            if (edge == ArcEdge.Morning) return MorningMin;
            if (edge == ArcEdge.Midday) return MiddayMin;
            return DuskMin;
        }

        public float Angle(ArcEdge edge)
        {
            return SundialRules.GnomonAngleDeg(Minute(edge));
        }

        public ArcTimes Copy()
        {
            return new ArcTimes
            {
                DayBoundaryMin = DayBoundaryMin,
                MorningMin = MorningMin,
                MiddayMin = MiddayMin,
                DuskMin = DuskMin
            };
        }

        /// <summary>
        /// Builds a schedule and pulls any illegal edge back inside the guards.
        /// A boundary outside one civil day falls back to 03:00. Missing edges use the plan defaults.
        /// </summary>
        public static ArcTimes From(int dayBoundaryMin, int morningMin, int middayMin, int duskMin)
        {
            ArcTimes times = DefaultWith(SanitizeBoundary(dayBoundaryMin));
            times = times.Moved(ArcEdge.Morning, morningMin);
            times = times.Moved(ArcEdge.Midday, middayMin);
            times = times.Moved(ArcEdge.Dusk, duskMin);
            return times;
        }

        /// <summary>
        /// Puts <paramref name="edge"/> on the snapped minute. The other edges stay.
        /// A hand that would shrink an arc under an hour, or cross the day boundary, stops at the guard.
        /// </summary>
        public ArcTimes Moved(ArcEdge edge, int rawMinute)
        {
            ArcTimes next = Copy();
            int target = next.Extend(Snap(rawMinute));
            int morning = next.Extend(next.MorningMin);
            int midday = next.Extend(next.MiddayMin);
            int dusk = next.Extend(next.DuskMin);
            int end = SundialRules.MinutesPerDay + next.DayBoundaryMin;
            if (edge == ArcEdge.Morning)
            {
                int max = midday - MinArcMin;
                target = Place(target, next.DayBoundaryMin, max);
                next.MorningMin = Clock(target);
            }
            else if (edge == ArcEdge.Midday)
            {
                int min = morning + MinArcMin;
                int max = dusk - MinArcMin;
                target = Place(target, min, max);
                next.MiddayMin = Clock(target);
            }
            else
            {
                int min = midday + MinArcMin;
                int max = end - MinArcMin;
                target = Place(target, min, max);
                next.DuskMin = Clock(target);
            }
            return next;
        }

        /// <summary>
        /// <paramref name="gnomonDeg"/> is the rim angle, 0 at 06:00. A non-finite angle leaves the schedule as it is.
        /// </summary>
        public ArcTimes Dragged(ArcEdge edge, float gnomonDeg)
        {
            if (float.IsNaN(gnomonDeg) || float.IsInfinity(gnomonDeg)) return Copy();
            return Moved(edge, MinuteAtAngle(gnomonDeg));
        }

        /// <summary>
        /// Arc that owns <paramref name="nowMin"/>, or null in the quiet gap after the day boundary and before morning.
        /// Minutes from midnight up to the boundary are shifted onto wind-down, the same way <see cref="SundialRules.ArcAt"/> does.
        /// </summary>
        public ArcId? ArcAt(int nowMin)
        {
            int minute = nowMin;
            if (minute >= 0 && minute < DayBoundaryMin)
                minute += SundialRules.MinutesPerDay;
            int morning = Extend(MorningMin);
            int midday = Extend(MiddayMin);
            int dusk = Extend(DuskMin);
            int end = SundialRules.MinutesPerDay + DayBoundaryMin;
            if (minute >= morning && minute < midday) return ArcId.Morning;
            if (minute >= midday && minute < dusk) return ArcId.Midday;
            if (minute >= dusk && minute < end) return ArcId.WindDown;
            return null;
        }

        /// <summary>Nearest <see cref="SnapMin"/>, wrapped into one civil day.</summary>
        public static int Snap(int minute)
        {
            double steps = minute / (double)SnapMin;
            if (double.IsNaN(steps) || double.IsInfinity(steps)) return 0;
            long snapped = (long)Math.Round(steps, MidpointRounding.AwayFromZero) * SnapMin;
            int day = SundialRules.MinutesPerDay;
            int mod = (int)(snapped % day);
            if (mod < 0) mod += day;
            return mod;
        }

        /// <summary>Clock minute under the gnomon. 0 degrees is 06:00. One degree is four minutes.</summary>
        public static int MinuteAtAngle(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees))
                return SundialRules.MorningStartMin;
            double raw = SundialRules.MorningStartMin + (double)degrees * 4.0;
            double wrapped = raw % SundialRules.MinutesPerDay;
            if (wrapped < 0d) wrapped += SundialRules.MinutesPerDay;
            int minute = (int)Math.Round(wrapped, MidpointRounding.AwayFromZero);
            if (minute >= SundialRules.MinutesPerDay) minute = 0;
            if (minute < 0) minute = 0;
            return minute;
        }

        public string ToJson()
        {
            var root = new JsonObject();
            root.Set("morning", JsonValue.Number(MorningMin));
            root.Set("midday", JsonValue.Number(MiddayMin));
            root.Set("dusk", JsonValue.Number(DuskMin));
            root.Set("boundary", JsonValue.Number(DayBoundaryMin));
            return Json.Write(root);
        }

        public static ArcTimes Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Default;
            try
            {
                JsonObject obj = Json.ParseObject(json);
                int boundary = obj.Has("boundary") ? obj.Get("boundary").AsInt() : SundialRules.DayBoundaryMin;
                int morning = obj.Has("morning") ? obj.Get("morning").AsInt() : SundialRules.MorningStartMin;
                int midday = obj.Has("midday") ? obj.Get("midday").AsInt() : 11 * 60;
                int dusk = obj.Has("dusk") ? obj.Get("dusk").AsInt() : 18 * 60;
                return From(boundary, morning, midday, dusk);
            }
            catch (Exception)
            {
                // A member of the wrong type or past the long range throws InvalidOperationException or
                // OverflowException, not only FormatException. Every unreadable schedule is the plan default.
                return Default;
            }
        }

        static ArcTimes DefaultWith(int boundary)
        {
            var times = new ArcTimes
            {
                DayBoundaryMin = boundary,
                MorningMin = SundialRules.MorningStartMin,
                MiddayMin = 11 * 60,
                DuskMin = 18 * 60
            };
            if (times.Extend(times.MorningMin) < boundary)
            {
                int end = SundialRules.MinutesPerDay + boundary;
                int dusk = end - MinArcMin;
                int midday = dusk - MinArcMin;
                int morning = midday - MinArcMin;
                if (morning < boundary) morning = boundary;
                times.MorningMin = Clock(morning);
                times.MiddayMin = Clock(midday);
                times.DuskMin = Clock(dusk);
            }
            return times;
        }

        static int SanitizeBoundary(int minute)
        {
            if (minute < 0 || minute >= SundialRules.MinutesPerDay)
                return SundialRules.DayBoundaryMin;
            return minute;
        }

        int Extend(int clockMin)
        {
            int minute = Clock(clockMin);
            if (minute < DayBoundaryMin) return minute + SundialRules.MinutesPerDay;
            return minute;
        }

        static int Clock(int extended)
        {
            int minute = extended % SundialRules.MinutesPerDay;
            if (minute < 0) minute += SundialRules.MinutesPerDay;
            return minute;
        }

        /// <summary>
        /// Keeps <paramref name="target"/> inside the inclusive span. Outside it, the nearer end wins
        /// on the civil-day circle, so a hand that slips past 03:00 sticks at the guard instead of
        /// jumping to the far edge.
        /// </summary>
        static int Place(int target, int min, int max)
        {
            if (max < min) max = min;
            if (target >= min && target <= max) return target;
            int span = SundialRules.MinutesPerDay;
            int toMin = CircularDistance(target, min, span);
            int toMax = CircularDistance(target, max, span);
            return toMin <= toMax ? min : max;
        }

        static int CircularDistance(int a, int b, int span)
        {
            int delta = a - b;
            delta %= span;
            if (delta < 0) delta += span;
            int other = span - delta;
            return delta <= other ? delta : other;
        }
    }
}
