using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace JollyLlama.SkillTreeSystem
{
    public enum SkillTreeDebugLevel
    {
        /// <summary>All informational and error messages are logged.</summary>
        Full,
        /// <summary>Only errors and warnings are logged. (Default)</summary>
        ErrorsOnly,
        /// <summary>No logging whatsoever.</summary>
        Off
    }

    public class SkillTreeManager : MonoBehaviour
    {
        [Header("Data")]
        public SkillTreeSO skillTree;
        public SkillTreeConfigSO config;

        [Header("Save Settings")]
        public string saveFileName = "skilltree.json";

        [Header("Debug")]
        [Tooltip("Full — all info + errors.\nErrorsOnly — warnings & errors only (recommended).\nOff — no logging.")]
        public SkillTreeDebugLevel debugLevel = SkillTreeDebugLevel.ErrorsOnly;

        // ── Events ────────────────────────────────────────────────────────────────

        public event Action<SkillNodeSO, int> OnNodeUnlocked;
        public event Action<SkillNodeSO, int> OnNodeRefunded;
        public event Action                   OnTreeLoaded;

        // ── Private ───────────────────────────────────────────────────────────────

        private SkillTreeRuntimeState _state;

        // Convenience accessor — null-safe
        private ResourceManager Resources => ResourceManager.Instance;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Push the Inspector-set level to the centralised logger before anything else runs.
            SkillTreeLogger.Level = debugLevel;
            LoadOrCreate();
        }

        /// <summary>
        /// Data-integrity problems found in the assigned SkillTreeSO the last time
        /// LoadOrCreate() ran (duplicate node ids, null/empty entries) — see
        /// SkillTreeSO.ValidateIntegrity(). Empty when the tree is clean.
        ///
        /// This is the runtime safety net for bad tree data: editor-time validation
        /// (OnValidate, the editor window's cycle/orphan/duplicate badges) never runs
        /// in an actual build, so without this a duplicate node id could ship
        /// unnoticed and silently make part of the tree unreachable. Checking this
        /// after LoadOrCreate() lets the game react deliberately — block the skill
        /// tree UI, surface it in a QA/smoke-test harness, report it to analytics —
        /// instead of relying on someone spotting a log line.
        /// </summary>
        public IReadOnlyList<string> IntegrityIssues { get; private set; } = Array.Empty<string>();

        public void LoadOrCreate()
        {
            IntegrityIssues = skillTree != null ? skillTree.ValidateIntegrity() : Array.Empty<string>();
            foreach (var issue in IntegrityIssues)
                SkillTreeLogger.LogError("SkillTreeManager", $"Tree data integrity: {issue}");

            var loaded    = Load();
            bool isNewState = loaded == null;
            _state = loaded ?? new SkillTreeRuntimeState { saveVersion = SkillTreeSaveMigration.CurrentVersion };

            bool migrated   = SkillTreeSaveMigration.Migrate(_state);
            bool reconciled = ReconcileState();
            if (!isNewState && (migrated || reconciled))
                Save(); // persist the fix so we don't re-warn about the same issue every load

            ReplayAllEffects();
            OnTreeLoaded?.Invoke();
            SkillTreeLogger.Log("SkillTreeManager", $"Tree loaded. Unlocked nodes: {_state.nodeRanks.Count}");
        }

        /// <summary>
        /// Sanity-checks loaded save data against the current SkillTreeSO after any
        /// version migration has run, catching the two ways design changes to the tree
        /// itself (independent of save schema) can leave old saves inconsistent:
        ///   - A node the player had ranks in no longer exists (removed/renamed nodeId).
        ///   - A node's maxRanks was reduced below a rank the player already has.
        /// Returns true if anything was changed (so the caller knows to re-save).
        /// </summary>
        private bool ReconcileState()
        {
            if (skillTree?.allNodes == null || _state == null) return false;

            bool changed = false;
            // Built via foreach over the dictionary itself (not .Keys) to match the
            // enumeration style already used elsewhere (see GetUnlockedIds), since
            // SerializableDictionary doesn't expose a Keys property.
            var idsToFix = new List<string>();
            foreach (var kvp in _state.nodeRanks) idsToFix.Add(kvp.Key);

            foreach (var nodeId in idsToFix)
            {
                var node = skillTree.GetNode(nodeId);

                if (node == null)
                {
                    // The node this save has ranks in no longer exists in the tree.
                    // We have no reliable way to know what it used to cost (it may have
                    // had different costs per rank, and per-node spend isn't tracked
                    // separately from the aggregate totalSpent), so the safest option is
                    // to drop the orphaned entry rather than silently keep dead data
                    // around forever. This is logged loudly since it represents player
                    // progress disappearing and is worth a designer's attention.
                    int lostRank = _state.GetRank(nodeId);
                    _state.nodeRanks.Remove(nodeId);
                    changed = true;
                    SkillTreeLogger.LogWarning("SkillTreeManager",
                        $"Save referenced unknown node '{nodeId}' at rank {lostRank} " +
                        "(likely removed or renamed since this save was written) — entry dropped.");
                    continue;
                }

                int rank = _state.GetRank(nodeId);
                if (rank > node.maxRanks)
                {
                    // maxRanks was reduced below what this save already has. Clamp down
                    // rather than let ReplayAllEffects apply ranks the node no longer
                    // defines as valid.
                    _state.nodeRanks[nodeId] = node.maxRanks;
                    changed = true;
                    SkillTreeLogger.LogWarning("SkillTreeManager",
                        $"'{node.displayName}' save rank ({rank}) exceeds its current maxRanks " +
                        $"({node.maxRanks}) — clamped down.");
                }
            }

            return changed;
        }

        // ── Unlock ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Attempts to unlock (or rank up) a node.
        /// Reads available balances from ResourceManager.
        /// On success, spends all costs atomically and returns them in UnlockResult.Costs.
        /// </summary>
        public UnlockResult TryUnlock(string nodeId)
        {
            if (skillTree == null)
                return UnlockResult.Fail("No skill tree assigned.");

            var node = skillTree.GetNode(nodeId);
            if (node == null)
                return UnlockResult.Fail($"Node '{nodeId}' not found.");

            int currentRank = _state.GetRank(nodeId);

            if (currentRank >= node.maxRanks)
                return UnlockResult.Fail($"'{node.displayName}' is already at max rank.");

            if (!skillTree.ArePrereqsMet(node, _state))
                return UnlockResult.Fail($"Prerequisites not met for '{node.displayName}'.");

            if (Resources == null)
                return UnlockResult.Fail("ResourceManager not found in scene.");

            int nextRank = currentRank + 1;
            var costs    = node.GetAllCosts(nextRank);

            // Check every resource before spending anything
            if (!Resources.CanAfford(costs))
            {
                string missing = BuildAffordabilityMessage(node, nextRank);
                return UnlockResult.Fail(missing);
            }

            // Atomic spend
            Resources.SpendAll(costs);

            _state.AddRank(nodeId, costs);
            node.ApplyRank(nextRank);
            Save();

            OnNodeUnlocked?.Invoke(node, nextRank);
            SkillTreeLogger.Log("SkillTreeManager", $"Unlocked '{node.displayName}' rank {nextRank}/{node.maxRanks}");

            return UnlockResult.Success(costs);
        }

        /// <summary>
        /// Legacy overload kept for call sites that still pass gold/shard ints.
        /// Ignores the passed values — balances are read from ResourceManager directly.
        /// </summary>
        [Obsolete("Pass no arguments — balances are read from ResourceManager automatically.")]
        public UnlockResult TryUnlock(string nodeId, int ignoredGold, int ignoredShards)
            => TryUnlock(nodeId);

        // ── Refund ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Attempts to refund the top rank of a node, returning its costs to ResourceManager.
        /// </summary>
        public UnlockResult TryRefund(string nodeId)
        {
            if (!_state.IsUnlocked(nodeId))
                return UnlockResult.Fail("Node is not unlocked.");

            if (config != null && !config.AreRefundsAllowed())
                return UnlockResult.Fail("Refunds are disabled.");

            var node = skillTree.GetNode(nodeId);
            if (node == null)
                return UnlockResult.Fail($"Node '{nodeId}' not found.");

            int currentRank = _state.GetRank(nodeId);
            int rankAfter   = currentRank - 1;

            // Block if another unlocked node depends on this one at the current rank
            foreach (var other in skillTree.GetUnlockedNodes(_state))
            {
                if (other.nodeId == nodeId || other.prerequisites == null) continue;
                foreach (var entry in other.prerequisites)
                {
                    if (entry?.node == null || entry.node.nodeId != nodeId) continue;
                    if (rankAfter < entry.requiredRank)
                        return UnlockResult.Fail(
                            $"Cannot refund '{node.displayName}' — '{other.displayName}' requires rank {entry.requiredRank}.");
                }
            }

            var originalCosts = node.GetAllCosts(currentRank);
            var returnedCosts = config != null
                ? config.ApplyRefundPolicy(originalCosts)
                : originalCosts;

            node.RemoveRank(currentRank);
            _state.RemoveRank(nodeId, originalCosts);

            if (Resources != null)
                Resources.RefundAll(returnedCosts);

            Save();

            OnNodeRefunded?.Invoke(node, currentRank);
            SkillTreeLogger.Log("SkillTreeManager", $"Refunded '{node.displayName}' rank {currentRank}");

            return UnlockResult.Success(returnedCosts);
        }

        /// <summary>
        /// Previews what a refund of the top rank of a node would return, without
        /// actually performing it. Respects the current RefundPolicy (Free/Taxed).
        /// Returns an empty list if the node isn't unlocked.
        /// </summary>
        public List<(ResourceDefinitionSO Resource, int Amount)> PreviewRefund(string nodeId)
        {
            var node = skillTree?.GetNode(nodeId);
            if (node == null || !_state.IsUnlocked(nodeId))
                return new List<(ResourceDefinitionSO, int)>();

            int currentRank = _state.GetRank(nodeId);
            var originalCosts = node.GetAllCosts(currentRank);
            return config != null ? config.ApplyRefundPolicy(originalCosts) : originalCosts;
        }

        // ── Reset ─────────────────────────────────────────────────────────────────

        [ContextMenu("Reset")]
        public void ResetTree()
        {
            var stats = SkillTreeStatRegistry.Current;
            stats?.BeginBatch();
            stats?.ClearAll();
            _state.Clear();
            Save();
            stats?.EndBatch();
            OnTreeLoaded?.Invoke();
            SkillTreeLogger.Log("SkillTreeManager", "Tree reset.");
        }

        // ── Queries ───────────────────────────────────────────────────────────────

        public bool IsUnlocked(string nodeId)  => _state.IsUnlocked(nodeId);
        public int  GetRank(string nodeId)     => _state.GetRank(nodeId);

        public bool IsMaxRank(string nodeId)
        {
            var node = skillTree.GetNode(nodeId);
            return node != null && _state.GetRank(nodeId) >= node.maxRanks;
        }

        /// <summary>
        /// Returns true if the node can be unlocked right now (prereqs met + affordable).
        /// </summary>
        public bool CanUnlock(string nodeId)
        {
            var node = skillTree.GetNode(nodeId);
            if (node == null) return false;

            int nextRank = _state.GetRank(nodeId) + 1;
            if (nextRank > node.maxRanks) return false;
            if (!skillTree.ArePrereqsMet(node, _state)) return false;
            if (Resources == null) return false;

            return Resources.CanAfford(node.GetAllCosts(nextRank));
        }

        /// <summary>Legacy overload — gold/shard args ignored.</summary>
        [Obsolete("Use CanUnlock(string) — balances are read from ResourceManager automatically.")]
        public bool CanUnlock(string nodeId, int ignoredGold, int ignoredShards)
            => CanUnlock(nodeId);

        /// <summary>
        /// Returns a human-readable reason why a node cannot be unlocked, or empty string if it can.
        /// </summary>
        public string GetLockReason(string nodeId)
        {
            var node = skillTree.GetNode(nodeId);
            if (node == null) return "Unknown node.";

            int currentRank = _state.GetRank(nodeId);
            if (currentRank >= node.maxRanks) return "Already at max rank.";

            if (!skillTree.ArePrereqsMet(node, _state))
            {
                foreach (var entry in node.prerequisites)
                {
                    if (entry?.node == null) continue;
                    int r = _state.GetRank(entry.node.nodeId);
                    if (r < entry.requiredRank)
                    {
                        string rankNote = entry.requiredRank > 1 ? $" (rank {entry.requiredRank} needed)" : "";
                        return $"Requires: {entry.node.displayName}{rankNote}";
                    }
                }
                return "Prerequisites not met.";
            }

            if (Resources == null) return "ResourceManager not found.";

            return BuildAffordabilityMessage(node, currentRank + 1);
        }

        /// <summary>Legacy overload — gold/shard args ignored.</summary>
        [Obsolete("Use GetLockReason(string) — balances are read from ResourceManager automatically.")]
        public string GetLockReason(string nodeId, int ignoredGold, int ignoredShards)
            => GetLockReason(nodeId);

        public NodeVisibilityState GetNodeVisibilityState(SkillNodeSO node)
        {
            if (node == null) return NodeVisibilityState.Hidden;

            int currentRank = _state.GetRank(node.nodeId);
            if (currentRank > 0) return NodeVisibilityState.Unlocked;

            if (config == null || config.visibilityMode == VisibilityMode.ShowAll)
                return CanUnlock(node.nodeId) ? NodeVisibilityState.Unlockable : NodeVisibilityState.Visible;

            int maxPrereqRank    = GetMaxPrereqRank(node);
            int revealBox        = config.GetRevealBoxRank(node);
            int revealInfo       = config.GetRevealInfoRank(node);
            int unlockThreshold  = config.GetUnlockRank(node);

            if (config.visibilityMode == VisibilityMode.HideUntilUnlocked && maxPrereqRank < revealBox)
                return NodeVisibilityState.Hidden;

            if (config.visibilityMode == VisibilityMode.MysteryBox && maxPrereqRank < revealBox)
                return NodeVisibilityState.Hidden;

            if (config.visibilityMode == VisibilityMode.MysteryBox && maxPrereqRank < revealInfo)
                return NodeVisibilityState.Mystery;

            if (maxPrereqRank < unlockThreshold || !skillTree.ArePrereqsMet(node, _state))
                return NodeVisibilityState.Visible;

            return CanUnlock(node.nodeId) ? NodeVisibilityState.Unlockable : NodeVisibilityState.Visible;
        }

        /// <summary>Legacy overload — gold/shard args ignored.</summary>
        [Obsolete("Use GetNodeVisibilityState(SkillNodeSO) — balances are read from ResourceManager automatically.")]
        public NodeVisibilityState GetNodeVisibilityState(SkillNodeSO node, int ignoredGold, int ignoredShards)
            => GetNodeVisibilityState(node);

        public List<SkillNodeSO> GetAvailableNodes()
            => skillTree.GetAvailableNodes(_state);

        public SkillTreeSO           GetTree()  => skillTree;
        public SkillTreeRuntimeState GetState() => _state;
        public IStatRegistry         Stats      => SkillTreeStatRegistry.Current;

        // ── Save / Load ───────────────────────────────────────────────────────────

        public void Save()
        {
            string path = GetSavePath();
            File.WriteAllText(path, JsonUtility.ToJson(_state, prettyPrint: true));
            SkillTreeLogger.Log("SkillTreeManager", $"Saved to {path}");
        }

        private SkillTreeRuntimeState Load()
        {
            string path = GetSavePath();
            if (!File.Exists(path)) return null;
            SkillTreeLogger.Log("SkillTreeManager", $"Loaded from {path}");
            return JsonUtility.FromJson<SkillTreeRuntimeState>(File.ReadAllText(path));
        }

        private string GetSavePath()
            => Path.Combine(Application.persistentDataPath, saveFileName);

        // ── Effect replay ─────────────────────────────────────────────────────────

        private void ReplayAllEffects()
        {
            if (skillTree == null || _state == null) return;

            var ordered = TopologicalSort(_state.GetUnlockedIds());
            foreach (var node in ordered)
            {
                int rank = _state.GetRank(node.nodeId);
                for (int r = 1; r <= rank; r++)
                    node.ApplyRank(r);
            }

            SkillTreeLogger.Log("SkillTreeManager", $"Replayed effects for {ordered.Count} nodes.");
        }

        private List<SkillNodeSO> TopologicalSort(HashSet<string> unlockedIds)
        {
            var result  = new List<SkillNodeSO>();
            var visited = new HashSet<string>();

            void Visit(SkillNodeSO node)
            {
                if (node == null || visited.Contains(node.nodeId)) return;
                if (node.prerequisites != null)
                    foreach (var entry in node.prerequisites)
                        if (entry?.node != null && unlockedIds.Contains(entry.node.nodeId))
                            Visit(entry.node);
                visited.Add(node.nodeId);
                result.Add(node);
            }

            foreach (var id in unlockedIds)
                Visit(skillTree.GetNode(id));

            return result;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private int GetMaxPrereqRank(SkillNodeSO node)
        {
            if (node.prerequisites == null || node.prerequisites.Count == 0) return int.MaxValue;
            int max = 0;
            foreach (var entry in node.prerequisites)
            {
                if (entry?.node == null) continue;
                max = Math.Max(max, _state.GetRank(entry.node.nodeId));
            }
            return max;
        }

        /// <summary>
        /// Builds a human-readable "Need Xg (have Yg)" style message for the first
        /// resource the player cannot afford.
        /// Returns empty string if all costs are met.
        /// </summary>
        private string BuildAffordabilityMessage(SkillNodeSO node, int rank)
        {
            if (Resources == null) return "ResourceManager not found.";

            foreach (var cost in node.costsPerRank)
            {
                if (cost.resource == null) continue;
                int required  = cost.GetAmount(rank);
                int available = Resources.GetBalance(cost.resource);
                if (available < required)
                    return $"Need {cost.resource.Format(required)} " +
                           $"(have {cost.resource.Format(available)}).";
            }

            return string.Empty;
        }
    }
}