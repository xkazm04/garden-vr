namespace GardenVR.Room
{
    /// <summary>
    /// Passthrough stand-in. PC shows a room plate; Quest will show a passthrough layer and leave this dark.
    /// </summary>
    public interface IRoomProvider
    {
        /// <summary>Fade the room in from black. Zero or negative jumps to the authored exposure.</summary>
        void Show(float fadeSeconds);

        /// <summary>0 leaves the plate at its faded exposure. 1 fades it to black.</summary>
        void Dim(float amount);
    }
}
