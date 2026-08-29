using System;
using System.Collections.Generic;
using UnityEngine;

namespace JollyLlama.SkillTreeSystem
{
    [Serializable]
    public class SkillTreeRuntimeState
    {
        // ── Persisted ─────────────────────────────────────────────────────────────

        public SerializableDictionary<string, int> nodeRanks = new();

        /// <summary>
        /// Total resources spent across the lifetime of this state, keyed by resourceId.
        /// Stored as parallel lists for Unity serialisation (mirrors SerializableDictionary pattern).
        /// </summary>
        public SerializableDictionary<string, int> totalSpent = new();

        // ── Node rank API ─────────────────────────────────────────────────────────

        public bool IsUnlocked(string nodeId) => nodeRanks.ContainsKey(nodeId);

        public int GetRank(string nodeId)
            => nodeRanks.TryGetValue(nodeId, out int rank) ? rank : 0;

        public bool CanRankUp(string nodeId, int maxRanks)
            => GetRank(nodeId) < maxRanks;

        public HashSet<string> GetUnlockedIds()
        {
            var set = new HashSet<string>();
            foreach (var kvp in nodeRanks)
                set.Add(kvp.Key);
            return set;
        }

        // ── Spend tracking ────────────────────────────────────────────────────────

        internal void AddRank(string nodeId,
            IReadOnlyList<(ResourceDefinitionSO Resource, int Amount)> costs)
        {
            nodeRanks[nodeId] = GetRank(nodeId) + 1;

            foreach (var (res, amt) in costs)
            {
                if (res == null) continue;
                string id = res.resourceId;
                totalSpent[id] = (totalSpent.TryGetValue(id, out int prev) ? prev : 0) + amt;
            }
        }

        internal void RemoveRank(string nodeId,
            IReadOnlyList<(ResourceDefinitionSO Resource, int Amount)> costs)
        {
            int current = GetRank(nodeId);
            if (current <= 1)
                nodeRanks.Remove(nodeId);
            else
                nodeRanks[nodeId] = current - 1;

            foreach (var (res, amt) in costs)
            {
                if (res == null) continue;
                string id = res.resourceId;
                int prev  = totalSpent.TryGetValue(id, out int p) ? p : 0;
                totalSpent[id] = Mathf.Max(0, prev - amt);
            }
        }

        internal void Clear()
        {
            nodeRanks.Clear();
            totalSpent.Clear();
        }

        // ── Convenience ───────────────────────────────────────────────────────────

        /// <summary>Returns total amount spent on a specific resource across all nodes.</summary>
        public int GetTotalSpent(ResourceDefinitionSO resource)
        {
            if (resource == null) return 0;
            return totalSpent.TryGetValue(resource.resourceId, out int v) ? v : 0;
        }

        /// <summary>Returns total amount spent by resourceId string.</summary>
        public int GetTotalSpent(string resourceId)
            => totalSpent.TryGetValue(resourceId, out int v) ? v : 0;
    }
}