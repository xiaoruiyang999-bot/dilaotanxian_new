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
        rb.velocity = direction.normalized * stats.MoveSpeed;
    }

    /// <summary>停止移动</summary>
    public void StopMoving()
    {
        if (rb != null) rb.velocity = Vector2.zero;
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

        // 延迟销毁
        Destroy(gameObject, 0.5f);
    }

    // ========== 外部访问接口 ==========

    public EnemyStats GetStats() => stats;
    public EnemyHealth GetHealth() => health;
    public EnemyAI GetAI() => ai;
    public EnemyCombat GetCombat() => combat;
}
