using System.Collections;
using UnityEngine;

/// <summary>
/// v2.0.10 批2-2 裂柱奔袭（开发文档 §六/招式一）：
/// 蓄力（面向玩家+直线预警跟随）→ 咆哮锁定（方向固定不再追踪）→ 直线高速冲刺
///（身体攻击判定开启）→ 撞击检测（玩家/石柱/碎石/外墙分别收招）→ 横跳重新锁定，
/// 最多三连。撞完整柱=柱裂+短失衡；撞裂柱=柱塌+长眩晕；撞墙=短恢复；全落空=中等喘息。
/// ChargeEndReason 枚举对齐文档。碎石区减速由 BossArenaState.IsInRubble 查询。
/// </summary>
public class GrandChargeAttack : MonoBehaviour
{
    public enum ChargeEndReason { Missed, HitPlayer, HitIntactPillar, HitCrackedPillar, HitWall, Interrupted }

    [Header("奔袭参数（文档 §六 测试值）")]
    [SerializeField, Min(0f)] private float windupTime = 0.9f;
    [SerializeField, Min(0f)] private float chargeSpeed = 12f;
    [SerializeField, Min(0f)] private float maxChargeDistance = 14f;
    [SerializeField, Min(0f)] private float bodyHitRadius = 1.4f;
    [SerializeField, Min(0f)] private float chargeDamage = 80f;
    [SerializeField, Min(0f)] private float gapBetweenCharges = 0.8f;
    [SerializeField, Min(0f)] private float allMissRecoverTime = 2.5f;

    [Header("恢复时长（文档 撞击结果表）")]
    [SerializeField, Min(0f)] private float hitPlayerRecover = 1.0f;
    [SerializeField, Min(0f)] private float intactPillarStagger = 1.8f;
    [SerializeField, Min(0f)] private float crackedPillarStun = 4.0f;
    [SerializeField, Min(0f)] private float wallRecover = 1.2f;

    [Header("组件")]
    [SerializeField] private BossArenaState arena;
    [SerializeField] private BossTelegraphController telegraph;
    [SerializeField] private GrandBossBrain brain;
    [SerializeField] private LayerMask playerMask;
    [SerializeField] private LayerMask wallMask;

    private Rigidbody2D rb;
    private bool cancelled;
    public ChargeEndReason LastResult { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        brain = GetComponent<GrandBossBrain>();
        telegraph = GetComponent<BossTelegraphController>();
        if (arena == null) arena = GetComponentInParent<BossArenaState>();
    }

    public void Wire(BossArenaState arenaState, BossTelegraphController tele, LayerMask player, LayerMask wall)
    {
        arena = arenaState; telegraph = tele; playerMask = player; wallMask = wall;
    }

    public void Cancel() => cancelled = true;

    /// <summary>连续奔袭主流程（≤3 次，撞击即断连）。</summary>
    public IEnumerator Run(Transform player, GrandBossBrain brainOwner)
    {
        cancelled = false;
        brain = brainOwner;

        for (int charge = 0; charge < 3; charge++)
        {
            if (cancelled || player == null) { LastResult = ChargeEndReason.Interrupted; yield break; }

            // 蓄力：面向玩家（预警跟随阶段）
            Vector2 dir = ((Vector2)player.position - rb.position).normalized;
            float t = 0f;
            while (t < windupTime && !cancelled)
            {
                t += Time.deltaTime;
                dir = ((Vector2)player.position - rb.position).normalized;   // 跟随
                yield return null;
            }
            if (cancelled) { LastResult = ChargeEndReason.Interrupted; yield break; }

            // 锁定：方向固定，直线预警
            Vector2 lockedDir = dir;
            Vector2 start = rb.position;
            telegraph?.ShowLine(start, lockedDir, maxChargeDistance, 0.25f);

            // 冲刺：沿固定方向，每帧检测撞击
            float dist = 0f;
            LastResult = ChargeEndReason.Missed;
            while (dist < maxChargeDistance && !cancelled)
            {
                float speed = chargeSpeed * (arena != null && arena.IsInRubble(rb.position)
                    ? BossArenaState.RubbleSlowMultiplier : 1f);   // 碎石减速
                float step = speed * Time.deltaTime;
                rb.MovePosition(rb.position + lockedDir * step);
                dist += step;

                var result = CheckImpact();
                if (result != ChargeEndReason.Missed) { LastResult = result; break; }
                yield return null;
            }
            telegraph?.HideAll();

            // 撞击结果处理
            switch (LastResult)
            {
                case ChargeEndReason.HitPlayer:
                    yield return new WaitForSeconds(hitPlayerRecover);
                    yield break;   // 文档：命中玩家立即结束连冲
                case ChargeEndReason.HitIntactPillar:
                    yield return new WaitForSeconds(intactPillarStagger);
                    yield break;   // 撞柱即断连
                case ChargeEndReason.HitCrackedPillar:
                    // P0-新2 修复:不走 EnterSubState(Stunned)(FinishAction 会拦截且无恢复协程——
                    // 永久卡死);改为自等待 crackedPillarStun 后自然结束→Brain FinishAction 正常收管
                    yield return new WaitForSeconds(crackedPillarStun);
                    yield break;
                case ChargeEndReason.HitWall:
                    yield return new WaitForSeconds(wallRecover);
                    break;   // 撞墙可继续连冲
                case ChargeEndReason.Missed:
                    if (charge < 2)
                    {
                        yield return new WaitForSeconds(gapBetweenCharges);   // 横跳→重新锁定
                        // 横跳占位：向垂直方向随机跳一小步
                        Vector2 sideDir = Vector2.Perpendicular(lockedDir) * (Random.value < 0.5f ? 1f : -1f);
                        rb.MovePosition(rb.position + sideDir * 2f);
                    }
                    break;
            }

            if (cancelled) { LastResult = ChargeEndReason.Interrupted; yield break; }
        }

        // 三次全落空：中等喘息（不眩晕——文档 §六）
        if (LastResult == ChargeEndReason.Missed)
            yield return new WaitForSeconds(allMissRecoverTime);
    }

    /// <summary>撞击检测：玩家(圆)→完整柱→裂柱→外墙。</summary>
    private ChargeEndReason CheckImpact()
    {
        // 玩家(P0-新3:必须同时有 IDamageable 才算命中——不能碰任何 Default 就 HitPlayer)
        Collider2D player = Physics2D.OverlapCircle(rb.position, bodyHitRadius, playerMask);
        if (player != null && player.TryGetComponent(out IDamageable dmg))
        {
            dmg.TakeDamage(chargeDamage);
            return ChargeEndReason.HitPlayer;
        }

        // 石柱（最近的可撞柱）
        if (arena != null)
        {
            var intact = arena.FindNearest(rb.position, s => s == BossArenaState.PillarState.Intact);
            if (intact != null && Vector2.Distance(rb.position, intact.Center) < bodyHitRadius + 1.0f)
            {
                arena.ApplyBossImpact(intact);
                return ChargeEndReason.HitIntactPillar;
            }
            var cracked = arena.FindNearest(rb.position, s => s == BossArenaState.PillarState.Cracked);
            if (cracked != null && Vector2.Distance(rb.position, cracked.Center) < bodyHitRadius + 1.0f)
            {
                arena.ApplyBossImpact(cracked);
                return ChargeEndReason.HitCrackedPillar;
            }
        }

        // 外墙
        Collider2D wall = Physics2D.OverlapCircle(rb.position, bodyHitRadius * 0.8f, wallMask);
        if (wall != null) return ChargeEndReason.HitWall;

        return ChargeEndReason.Missed;
    }
}
