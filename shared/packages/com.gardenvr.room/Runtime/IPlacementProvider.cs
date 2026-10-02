using System;
using UnityEngine;

namespace GardenVR.Room
{
    /// <summary>
    /// Where the hero sits. PC uses a fixed desk pose. Quest will use a scene table later.
    /// The save stores where, never what grew, so it does not depend on this anchor.
    /// </summary>
    public interface IPlacementProvider
    {
        Pose DeskPose { get; }
        Plane DeskPlane { get; }

        /// <summary>Raised once the desk edge trace has finished.</summary>
        event Action Placed;
    }
}
