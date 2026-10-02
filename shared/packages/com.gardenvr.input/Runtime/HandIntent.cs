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
        /// <summary>0..1 strength (pinch strength, palm openness).</summary>
        public readonly float Strength;
        /// <summary>Seconds the intent has been held (PinchHold, Release), else 0.</summary>
        public readonly float Held;
        public readonly bool RightHand;
        /// <summary>Stable target id this intent is aimed at, or null.</summary>
        public readonly string TargetId;

        public HandIntent(HandIntentKind kind, Ray ray, float strength = 1f, float held = 0f, bool rightHand = true, string targetId = null)
        {
            Kind = kind;
            Ray = ray;
            Strength = strength;
            Held = held;
            RightHand = rightHand;
            TargetId = targetId;
        }
    }

    /// <summary>Implemented by every provider. Consumers subscribe; providers raise in Update.</summary>
    public interface IHandIntentSource
    {
        event Action<HandIntent> Intent;
        /// <summary>True while a pinch is held (used by rituals that pace on hold/release).</summary>
        bool IsPinching { get; }
        /// <summary>Current look ray (gaze on Quest/glasses, mouse ray on PC).</summary>
        Ray LookRay { get; }
        /// <summary>Analog pinch strength, 0 open to 1 closed. Keyboard/mouse ramps this over 0.12 seconds.</summary>
        float PinchStrength { get; }
        /// <summary>False while tracking is lost. Keyboard/mouse stays true; scripted playback can clear it.</summary>
        bool IsTracked { get; }
        /// <summary>Short PC binding text for prompts.</summary>
        string BindingHint(HandIntentKind kind);
        /// <summary>Esc, or the app losing focus. Lifecycle, not a hand intent.</summary>
        event Action SystemPause;
    }

    /// <summary>Seated head orientation. Keyboard/mouse supplies this with right-drag; a headset provider will later.</summary>
    public interface IHeadPoseSource
    {
        Quaternion LocalRotation { get; }
        event Action Recentred;
    }

    /// <summary>PC binding copy shared by the keyboard/mouse provider and scripted playback.</summary>
    public static class HandBindings
    {
        public static string PcHint(HandIntentKind kind)
        {
            switch (kind)
            {
                case HandIntentKind.PinchHold: return "Space or mouse";
                case HandIntentKind.Pinch: return "click";
                case HandIntentKind.Poke: return "F";
                case HandIntentKind.PalmOpen: return "hold P";
                case HandIntentKind.Look: return "mouse";
                case HandIntentKind.Release: return "release";
                default: return "";
            }
        }
    }
}
