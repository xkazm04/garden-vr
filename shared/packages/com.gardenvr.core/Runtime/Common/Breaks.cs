using System;
using System.Collections.Generic;

namespace GardenVR.Core
{
    /// <summary>One frame of input for a break. Each part reads only what it needs.</summary>
    public readonly struct BreakSample
    {
        /// <summary>The stretch mark being looked at, or -1.</summary>
        public readonly int LookMark;
        /// <summary>The single frame an open palm is committed (the stretch reach).</summary>
        public readonly bool PalmCommit;
        /// <summary>An open palm is being held this frame.</summary>
        public readonly bool PalmHeld;
        /// <summary>A pinch is being held this frame.</summary>
        public readonly bool PinchHeld;
        /// <summary>The look rests on the eye-rest target this frame.</summary>
        public readonly bool OnTarget;

        public BreakSample(int lookMark, bool palmCommit, bool palmHeld, bool pinchHeld, bool onTarget)
        {
            LookMark = lookMark;
            PalmCommit = palmCommit;
            PalmHeld = palmHeld;
            PinchHeld = pinchHeld;
            OnTarget = onTarget;
        }

        public static BreakSample None { get { return new BreakSample(-1, false, false, false, false); } }
    }

    public enum HandPose { PalmOpen, Pinch }

    /// <summary>
    /// Three held hand poses for desk-worn wrists and hands (upgrade plan section 6.4). Each pose is held for
    /// <see cref="HoldSeconds"/>; letting go early pauses that pose and keeps the poses already done. Progress only rises.
    /// </summary>
    public sealed class HandMobility
    {
        public const float HoldSeconds = 3f;
        // Frame times summed in float land a hair under whole seconds; a hold that close counts.
        const float Slack = 1e-3f;
        public static readonly HandPose[] Poses = { HandPose.PalmOpen, HandPose.Pinch, HandPose.PalmOpen };

        float _held;

        public int Done { get; private set; }
        public bool Complete { get { return Done >= Poses.Length; } }
        public HandPose Current { get { return Poses[Math.Min(Done, Poses.Length - 1)]; } }
        /// <summary>0..1 over all three poses; never decreases.</summary>
        public float Progress { get; private set; }

        public void Update(float dt, BreakSample sample)
        {
            if (Complete) return;
            if (dt < 0f || float.IsNaN(dt) || float.IsInfinity(dt)) dt = 0f;
            bool holding = Current == HandPose.PalmOpen ? sample.PalmHeld : sample.PinchHeld;
            if (!holding) return;
            _held += dt;
            if (_held >= HoldSeconds - Slack)
            {
                Done++;
                _held = 0f;
            }
            float now = Complete ? 1f : (Done + Math.Min(1f, _held / HoldSeconds)) / Poses.Length;
            if (now > Progress) Progress = now;
        }
    }

    /// <summary>
    /// Rest the eyes on a far point in the room for <see cref="Seconds"/>. Time counts only while the look rests on the
    /// target; looking away pauses and never resets.
    /// </summary>
    public sealed class EyeRest
    {
        public const float Seconds = 20f;
        public const float DefaultDistance = 3f;
        public const float MinDistance = 2f;
        public const float MaxDistance = 8f;
        const float Slack = 1e-3f;

        public float Rested { get; private set; }
        public bool Complete { get { return Rested >= Seconds - Slack; } }
        /// <summary>Whole seconds left, for the ring and the "13 s" label.</summary>
        public int SecondsLeft { get { return (int)Math.Ceiling(Math.Max(0f, Seconds - Rested - Slack)); } }

        public void Update(float dt, BreakSample sample)
        {
            if (Complete || !sample.OnTarget) return;
            if (dt < 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            Rested = Math.Min(Seconds, Rested + dt);
            if (Rested >= Seconds - Slack) Rested = Seconds;
        }

        /// <summary>
        /// Where the target goes: the farthest distance the room reports in metres, held between 2 and 8 m; 3 m when the
        /// room reports nothing usable (the PC plate, or no scene mesh).
        /// </summary>
        public static float TargetDistance(float? farthestMeters)
        {
            if (!farthestMeters.HasValue || float.IsNaN(farthestMeters.Value) || float.IsInfinity(farthestMeters.Value) || farthestMeters.Value <= 0f)
                return DefaultDistance;
            return Math.Max(MinDistance, Math.Min(MaxDistance, farthestMeters.Value));
        }
    }

    public enum BreakPart { Stretch, Mobility, EyeRest, Done }

    /// <summary>
    /// A break: the stretch to three marks, then the hand poses when chosen, then the eye rest. Completion authorises one
    /// tend of the break habit, handed over once by <see cref="ConsumeTend"/>. This type does not write the ledger.
    /// </summary>
    public sealed class BreakSession
    {
        public const string PresetKey = "break";

