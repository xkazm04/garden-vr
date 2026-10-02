using System;
using UnityEngine;

namespace GardenVR.Input
{
    /// <summary>The hand vocabulary both apps are designed around. App code reacts to these intents only;
    /// a provider (keyboard/mouse on PC, XR hands on Quest later) is the only code that reads raw devices.</summary>
    public enum HandIntentKind { Look, Pinch, PinchHold, Release, Poke, PalmOpen }

    public readonly struct HandIntent
    {
        public readonly HandIntentKind Kind;
        /// <summary>Ray from the user's eye (Look) or fingertip (Pinch/Poke) in world space.</summary>
        public readonly Ray Ray;
        /// <summary>0..1 strength (pinch strength, palm openness); 1 for digital providers.</summary>
        public readonly float Strength;
        /// <summary>Seconds the intent has been held (PinchHold), else 0.</summary>
        public readonly float Held;
        public readonly bool RightHand;

        public HandIntent(HandIntentKind kind, Ray ray, float strength = 1f, float held = 0f, bool rightHand = true)
        { Kind = kind; Ray = ray; Strength = strength; Held = held; RightHand = rightHand; }
    }

    /// <summary>Implemented by every provider. Consumers subscribe; providers raise in Update.</summary>
    public interface IHandIntentSource
    {
        event Action<HandIntent> Intent;
        /// <summary>True while a pinch is held (used by rituals that pace on hold/release).</summary>
        bool IsPinching { get; }
        /// <summary>Current look ray (gaze on Quest/glasses, mouse ray on PC).</summary>
        Ray LookRay { get; }
    }
}
