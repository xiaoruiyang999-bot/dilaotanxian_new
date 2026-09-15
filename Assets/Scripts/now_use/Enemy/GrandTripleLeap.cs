using System.Collections;
using UnityEngine;

/// <summary>
/// v2.0.10 批1 一阶段"三重跃击"（文档 §三）：起跳前摇→记录玩家位置→圆形落点预警→
/// 锁定落点（不追踪）→空中（禁普通移动/攻击，可受伤）→位移至落点→落地双判定
///（中心坠落=跃击命中口径；外围震荡波）→间隔 2 秒→下一跳。三跳全落空→眩晕 6.5 秒；
/// 任意中心命中→第三跳后接双爪连击（由 Brain 决策层兑现：报告 hasHit）。
/// 预警/震荡用程序化圆形（BossTelegraphController）；中心判定 OverlapCircle 一次性结算。
/// </summary>
public class GrandTripleLeap : MonoBehaviour
{
    [Header("跃击参数（文档 §三 测试值）")]
    [SerializeField, Min(0f)] private float jumpWindup = 0.7f;        // 起跳前摇
    [SerializeField, Min(0f)] private float airTime = 0.55f;          // 空中时长
    [SerializeField, Min(0f)] private float gapBetweenJumps = 2.0f;
    [SerializeField, Min(0f)] private float centerRadius = 1.3f;      // 中心坠落判定半径
    [SerializeField, Min(0f)] private float shockRadius = 2.8f;       // 外围震荡半径
    [SerializeField, Min(0f)] private float shockDamage = 18f;        // 震荡伤害（中心走攻击资产另配）
    [SerializeField, Min(0f)] private float allMissStunSeconds = 6.5f;

    [Header("资产/组件")]
    [SerializeField] private AttackData landAttack;        // 中心坠落判定（AttackDamage/TargetLayer 同源）
    [SerializeField] private BossTelegraphController telegraph;
    [SerializeField] private LayerMask playerLayer;        // 中心命中口径（只判玩家，震荡擦边不算）

    private Rigidbody2D rb;
    private bool cancelled;

    public bool LastRunHitPlayer { get; private set; }

    // ========== 美术预留事件（v2.0.10）：跳跃子时机——起跳(前摇结束)/落地(判定瞬间)/眩晕开始 ==========
    public event System.Action<int, Vector2> OnLeapTakeoff;   // (跳序0起,起跳点)
    public event System.Action<int, Vector2, bool> OnLeapLanded;   // (跳序,落点,中心命中)
    public event System.Action OnStunned;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (telegraph == null) telegraph = GetComponent<BossTelegraphController>();
    }

    public void Cancel() => cancelled = true;

    /// <summary>运行时自举接线（EnsureOn 调用）。</summary>
    public void WireAssets(AttackData land, BossTelegraphController tele, LayerMask mask)
    {
        landAttack = land;
        telegraph = tele;
        playerLayer = mask;
    }

    /// <summary>三连跳主流程；yield break 前向 Brain 报告命中口径。</summary>
    public IEnumerator Run(Transform player, GrandBossBrain brain)
    {
        cancelled = false;
        LastRunHitPlayer = false;

        for (int jump = 0; jump < 3; jump++)
        {
            if (cancelled || player == null) yield break;

            // 起跳前摇 → 记录玩家当前位置 → 预警 → 锁定
            yield return new WaitForSeconds(jumpWindup);
            if (cancelled) yield break;
            Vector2 landPos = player.position;   // 记录时刻快照，锁定后不追踪
            OnLeapTakeoff?.Invoke(jump, landPos);   // 美术：起跳时机

            telegraph?.ShowCircle(landPos, shockRadius, jumpWindup + airTime);

            // 空中：直线位移至落点（普通移动已由 Brain 禁用；可受伤=不关碰撞）
            Vector2 start = rb != null ? rb.position : (Vector2)transform.position;
            float t = 0f;
            while (t < airTime && !cancelled)
            {
                t += Time.deltaTime;
                Vector2 pos = Vector2.Lerp(start, landPos, t / airTime);
                if (rb != null) rb.MovePosition(pos); else transform.position = pos;
                yield return null;
            }
            if (cancelled) { telegraph?.HideAll(); yield break; }

            // 落地：中心坠落判定（跃击命中口径）+ 外围震荡
            bool centerHit = ResolveLanding(landPos);
            if (centerHit) LastRunHitPlayer = true;
            OnLeapLanded?.Invoke(jump, landPos, centerHit);   // 美术：落地/尘土/命中反馈

            if (jump < 2)
                yield return new WaitForSeconds(gapBetweenJumps);
        }

        telegraph?.HideAll();

        // 三跳结束条件分支（文档 §三.2）
        if (!LastRunHitPlayer)
        {
            OnStunned?.Invoke();   // 美术：眩晕开始（星星/摇晃）
            brain.EnterSubState(GrandBossBrain.BossState.Stunned);
        }   // 全落空→眩晕（Brain 恢复后回循环）
        // 命中口径下接双爪连击：交回 Brain 决策（近距离必选 BasicCombo）
    }

    /// <summary>中心=攻击资产判定（每爪一次去重由资产链保证）；外围=震荡 Circle 一次结算。</summary>
    private bool ResolveLanding(Vector2 landPos)
    {
        bool centerHit = false;

        // 中心坠落：用 playerLayer 只测玩家（震荡擦到不算跃击命中文档口径）
        Collider2D hit = Physics2D.OverlapCircle(landPos, centerRadius, playerLayer);
        if (hit != null && hit.TryGetComponent(out IDamageable dmg))
        {
            float damage = landAttack != null ? landAttack.AttackDamage : shockDamage * 1.5f;
            dmg.TakeDamage(damage);
            centerHit = true;
        }

        // 外围震荡波：环形区域（shockRadius 内、centerRadius 外的玩家）
        Collider2D[] hits = Physics2D.OverlapCircleAll(landPos, shockRadius, playerLayer);
        foreach (Collider2D h in hits)
        {
            if (h == hit) continue;   // 中心已结算
            if (h.TryGetComponent(out IDamageable d))
                d.TakeDamage(shockDamage);
        }
        return centerHit;
    }

    /// <summary>眩晕时长等待（Brain 恢复协程消费）。</summary>
    public IEnumerator StunDurationWait()
    {
        yield return new WaitForSeconds(allMissStunSeconds);
    }
}
