using System;
using System.Collections.Generic;
using UnityEngine;

namespace JollyLlama.SkillTreeSystem
{
    [CreateAssetMenu(fileName = "New Skill Node", menuName = "Skill Tree/Skill Node")]
    public class SkillNodeSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable unique ID used for save data. Set this once and never change it.")]
        public string nodeId;

        [Tooltip("Display name shown in the skill tree UI.")]
        public string displayName;

        [Tooltip("Icon shown on the node button.")]
        public Sprite icon;

        [TextArea(2, 4)]
        [Tooltip("Flavour / mechanical description shown in the tooltip.")]
        public string description;

        [Header("Branch")]
        public SkillBranch branch;

        [Header("Ranks")]
        [Min(1)] public int maxRanks = 1;

        [Header("Cost — per rank")]
        [Tooltip("Each entry is one resource cost. Actual cost = amountPerRank × rank. " +
                 "Drag in ResourceDefinitionSO assets created via Assets > Skill Tree > Resource Definition.")]
        public List<ResourceCost> costsPerRank = new();

        [Header("Prerequisites")]
        [Tooltip("ALL of these must be met before this node is available.")]
        public List<PrerequisiteEntry> prerequisites = new();

        [Header("Effects")]
        [Tooltip("Applied once per rank unlock.")]
        [SerializeReference]
        public List<SkillEffect> effects = new();

        [Header("Visibility")]
        [Tooltip("When true, this node uses the global visibility rules from SkillTreeConfigSO. " +
                 "Uncheck to override thresholds individually for this node.")]
        public bool useGlobalVisibilityRules = true;

        [Tooltip("How many ranks must be spent on ANY prerequisite node before this node's box " +
                 "becomes visible at all. 0 = always visible. Only used when useGlobalVisibilityRules is false.")]
        [Min(0)] public int revealBoxRank = 0;

        [Tooltip("How many ranks must be spent on ANY prerequisite node before the '?' is lifted " +
                 "and real name/description/effects are shown. 0 = always revealed. " +
                 "Only used when useGlobalVisibilityRules is false.")]
        [Min(0)] public int revealInfoRank = 0;

        [Tooltip("How many ranks must be spent on ANY prerequisite node before this node can be " +
                 "purchased. Only used when useGlobalVisibilityRules is false.")]
        [Min(0)] public int unlockRank = 1;

        [Header("Graph Layout")]
        public Vector2 graphPosition;

        // ── Validation ───────────────────────────────────────────────────────────

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                Debug.LogWarning($"[SkillNodeSO] '{name}' has no nodeId set.");

            if (string.IsNullOrWhiteSpace(displayName))
                displayName = name;
        }

        // ── Rank application ─────────────────────────────────────────────────────

        public void ApplyRank(int rank)
        {
            foreach (var effect in effects)
                effect?.Apply();
        }

        public void RemoveRank(int rank)
        {
            foreach (var effect in effects)
                effect?.Remove();
        }

        // ── Cost helpers ─────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the amount owed for a specific resource at a given rank.
        /// Returns 0 if that resource is not in this node's cost list.
        /// </summary>
        public int GetCost(ResourceDefinitionSO resource, int rank)
        {
            foreach (var cost in costsPerRank)
                if (cost.resource == resource)
                    return cost.GetAmount(rank);
            return 0;
        }

        /// <summary>
        /// Returns all costs for a given rank as a list of (resource, amount) pairs.
        /// </summary>
        public List<(ResourceDefinitionSO Resource, int Amount)> GetAllCosts(int rank)
        {
            var result = new List<(ResourceDefinitionSO, int)>(costsPerRank.Count);
            foreach (var cost in costsPerRank)
                if (cost.resource != null)
                    result.Add((cost.resource, cost.GetAmount(rank)));
            return result;
        }

        /// <summary>
        /// Builds a human-readable cost string for a given rank, e.g. "100g  5s".
        /// </summary>
        public string GetCostString(int rank)
        {
            if (costsPerRank == null || costsPerRank.Count == 0) return "Free";
            var parts = new System.Text.StringBuilder();
            foreach (var cost in costsPerRank)
            {
                if (cost.resource == null) continue;
                if (parts.Length > 0) parts.Append("  ");
                parts.Append(cost.Format(rank));
            }
            return parts.Length > 0 ? parts.ToString() : "Free";
        }

        // ── Tooltip ──────────────────────────────────────────────────────────────

        public string GetTooltip(int currentRank)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(displayName);
            if (maxRanks > 1)
                sb.AppendLine($"Rank {currentRank} / {maxRanks}");
            sb.AppendLine();
            sb.AppendLine(description);
            if (effects != null && effects.Count > 0)
            {
                sb.AppendLine();
                foreach (var effect in effects)
                    if (effect != null)
                        sb.AppendLine($"  • {effect.GetDescription()}");
            }
            return sb.ToString().TrimEnd();
        }
    }

    // ── Prerequisite entry ────────────────────────────────────────────────────────

    [Serializable]
    public class PrerequisiteEntry
    {
        [Tooltip("The node that must be unlocked.")]
        public SkillNodeSO node;

        [Tooltip("How many ranks must be spent on that node. 1 = just unlocked.")]
        [Min(1)] public int requiredRank = 1;
    }
}