using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JollyLlama.SkillTreeSystem
{
    public class SkillNodeButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("References")]
        [SerializeField] private Button             button;
        [SerializeField] private Image              background;
        [SerializeField] private Image              icon;
        [SerializeField] private Image              rimHighlight;   // border glow when unlockable
        [SerializeField] private TextMeshProUGUI    rankText;       // "1/3", hidden if maxRanks=1
        [SerializeField] private TextMeshProUGUI    costText;       // cost string below node
        [SerializeField] private GameObject         lockedOverlay;
        [SerializeField] private GameObject         maxRankBadge;
        [SerializeField] private GameObject         mysteryOverlay;
        [SerializeField] private SkillNodeUnlockBurst unlockBurst; // optional — dark-fantasy purchase VFX

        [Header("Branch Colors")]
        [SerializeField] private Color offenseColor = new Color(0.85f, 0.25f, 0.20f);
        [SerializeField] private Color controlColor = new Color(0.20f, 0.45f, 0.85f);
        [SerializeField] private Color economyColor = new Color(0.85f, 0.75f, 0.15f);
        [SerializeField] private Color defenseColor = new Color(0.55f, 0.55f, 0.60f);
        [SerializeField] private Color lockedColor  = new Color(0.25f, 0.25f, 0.25f);

        // ── Runtime ───────────────────────────────────────────────────────────────

        private SkillNodeSO  _node;
        private SkillTreePanel _panel;

        public SkillNodeSO Node => _node;

        // ── Init ──────────────────────────────────────────────────────────────────

        public void Initialize(SkillNodeSO node, SkillTreePanel panel)
        {
            _node  = node;
            _panel = panel;

            if (icon != null && node.icon != null)
                icon.sprite = node.icon;

            button.onClick.AddListener(OnClicked);
        }

        // ── Refresh ───────────────────────────────────────────────────────────────

        public void Refresh(int currentRank, NodeVisibilityState visibility)
        {
            bool isMystery    = visibility == NodeVisibilityState.Mystery;
            bool isUnlockable = visibility == NodeVisibilityState.Unlockable;
            bool isUnlocked   = visibility == NodeVisibilityState.Unlocked;
            bool maxRank      = currentRank >= _node.maxRanks;

            // Mystery overlay
            if (mysteryOverlay != null) mysteryOverlay.SetActive(isMystery);

            // Background tint
            if (background != null)
                background.color = isUnlocked || isUnlockable
                    ? BranchColor(_node.branch)
                    : lockedColor;

            // Icon
            if (icon != null) icon.gameObject.SetActive(!isMystery);

            // Rank text  e.g. "1/3"
            if (rankText != null)
            {
                rankText.gameObject.SetActive(!isMystery && _node.maxRanks > 1);
                rankText.text = $"{currentRank}/{_node.maxRanks}";
            }

            // Cost text — uses generic GetCostString so it works for any resource combo
            if (costText != null)
            {
                costText.gameObject.SetActive(!isMystery);

                if (maxRank)
                {
                    costText.text  = "MAX";
                    costText.color = Color.green;
                }
                else
                {
                    int nextRank = currentRank + 1;
                    costText.text  = _node.GetCostString(nextRank);
                    costText.color = isUnlockable ? Color.white : new Color(1f, 0.4f, 0.4f);
                }
            }

            // Overlays
            if (lockedOverlay != null) lockedOverlay.SetActive(
                visibility == NodeVisibilityState.Visible);

            if (maxRankBadge != null)  maxRankBadge.SetActive(isUnlocked && maxRank);
            if (rimHighlight != null)  rimHighlight.gameObject.SetActive(isUnlockable);

            button.interactable = isUnlockable || isUnlocked;
        }

        // ── Pointer events ────────────────────────────────────────────────────────

        /// <summary>Plays the unlock/rank-up VFX (ember burn, sparks, scale punch)
        /// if an unlockBurst component is assigned. No-op otherwise, so this is
        /// safe to call unconditionally from SkillTreePanel on every successful
        /// purchase, whether or not this particular prefab has the effect wired up.</summary>
        public void PlayUnlockBurst() => unlockBurst?.Play();

        private void OnClicked() => _panel.OnNodeButtonClicked(_node);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                _panel.OnNodeRightClicked(_node);
        }

        public void OnPointerEnter(PointerEventData eventData)
            => _panel.OnNodeHovered(_node, eventData.position);

        public void OnPointerExit(PointerEventData eventData)
            => _panel.OnNodeHoverEnd();

        // ── Helpers ───────────────────────────────────────────────────────────────

        private Color BranchColor(SkillBranch branch) => branch switch
        {
            SkillBranch.Offense => offenseColor,
            SkillBranch.Control => controlColor,
            SkillBranch.Economy => economyColor,
            SkillBranch.Defense => defenseColor,
            _ => Color.white
        };
    }
}