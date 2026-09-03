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

    [Header("金币掉落（v1.1.3 自 MCP 分支还原）")]
    [Tooltip("死亡是否掉落金币；逻辑开关不序列化（召唤物由 EnemyCombat 置 false 防刷币）")]
    public bool DropCoins { get; set; } = true;
    [Tooltip("单次掉落数量下限（含）")]
    [SerializeField, Min(0)] private int coinsMin = 1;
    [Tooltip("单次掉落数量上限（含）；0 = 不掉")]
    [SerializeField, Min(0)] private int coinsMax = 3;

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
    public void MoveTowards(Vector2 direction)
    {
        if (stats == null || rb == null) return;
        rb.linearVelocity = direction.normalized * stats.MoveSpeed;
    }

    /// <summary>向指定方向移动（带速度倍率，v1.0.13 自 MCP 分支还原：远程后撤/横移、游击后撤用）</summary>
    public void MoveTowards(Vector2 direction, float speedMultiplier)
    {
        if (stats == null || rb == null) return;
        rb.linearVelocity = direction.normalized * (stats.MoveSpeed * speedMultiplier);
    }

    /// <summary>冲锋位移（v1.0.13 自 MCP 分支还原）：直接写 Rigidbody2D.velocity，
    /// 绕过 StopMoving 后的零速限制，由 EnemyCombat.UpdateActive 每帧驱动。</summary>
    public void SetChargeVelocity(Vector2 velocity)
    {
        if (rb != null) rb.linearVelocity = velocity;
    }

    /// <summary>停止移动</summary>
    public void StopMoving()
    {
        if (rb != null) rb.linearVelocity = Vector2.zero;
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
        StartCoroutine(HitFlashCoroutine());
    }

    private System.Collections.IEnumerator HitFlashCoroutine()
    {
        Color original = sr.color;
        sr.color = hitFlashColor;
        yield return new WaitForSeconds(hitFlashDuration);
        // 如果还没死，恢复颜色
        if (health != null && !health.IsDead)
            sr.color = original;
    }

    // ========== 死亡处理 ==========

    private void OnEnemyDeath()
    {
        // 训练木桩由TrainingDummy自己管理重置与视觉，不执行敌人死亡流程
        if (GetComponent<TrainingDummy>() != null) return;

        StopMoving();
        // 变灰
        if (sr != null) sr.color = Color.gray;

        // 禁用AI和碰撞
        if (ai != null) ai.enabled = false;
        if (combat != null) combat.enabled = false;
        if (TryGetComponent<Collider2D>(out var col)) col.enabled = false;

        // v0.6.3 掉落闭环：击杀掉落法力球（walk-over 吸附，数值读 EnemyStats.manaOrbValue，>0 才掉）
        if (stats != null && stats.ManaOrbValue > 0f)
            ManaOrb.Spawn(transform.position, stats.ManaOrbValue);

        // v1.1.3 金币掉落（自 MCP 分支还原）：散落 + 磁吸拾取，入 PlayerStats 钱包
        if (DropCoins && coinsMax > 0)
            CoinDrop.Spawn(transform.position, Random.Range(coinsMin, coinsMax + 1));

        // 延迟销毁
        Destroy(gameObject, 0.5f);
    }

    // ========== 外部访问接口 ==========

    public EnemyStats GetStats() => stats;
    public EnemyHealth GetHealth() => health;
    public EnemyAI GetAI() => ai;
    public EnemyCombat GetCombat() => combat;
}
