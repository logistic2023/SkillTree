using System;
using UnityEngine;

namespace JollyLlama.SkillTreeSystem
{
    /// <summary>
    /// Defines a spendable resource type (e.g. Gold, Shards, Crystals).
    /// Create one SO per currency via Assets > Skill Tree > Resource Definition.
    /// Assign these to SkillNodeSO costs and to ResourceManager's tracked resources.
    /// </summary>
    [CreateAssetMenu(fileName = "New Resource", menuName = "Skill Tree/Resource Definition")]
    public class ResourceDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable unique key used in save data and lookups. Set once, never change.")]
        public string resourceId = "gold";

        [Tooltip("Human-readable name shown in UI (e.g. 'Gold', 'Shards').")]
        public string displayName = "Gold";

        [Tooltip("Icon shown next to the resource amount in UI.")]
        public Sprite icon;

        [Tooltip("Short suffix shown inline with amounts, e.g. 'g' → '100g'. Leave empty to use displayName.")]
        public string shortSuffix = "g";

        [Header("Stat Integration")]
        [Tooltip("Optional. When set, incoming drops of this resource are multiplied by this stat's " +
                 "total multiplier from StatSystem. Leave null if no drop scaling is needed.")]
        public string dropMultiplierStat;

        [Tooltip("If true, drop multiplier stat is active. Uncheck to bypass stat scaling entirely.")]
        public bool useDropMultiplier = false;

        [Header("Display")]
        [Tooltip("Color used for this resource in UI labels and cost text.")]
        public Color displayColor = Color.white;

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Formats an amount using the short suffix, e.g. 150 → "150g".
        /// </summary>
        public string Format(int amount)
        {
            string suffix = string.IsNullOrEmpty(shortSuffix) ? "" : shortSuffix;
            return $"{amount}{suffix}";
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(resourceId))
                Debug.LogWarning($"[ResourceDefinitionSO] '{name}' has no resourceId set.");

            if (string.IsNullOrWhiteSpace(displayName))
                displayName = name;
        }
    }

    // ── ResourceCost ──────────────────────────────────────────────────────────────

    /// <summary>
    /// A single (resource, amount) pair used to express unlock costs on skill nodes.
    /// The amount is the BASE cost for rank 1; actual cost = amount × rank.
    /// </summary>
    [Serializable]
    public class ResourceCost
    {
        [Tooltip("Which resource this cost draws from.")]
        public ResourceDefinitionSO resource;

        [Tooltip("Base amount per rank. Actual cost = amountPerRank × rank.")]
        [Min(0)]
        public int amountPerRank = 100;

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>Returns the total cost for the given rank (amountPerRank × rank).</summary>
        public int GetAmount(int rank) => amountPerRank * rank;

        /// <summary>Formatted string using the resource's short suffix, e.g. "150g".</summary>
        public string Format(int rank) => resource != null ? resource.Format(GetAmount(rank)) : $"{GetAmount(rank)}";
    }
}