using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 攻击查询器。
/// 只负责根据 AttackData 执行攻击范围检测，不控制动画、AI 或 WeaponPivot。
/// 可被 PlayerCombat 与 EnemyCombat 复用。
/// </summary>
public class AttackQuery : MonoBehaviour
{
    [Header("攻击数据")]
    [SerializeField] private AttackData attackData;

    [Header("调试")]
    [SerializeField] private bool drawGizmos = false;

    /// <summary>
    /// 执行一次攻击检测。
    /// </summary>
    /// <param name="origin">攻击原点（通常为角色位置）</param>
    /// <param name="attackDirection">攻击方向（归一化）</param>
    /// <param name="hitTargets">检测到的可伤害目标列表</param>
    /// <returns>是否命中任何目标</returns>
    public bool ExecuteAttack(Vector2 origin, Vector2 attackDirection, out List<IDamageable> hitTargets)
    {
        hitTargets = new List<IDamageable>();

        if (attackData == null)
        {
            Debug.LogWarning($"[{nameof(AttackQuery)}] attackData is null on {gameObject.name}", this);
            return false;
        }

        if (attackDirection.sqrMagnitude < 0.0001f)
            attackDirection = Vector2.right;
        else
            attackDirection.Normalize();

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            origin, attackData.AttackRange, attackData.TargetLayer);

        float halfAngle = attackData.AttackAngle * 0.5f;

        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;
            if (hit.isTrigger) continue;
            if (hit.gameObject == gameObject) continue;

            Vector2 closestPoint = hit.ClosestPoint(origin);
            Vector2 toPoint = closestPoint - origin;

            // 距离判定
            if (toPoint.magnitude > attackData.AttackRange) continue;

            // 角度判定（AttackAngle = 360 时自动通过）
            if (attackData.AttackAngle < 360f)
            {
                float angle = Vector2.Angle(attackDirection, toPoint.normalized);
                if (angle > halfAngle) continue;
            }

            // 造成伤害
            if (hit.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(attackData.AttackDamage);
                hitTargets.Add(damageable);
            }
        }

        return hitTargets.Count > 0;
    }

    /// <summary>
    /// 简化版：执行攻击检测，不返回目标列表。
    /// </summary>
    public bool ExecuteAttack(Vector2 origin, Vector2 attackDirection)
    {
        return ExecuteAttack(origin, attackDirection, out _);
    }

    /// <summary>
    /// 外部切换攻击数据（用于 v0.5 技能/武器切换）。
    /// </summary>
    public void SetAttackData(AttackData data)
    {
        attackData = data;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos || attackData == null) return;

        Vector2 origin = transform.position;
        float halfAngle = attackData.AttackAngle * 0.5f;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(origin, attackData.AttackRange);

        Vector2 forward = transform.right;
        Vector2 leftDir = Quaternion.Euler(0, 0, -halfAngle) * forward;
        Vector2 rightDir = Quaternion.Euler(0, 0, halfAngle) * forward;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin + leftDir * attackData.AttackRange);
        Gizmos.DrawLine(origin, origin + rightDir * attackData.AttackRange);
    }
}
