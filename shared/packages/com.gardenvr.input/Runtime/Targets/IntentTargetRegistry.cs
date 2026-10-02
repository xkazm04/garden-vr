using System;
using System.Collections.Generic;
using UnityEngine;

namespace GardenVR.Input
{
    /// <summary>Live lookup of <see cref="IntentTarget"/> by id, plus a closest-hit raycast.</summary>
    public static class IntentTargetRegistry
    {
        static readonly Dictionary<string, IntentTarget> ById = new Dictionary<string, IntentTarget>();

        public static void Register(IntentTarget target)
        {
            if (target == null || string.IsNullOrEmpty(target.Id)) return;
            if (!target.isActiveAndEnabled) return;
            ById[target.Id] = target;
        }

        public static void Unregister(IntentTarget target)
        {
            if (target == null || string.IsNullOrEmpty(target.Id)) return;
            IntentTarget current;
            if (ById.TryGetValue(target.Id, out current) && current == target)
                ById.Remove(target.Id);
        }

        public static bool TryGet(string id, out IntentTarget target)
        {
            if (string.IsNullOrEmpty(id))
            {
                target = null;
                return false;
            }
            return ById.TryGetValue(id, out target) && target != null;
        }

        /// <summary>Enabled, active targets sorted by id (ordinal). Hidden and disabled targets are absent.</summary>
        public static IReadOnlyList<string> OrderedIds()
        {
            var list = new List<string>();
            foreach (var pair in ById)
            {
                if (Selectable(pair.Value)) list.Add(pair.Key);
            }
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        /// <summary>Closest selectable target whose collider the ray hits, or null.</summary>
        public static IntentTarget Raycast(Ray ray, float maxDistance = 1000f)
        {
            IntentTarget best = null;
            float bestDistance = maxDistance;
            foreach (var pair in ById)
            {
                IntentTarget target = pair.Value;
                if (!Selectable(target)) continue;
                Collider col = target.HitCollider;
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;
                RaycastHit hit;
                if (col.Raycast(ray, out hit, maxDistance) && hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    best = target;
                }
            }
            return best;
        }

        /// <summary>Drops every registration. Tests call this between cases. Play mode relies on OnDisable.</summary>
        public static void Reset()
        {
            ById.Clear();
        }

        static bool Selectable(IntentTarget target)
        {
            return target != null
                && target.isActiveAndEnabled
                && target.gameObject.activeInHierarchy
                && !string.IsNullOrEmpty(target.Id);
        }
    }
}
