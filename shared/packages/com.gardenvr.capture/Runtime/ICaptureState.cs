using System.Collections.Generic;

namespace GardenVR.Capture
{
    /// <summary>
    /// A scene component that can be posed for a batchmode shot.
    /// CaptureCli passes the <c>-state k=v,k=v</c> dictionary to every instance in the open scene.
    /// </summary>
    public interface ICaptureState
    {
        void ApplyCaptureState(IReadOnlyDictionary<string, string> state);
    }
}
