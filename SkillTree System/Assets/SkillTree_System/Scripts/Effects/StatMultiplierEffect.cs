using System;
using UnityEngine;

namespace JollyLlama.SkillTreeSystem
{
    [Serializable]
    public class StatMultiplierEffect : SkillEffect
    {
        [Tooltip("Which stat to multiply. Any id the client project's IStatRegistry " +
                 "implementation recognizes - this package doesn't need to know the " +
                 "set of valid stats ahead of time.")]
        public string statId = "";

        [Tooltip("Optional friendlier label for tooltips. Falls back to statId if left blank.")]
        public string displayName = "";

        [Tooltip("Multiplier applied to the stat. 1.2 = +20%, 0.8 = -20%.")]
        public float multiplier = 1.2f;

        public override void Apply()  => SkillTreeStatRegistry.Current?.RegisterMultiplier(statId, multiplier);
        public override void Remove() => SkillTreeStatRegistry.Current?.UnregisterMultiplier(statId, multiplier);

        public override string GetDescription()
        {
            float pct   = Mathf.RoundToInt((multiplier - 1f) * 100f);
            string sign  = pct >= 0 ? "+" : "";
            string label = string.IsNullOrEmpty(displayName) ? statId : displayName;
            return $"{sign}{pct}% {label}";
        }
    }
}

