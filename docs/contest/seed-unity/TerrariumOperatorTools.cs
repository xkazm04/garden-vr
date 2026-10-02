using System;
using UnityEngine;

namespace Terrarium.XR
{
    /// <summary>
    /// Exposes the ritual's state to Meta XR Operator as an agentic tool, so an agent that pinches through Operator's
    /// openxr_hand_gesture can assert on the app's own answer ("breaths went 0 -> 1") instead of eyeballing pixels.
    /// Registration needs a live OpenXR instance with the Operator layer loaded, so it retries once a second.
    /// </summary>
    public sealed class TerrariumOperatorTools : MonoBehaviour
    {
        float _next; bool _done; int _attempts;

        void Update()
        {
#if USING_XR_SDK_OPENXR
            if (_done || Time.unscaledTime < _next || _attempts > 60) return;
            _next = Time.unscaledTime + 1f; _attempts++;
            try
            {
                var r = Meta.XR.MetaXROperatorExternalTool.RegisterAgenticTool(
                    "terrarium_get_state",
                    "Breathing Terrarium: returns JSON with the ritual phase, breaths counted of target, frond uncoil 0..1, glass fog 0..1, " +
                    "the pinch state the app sees, and the garden record (fronds, vitality, rituals).",
                    null,
                    _ => TerrariumDriver.Instance != null ? TerrariumDriver.Instance.StateJson() : "{\"error\":\"no driver\"}");
                _done = r == Meta.XR.XrResult.Success;
                Debug.Log($"[Terrarium] RegisterAgenticTool(terrarium_get_state) attempt {_attempts} -> {r}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Terrarium] RegisterAgenticTool attempt {_attempts} threw: {e.GetType().Name}: {e.Message}");
                _next += 4f;
            }
#endif
        }
    }
}
