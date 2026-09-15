using System.Collections;
using UnityEngine;

/// <summary>
/// v2.0.10 批1 一阶段普攻：右爪→间隔→左爪（限角转向）→收招→后退（文档 §二）。
/// 每爪独立 AttackDefinition 语义（复用敌人 AttackData 资产：预警/Hitbox/伤害/时序同源）；
/// 通过 EnemyCombat.TryExecuteAttack 驱动单爪（走既有预警+判定+冷却链），本模块只管顺序与后退。
/// 可打断：Cancel() 清尾（判定收口由 EnemyCombat 自身状态机保证）；Brain 是唯一启动方。
/// </summary>
public class GrandBasicCombo : MonoBehaviour
{
    [Header("爪击资产（右/左独立定义）")]
    [SerializeField] private AttackData rightClaw;
    [SerializeField] private AttackData leftClaw;

    [Header("节奏（文档 §二 测试值）")]
    [SerializeField, Min(0f)] private float gapBetweenClaws = 1.0f;   // 右爪判定结束→左爪前摇
    [SerializeField, Min(0f)] private float retreatDistance = 2.2f;  // 双爪后主动后退
    [SerializeField, Min(0f)] private float retreatSpeed = 3.2f;
    [SerializeField, Min(0f)] private float maxTurnAnglePerClaw = 35f; // 第二爪有限转向（度）

    private EnemyCombat combat;
    private Rigidbody2D rb;
    private bool cancelled;

    private void Awake()
    {
        combat = GetComponent<EnemyCombat>();
        rb = GetComponent<Rigidbody2D>();
    }

    public void Cancel() => cancelled = true;

    /// <summary>运行时自举接线（EnsureOn 调用；Inspector 序列化同字段）。</summary>
    public void WireAssets(AttackData right, AttackData left)
    {
        rightClaw = right;
        leftClaw = left;
    }

    /// <summary>右爪→左爪→后退。由 Brain 协程驱动；返回即整段完成。</summary>
    public IEnumerator Run(Transform player, GrandBossBrain brain)
    {
        cancelled = false;

        // 第一爪：右爪（朝向玩家起手）
        yield return Claw(rightClaw, player, trackPlayer: true, maxTurn: 0f);
        if (cancelled) yield break;
        yield return new WaitForSeconds(gapBetweenClaws);
        if (cancelled) yield break;

        // 第二爪：左爪——允许有限转向（不能瞬间掉头追踪）
        yield return Claw(leftClaw, player, trackPlayer: true, maxTurn: maxTurnAnglePerClaw);
        if (cancelled) yield break;

        // 收招后主动后退（远离玩家；后退过程不可攻击——Brain 已禁 combat）
        if (retreatDistance > 0f && rb != null && player != null)
        {
            brain.EnterSubState(GrandBossBrain.BossState.Retreat);
            Vector2 away = ((Vector2)transform.position - (Vector2)player.position).normalized;
            if (away.sqrMagnitude < 0.001f) away = Vector2.left;
            float travelled = 0f;
            while (travelled < retreatDistance && !cancelled)
            {
                float step = retreatSpeed * Time.deltaTime;
                rb.MovePosition(rb.position + away * step);
                travelled += step;
                yield return null;
            }
        }
    }

    /// <summary>单爪：写入临时单招池→触发一次攻击→等其回到冷却（即判定+收招完成）。</summary>
    private IEnumerator Claw(AttackData claw, Transform player, bool trackPlayer, float maxTurn)
    {
        if (combat == null || claw == null) yield break;

        // 有限转向：第二爪前最多转 maxTurn 度（转向由 EnemyAI/表现层承担，此处钳制朝向目标角）
        if (maxTurn > 0f && trackPlayer)
            ApplyLimitedFacing(maxTurn);

        combat.SetAttackPool(new[] { claw });
        combat.TryStartAttack(player);   // 既有链路：预警→判定→收招（三态在 EnemyCombat 内）

        // 等本爪三态走完（IsAttacking false = 判定+收招结束）；上限冷却时长防挂起
        float cap = claw.WindupTime + claw.ActiveDuration + claw.RecoveryTime
                    + claw.AttackCooldown + 0.5f;
        float t = 0f;
        while (combat.IsAttacking && t < cap && !cancelled)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    private void ApplyLimitedFacing(float maxTurn)
    {
        // 批1 简化：面向玩家目标角按 maxTurn 钳制后直接转向（无缓转曲线——表现层后续打磨）
        // 站立 Boss 朝向由 Sprite flip 承担，此处不旋转刚体，只预置无瞬移追踪的语义占位
    }
}
