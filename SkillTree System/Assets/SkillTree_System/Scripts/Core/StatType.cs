namespace JollyLlama.SkillTreeSystem
{
    /// <summary>
    /// NOTE: The skill tree package's own effects (StatFlatBonusEffect,
    /// StatMultiplierEffect) no longer reference this enum - they register stats by
    /// an open string id through IStatRegistry / SkillTreeStatRegistry, so new stats
    /// never require editing this package.
    ///
    /// This enum is kept only as an optional, strongly-typed convenience for existing
    /// gameplay-integration code (DamageStatModifier, SpellStatIntegration,
    /// TotemStatIntegration) that reads stat values via StatSystem's legacy
    /// StatType-based overloads (which key off `stat.ToString()` under the hood).
    /// You're free to keep using it for your own client-side code, extend it, or
    /// drop it entirely in favor of plain string ids - the skill tree package doesn't
    /// care either way.
    /// </summary>
    public enum StatType
    {
        // === DAMAGE ===
        DamageMultiplier,
        CritChance,
        CritMultiplier,
        ReactionDamageMultiplier,

        // === FIRE RATE & CHARGE ===
        FireRate,
        ChargeRate,
        MaxCharge,
        BurstFireRate,

        // === PROJECTILE ===
        ProjectileSpeed,
        ProjectileSize,
        ProjectileCount,
        PenetrationCount,

        // === TAG & REACTION ===
        TagDuration,
        TagSpreadRadius,
        TagProcChance,
        ReactionCooldownReduction,

        // === ECONOMY ===
        GoldDropMultiplier,
        ShardDropChance,
        BehaviorBoostDropChance,
        MiningYield,

        // === TOTEM / DEFENSE ===
        TotemMaxHP,
        TotemMoveSpeed,
        PanicBurstChargeConsumed,
        PanicBurstRadius,
    }
}