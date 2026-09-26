using System.Collections;
using UnityEngine;

/// <summary>
/// 格兰一阶段三重跃击。落点在起跳时锁定；每跳使用无分配物理查询，
/// 命中结果只负责上报，眩晕或追击连段由 GrandBossBrain 统一决定。
/// </summary>
public class GrandTripleLeap : MonoBehaviour
{
    private const int OverlapCapacity = 16;

    [Header("跃击参数")]
    [SerializeField, Min(0f)] private float jumpWindup = 0.7f;
    [SerializeField, Min(0f)] private float airTime = 0.55f;
    [SerializeField, Min(0f)] private float gapBetweenJumps = 2.0f;
    [SerializeField, Min(0f)] private float centerRadius = 1.3f;
    [SerializeField, Min(0f)] private float shockRadius = 2.8f;
    [SerializeField, Min(0f)] private float shockDamage = 18f;
    [SerializeField, Min(0f)] private float allMissStunSeconds = 6.5f;

    [Header("资产/组件")]
    [SerializeField] private AttackData landAttack;
    [SerializeField] private BossTelegraphController telegraph;
    [SerializeField] private LayerMask playerLayer;

    private readonly Collider2D[] overlapBuffer = new Collider2D[OverlapCapacity];
    private static readonly RaycastHit2D[] blockBuf = new RaycastHit2D[2];   // P1 零GC:柱遮挡查询缓冲
    private readonly IDamageable[] damagedTargets = new IDamageable[OverlapCapacity];
    private Rigidbody2D rb;
    private bool cancelled;
    private const float shockExpansionSpeed = 6f;   // 扩张环速度(§4.4 测试值)
    private const float shockBandWidth = 0.8f;       // 环带宽度(annulus 过滤)
    private int damagedCount;

    public enum Outcome { FollowUpCombo, Stunned }

    public bool LastRunHitPlayer { get; private set; }
    public float StunDurationSeconds => allMissStunSeconds;
    public LayerMask TargetLayer => playerLayer;

    public static Outcome ResolveOutcome(bool anyCenterHit)
        => anyCenterHit ? Outcome.FollowUpCombo : Outcome.Stunned;

