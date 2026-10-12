using System;

namespace GardenVR.Input
{
    /// <summary>Optional continuous palm state, independent of the single PalmOpen commit intent.
    /// A released palm, lost tracking, lost focus or disabled provider ends the hold.</summary>
    public interface IPalmStateSource
    {
        bool PalmHeld { get; }
        event Action PalmReleased;
    }
}
