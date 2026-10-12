using System;
using GardenVR.Core;
using UnityEngine;

namespace GardenVR.Sundial
{
    [CreateAssetMenu(menuName = "Garden VR/Coach relay")]
    public sealed class RelayConfig : ScriptableObject
    {
        public string RelayUrl = "";
        public float BudgetSeconds = 4f;
#if UNITY_EDITOR_WIN
        public bool UseEditorCli;
#endif
        public TimeSpan Budget => float.IsNaN(BudgetSeconds) || float.IsInfinity(BudgetSeconds) || BudgetSeconds <= 0
            ? CoachFlow.Budget : TimeSpan.FromSeconds(BudgetSeconds);

        public static RelayConfig LoadDefault()
        {
#if UNITY_EDITOR_WIN
            var local = UnityEditor.AssetDatabase.LoadAssetAtPath<RelayConfig>(
                "Assets/Editor/Coach/RelayConfigLocal.asset");
            if (local != null) return local;
#endif
            return Resources.Load<RelayConfig>("RelayConfig");
        }
    }
}
