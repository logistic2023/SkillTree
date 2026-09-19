namespace JollyLlama.SkillTreeSystem
{
    /// <summary>
    /// Single point of contact the skill tree package's own code uses to reach
    /// whatever stat system the client project is running — see IStatRegistry for
    /// the full explanation of why this exists.
    ///
    /// Set once at startup:
    ///     SkillTreeStatRegistry.Current = myStatRegistryImplementation;
    ///
    /// (StatSystem.cs, if you use it, does this for you automatically in Awake().)
    ///
    /// If nothing has been assigned yet, skill effects that touch stats simply no-op
    /// rather than throwing — this lets a skill tree be authored and tested in the
    /// editor before the client project's stat system exists at all.
    /// </summary>
    public static class SkillTreeStatRegistry
    {
        public static IStatRegistry Current { get; set; }
    }
}

