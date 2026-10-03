using System;

namespace GardenVR.Core
{
    /// <summary>
    /// Pinch three dew drops onto three leaves, one per thing you are glad of.
    /// The things themselves are never stored. Finishing calls <see cref="Garden.CompleteRitual"/>
    /// once, so the day counts like a breath ritual and growth only rises.
    /// A second finish on the same garden day does not touch the garden.
    /// </summary>
    public sealed class ThreeGoodThings
    {
        public const int LeafCount = 3;

        readonly bool[] _on = new bool[LeafCount];

        public int Placed { get; private set; }
        public bool Complete { get { return Placed >= LeafCount; } }

        public bool HasDrop(int leaf)
        {
            return leaf >= 0 && leaf < LeafCount && _on[leaf];
        }

        /// <summary>One new drop on an empty leaf. A repeat, a finished ritual, or a bad index does nothing.</summary>
        public bool TryPlace(int leaf)
        {
            if (Complete) return false;
            if (leaf < 0 || leaf >= LeafCount) return false;
            if (_on[leaf]) return false;
            _on[leaf] = true;
            Placed++;
            return true;
        }

        /// <summary>The leaves already carry drops. Used when a save says this garden day is already done.</summary>
        public void RestoreDone()
        {
            for (int i = 0; i < LeafCount; i++) _on[i] = true;
            Placed = LeafCount;
        }

        /// <summary>Drops in progress are not kept. A new garden day starts empty.</summary>
        public void Reset()
        {
            for (int i = 0; i < LeafCount; i++) _on[i] = false;
            Placed = 0;
        }

        /// <summary>
        /// The third drop counts <paramref name="today"/> once.
        /// <paramref name="doneDay"/> is the garden day already stored, or null.
        /// Returns false when the ritual is unfinished or that day was already counted.
        /// The garden's own answer is the only growth this method writes.
        /// </summary>
        public bool TryCountDay(Garden garden, int today, int? doneDay, out GrowthAnswer answer)
        {
            answer = default(GrowthAnswer);
            if (!Complete) return false;
            if (garden == null) throw new ArgumentNullException(nameof(garden));
            if (doneDay == today) return false;
            answer = garden.CompleteRitual(today);
            return true;
        }
    }
}
