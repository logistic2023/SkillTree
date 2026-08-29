using UnityEngine;


namespace JollyLlama.SkillTreeSystem
{
    public class DamageStatModifier : MonoBehaviour
    {
        [Tooltip("If true, applies crit chance roll on every hit.")]
        public bool applycrits = true;

        private StatSystem _stats;

        private void Awake()
        {
            _stats = StatSystem.Instance;
        }

        public float Scale(float rawDamage)
        {
            if (_stats == null) return rawDamage;

            float damage = _stats.GetValue(StatType.DamageMultiplier, rawDamage);

            if (applycrits)
                damage = ApplyCrit(damage);

            return damage;
        }

        private float ApplyCrit(float damage)
        {
            if (_stats == null) return damage;

            float critChance = _stats.GetValue(StatType.CritChance, 0f);
            float critMult = _stats.GetValue(StatType.CritMultiplier, 1f);

            if (Random.value <= critChance)
            {
                SkillTreeLogger.Log("DamageStatModifier", $"CRIT! {damage:F1} × {critMult:F2} = {damage * critMult:F1}");
                return damage * critMult;
            }

            return damage;
        }
    }
}