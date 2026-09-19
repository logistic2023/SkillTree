using System.Collections.Generic;
using UnityEngine;

namespace JollyLlama.SkillTreeSystem
{
    /// <summary>
    /// Stores a reusable node configuration that can be stamped to quickly create
    /// new nodes with pre-filled settings (branch, costs, effects, ranks, visibility).
    /// Created via the Skill Tree Editor → New Node tab → Save as Template.
    /// </summary>
    [CreateAssetMenu(fileName = "New Node Template", menuName = "Skill Tree/Node Template")]
    public class NodeTemplateSO : ScriptableObject
    {
        [Header("Template Info")]
        public string templateName = "New Template";
        [TextArea(1, 2)]
        public string notes = "";

        [Header("Node Settings")]
        public SkillBranch branch    = SkillBranch.Offense;
        public Sprite       icon;
        public int          maxRanks = 1;

        [Header("Costs")]
        public List<ResourceCost> costsPerRank = new();

        [Header("Effects")]
        [SerializeReference]
        public List<SkillEffect> effects = new();

        [Header("Visibility")]
        public bool useGlobalVisibilityRules = true;
        public int  revealBoxRank  = 0;
        public int  revealInfoRank = 0;
        public int  unlockRank     = 1;
    }
}