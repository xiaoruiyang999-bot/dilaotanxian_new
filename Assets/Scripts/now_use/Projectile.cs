using UnityEngine;

/// <summary>
/// 通用投射物（v0.5.4.2）。
/// 直线飞行、命中 IDamageable 后结算伤害并自毁。
/// 程序员美术：白圆染色 + 可选 DOTween 拖尾（后续加）。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    [Header("运行时注入（发射时设置）")]
    [SerializeField] private float damage = 1f;
    [SerializeField] private float speed = 8f;
    [SerializeField] private float maxLifetime = 3f;
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private LayerMask wallLayer;

    [Header("视觉")]
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private TrailRenderer trail;

    private Vector2 flightDirection;
    private float lifetime;
    private bool launched;

    /// <summary>发射者的 Transform，用于避免打到自己。</summary>
    private Transform source;

    void Awake()
    {
        if (visual == null) visual = GetComponent<SpriteRenderer>();
        lifetime = maxLifetime;
    }

    void Start()
    {
        if (!launched) return;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.linearVelocity = flightDirection * speed;
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // 面向飞行方向
        float angle = Mathf.Atan2(flightDirection.y, flightDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    void Update()
    {
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f) Destroy(gameObject);
    }

    /// <summary>
    /// 发射投射物。调用方设置所有参数并启动飞行。
    /// </summary>
    public void Launch(Vector2 direction, float dmg, float spd, LayerMask targets,
        Transform src = null, float life = 3f)
    {
        flightDirection = direction.normalized;
        damage = dmg;
        speed = spd;
        targetLayer = targets;
        source = src;
        maxLifetime = life;
        lifetime = life;
        launched = true;

        // 面向飞行方向
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.linearVelocity = flightDirection * speed;
        rb.gravityScale = 0f;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!launched) return;

        // 跳过发射者自身
        if (source != null && other.transform.IsChildOf(source)) return;

        // 检查是否为可命中的目标层
        if (!InTargetLayer(other.gameObject.layer)) return;

        // 命中
        if (other.TryGetComponent<IDamageable>(out var damageable))
        {
            damageable.TakeDamage(damage);
        }

        Destroy(gameObject);
    }

    private bool InTargetLayer(int layer)
    {
        return (targetLayer.value & (1 << layer)) != 0;
    }

    public void SetColor(Color color)
    {
        if (visual != null) visual.color = color;
        if (trail != null)
        {
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
        }
    }
}
