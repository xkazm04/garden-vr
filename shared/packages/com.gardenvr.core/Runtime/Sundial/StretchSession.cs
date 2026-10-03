using System;
using System.Collections.Generic;

namespace GardenVR.Core
{
    public enum StretchPhase { Waiting, Reaching, Complete }

    public enum StretchEventKind { ReachCounted, RepeatIgnored, AheadIgnored, RitualComplete }

    public readonly struct StretchEvent
    {
        public readonly StretchEventKind Kind;
        public readonly float Time;
        public readonly int Reaches;
        public readonly int Mark;

        public StretchEvent(StretchEventKind kind, float time, int reaches, int mark)
        {
            Kind = kind;
            Time = time;
            Reaches = reaches;
            Mark = mark;
        }

        public override string ToString()
        {
            return Time.ToString("0.00") + "s " + Kind + " mark=" + Mark + " reaches=" + Reaches;
        }
    }

    /// <summary>
    /// One sample for <see cref="StretchSession"/>. <see cref="LookMark"/> is the mark being looked at,
    /// or -1 when the look is somewhere else. <see cref="PalmCommit"/> is the single frame the provider
    /// commits an open palm. The keyboard provider commits that at 0.6 seconds, so a short press never arrives.
    /// </summary>
    public readonly struct StretchSample
    {
        public readonly int LookMark;
        public readonly bool PalmCommit;

        public StretchSample(int lookMark, bool palmCommit)
        {
            LookMark = lookMark;
            PalmCommit = palmCommit;
        }
    }

    /// <summary>
    /// Morning stretch-reach. Three ink marks, in order. Look at the next mark and commit an open palm.
    /// A repeat or a skip is ignored. Nothing is lost and nothing is shamed.
    /// <see cref="Stretch"/> only rises. Completion authorises one Ritual tend of the morning arc,
    /// and <see cref="ConsumeTend"/> hands that authorisation over once. This type does not write the ledger.
    /// </summary>
    public sealed class StretchSession
    {
        public const int MarkCount = 3;

        readonly List<StretchEvent> _events = new List<StretchEvent>();

        public StretchPhase Phase { get; private set; } = StretchPhase.Waiting;
        public int Reaches { get; private set; }
        public float Time { get; private set; }

        /// <summary>0..1. Earned reaches divided by <see cref="MarkCount"/>. Never decreases.</summary>
        public float Stretch { get; private set; }

        public int NextMark { get { return Reaches >= MarkCount ? MarkCount - 1 : Reaches; } }
        public bool TendAuthorised { get; private set; }
        public ArcId TendsArc { get { return ArcId.Morning; } }
        public TendSource TendsAs { get { return TendSource.Ritual; } }
        public IReadOnlyList<StretchEvent> Events { get { return _events; } }

        public void Update(float dt, StretchSample sample)
        {
            if (dt < 0f) dt = 0f;
            Time += dt;
            if (Phase == StretchPhase.Complete) return;
            if (!sample.PalmCommit) return;

            int mark = sample.LookMark;
            if (mark < 0 || mark >= MarkCount) return;

            if (mark < Reaches)
            {
                Emit(StretchEventKind.RepeatIgnored, mark);
                return;
            }
            if (mark > Reaches)
            {
                Emit(StretchEventKind.AheadIgnored, mark);
                return;
            }

            Reaches++;
            float earned = (float)Reaches / MarkCount;
            if (earned > Stretch) Stretch = earned;
            Emit(StretchEventKind.ReachCounted, mark);
            if (Reaches >= MarkCount)
            {
                Phase = StretchPhase.Complete;
                TendAuthorised = true;
                Emit(StretchEventKind.RitualComplete, mark);
            }
            else
            {
                Phase = StretchPhase.Reaching;
            }
        }

        /// <summary>
        /// True once, when the three reaches are done and the morning arc may be tended.
        /// A second call is false. A session that is still reaching returns false.
        /// </summary>
        public bool ConsumeTend()
        {
            if (!TendAuthorised) return false;
            TendAuthorised = false;
            return true;
        }

        void Emit(StretchEventKind kind, int mark)
        {
            _events.Add(new StretchEvent(kind, Time, Reaches, mark));
        }
    }
}
