using System;

namespace JollyLlama.SkillTreeSystem
{
    /// <summary>
    /// The seam between the skill tree system and a client project's own stats.
    ///
    /// The skill tree package never assumes a fixed, closed set of stats — its own
    /// effects (StatFlatBonusEffect, StatMultiplierEffect) only know how to
    /// register/unregister/read a named bonus through this interface, keyed by an
    /// arbitrary string chosen by whoever authors the skill tree. Adding a brand new
    /// stat is just typing a new id in the Inspector; it never requires editing a
    /// closed enum inside this package.
    ///
    /// A client project provides ANY implementation — their own stat system, an ECS
    /// component, a simple dictionary, whatever — and assigns it once at startup:
    ///
    ///     SkillTreeStatRegistry.Current = myImplementation;
    ///
    /// StatSystem.cs in this package is one ready-made implementation (a dictionary
    /// of generics, as a reference/example) — use it as-is, or ignore it completely
    /// and wire up your own. Either way, nothing in the skill tree package itself
    /// needs to change.
    /// </summary>
    public interface IStatRegistry
    {
        /// <summary>
        /// Raised whenever a stat's total flat bonus or multiplier changes, so
        /// listeners (gameplay integrations, UI, etc.) know to re-pull GetValue
        /// for that stat id.
        /// </summary>
        event Action<string> OnStatChanged;

        void RegisterFlatBonus(string statId, float bonus);
        void UnregisterFlatBonus(string statId, float bonus);

        void RegisterMultiplier(string statId, float multiplier);
        void UnregisterMultiplier(string statId, float multiplier);

        float GetTotalFlat(string statId);
        float GetTotalMultiplier(string statId);

        /// <summary>(baseValue + total flat) × total multiplier.</summary>
        float GetValue(string statId, float baseValue);
        int   GetValueInt(string statId, int baseValue);
    }
}

namespace JollyLlama.SkillTreeSystem
{
}