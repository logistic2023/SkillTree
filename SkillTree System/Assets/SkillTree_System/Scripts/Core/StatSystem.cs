using System;
using System.Collections.Generic;
using UnityEngine;

namespace JollyLlama.SkillTreeSystem
{
    public class StatSystem : MonoBehaviour
    {
        public static StatSystem Instance { get; private set; }

        private readonly Dictionary<StatType, List<float>> _flatBonuses = new();
        private readonly Dictionary<StatType, List<float>> _multipliers = new();

        public event Action<StatType> OnStatChanged;

        // Batch state
        private bool _batching;
        private readonly HashSet<StatType> _changedDuringBatch = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
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


        public float GetValue(StatType stat, float baseValue)
            => (baseValue + GetTotalFlat(stat)) * GetTotalMultiplier(stat);

        public int GetValueInt(StatType stat, int baseValue)
            => Mathf.RoundToInt(GetValue(stat, (float)baseValue));

        public float GetTotalFlat(StatType stat)
        {
            if (!_flatBonuses.TryGetValue(stat, out var list) || list.Count == 0) return 0f;
            float sum = 0f;
            foreach (float v in list) sum += v;
            return sum;
        }

        public float GetTotalMultiplier(StatType stat)
        {
            if (!_multipliers.TryGetValue(stat, out var list) || list.Count == 0) return 1f;
            float p = 1f;
            foreach (float v in list) p *= v;
            return p;
        }

        public void RegisterMultiplier(StatType stat, float multiplier)
        {
            if (!_multipliers.ContainsKey(stat)) _multipliers[stat] = new List<float>();
            _multipliers[stat].Add(multiplier);
            NotifyChanged(stat);
            SkillTreeLogger.Log("StatSystem", $"+mult {stat} ×{multiplier:F3}  →  ×{GetTotalMultiplier(stat):F3}");
        }

        public void UnregisterMultiplier(StatType stat, float multiplier)
        {
            if (!_multipliers.TryGetValue(stat, out var list)) return;
            list.Remove(multiplier);
            NotifyChanged(stat);
        }

        public void RegisterFlatBonus(StatType stat, float bonus)
        {
            if (!_flatBonuses.ContainsKey(stat)) _flatBonuses[stat] = new List<float>();
            _flatBonuses[stat].Add(bonus);
            NotifyChanged(stat);
            SkillTreeLogger.Log("StatSystem", $"+flat {stat} +{bonus}  →  +{GetTotalFlat(stat)}");
        }

        public void UnregisterFlatBonus(StatType stat, float bonus)
        {
            if (!_flatBonuses.TryGetValue(stat, out var list)) return;
            list.Remove(bonus);
            NotifyChanged(stat);
        }

        public void ClearAll()
        {
            var changed = new HashSet<StatType>();
            foreach (var kv in _flatBonuses)
            {
                changed.Add(kv.Key);
                kv.Value.Clear();
            }

            foreach (var kv in _multipliers)
            {
                changed.Add(kv.Key);
                kv.Value.Clear();
            }

            if (_batching)
            {
                foreach (var s in changed) _changedDuringBatch.Add(s);
            }
            else
            {
                foreach (var s in changed) OnStatChanged?.Invoke(s);
            }

            SkillTreeLogger.Log("StatSystem", "All modifiers cleared.");
        }


        private void NotifyChanged(StatType stat)
        {
            if (_batching)
                _changedDuringBatch.Add(stat);
            else
                OnStatChanged?.Invoke(stat);
        }

        public void LogStat(StatType stat, float baseValue)
        {
            float flat = GetTotalFlat(stat);
            float mult = GetTotalMultiplier(stat);
            float final = GetValue(stat, baseValue);
            SkillTreeLogger.Log("StatSystem", $"<color=cyan>{stat}</color> | base={baseValue} + flat={flat} → {baseValue + flat} | ×{mult:F3} | <color=yellow>final={final:F3}</color>");
        }

        public void LogAll(float defaultBase = 1f)
        {
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                bool hasFlat = _flatBonuses.ContainsKey(stat) && _flatBonuses[stat].Count > 0;
                bool hasMult = _multipliers.ContainsKey(stat) && _multipliers[stat].Count > 0;
                if (hasFlat || hasMult) LogStat(stat, defaultBase);
            }
        }
    }
}