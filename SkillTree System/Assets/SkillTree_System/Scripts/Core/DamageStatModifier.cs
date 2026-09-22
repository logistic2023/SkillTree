using UnityEngine;
using UnityEngine.Serialization;

namespace JollyLlama.SkillTreeSystem
{
    public class DamageStatModifier : MonoBehaviour
    {
        [Tooltip("If true, applies crit chance roll on every hit.")]
        [FormerlySerializedAs("applycrits")]
        public bool applyCrits = true;

        private IStatRegistry _stats;

        private void Awake()
        {
            _stats = SkillTreeStatRegistry.Current;
        }

        public float Scale(float rawDamage)
        {
            if (_stats == null) return rawDamage;

            float damage = _stats.GetValue("DamageMultiplier", rawDamage);

            if (applyCrits)
                damage = ApplyCrit(damage);

            return damage;
        }

        private float ApplyCrit(float damage)
        {
            if (_stats == null) return damage;

            float critChance = _stats.GetValue("CritChance", 0f);
            float critMult   = _stats.GetValue("CritMultiplier", 1f);

            if (Random.value <= critChance)
            {
                SkillTreeLogger.Log("DamageStatModifier", $"CRIT! {damage:F1} × {critMult:F2} = {damage * critMult:F1}");
                return damage * critMult;
            }

            return damage;
        }
    }
}