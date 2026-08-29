using UnityEngine;

namespace JollyLlama.SkillTreeSystem
{
    public class SkillTreeBootstrap : MonoBehaviour
    {
        [Header("Required")] [SerializeField] private SkillTreeManager skillTreeManager;

        [Header("Optional — auto-found if null")] [SerializeField]
        private StatSystem statSystem;

        private void Awake()
        {
            if (statSystem == null)
                statSystem = StatSystem.Instance ?? FindObjectOfType<StatSystem>();

            if (statSystem == null)
            {
                var go = new GameObject("StatSystem [Auto]");
                statSystem = go.AddComponent<StatSystem>();
                SkillTreeLogger.LogWarning("SkillTreeBootstrap", "StatSystem not found — created one automatically. Add a StatSystem component to your GameManager for proper setup.");
            }

            if (skillTreeManager == null)
                skillTreeManager = GetComponent<SkillTreeManager>() ?? FindObjectOfType<SkillTreeManager>();

            if (skillTreeManager == null)
            {
                SkillTreeLogger.LogError("SkillTreeBootstrap", "SkillTreeManager not found! Assign it in the Inspector.");
                return;
            }

            skillTreeManager.LoadOrCreate();
            SkillTreeLogger.Log("SkillTreeBootstrap", "Skill tree loaded and stats replayed.");
        }
    }
}