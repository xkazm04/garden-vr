using System.Collections.Generic;
using UnityEngine;

namespace GardenVR.Sundial
{
    /// <summary>Keep competing dial invitations out of an open paper moment; restore their prior state on exit.</summary>
    [DefaultExecutionOrder(500)]
    public sealed class ReflectionModal : MonoBehaviour
    {
        readonly Dictionary<Renderer, bool> _renderers = new Dictionary<Renderer, bool>();
        readonly Dictionary<Collider, bool> _colliders = new Dictionary<Collider, bool>();
        void LateUpdate()
        {
            Restore();
            var c = GetComponent<SundialController>();
            if (c == null || (!(GetComponent<ReflectionController>()?.Open ?? false) && !(c.Settings?.Open ?? false))) return;
            foreach (var target in GetComponentsInChildren<GardenVR.Input.IntentTarget>(true))
            {
                if (!(target.Id.StartsWith("plant.") || target.Id == "gnomon")) continue;
                var col = target.GetComponent<Collider>();
                if (col != null && !_colliders.ContainsKey(col)) { _colliders.Add(col, col.enabled); col.enabled = false; }
            }
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (!(n.EndsWith("Prompt") || n == "VoiceCaption" || n == "DuskSparkle" || n.StartsWith("more.") ||
                    (c.Settings.Open && n.StartsWith("zone.dot.")) ||
                    n.StartsWith("symbol.") || n.StartsWith("mark."))) continue;
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                    if (!_renderers.ContainsKey(r)) { _renderers.Add(r, r.forceRenderingOff); r.forceRenderingOff = true; }
                foreach (var col in t.GetComponentsInChildren<Collider>(true))
                    if (!_colliders.ContainsKey(col)) { _colliders.Add(col, col.enabled); col.enabled = false; }
            }
        }
        void OnDisable() { Restore(); }
        void Restore()
        {
            foreach (var pair in _renderers) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
            foreach (var pair in _colliders) if (pair.Key != null) pair.Key.enabled = pair.Value;
            _renderers.Clear(); _colliders.Clear();
        }
    }
}