        readonly bool _withMobility;
        bool _tendAuthorised;

        public StretchSession Stretch { get; } = new StretchSession();
        public HandMobility Mobility { get; } = new HandMobility();
        public EyeRest Eyes { get; } = new EyeRest();

        public BreakSession(bool withMobility) { _withMobility = withMobility; }

        public BreakPart Part
        {
            get
            {
                if (Stretch.Phase != StretchPhase.Complete) return BreakPart.Stretch;
                if (_withMobility && !Mobility.Complete) return BreakPart.Mobility;
                if (!Eyes.Complete) return BreakPart.EyeRest;
                return BreakPart.Done;
            }
        }

        public bool Complete { get { return Part == BreakPart.Done; } }

        public void Update(float dt, BreakSample sample)
        {
            BreakPart before = Part;
            switch (before)
            {
                case BreakPart.Stretch: Stretch.Update(dt, new StretchSample(sample.LookMark, sample.PalmCommit)); break;
                case BreakPart.Mobility: Mobility.Update(dt, sample); break;
                case BreakPart.EyeRest: Eyes.Update(dt, sample); break;
            }
            if (before != BreakPart.Done && Part == BreakPart.Done) _tendAuthorised = true;
        }

        /// <summary>True once, when the break is done and its habit may be tended (TendSource.Ritual).</summary>
        public bool ConsumeTend()
        {
            if (!_tendAuthorised) return false;
            _tendAuthorised = false;
            return true;
        }
    }

    /// <summary>A part of the day when a nudge may arrive, in local wall time; start before end, inside one civil day.</summary>
    public readonly struct NudgeWindow
    {
        public readonly TimeSpan Start;
        public readonly TimeSpan End;

        public NudgeWindow(TimeSpan start, TimeSpan end) { Start = start; End = end; }

        public bool IsValid { get { return Start >= TimeSpan.Zero && End <= TimeSpan.FromDays(1) && Start < End; } }
    }

    /// <summary>
    /// When to offer the next break (upgrade plan section 6.4): at most <see cref="MaxPerDay"/> a day, only inside the
    /// user's windows, at least <see cref="MinSpacing"/> after the last nudge and <see cref="QuietAfterBreak"/> after a
    /// break. Quiet by design: the copy is one calm line and nothing is sent about a break not taken.
    /// </summary>
    public static class NudgePlanner
    {
        public const int MaxPerDay = 2;
        public static readonly TimeSpan MinSpacing = TimeSpan.FromMinutes(90);
        public static readonly TimeSpan QuietAfterBreak = TimeSpan.FromMinutes(50);
        public const string Text = "The garden has a break ready.";

        public static readonly NudgeWindow[] DefaultWindows =
        {
            new NudgeWindow(new TimeSpan(10, 30, 0), new TimeSpan(12, 0, 0)),
            new NudgeWindow(new TimeSpan(14, 30, 0), new TimeSpan(17, 0, 0))
        };

        /// <summary>
        /// The earliest local time at or after <paramref name="now"/>, on the same civil date, that a nudge may arrive; null
        /// when today's nudges are used up or no window is left. <paramref name="sentToday"/> and
        /// <paramref name="lastBreak"/> are local times of today. Invalid windows are ignored; no valid window means none.
        /// </summary>
        public static DateTimeOffset? Next(DateTimeOffset now, IReadOnlyList<NudgeWindow> windows, IReadOnlyList<DateTimeOffset> sentToday, DateTimeOffset? lastBreak)
        {
            int sent = sentToday == null ? 0 : sentToday.Count;
            if (sent >= MaxPerDay || windows == null) return null;
            DateTimeOffset earliest = now;
            if (sentToday != null)
                foreach (DateTimeOffset t in sentToday)
                    if (t + MinSpacing > earliest) earliest = t + MinSpacing;
            if (lastBreak.HasValue && lastBreak.Value + QuietAfterBreak > earliest) earliest = lastBreak.Value + QuietAfterBreak;
            if (earliest.Date != now.Date) return null;

            DateTimeOffset? best = null;
            foreach (NudgeWindow w in windows)
            {
                if (!w.IsValid) continue;
                DateTimeOffset start = new DateTimeOffset(now.Date + w.Start, now.Offset);
                DateTimeOffset end = new DateTimeOffset(now.Date + w.End, now.Offset);
                DateTimeOffset candidate = earliest > start ? earliest : start;
                if (candidate >= end) continue;
                if (!best.HasValue || candidate < best.Value) best = candidate;
            }
            return best;
        }
    }
}
