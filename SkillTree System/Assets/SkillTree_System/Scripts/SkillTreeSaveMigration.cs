namespace JollyLlama.SkillTreeSystem
{
    /// <summary>
    /// Runs a save file forward through any schema/data changes needed to bring it up
    /// to the current version, in order, so a player who last played on an old build
    /// doesn't end up with broken or misinterpreted data after an update.
    ///
    /// To ship a breaking change to save data:
    ///   1. Bump CurrentVersion by 1.
    ///   2. Add a private MigrateToN(state) method for the new version.
    ///   3. Add "if (from &lt; N) MigrateToN(state);" to Migrate(), in order.
    ///
    /// Each migration step should be additive/defensive — assume it may run against a
    /// save that's several versions behind, and never assume a field introduced by a
    /// later step already exists.
    /// </summary>
    public static class SkillTreeSaveMigration
    {
        /// <summary>
        /// The schema version new saves are written at. Bump this whenever a change to
        /// SkillTreeRuntimeState (or how it should be interpreted) needs a migration step.
        /// </summary>
        public const int CurrentVersion = 1;

        /// <summary>
        /// Migrates `state` in place to CurrentVersion. Returns true if anything changed
        /// (including just stamping the version), so the caller knows whether to re-save.
        /// Safe to call on an already-current save (no-op, returns false).
        /// </summary>
        public static bool Migrate(SkillTreeRuntimeState state)
        {
            if (state == null) return false;

            int from = state.saveVersion;

            if (from > CurrentVersion)
            {
                // Save was written by a newer build than this one (e.g. a rollback).
                // We can't migrate backwards — leave the data untouched and let normal
                // play continue. Flag it loudly so a dev notices during testing.
                SkillTreeLogger.LogWarning("SkillTreeSaveMigration",
                    $"Save version ({from}) is newer than this build supports ({CurrentVersion}). " +
                    "Leaving data as-is — some fields it contains may be ignored.");
                return false;
            }

            if (from == CurrentVersion) return false;

            // ── Migration chain — each step only runs if the save predates it ───────
            if (from < 1) MigrateTo1(state);
            // if (from < 2) MigrateTo2(state);   // next migration goes here

            state.saveVersion = CurrentVersion;
            SkillTreeLogger.Log("SkillTreeSaveMigration", $"Migrated save v{from} → v{CurrentVersion}.");
            return true;
        }

        /// <summary>
        /// v1 introduces explicit save versioning itself. No data shape changed — this
        /// just exists so every save from before versioning (implicitly v0) has a
        /// documented starting point for any future migration to build on.
        /// </summary>
        private static void MigrateTo1(SkillTreeRuntimeState state)
        {
            // Nothing to transform yet — presence of this method is the record that
            // "pre-versioning saves become v1 with no data changes."
        }
    }
}

