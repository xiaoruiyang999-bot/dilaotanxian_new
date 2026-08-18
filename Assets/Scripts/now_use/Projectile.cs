using UnityEngine;

/// <summary>
/// 通用投射物（v0.5.4.2）。
/// 直线飞行、命中 IDamageable 后结算伤害并自毁。
/// 程序员美术：白圆染色 + 可选 DOTween 拖尾（后续加）。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    private static Material sharedTrailMaterial;

    [Header("运行时注入（发射时设置）")]
    [SerializeField] private float damage = 1f;
    [SerializeField] private float speed = 8f;
    [SerializeField] private float maxLifetime = 3f;
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private LayerMask obstacleLayer;

    [Header("视觉")]
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private TrailRenderer trail;

    private Vector2 flightDirection;
    private float lifetime;
    private bool launched;
    private bool consumed;

    /// <summary>发射者的 Transform，用于避免打到自己。</summary>
    private Transform source;

    void Awake()
    {
        if (visual == null) visual = GetComponent<SpriteRenderer>();
        EnsureVisiblePresentation();
        lifetime = maxLifetime;
    }

    private void EnsureVisiblePresentation()
    {
        if (visual != null)
        {
            visual.sortingOrder = Mathf.Max(visual.sortingOrder, 220);
            visual.color = new Color(0.2f, 0.85f, 1f, 1f);
        }

        if (trail == null) trail = GetComponent<TrailRenderer>();
        if (trail == null) trail = gameObject.AddComponent<TrailRenderer>();

        if (sharedTrailMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) sharedTrailMaterial = new Material(shader);
        }

        if (sharedTrailMaterial != null) trail.sharedMaterial = sharedTrailMaterial;
        trail.time = 0.1f;
        trail.startWidth = 0.14f;
        trail.endWidth = 0.01f;
        trail.minVertexDistance = 0.05f;
        trail.sortingOrder = 219;
        trail.startColor = new Color(0.2f, 0.85f, 1f, 0.9f);
        trail.endColor = new Color(0.2f, 0.85f, 1f, 0f);
    }

    void Update()
    {
        if (!launched || consumed) return;

        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            consumed = true;
            Destroy(gameObject);
            return;
        }

        Advance(Time.deltaTime);
    }

    /// <summary>唯一的运动入口：连续检测后推进，避免多套物理运动互相覆盖。</summary>
    private void Advance(float deltaTime)
    {
        if (!launched || consumed || deltaTime <= 0f) return;

        float step = speed * deltaTime;
        Vector2 currentPosition = transform.position;

        // 先扫掠再移动，避免高速弹体跨过较小的 Player Collider。
        int queryMask = targetLayer.value | obstacleLayer.value;
        if (queryMask == 0) queryMask = Physics2D.AllLayers;
        // v0.5.4.4.2 修复：扫掠半径 0.12f → 0.25f，与 Projectile.prefab 的 CircleCollider2D
        // radius=0.5 更匹配；0.12 在 8 m/s × dt 下容易擦边漏命中 Player。
        RaycastHit2D[] hits = Physics2D.CircleCastAll(currentPosition, 0.25f,
            flightDirection, step, queryMask);
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null || IsSourceCollider(hit.collider)) continue;
            if (ResolveHit(hit.collider)) return;
        }

        // 命中由上面的连续扫掠负责；Transform 推进不依赖 Physics2D 的更新模式。
        transform.position = currentPosition + flightDirection * step;
    }

    /// <summary>
    /// 发射投射物。调用方设置所有参数并启动飞行。
    /// </summary>
    public void Launch(Vector2 direction, float dmg, float spd, LayerMask targets,
        LayerMask obstacles, Transform src = null, float life = 3f)
    {
        flightDirection = direction.normalized;
        damage = dmg;
        speed = spd;
        targetLayer = targets;
        obstacleLayer = obstacles;
        source = src;
        maxLifetime = life;
        lifetime = life;
        launched = true;

        // 面向飞行方向
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!launched || consumed) return;

        ResolveHit(other);
    }

    private bool ResolveHit(Collider2D other)
    {
        if (other == null || IsSourceCollider(other)) return false;

        if (obstacleLayer.value != 0 && InLayer(other.gameObject.layer, obstacleLayer))
        {
            consumed = true;
            Destroy(gameObject);
            return true;
        }

        // 同时检查碰撞体和根节点 Layer，兼容 Player 子碰撞体层未同步的情况。
        bool isTargetLayer = InLayer(other.gameObject.layer, targetLayer)
            || InLayer(other.transform.root.gameObject.layer, targetLayer);
        if (!isTargetLayer) return false;

        // 命中
        IDamageable damageable = FindDamageableInParents(other.transform);
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
            DamagePopup.Spawn(other.bounds.center, damage);
        }

        consumed = true;
        Destroy(gameObject);
        return true;
    }

    private bool IsSourceCollider(Collider2D other)
    {
        if (other == null) return false;
        if (other.transform == transform || other.transform.IsChildOf(transform)) return true;
        if (source == null) return false;
        return other.transform == source || other.transform.IsChildOf(source);
    }

    private bool InLayer(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private static IDamageable FindDamageableInParents(Transform current)
    {
        while (current != null)
        {
            if (current.TryGetComponent<IDamageable>(out var damageable))
                return damageable;
            current = current.parent;
        }
        return null;
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
