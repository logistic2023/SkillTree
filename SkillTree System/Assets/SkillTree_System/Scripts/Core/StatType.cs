
namespace JollyLlama.SkillTreeSystem
{
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

