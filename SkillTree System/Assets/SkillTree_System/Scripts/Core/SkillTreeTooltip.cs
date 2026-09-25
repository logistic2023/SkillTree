using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JollyLlama.SkillTreeSystem
{
    public class SkillTreeTooltip : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup      canvasGroup;
        [SerializeField] private RectTransform    panel;
        [SerializeField] private Image            nodeIcon;
        [SerializeField] private Image            branchBadge;
        [SerializeField] private TextMeshProUGUI  nodeNameText;
        [SerializeField] private TextMeshProUGUI  rankText;
        [SerializeField] private TextMeshProUGUI  descriptionText;
        [SerializeField] private TextMeshProUGUI  effectsText;
        [SerializeField] private TextMeshProUGUI  costText;
        [SerializeField] private TextMeshProUGUI  lockReasonText;

        [Header("Branch")]
        [Tooltip("Optional. Shows the branch name (e.g. \"Offense\") tinted with its color.")]
        [SerializeField] private TextMeshProUGUI branchNameText;
        [Tooltip("Badge color when the node has no branch assigned.")]
        [SerializeField] private Color unassignedColor = Color.white;

        [Header("Positioning")]
        [SerializeField] private Vector2 offset      = new Vector2(160f, 0f);
        [SerializeField] private float   edgePadding = 20f;
        [SerializeField] [Tooltip("Extra top padding to keep tooltip below the tab bar (pixels).")]
        private float topPadding = 80f;

        private Canvas _canvas;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            Hide();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void Show(SkillNodeSO node, int currentRank, Vector3 anchorWorldPos,
            NodeVisibilityState visibility = NodeVisibilityState.Unlockable, string lockReason = null)
        {
            if (node == null) return;
            if (visibility == NodeVisibilityState.Hidden) return;

            bool isMystery = visibility == NodeVisibilityState.Mystery;

            // Icon
            if (nodeIcon != null)
            {
                nodeIcon.sprite  = isMystery ? null : node.icon;
                nodeIcon.enabled = !isMystery && node.icon != null;
            }

            // Branch badge
            if (branchBadge != null)
                branchBadge.color = isMystery ? Color.gray : BranchColor(node.branch);

            if (branchNameText != null)
            {
                bool showBranch = !isMystery && node.branch != null;
                branchNameText.gameObject.SetActive(showBranch);
                if (showBranch)
                {
                    branchNameText.text  = node.branch.DisplayName;
                    branchNameText.color = node.branch.color;
                }
            }

            // Name
            if (nodeNameText != null)
                nodeNameText.text = isMystery ? "???" : node.displayName;

            // Rank
            if (rankText != null)
            {
                rankText.gameObject.SetActive(!isMystery && node.maxRanks > 1);
                rankText.text = $"Rank {currentRank} / {node.maxRanks}";
            }

            // Description
            if (descriptionText != null)
                descriptionText.text = isMystery
                    ? "Unlock the previous node to reveal."
                    : node.description;

            // Effects
            if (effectsText != null)
            {
                if (isMystery)
                {
                    effectsText.text = "???";
                    effectsText.gameObject.SetActive(true);
                }
                else
                {
                    var sb = new System.Text.StringBuilder();
                    if (node.effects != null && node.effects.Count > 0)
                    {
                        // Each rank grants the same effect list again (stacking), so this
                        // doubles as both "what you have" (if rank > 0) and "what the
                        // next rank adds" (if rank < maxRanks) — labelled accordingly.
                        bool hasRank = currentRank > 0;
                        bool hasNext = currentRank < node.maxRanks;

                        if (hasRank)
                        {
                            sb.AppendLine("Current:");
                            foreach (var effect in node.effects)
                                if (effect != null) sb.AppendLine($"  • {effect.GetDescription()}");
                        }

                        if (hasNext)
                        {
                            if (hasRank) sb.AppendLine();
                            sb.AppendLine("Next rank adds:");
                            foreach (var effect in node.effects)
                                if (effect != null) sb.AppendLine($"  • {effect.GetDescription()}");
                        }
                    }
                    effectsText.text = sb.ToString().TrimEnd();
                    effectsText.gameObject.SetActive(node.effects != null && node.effects.Count > 0);
                }
            }

            // Cost — generic, works for any number of resources
            if (costText != null)
            {
                if (isMystery)
                {
                    costText.text = string.Empty;
                }
                else if (currentRank >= node.maxRanks)
                {
                    costText.text = "Max rank reached";
                }
                else
                {
                    int nextRank  = currentRank + 1;
                    costText.text = $"Cost: {node.GetCostString(nextRank)}";
                }
            }

            // Lock reason — only meaningful for a node the player can see but can't yet buy.
            if (lockReasonText != null)
            {
                bool showLockReason = !isMystery
                    && visibility != NodeVisibilityState.Hidden
                    && !string.IsNullOrEmpty(lockReason);

                lockReasonText.gameObject.SetActive(showLockReason);
                lockReasonText.text = showLockReason ? $"🔒 {lockReason}" : string.Empty;
            }

            gameObject.SetActive(true);
            if (canvasGroup != null) canvasGroup.alpha = 1f;

            PositionNearAnchor(anchorWorldPos);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        // ── Positioning ───────────────────────────────────────────────────────────

        private void PositionNearAnchor(Vector3 anchorScreenPos)
        {
            if (panel == null) return;

            Canvas.ForceUpdateCanvases();

            float   w         = panel.rect.width;
            float   h         = panel.rect.height;
            Vector2 screenPos = (Vector2)anchorScreenPos + offset;

            screenPos.x = Mathf.Clamp(screenPos.x, edgePadding, Screen.width  - w - edgePadding);
            screenPos.y = Mathf.Max(screenPos.y, edgePadding);
            screenPos.y = Mathf.Min(screenPos.y, Screen.height - h - topPadding);

            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvas.GetComponent<RectTransform>(),
                    screenPos, _canvas.worldCamera, out Vector2 localPoint);
                panel.localPosition = localPoint;
            }
            else
            {
                panel.position = screenPos;
            }
        }

        private Color BranchColor(SkillBranchSO branch)
            => branch != null ? branch.color : unassignedColor;
    }
}