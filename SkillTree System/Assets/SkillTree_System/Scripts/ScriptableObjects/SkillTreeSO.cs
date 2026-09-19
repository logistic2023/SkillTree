using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace JollyLlama.SkillTreeSystem
{
    /// <summary>
    /// A freeform labelled rectangle drawn behind nodes on the editor canvas — purely a
    /// visual/organisational aid (e.g. grouping "Early Game" or "Ultimate" nodes). Has no
    /// effect at runtime.
    /// </summary>
    [Serializable]
    public class NodeRegion
    {
        public string label = "Region";
        public Vector2 position;
        public Vector2 size = new Vector2(300f, 200f);
        public Color color = new Color(0.3f, 0.3f, 0.3f, 0.18f);
    }

    [CreateAssetMenu(fileName = "New Skill Tree", menuName = "Skill Tree/Skill Tree")]
    public class SkillTreeSO : ScriptableObject
    {
        [Header("Tree Info")] public string treeName = "Main Skill Tree";

        [TextArea(1, 2)] public string treeDescription;

        [Header("Nodes")] public List<SkillNodeSO> allNodes = new();

        [Header("Regions")]
        [Tooltip("Freeform labelled rectangles drawn behind nodes in the editor canvas, " +
                 "purely for visual organisation. No effect at runtime.")]
        public List<NodeRegion> regions = new();

        // ── Node lookup ───────────────────────────────────────────────────────────

        public SkillNodeSO GetNode(string nodeId)
        {
            BuildCacheIfNeeded();
            _nodeCache.TryGetValue(nodeId, out var node);
            return node;
        }

        // ── Prerequisite check ────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if every PrerequisiteEntry on the node is satisfied.
        /// rankLookup maps nodeId → current rank (0 if not unlocked).
        /// </summary>
        public bool ArePrereqsMet(SkillNodeSO node, Dictionary<string, int> rankLookup)
        {
            if (node.prerequisites == null || node.prerequisites.Count == 0)
                return true;

            foreach (var entry in node.prerequisites)
            {
                if (entry == null || entry.node == null) continue;

                int currentRank = rankLookup.TryGetValue(entry.node.nodeId, out int r) ? r : 0;
                if (currentRank < entry.requiredRank)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Convenience overload: builds a rank lookup from the runtime state.
        /// </summary>
        public bool ArePrereqsMet(SkillNodeSO node, SkillTreeRuntimeState state)
        {
            var lookup = BuildRankLookup(state);
            return ArePrereqsMet(node, lookup);
        }

        // ── Node queries ──────────────────────────────────────────────────────────

        public List<SkillNodeSO> GetBranchNodes(SkillBranch branch)
            => allNodes.Where(n => n != null && n.branch == branch)
                .OrderBy(n => n.graphPosition.y).ToList();

        public List<SkillNodeSO> GetAvailableNodes(SkillTreeRuntimeState state)
        {
            var lookup = BuildRankLookup(state);
            return allNodes.Where(n => n != null
                                       && (state.GetRank(n.nodeId) < n.maxRanks)
                                       && ArePrereqsMet(n, lookup)).ToList();
        }

        public List<SkillNodeSO> GetUnlockedNodes(SkillTreeRuntimeState state)
            => allNodes.Where(n => n != null && state.GetRank(n.nodeId) > 0).ToList();

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>Builds a nodeId → rank dictionary from state for a single-pass prereq check.</summary>
        public Dictionary<string, int> BuildRankLookup(SkillTreeRuntimeState state)
        {
            var lookup = new Dictionary<string, int>();
            foreach (var id in state.GetUnlockedIds())
                lookup[id] = state.GetRank(id);
            return lookup;
        }

        // ── Validation ────────────────────────────────────────────────────────────

        private void OnValidate()
        {
            _nodeCache = null;

            var seen = new HashSet<string>();
            foreach (var node in allNodes)
            {
                if (node == null || string.IsNullOrEmpty(node.nodeId)) continue;
                if (!seen.Add(node.nodeId))
                    Debug.LogError($"[SkillTreeSO] Duplicate nodeId '{node.nodeId}' in '{name}'.");
            }
        }

        // ── Cache ─────────────────────────────────────────────────────────────────

        private Dictionary<string, SkillNodeSO> _nodeCache;

        private void BuildCacheIfNeeded()
        {
            if (_nodeCache != null) return;
            _nodeCache = new Dictionary<string, SkillNodeSO>();
            foreach (var node in allNodes)
            {
                if (node == null || string.IsNullOrEmpty(node.nodeId)) continue;
                if (!_nodeCache.TryAdd(node.nodeId, node))
                    Debug.LogError($"[SkillTreeSO] Duplicate nodeId '{node.nodeId}' — cannot cache.");
            }
        }
    }
}