    public event System.Action<int, Vector2> OnLeapTakeoff;
    public event System.Action<int, Vector2, bool> OnLeapLanded;
    public event System.Action OnStunned;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (telegraph == null) telegraph = GetComponent<BossTelegraphController>();
        RefreshTargetLayer();
    }

    public void Cancel()
    {
        cancelled = true;
        telegraph?.HideAll();
    }

    private void OnDisable() => Cancel();

    public void WireAssets(AttackData land, BossTelegraphController tele)
    {
        landAttack = land;
        telegraph = tele;
        RefreshTargetLayer();
    }

    public IEnumerator Run(Transform player)
    {
        cancelled = false;
        LastRunHitPlayer = false;

        for (int jump = 0; jump < 3; jump++)
        {
            if (cancelled || player == null) yield break;

            yield return WaitCancelable(jumpWindup);
            if (cancelled || player == null) yield break;

            Vector2 landPos = player.position;
            OnLeapTakeoff?.Invoke(jump, landPos);
            telegraph?.ShowCircle(landPos, shockRadius, airTime);

            Vector2 start = rb != null ? rb.position : (Vector2)transform.position;
            float elapsed = 0f;
            while (elapsed < airTime && !cancelled)
            {
                elapsed += Time.deltaTime;
                float normalized = airTime > 0f ? Mathf.Clamp01(elapsed / airTime) : 1f;
                Vector2 pos = Vector2.Lerp(start, landPos, normalized);
                if (rb != null) rb.MovePosition(pos); else transform.position = pos;
                yield return null;
            }
            if (cancelled) yield break;

            telegraph?.HideAll();
            bool centerHit = ResolveLanding(landPos);
            LastRunHitPlayer |= centerHit;
            OnLeapLanded?.Invoke(jump, landPos, centerHit);

            if (jump < 2)
                yield return WaitCancelable(gapBetweenJumps);
        }

        telegraph?.HideAll();
        if (!LastRunHitPlayer) OnStunned?.Invoke();
    }

    private bool ResolveLanding(Vector2 landPos)
    {
        damagedCount = 0;
        LayerMask mask = playerLayer.value != 0
            ? playerLayer
            : landAttack != null ? landAttack.TargetLayer : Physics2D.AllLayers;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(mask);
        filter.useTriggers = true;

        bool centerHit = false;
        int centerCount = Physics2D.OverlapCircle(landPos, centerRadius, filter, overlapBuffer);
        for (int i = 0; i < centerCount; i++)
        {
            IDamageable damageable = FindPlayerDamageable(overlapBuffer[i]);
            if (damageable == null || WasDamaged(damageable)) continue;
            DamageResolver.DealEnemy(damageable, landAttack != null ? landAttack.AttackDamage : shockDamage * 1.5f);
            RememberDamageable(damageable);
            centerHit = true;
        }

        // P1-1 修复(§4.4):震荡改为扩张环(协程驱动)——取代瞬时大圆
        if (shockRadius > centerRadius)
            StartCoroutine(ExpandingShockwave(landPos, filter));
        return centerHit;
    }

    /// <summary>扩张环(§4.4):从中心到外围逐帧扫掠;站立柱截断(§4.4 柱遮挡)。</summary>
    private System.Collections.IEnumerator ExpandingShockwave(Vector2 landPos, ContactFilter2D filter)
    {
        float radius = centerRadius;
        while (radius < shockRadius && !cancelled)
        {
            radius += shockExpansionSpeed * Time.deltaTime;
            int count = Physics2D.OverlapCircle(landPos, radius, filter, overlapBuffer);
            for (int i = 0; i < count; i++)
            {
                IDamageable damageable = FindPlayerDamageable(overlapBuffer[i]);
                if (damageable == null || WasDamaged(damageable)) continue;
                // annulus:内圈已结算,只打环带上的
                float dist = Vector2.Distance(landPos, overlapBuffer[i].transform.position);
                if (dist < radius - shockBandWidth) continue;
                // 柱遮挡:落点→玩家先碰站立柱则截断(§4.4)
                if (IsBlockedByPillar(landPos, overlapBuffer[i].transform.position)) continue;
                DamageResolver.DealEnemy(damageable, shockDamage);
                RememberDamageable(damageable);
            }
            yield return null;
        }
    }

    /// <summary>线段遮挡:落点→目标是否先碰到站立石柱(BossPillar 层)。</summary>
    private static bool IsBlockedByPillar(Vector2 from, Vector2 to)
    {
        int pillarMask = LayerMask.GetMask("BossPillar");
        if (pillarMask == 0) return false;
        Vector2 dir = to - from;
        float dist = dir.magnitude;
        // P1 零GC:Unity 6 NonAlloc(带 results 数组,无分配)
        var filter = new ContactFilter2D { useLayerMask = true };
        filter.SetLayerMask(pillarMask);
        return Physics2D.CircleCast(from, 0.1f, dir.normalized, filter, blockBuf, dist) > 0;
    }

    private static IDamageable FindPlayerDamageable(Collider2D hit)
    {
        if (hit == null) return null;
        Transform cursor = hit.transform;
        while (cursor != null)
        {
            if (cursor.CompareTag("Player") && cursor.TryGetComponent(out IDamageable damageable))
                return damageable;
            cursor = cursor.parent;
        }
        return null;
    }

    private bool WasDamaged(IDamageable target)
    {
        for (int i = 0; i < damagedCount; i++)
            if (ReferenceEquals(damagedTargets[i], target)) return true;
        return false;
    }

    private void RememberDamageable(IDamageable target)
    {
        if (damagedCount < damagedTargets.Length)
            damagedTargets[damagedCount++] = target;
    }

    private void RefreshTargetLayer()
    {
        if (landAttack != null) playerLayer = landAttack.TargetLayer;
    }

    private IEnumerator WaitCancelable(float seconds)
    {
        float remaining = seconds;
        while (remaining > 0f && !cancelled)
        {
            remaining -= Time.deltaTime;
            yield return null;
        }
    }
}
