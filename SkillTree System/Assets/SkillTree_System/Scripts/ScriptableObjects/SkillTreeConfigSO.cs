using UnityEngine;

namespace JollyLlama.SkillTreeSystem
{
    /// <summary>
    /// Global policy asset for the skill tree.
    /// Create one via Assets > Skill Tree > Skill Tree Config and assign it to SkillTreeManager.
    /// Individual nodes can override these rules by unchecking useGlobalVisibilityRules.
    /// </summary>
    [CreateAssetMenu(fileName = "SkillTreeConfig", menuName = "Skill Tree/Skill Tree Config")]
    public class SkillTreeConfigSO : ScriptableObject
    {
        // ── Visibility policy ─────────────────────────────────────────────────────

        [Header("Visibility Mode")]
        [Tooltip("Controls how undiscovered nodes appear in the UI.\n\n" +
                 "ShowAll         — Every node is always fully visible (current behaviour).\n" +
                 "GreyedOut       — Every node is visible but locked nodes are greyed out and non-interactive.\n" +
                 "MysteryBox      — Locked nodes show as a '?' box. Info is hidden until revealInfoRank is met.\n" +
                 "HideUntilUnlocked — Nodes are completely invisible until their revealBoxRank is met.")]
        public VisibilityMode visibilityMode = VisibilityMode.ShowAll;

        // ── Global thresholds (used when a node has useGlobalVisibilityRules = true) ──

        [Header("Global Thresholds")]
        [Tooltip("How many ranks must be spent on ANY prerequisite node before a connected node's " +
                 "box appears in the UI at all. 0 = always visible.\n" +
                 "Only meaningful when VisibilityMode is MysteryBox or HideUntilUnlocked.")]
        [Min(0)] public int defaultRevealBoxRank = 0;

        [Tooltip("How many ranks must be spent on ANY prerequisite node before the '?' is lifted " +
                 "and the real name, description, and effects are shown. 0 = always revealed.\n" +
                 "Only meaningful when VisibilityMode is MysteryBox.")]
        [Min(0)] public int defaultRevealInfoRank = 1;

        [Tooltip("How many ranks must be spent on ANY prerequisite node before this node can be " +
                 "purchased. Acts as a global minimum on top of each node's own PrerequisiteEntry.\n" +
                 "Set to 1 to require at least one point on any prereq node before unlocking.")]
        [Min(0)] public int defaultUnlockRank = 1;

        // ── Refund policy ─────────────────────────────────────────────────────────

        [Header("Refund Policy")]
        [Tooltip("Whether refunds are allowed at all, and whether they cost anything.\n\n" +
                 "Free      — Full refund, no cost.\n" +
                 "Disabled  — Refunds are never allowed.\n" +
                 "(Future: Taxed — Refund returns a percentage of the original cost.)")]
        public RefundPolicy refundPolicy = RefundPolicy.Free;

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the effective revealBoxRank for a node, respecting its override flag.
        /// </summary>
        public int GetRevealBoxRank(SkillNodeSO node)
            => node.useGlobalVisibilityRules ? defaultRevealBoxRank : node.revealBoxRank;

        /// <summary>
        /// Returns the effective revealInfoRank for a node, respecting its override flag.
        /// </summary>
        public int GetRevealInfoRank(SkillNodeSO node)
            => node.useGlobalVisibilityRules ? defaultRevealInfoRank : node.revealInfoRank;

        /// <summary>
        /// Returns the effective unlockRank for a node, respecting its override flag.
        /// </summary>
        public int GetUnlockRank(SkillNodeSO node)
            => node.useGlobalVisibilityRules ? defaultUnlockRank : node.unlockRank;
    }
    
    public enum VisibilityMode
    {
        /// <summary>Every node is always fully visible. Current / legacy behaviour.</summary>
        ShowAll,

        /// <summary>All nodes shown, but nodes that don't meet revealBoxRank are greyed out
        /// and non-interactive. Info is always visible.</summary>
        GreyedOut,

        /// <summary>Nodes that haven't met revealBoxRank show as a locked '?' box.
        /// Info hidden until revealInfoRank is met on the prereq node.</summary>
        MysteryBox,

        /// <summary>Nodes are completely invisible until their revealBoxRank is met.</summary>
        HideUntilUnlocked,
    }

    public enum RefundPolicy
    {
        /// <summary>Refunds are free — full cost returned.</summary>
        Free,

        /// <summary>Refunds are disabled entirely.</summary>
        Disabled,
    }
}