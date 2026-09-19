using System;
using UnityEngine;

namespace JollyLlama.SkillTreeSystem
{
    [Serializable]
    public class StatFlatBonusEffect : SkillEffect
    {
        [Tooltip("Which stat to add a flat bonus to. Any id the client project's " +
                 "IStatRegistry implementation recognizes - this package doesn't need " +
                 "to know the set of valid stats ahead of time.")]
        public string statId = "";

        [Tooltip("Optional friendlier label for tooltips. Falls back to statId if left blank.")]
        public string displayName = "";

        [Tooltip("Flat value added. Use negative values for penalties.")]
        public float bonus = 1f;

        public override void Apply()  => SkillTreeStatRegistry.Current?.RegisterFlatBonus(statId, bonus);
        public override void Remove() => SkillTreeStatRegistry.Current?.UnregisterFlatBonus(statId, bonus);

        public override string GetDescription()
        {
            string sign  = bonus >= 0 ? "+" : "";
            string label = string.IsNullOrEmpty(displayName) ? statId : displayName;
            return $"{sign}{bonus} {label}";
        }
    }
}