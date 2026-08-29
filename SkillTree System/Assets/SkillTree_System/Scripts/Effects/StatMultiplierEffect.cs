using System;
using UnityEngine;

namespace JollyLlama.SkillTreeSystem
{
    [Serializable]
    public class StatMultiplierEffect : SkillEffect
    {
        [Tooltip("Which stat to multiply.")] public StatType statType;

        [Tooltip("Multiplier applied to the stat. 1.2 = +20%, 0.8 = -20%.")]
        public float multiplier = 1.2f;

        public override void Apply() => StatSystem.Instance?.RegisterMultiplier(statType, multiplier);
        public override void Remove() => StatSystem.Instance?.UnregisterMultiplier(statType, multiplier);

        public override string GetDescription()
        {
            float pct = Mathf.RoundToInt((multiplier - 1f) * 100f);
            string sign = pct >= 0 ? "+" : "";
            return $"{sign}{pct}% {FriendlyName(statType)}";
        }

        private static string FriendlyName(StatType s) => s switch
        {
            StatType.DamageMultiplier => "Damage",
            StatType.FireRate => "Fire Rate",
            StatType.ChargeRate => "Charge Rate",
            StatType.MaxCharge => "Max Charge",
            StatType.BurstFireRate => "Burst Fire Rate",
            StatType.TagDuration => "Tag Duration",
            StatType.TagSpreadRadius => "Tag Spread Radius",
            StatType.GoldDropMultiplier => "Gold Drop",
            StatType.ReactionDamageMultiplier => "Reaction Damage",
            StatType.CritChance => "Crit Chance",
            StatType.CritMultiplier => "Crit Multiplier",
            StatType.ProjectileCount => "Projectile Count",
            StatType.ProjectileSize => "Projectile Size",
            StatType.ProjectileSpeed => "Projectile Speed",
            StatType.MiningYield => "Mining Yield",
            StatType.TotemMoveSpeed => "Totem Speed",
            StatType.TotemMaxHP => "Totem HP",
            _ => s.ToString()
        };
    }
}