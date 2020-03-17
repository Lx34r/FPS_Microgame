using UnityEngine;

public class Damageable : MonoBehaviour
{
    [Tooltip("伤害倍数")]
    public float damageMultiplier = 1f;
    [Range(0, 1)]
    [Tooltip("自伤倍数（自己误伤）")]
    public float sensibilityToSelfdamage = 0.5f;

    public Health health { get; private set; }

    void Awake()
    {
        // 在hierarchy中获取health组件（没找到就往上层找）
        health = GetComponent<Health>();
        if (!health)
        {
            health = GetComponentInParent<Health>();
        }
    }

    public void InflictDamage(float damage, bool isExplosionDamage, GameObject damageSource)
    {

        if(health)
        {
            var totalDamage = damage;

            // 爆炸跳过伤害倍数
            if (!isExplosionDamage)
            {
                totalDamage *= damageMultiplier;
            }

            // 自伤时应用自伤倍数
            if (health.gameObject == damageSource)
            {
                totalDamage *= sensibilityToSelfdamage;
            }

            health.TakeDamage(totalDamage, damageSource);
        }
    }
}
