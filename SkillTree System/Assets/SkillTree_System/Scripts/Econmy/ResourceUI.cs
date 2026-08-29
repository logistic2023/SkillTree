using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JollyLlama.SkillTreeSystem
{
    /// <summary>
    /// Displays the current balance of any ResourceDefinitionSO in a TextMeshPro label.
    /// Replaces the old GoldUI. Works for Gold, Shards, Crystals — any resource tracked
    /// by ResourceManager.
    ///
    /// Setup:
    ///   1. Assign the ResourceDefinitionSO you want to display to 'resource'.
    ///   2. Assign a TMP label to 'amountText'.
    ///   3. Optionally assign an Image to 'resourceIcon' — it will be set from the SO's icon.
    /// </summary>
    public class ResourceUI : MonoBehaviour
    {
        [Header("Resource")]
        [Tooltip("Which resource to display. Must be tracked by ResourceManager.")]
        [SerializeField] private ResourceDefinitionSO resource;

        [Header("References")]
        [SerializeField] private TextMeshProUGUI amountText;
        [SerializeField] private Image resourceIcon;

        [Header("Display")]
        [Tooltip("Prefix before the amount, e.g. 'Gold: '.  Leave empty to use the resource's displayName + ': '.")]
        [SerializeField] private string prefixOverride = "";

        [Tooltip("Number format passed to int.ToString(), e.g. 'N0' for thousands separators.")]
        [SerializeField] private string numberFormat = "N0";

        [Tooltip("When true, the label is tinted with the resource's displayColor.")]
        [SerializeField] private bool tintLabel = true;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            ResourceManager.OnResourceChanged += OnResourceChanged;
            RefreshIcon();
            RefreshDisplay(ResourceManager.Instance != null && resource != null
                ? ResourceManager.Instance.GetBalance(resource) : 0);
        }

        private void OnDisable()
        {
            ResourceManager.OnResourceChanged -= OnResourceChanged;
        }

        // ── Callbacks ─────────────────────────────────────────────────────────────

        private void OnResourceChanged(ResourceDefinitionSO changed, int newAmount)
        {
            if (changed == resource)
                RefreshDisplay(newAmount);
        }

        // ── Display ───────────────────────────────────────────────────────────────

        private void RefreshIcon()
        {
            if (resourceIcon == null || resource == null) return;
            resourceIcon.sprite  = resource.icon;
            resourceIcon.enabled = resource.icon != null;
        }

        private void RefreshDisplay(int amount)
        {
            if (amountText == null || resource == null) return;

            string prefix = string.IsNullOrEmpty(prefixOverride)
                ? $"{resource.displayName}: "
                : prefixOverride;

            amountText.text = prefix + amount.ToString(numberFormat);

            if (tintLabel)
                amountText.color = resource.displayColor;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Backwards-compatibility alias so existing GoldUI references keep working.
    /// Just assign the Gold ResourceDefinitionSO — it works identically to ResourceUI.
    /// Remove once all old GoldUI references are updated.
    /// </summary>
    [System.Obsolete("GoldUI is deprecated. Use ResourceUI instead.")]
    public class GoldUI : ResourceUI { }
}