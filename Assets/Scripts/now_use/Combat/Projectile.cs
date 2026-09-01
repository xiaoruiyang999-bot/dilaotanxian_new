using UnityEngine;

/// <summary>
/// 子弹（v0.6.3，计划书 §三）：直线飞行 + Trigger 命中结算。
/// - 构建：Kinematic Rigidbody2D + CircleCollider2D(trigger, radius=data.Radius)
///   + Projectile + ProjectileVisualBuilder 视觉子物体（根旋转 = 飞行方向 Atan2 角度）；
/// - Update 直线移动 dir × Speed×speedMul，lifetime 到期自毁（兜底）；
/// - OnTriggerEnter2D：跳过 owner 及其同根（防生成瞬间自伤）、跳过其他 trigger（敌人探测圈）；
///   命中 TargetLayer 内 IDamageable → 结算伤害（v0.7.0：玩家子弹走 DamageResolver 新管线，
///   敌人子弹 TakeDamage(Damage×damageMul) 原路径）+ 命中特效 + 销毁；
///   命中 Default 层非触发器（墙/关闭的门）→ 命中特效 + 销毁（子弹撞墙销毁，硬性要求）。
/// 对象池不做（技术债，计划书第七章）。
/// </summary>
public class Projectile : MonoBehaviour
{
    private ProjectileData data;
    private Vector2 direction;
    private GameObject owner;
    private PlayerStats ownerStats;   // v0.7.0：owner 根上的 PlayerStats；非空 = 玩家子弹（走新管线），空 = 敌人子弹（原路径）
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

        // v0.7.0：玩家发射的子弹走 DamageResolver（owner 根查 PlayerStats，敌人根无此组件）
        ownerStats = owner != null ? owner.GetComponentInParent<PlayerStats>() : null;
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

        // 同时检查碰撞体与根对象 Layer，并向父级查找受伤接口：恢复 MCP 分支对
        // “子碰撞体未同步 Layer / IDamageable 挂根节点”Prefab 结构的兼容。
        bool isTargetLayer = InLayer(other.gameObject.layer, data.TargetLayer)
            || InLayer(other.transform.root.gameObject.layer, data.TargetLayer);
        IDamageable damageable = isTargetLayer ? FindDamageableInParents(other.transform) : null;
        if (damageable != null)
        {
            // v0.7.0：玩家子弹走新管线（角色攻击+子弹伤害）×damageMul(蓄力)×暴击；敌人子弹原路径
            float dealtDamage;
            if (ownerStats != null)
            {
                DamageContext ctx = new DamageContext
                {
                    baseAttack = ownerStats.Attack + data.Damage,
                    // v0.7.5：输出倍率通道，命中时刻查询（发射后开强力一击也吃加成）；无 BuffManager 为 1 零差异
                    multiplier = damageMul * BuffManager.DamageDealtMulOf(owner),
                    critRate = ownerStats.CritRate,
                    critDamage = ownerStats.CritDamage
                };
                dealtDamage = DamageResolver.Deal(damageable, ctx);
            }
            else
            {
                dealtDamage = data.Damage * damageMul;
                damageable.TakeDamage(dealtDamage);
            }
            DamagePopup.Spawn(other.bounds.center, dealtDamage);
            ProjectileVisualBuilder.SpawnHitEffect(hitPoint, data.BodyColor);
            Destroy(gameObject);
            return;
        }

        // Trigger 已在上方跳过；其余非 Trigger 实体（墙、关闭的门、障碍物、无受伤接口目标）
        // 都应挡弹，不能只认 Default 层。
        ProjectileVisualBuilder.SpawnHitEffect(hitPoint, data.BodyColor);
        Destroy(gameObject);
    }

    private static bool InLayer(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private static IDamageable FindDamageableInParents(Transform current)
    {
        while (current != null)
        {
            if (current.TryGetComponent(out IDamageable damageable))
                return damageable;
            current = current.parent;
        }
        return null;
    }
}
