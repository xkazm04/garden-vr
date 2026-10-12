using System;

namespace GardenVR.Input
{
    /// <summary>Transient speech input. Consumers own the lifetime of received text; providers never save it.</summary>
    public interface ISpeechToText
    {
        void Begin();
        void End();
        event Action<string> Final;
        event Action<string> Partial;
        bool Available { get; }
    }

    /// <summary>Optional live device state, so a view can stop showing listening after an asynchronous failure.</summary>
    public interface ISpeechCaptureState
    {
        bool Listening { get; }
    }
}
