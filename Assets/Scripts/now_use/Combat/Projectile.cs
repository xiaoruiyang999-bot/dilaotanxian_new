using UnityEngine;

/// <summary>
/// 子弹（v0.6.3，计划书 §三）：直线飞行 + Trigger 命中结算。
/// - 构建：Kinematic Rigidbody2D + CircleCollider2D(trigger, radius=data.Radius)
///   + Projectile + ProjectileVisualBuilder 视觉子物体（根旋转 = 飞行方向 Atan2 角度）；
/// - Update 直线移动 dir × Speed×speedMul，lifetime 到期自毁（兜底）；
/// - OnTriggerEnter2D：跳过 owner 及其同根（防生成瞬间自伤）、跳过其他 trigger（敌人探测圈）；
///   命中 TargetLayer 内 IDamageable → TakeDamage(Damage×damageMul) + 命中特效 + 销毁；
///   命中 Default 层非触发器（墙/关闭的门）→ 命中特效 + 销毁（子弹撞墙销毁，硬性要求）。
/// 对象池不做（技术债，计划书第七章）。
/// </summary>
public class Projectile : MonoBehaviour
{
    private ProjectileData data;
    private Vector2 direction;
    private GameObject owner;
    private float damageMul = 1f;
    private float speedMul = 1f;
    private float lifetime;

    /// <summary>
    /// 发射一颗子弹（PlayerCombat 远程模式调用）。
    /// damageMul/speedMul 为弓箭蓄力增益入口（不动数据资产）。
    /// </summary>
    public static Projectile Launch(ProjectileData data, Vector2 origin, Vector2 direction,
        GameObject owner, float damageMul = 1f, float speedMul = 1f)
    {
        if (data == null || direction.sqrMagnitude < 0.0001f) return null;

        GameObject go = new GameObject($"Projectile_{data.DisplayName}");
        go.transform.position = origin;
        // 根旋转 = 飞行方向：视觉部件沿 +X 排布，直接对齐
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = data.Radius;

        Projectile p = go.AddComponent<Projectile>();
        p.Init(data, direction.normalized, owner, damageMul, speedMul);

        GameObject visual = ProjectileVisualBuilder.BuildVisual(data);
        if (visual != null)
            visual.transform.SetParent(go.transform, false);
        return p;
    }

    /// <summary>存发射参数（Launch 内部调用）。</summary>
    private void Init(ProjectileData data, Vector2 dir, GameObject owner, float damageMul, float speedMul)
    {
        this.data = data;
        direction = dir;
        this.owner = owner;
        this.damageMul = damageMul;
        this.speedMul = speedMul;
        lifetime = data.Lifetime;
    }

    private void Update()
    {
        if (data == null) return;

        transform.position += (Vector3)(direction * (data.Speed * speedMul * Time.deltaTime));

        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (data == null) return;

        // 防自伤：跳过 owner 自身/子物体/同根（生成瞬间与玩家碰撞体重叠）
        if (owner != null && (other.transform.IsChildOf(owner.transform)
            || other.transform.root == owner.transform.root))
            return;

        // 跳过其他触发器（敌人探测圈等）
        if (other.isTrigger) return;

        Vector2 hitPoint = other.ClosestPoint(transform.position);

        // 目标层内 IDamageable → 结算伤害
        if (((1 << other.gameObject.layer) & data.TargetLayer.value) != 0
            && other.TryGetComponent(out IDamageable damageable))
        {
            damageable.TakeDamage(data.Damage * damageMul);
            ProjectileVisualBuilder.SpawnHitEffect(hitPoint, data.BodyColor);
            Destroy(gameObject);
            return;
        }

        // Default 层非触发器 = 墙/关闭的门/其他固体 → 撞墙销毁
        if (other.gameObject.layer == 0)
        {
            ProjectileVisualBuilder.SpawnHitEffect(hitPoint, data.BodyColor);
            Destroy(gameObject);
        }
    }
}
