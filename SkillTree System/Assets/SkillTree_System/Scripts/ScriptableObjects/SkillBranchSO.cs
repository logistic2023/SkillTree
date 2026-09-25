using UnityEngine;

namespace JollyLlama.SkillTreeSystem
{
    /// <summary>
    /// A skill tree branch defined as data instead of an enum value.
    /// Create as many as you like via Assets > Create > Skill Tree > Skill Branch
    /// (or the "+ New Branch" button in the Skill Tree Editor), then add them to
    /// SkillTreeSO.branches. The order of that list drives tab order in the runtime
    /// panel and column order in the editor's Auto Layout.
    /// </summary>
    [CreateAssetMenu(fileName = "New Skill Branch", menuName = "Skill Tree/Skill Branch")]
    public class SkillBranchSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable unique ID used by CSV/JSON import & export. Set once, never change.")]
        public string branchId;

        [Tooltip("Name shown on tabs, tooltips and in the editor. Falls back to branchId.")]
        public string displayName;

        [TextArea(1, 3)]
        public string description;

        [Header("Visuals")]
        [Tooltip("Tints node backgrounds, tooltip badges, tab buttons and editor nodes.")]
        public Color color = new Color(0.6f, 0.6f, 0.6f);

        [Tooltip("Optional icon for the branch tab.")]
        public Sprite icon;

        /// <summary>Display name, falling back to branchId, then the asset name.</summary>
        public string DisplayName =>
            !string.IsNullOrWhiteSpace(displayName) ? displayName :
            !string.IsNullOrWhiteSpace(branchId)    ? branchId    : name;

        public override string ToString() => DisplayName;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(branchId))
                branchId = name.Replace(' ', '_').ToLowerInvariant();
        }
    }
}