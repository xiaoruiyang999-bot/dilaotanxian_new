using UnityEngine;

/// <summary>
/// 敌人门面控制器。持有所有子组件引用，提供统一的移动控制接口。
/// 类似PlayerController，但输入由EnemyAI驱动而非Input System。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyStats))]
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(EnemyCombat))]
public class EnemyController : MonoBehaviour
{
    private Rigidbody2D rb;
    private EnemyStats stats;
    private EnemyHealth health;
    private EnemyAI ai;
    private EnemyCombat combat;
    private SpriteRenderer sr;

    [Header("受伤反馈")]
    [SerializeField] private float hitFlashDuration = 0.15f;
    [SerializeField] private Color hitFlashColor = Color.white;

    /// <summary>是否掉落金币（v0.8：召唤生物置 false 防刷钱）。</summary>
    public bool DropCoins { get; set; } = true;

    [Header("金币掉落（M2·v0.7.0）")]
    [Tooltip("死亡掉落金币枚数区间；Boss/精英 prefab 上调大即可")]
    [SerializeField] private int coinsMin = 1;
    [SerializeField] private int coinsMax = 3;
    // v0.6.1 修复闪烁协程重叠：基线色 = 首次受击时的颜色（含词缀染色），协程互斥保证恢复目标正确
    private Color flashBaseColor;
    private Coroutine hitFlashRoutine;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        stats = GetComponent<EnemyStats>();
        health = GetComponent<EnemyHealth>();
        ai = GetComponent<EnemyAI>();
        combat = GetComponent<EnemyCombat>();
        sr = GetComponent<SpriteRenderer>();

        // 监听事件
        if (health != null)
        {
            health.OnDeath += OnEnemyDeath;
            health.OnTakeDamage += OnHitFlash; // 受伤闪烁
        }
    }

    void OnDestroy()
    {
        if (health != null)
        {
            health.OnDeath -= OnEnemyDeath;
            health.OnTakeDamage -= OnHitFlash;
        }
    }

    // ========== 移动接口（供EnemyAI调用）==========

    /// <summary>向指定方向移动</summary>
    public void MoveTowards(Vector2 direction, float speedMultiplier = 1f)
    {
        if (stats == null || rb == null) return;
        rb.linearVelocity = direction.normalized * stats.MoveSpeed * Mathf.Max(0f, speedMultiplier);
    }

    /// <summary>停止移动</summary>
    public void StopMoving()
    {
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// v0.5.4.2：设置冲锋速度（由 EnemyCombat 在 isCharge Active 阶段调用）。
    /// 直接操作 Rigidbody2D.velocity，绕过 StopMoving 后的零速限制。
    /// </summary>
    public void SetChargeVelocity(Vector2 velocity)
    {
        if (rb != null) rb.linearVelocity = velocity;
    }

    /// <summary>面向指定方向</summary>
    public void FaceTowards(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.001f) return;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    // ========== 受伤反馈 ==========

    private void OnHitFlash()
    {
        // 训练木桩由 TrainingDummy 自己管理闪烁，避免两个协程同时改颜色导致闪烁不消失
        if (GetComponent<TrainingDummy>() != null) return;
        if (sr == null) return;

        // v0.6.1 修复：闪烁协程互斥。新受击到来时先终止旧协程并恢复基线色——
        // 否则旧协程停在闪烁色，新协程会把闪烁色错误缓存为"原色"，
        // 0.15s 内连续受击数次后敌人永久变白（遗留清单 #1）。
        if (hitFlashRoutine != null)
        {
            StopCoroutine(hitFlashRoutine);
            sr.color = flashBaseColor;
        }
        else
        {
            flashBaseColor = sr.color;   // 首次闪烁：以当前色（含词缀染色）为基线
        }
        hitFlashRoutine = StartCoroutine(HitFlashCoroutine());
    }

    private System.Collections.IEnumerator HitFlashCoroutine()
    {
        sr.color = hitFlashColor;
        yield return new WaitForSeconds(hitFlashDuration);
        // 如果还没死，恢复基线色（不用协程启动瞬间的 sr.color，防重叠缓存污染）
        if (health != null && !health.IsDead)
            sr.color = flashBaseColor;
        hitFlashRoutine = null;
    }

    // ========== 死亡处理 ==========

    private void OnEnemyDeath()
    {
        // 训练木桩由TrainingDummy自己管理重置与视觉，不执行敌人死亡流程
        if (GetComponent<TrainingDummy>() != null) return;

        // 死亡反馈（M1.5·v0.6.1）
        AudioManager.PlaySFX("enemyDie");

        // 金币掉落（M2.3·v0.7.0）：战斗表现随机，不进地牢生成的 seed 复现流
        if (DropCoins && coinsMax > 0)
            CoinDrop.Spawn(transform.position, Random.Range(coinsMin, coinsMax + 1));

        StopAllCoroutines();
        StopMoving();

        // 死亡后立即退出所有运行逻辑。不能保留半秒“灰色尸体”，否则冲锋等
        // Variant 的子碰撞体、状态机或世界空间 UI 会在 Destroy 前继续留在场景中。
        if (ai != null) ai.enabled = false;
        if (combat != null) combat.enabled = false;

        foreach (Collider2D col in GetComponentsInChildren<Collider2D>(true))
            col.enabled = false;

        foreach (Renderer rendererComponent in GetComponentsInChildren<Renderer>(true))
            rendererComponent.enabled = false;

        // SetActive(false) 立即移除视觉、物理和 Update；Destroy 在本帧结束回收对象。
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    // ========== 外部访问接口 ==========

    public EnemyStats GetStats() => stats;
    public EnemyHealth GetHealth() => health;
    public EnemyAI GetAI() => ai;
    public EnemyCombat GetCombat() => combat;
}
