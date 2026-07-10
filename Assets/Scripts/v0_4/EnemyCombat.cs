using UnityEngine;

/// <summary>
/// 敌人攻击管理。负责冷却计时、距离判定、执行攻击。
/// 后续扩展：近战/远程/Boss技能通过不同攻击模式实现。
/// </summary>
public class EnemyCombat : MonoBehaviour
{
    private EnemyStats stats;
    private float cooldownTimer = 0f;
    private bool canAttack = true;

    void Awake()
    {
        stats = GetComponent<EnemyStats>();
    }

    void Update()
    {
        if (!canAttack)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f)
                canAttack = true;
        }
    }

    /// <summary>
    /// 检查目标是否在攻击范围内。
    /// </summary>
    public bool IsInAttackRange(Transform target)
    {
        if (target == null || stats == null) return false;
        return Vector2.Distance(transform.position, target.position) <= stats.AttackRange;
    }

    /// <summary>
    /// 尝试攻击目标。冷却好且范围内才能攻击。
    /// </summary>
    public bool TryAttack(IDamageable target)
    {
        if (!canAttack) return false;
        if (target == null) return false;

        target.TakeDamage(stats.AttackDamage);
        canAttack = false;
        cooldownTimer = stats.AttackCooldown;
        return true;
    }

    /// <summary>
    /// 是否能攻击（冷却已好）。
    /// </summary>
    public bool CanAttack => canAttack;
}
