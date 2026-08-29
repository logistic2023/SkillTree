using System;
using System.Collections.Generic;
using UnityEngine;

namespace JollyLlama.SkillTreeSystem
{
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance { get; private set; }

        [Header("Tracked Resources")]
        [SerializeField] private List<ResourceEntry> trackedResources = new();



        public static event Action<ResourceDefinitionSO, int> OnResourceChanged;
        public static event Action<string, int>               OnResourceChangedById;

        private readonly Dictionary<string, ResourceRecord> _records = new();


                // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitialiseRecords();
        }

        private void InitialiseRecords()
        {
            _records.Clear();
            if (trackedResources == null || trackedResources.Count == 0)
            {
                SkillTreeLogger.LogWarning("ResourceManager", "trackedResources list is empty! " +
                                 "Add your ResourceDefinitionSO assets in the Inspector.");
                return;
            }

            foreach (var entry in trackedResources)
            {
                if (entry.definition == null)
                {
                    SkillTreeLogger.LogWarning("ResourceManager", "Null entry in trackedResources — skipped.");
                    continue;
                }

                if (string.IsNullOrEmpty(entry.definition.resourceId))
                {
                    SkillTreeLogger.LogWarning("ResourceManager", $"ResourceDefinitionSO '{entry.definition.name}' " +
                                     "has an empty resourceId — skipped. Set it in the SO's Inspector.");
                    continue;
                }

                string id = entry.definition.resourceId;
                if (_records.ContainsKey(id))
                {
                    SkillTreeLogger.LogWarning("ResourceManager", $"Duplicate resourceId '{id}' — skipped.");
                    continue;
                }

                _records[id] = new ResourceRecord
                {
                    Definition = entry.definition,
                    Balance    = entry.startingAmount
                };

                SkillTreeLogger.Log("ResourceManager", $"Tracking '{entry.definition.displayName}' " +
                          $"(id='{id}') starting={entry.startingAmount}");
            }
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public int GetBalance(ResourceDefinitionSO resource)
        {
            if (resource == null) return 0;
            return _records.TryGetValue(resource.resourceId, out var rec) ? rec.Balance : 0;
        }

        public int GetBalance(string resourceId)
            => _records.TryGetValue(resourceId, out var rec) ? rec.Balance : 0;

        public bool CanAfford(IReadOnlyList<(ResourceDefinitionSO Resource, int Amount)> costs)
        {
            foreach (var (res, amt) in costs)
                if (GetBalance(res) < amt) return false;
            return true;
        }

        public bool CanAfford(ResourceDefinitionSO resource, int amount)
            => GetBalance(resource) >= amount;

        public void Add(ResourceDefinitionSO resource, int amount)
        {
            if (resource == null)
            {
                SkillTreeLogger.LogWarning("ResourceManager", "Add called with a null ResourceDefinitionSO. " +
                                 "Assign the SO to the caller's 'resource' field in the Inspector.");
                return;
            }

            if (amount <= 0) return;

            if (!_records.TryGetValue(resource.resourceId, out var rec))
            {
                // Show all tracked IDs so the mismatch is immediately obvious
                string tracked = _records.Count > 0
                    ? string.Join(", ", _records.Keys)
                    : "(none — trackedResources list may be empty)";

                SkillTreeLogger.LogWarning("ResourceManager",
                    $"Add FAILED: resourceId '{resource.resourceId}' is not tracked.\n" +
                    $"Tracked ids: [{tracked}]\n" +
                    "Fix: add this ResourceDefinitionSO to ResourceManager.trackedResources in the Inspector.");
                return;
            }

            // Apply stat-based drop multiplier if configured on the SO
            if (resource.useDropMultiplier && resource.dropMultiplierStat.HasValue
                && StatSystem.Instance != null)
            {
                float mult = StatSystem.Instance.GetTotalMultiplier(resource.dropMultiplierStat.Value);
                amount = Mathf.Max(1, Mathf.RoundToInt(amount * mult));
            }

            rec.Balance += amount;
            _records[resource.resourceId] = rec;

            SkillTreeLogger.Log("ResourceManager", $"+{amount} '{resource.displayName}' → {rec.Balance}");

            FireChanged(resource, rec.Balance);
        }

        public void Add(string resourceId, int amount)
        {
            if (_records.TryGetValue(resourceId, out var rec))
                Add(rec.Definition, amount);
            else
            {
                string tracked = _records.Count > 0
                    ? string.Join(", ", _records.Keys)
                    : "(none)";
                SkillTreeLogger.LogWarning("ResourceManager", $"Add: resourceId '{resourceId}' not tracked. " +
                                 $"Tracked: [{tracked}]");
            }
        }

        public bool Spend(ResourceDefinitionSO resource, int amount)
        {
            if (resource == null || amount <= 0) return true;
            if (!_records.TryGetValue(resource.resourceId, out var rec))
            {
                SkillTreeLogger.LogWarning("ResourceManager", $"Spend: '{resource.resourceId}' is not tracked.");
                return false;
            }
            if (rec.Balance < amount) return false;

            rec.Balance -= amount;
            _records[resource.resourceId] = rec;

            SkillTreeLogger.Log("ResourceManager", $"-{amount} '{resource.displayName}' → {rec.Balance}");

            FireChanged(resource, rec.Balance);
            return true;
        }

        public bool SpendAll(IReadOnlyList<(ResourceDefinitionSO Resource, int Amount)> costs)
        {
            foreach (var (res, amt) in costs)
                if (GetBalance(res) < amt) return false;
            foreach (var (res, amt) in costs)
                Spend(res, amt);
            return true;
        }

        public void RefundAll(IReadOnlyList<(ResourceDefinitionSO Resource, int Amount)> costs)
        {
            foreach (var (res, amt) in costs)
            {
                if (!_records.TryGetValue(res.resourceId, out var rec)) continue;
                rec.Balance += amt;
                _records[res.resourceId] = rec;
                FireChanged(res, rec.Balance);
            }
        }

        public void SetBalance(ResourceDefinitionSO resource, int amount)
        {
            if (resource == null) return;
            if (!_records.TryGetValue(resource.resourceId, out var rec))
            {
                SkillTreeLogger.LogWarning("ResourceManager", $"SetBalance: '{resource.resourceId}' is not tracked.");
                return;
            }
            rec.Balance = Mathf.Max(0, amount);
            _records[resource.resourceId] = rec;
            FireChanged(resource, rec.Balance);
        }

        public Dictionary<string, int> GetAllBalances()
        {
            var snap = new Dictionary<string, int>(_records.Count);
            foreach (var kv in _records) snap[kv.Key] = kv.Value.Balance;
            return snap;
        }

        public void RestoreBalances(Dictionary<string, int> snapshot)
        {
            foreach (var kv in snapshot)
            {
                if (!_records.TryGetValue(kv.Key, out var rec)) continue;
                rec.Balance = Mathf.Max(0, kv.Value);
                _records[kv.Key] = rec;
                FireChanged(rec.Definition, rec.Balance);
            }
        }

        // ── Context menu — test without leaving the Inspector ─────────────────────

        [ContextMenu("Debug: Log All Balances")]
        private void DebugLogAll()
        {
            if (_records.Count == 0) { SkillTreeLogger.Log("ResourceManager", "No resources tracked."); return; }
            foreach (var kv in _records)
                SkillTreeLogger.Log("ResourceManager", $"{kv.Value.Definition.displayName} ('{kv.Key}'): {kv.Value.Balance}");
        }

        [ContextMenu("Debug: Add 100 to each resource")]
        private void DebugAddAll()
        {
            foreach (var kv in _records)
                Add(kv.Value.Definition, 100);
        }

        // ── Internal ──────────────────────────────────────────────────────────────

        private void FireChanged(ResourceDefinitionSO resource, int newBalance)
        {
            OnResourceChanged?.Invoke(resource, newBalance);
            OnResourceChangedById?.Invoke(resource.resourceId, newBalance);
        }

        [Serializable]
        public class ResourceEntry
        {
            public ResourceDefinitionSO definition;
            [Min(0)] public int startingAmount = 0;
        }

        private struct ResourceRecord
        {
            public ResourceDefinitionSO Definition;
            public int Balance;
        }
    }
}