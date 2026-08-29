using System;
using UnityEngine;

namespace JollyLlama.SkillTreeSystem
{
    [Serializable]
    public class StatFlatBonusEffect : SkillEffect
    {
        [Tooltip("Which stat to add a flat bonus to.")]
        public StatType statType;

        [Tooltip("Flat value added. Use negative values for penalties.")]
        public float bonus = 1f;

        public override void Apply() => StatSystem.Instance?.RegisterFlatBonus(statType, bonus);
        public override void Remove() => StatSystem.Instance?.UnregisterFlatBonus(statType, bonus);

        public override string GetDescription()
        {
            string sign = bonus >= 0 ? "+" : "";
            return $"{sign}{bonus} {statType}";
        }
    }
}