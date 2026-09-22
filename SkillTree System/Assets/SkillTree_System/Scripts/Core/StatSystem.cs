using System;
using System.Collections.Generic;
using UnityEngine;

namespace JollyLlama.SkillTreeSystem
{
    /// <summary>
    /// A ready-made implementation of IStatRegistry — the seam the skill tree package
    /// uses to register/unregister/read stat bonuses without knowing what stats a
    /// client project actually has. Internally this is exactly "a dictionary of
    /// generics" keyed by an open string id: any stat id can be registered on demand,
    /// nothing here needs editing to add a new one.
    ///
    /// This class is provided for convenience — assign it to
    /// SkillTreeStatRegistry.Current (done automatically below, in Awake) and use it
    /// as your project's stat system, or ignore it entirely and provide your own
    /// IStatRegistry implementation instead. The skill tree package only ever talks
    /// to IStatRegistry, never to this class directly.
    /// </summary>
    public class StatSystem : MonoBehaviour, IStatRegistry
    {
        public static StatSystem Instance { get; private set; }

        private readonly Dictionary<string, List<float>> _flatBonuses = new();
        private readonly Dictionary<string, List<float>> _multipliers = new();

        public event Action<string> OnStatChanged;

        // Batch state
        private bool _batching;
        private readonly HashSet<string> _changedDuringBatch = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Self-register as the skill tree package's stat registry. Remove this
            // line (and assign SkillTreeStatRegistry.Current yourself) if you're
            // using your own IStatRegistry implementation instead of this class.
            SkillTreeStatRegistry.Current = this;
        }

        public void BeginBatch()
        {
            _batching = true;
            _changedDuringBatch.Clear();
        }

        public void EndBatch()
        {
            _batching = false;
            foreach (var stat in _changedDuringBatch)
                OnStatChanged?.Invoke(stat);
            _changedDuringBatch.Clear();
        }

        // ── IStatRegistry — string-keyed, open registration ────────────────────────

        public float GetValue(string statId, float baseValue)
            => (baseValue + GetTotalFlat(statId)) * GetTotalMultiplier(statId);

        public int GetValueInt(string statId, int baseValue)
            => Mathf.RoundToInt(GetValue(statId, (float)baseValue));

        public float GetTotalFlat(string statId)
        {
            if (!_flatBonuses.TryGetValue(statId, out var list) || list.Count == 0) return 0f;
            float sum = 0f;
            foreach (float v in list) sum += v;
            return sum;
        }

        public float GetTotalMultiplier(string statId)
        {
            if (!_multipliers.TryGetValue(statId, out var list) || list.Count == 0) return 1f;
            float p = 1f;
            foreach (float v in list) p *= v;
            return p;
        }

        public void RegisterMultiplier(string statId, float multiplier)
        {
            if (!_multipliers.ContainsKey(statId)) _multipliers[statId] = new List<float>();
            _multipliers[statId].Add(multiplier);
            NotifyChanged(statId);
            SkillTreeLogger.Log("StatSystem", $"+mult {statId} ×{multiplier:F3}  →  ×{GetTotalMultiplier(statId):F3}");
        }

        public void UnregisterMultiplier(string statId, float multiplier)
        {
            if (!_multipliers.TryGetValue(statId, out var list)) return;
            list.Remove(multiplier);
            NotifyChanged(statId);
        }

        public void RegisterFlatBonus(string statId, float bonus)
        {
            if (!_flatBonuses.ContainsKey(statId)) _flatBonuses[statId] = new List<float>();
            _flatBonuses[statId].Add(bonus);
            NotifyChanged(statId);
            SkillTreeLogger.Log("StatSystem", $"+flat {statId} +{bonus}  →  +{GetTotalFlat(statId)}");
        }

        public void UnregisterFlatBonus(string statId, float bonus)
        {
            if (!_flatBonuses.TryGetValue(statId, out var list)) return;
            list.Remove(bonus);
            NotifyChanged(statId);
        }

        public void ClearAll()
        {
            var changed = new HashSet<string>();
            foreach (var kv in _flatBonuses) { changed.Add(kv.Key); kv.Value.Clear(); }
            foreach (var kv in _multipliers) { changed.Add(kv.Key); kv.Value.Clear(); }

            if (_batching)
                foreach (var s in changed) _changedDuringBatch.Add(s);
            else
                foreach (var s in changed) OnStatChanged?.Invoke(s);

            SkillTreeLogger.Log("StatSystem", "All modifiers cleared.");
        }

        private void NotifyChanged(string statId)
        {
            if (_batching)
                _changedDuringBatch.Add(statId);
            else
                OnStatChanged?.Invoke(statId);
        }

        public void LogStat(string statId, float baseValue)
        {
            float flat = GetTotalFlat(statId);
            float mult = GetTotalMultiplier(statId);
            float final = GetValue(statId, baseValue);
            SkillTreeLogger.Log("StatSystem", $"<color=cyan>{statId}</color> | base={baseValue} + flat={flat} → {baseValue + flat} | ×{mult:F3} | <color=yellow>final={final:F3}</color>");
        }

        /// <summary>Logs every stat id currently registered (flat and/or multiplier).</summary>
        public void LogAll(float defaultBase = 1f)
        {
            var ids = new HashSet<string>(_flatBonuses.Keys);
            ids.UnionWith(_multipliers.Keys);
            foreach (var id in ids)
            {
                bool hasFlat = _flatBonuses.TryGetValue(id, out var fl) && fl.Count > 0;
                bool hasMult = _multipliers.TryGetValue(id, out var ml) && ml.Count > 0;
                if (hasFlat || hasMult) LogStat(id, defaultBase);
            }
        }
    }
}