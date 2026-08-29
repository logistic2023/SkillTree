using System;
using UnityEngine;

namespace JollyLlama.SkillTreeSystem
{
    /// <summary>
    /// Backwards-compatibility shim for code that still calls GoldManager directly.
    /// Forwards every call to ResourceManager under the hood.
    ///
    /// MIGRATION:
    ///   - Remove this from your scene once you've updated all call sites to use ResourceManager.
    ///   - Assign your Gold ResourceDefinitionSO to the 'goldResource' field in the Inspector.
    ///
    /// If ResourceManager is not present, this component logs an error and does nothing.
    /// </summary>
    [Obsolete("GoldManager is deprecated. Use ResourceManager directly.")]
    public class GoldManager : MonoBehaviour
    {
        public static GoldManager Instance { get; private set; }

        [Header("Gold Resource")]
        [Tooltip("Assign the Gold ResourceDefinitionSO here. " +
                 "Must match an entry in ResourceManager's tracked resources.")]
        [SerializeField] private ResourceDefinitionSO goldResource;

        /// <summary>Fired when the gold balance changes. Kept for backwards compatibility.</summary>
        public static event Action<int> OnGoldChanged;

        public int CurrentGold => ResourceManager.Instance != null && goldResource != null
            ? ResourceManager.Instance.GetBalance(goldResource)
            : 0;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Forward ResourceManager gold changes to the legacy event
            ResourceManager.OnResourceChanged += OnResourceChanged;
        }

        private void OnDestroy()
        {
            ResourceManager.OnResourceChanged -= OnResourceChanged;
        }

        private void OnResourceChanged(ResourceDefinitionSO resource, int newAmount)
        {
            if (resource == goldResource)
                OnGoldChanged?.Invoke(newAmount);
        }

        // ── Legacy API ────────────────────────────────────────────────────────────

        public void AddGold(int amount)
        {
            if (!Check()) return;
            ResourceManager.Instance.Add(goldResource, amount);
        }

        public bool SpendGold(int amount)
        {
            if (!Check()) return false;
            return ResourceManager.Instance.Spend(goldResource, amount);
        }

        public bool CanAfford(int amount)
            => ResourceManager.Instance != null && goldResource != null
               && ResourceManager.Instance.CanAfford(goldResource, amount);

        public void SetGold(int amount)
        {
            if (!Check()) return;
            ResourceManager.Instance.SetBalance(goldResource, amount);
        }

        private bool Check()
        {
            if (ResourceManager.Instance == null)
            {
                SkillTreeLogger.LogError("GoldManager", "ResourceManager not found. Add it to your scene.");
                return false;
            }
            if (goldResource == null)
            {
                SkillTreeLogger.LogError("GoldManager", "goldResource not assigned in Inspector.");
                return false;
            }
            return true;
        }
    }
}