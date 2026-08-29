using UnityEngine;


namespace JollyLlama.SkillTreeSystem
{


    /// <summary>
    /// Centralised logger for the entire skill tree system.
    /// Set <see cref="Level"/> once (via SkillTreeManager.Awake) and every
    /// script in the system will respect it automatically.
    /// </summary>
    public static class SkillTreeLogger
    {
        public static SkillTreeDebugLevel Level { get; set; } = SkillTreeDebugLevel.ErrorsOnly;

        /// <summary>Logs an informational message. Only shown on Full.</summary>
        public static void Log(string tag, string msg)
        {
            if (Level == SkillTreeDebugLevel.Full)
                Debug.Log($"[{tag}] {msg}");
        }

        /// <summary>Logs a warning. Shown on Full and ErrorsOnly.</summary>
        public static void LogWarning(string tag, string msg)
        {
            if (Level != SkillTreeDebugLevel.Off)
                Debug.LogWarning($"[{tag}] {msg}");
        }

        /// <summary>Logs an error. Shown on Full and ErrorsOnly.</summary>
        public static void LogError(string tag, string msg)
        {
            if (Level != SkillTreeDebugLevel.Off)
                Debug.LogError($"[{tag}] {msg}");
        }
    }
}