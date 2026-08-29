namespace JollyLlama.SkillTreeSystem
{
    /// <summary>
    /// Describes how a skill node should be presented in the UI for the current player state.
    /// Computed each refresh by SkillTreePanel using SkillTreeConfigSO thresholds.
    /// </summary>
    public enum NodeVisibilityState
    {
        /// <summary>
        /// Node is completely hidden — no button, no slot, no connection line.
        /// Triggered when the player hasn't met revealBoxRank on any prereq node
        /// and the VisibilityMode is HideUntilUnlocked.
        /// </summary>
        Hidden,

        /// <summary>
        /// Node box is visible but shows only a '?' — name, description, icon,
        /// effects and cost are all concealed. Button is non-interactive.
        /// Triggered when revealBoxRank is met but revealInfoRank is not.
        /// </summary>
        Mystery,

        /// <summary>
        /// Node box is fully visible with real info, but greyed out and non-interactive.
        /// The player can see what's coming but can't spend points yet.
        /// Triggered when revealInfoRank is met but unlockRank / prereqs are not.
        /// Also used globally when VisibilityMode is GreyedOut.
        /// </summary>
        Visible,

        /// <summary>
        /// Node is fully visible and the player meets all prerequisites and can afford it.
        /// Normal interactive state — button is clickable.
        /// </summary>
        Unlockable,

        /// <summary>
        /// Node has at least one rank spent. Always fully visible and interactive
        /// (for refund). Supersedes all other states.
        /// </summary>
        Unlocked,
    }
}