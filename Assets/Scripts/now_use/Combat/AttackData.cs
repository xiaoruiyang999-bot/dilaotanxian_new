using UnityEngine;
using DG.Tweening;

/// <summary>
/// 攻击判定形状。
/// Fan：扇形，使用 AttackRange + AttackAngle。
/// Circle：圆形，仅使用 AttackRange。
/// Box：矩形，预留。
/// </summary>
public enum AttackShape
{
    Fan,
    Circle,
    Box
}

/// <summary>
/// 攻击动画类型。
/// Arc：以父对象朝向为中心，向左右各挥动 AttackAngle/2 的扇形。
/// FullCircle：从父对象朝向开始，相对旋转 360°。
/// Thrust / Spin / Throw：预留，供 v0.5 技能系统扩展。
/// </summary>
public enum AttackAnimationType
{
    Arc,
    FullCircle,
    Thrust,
    Spin,
    Throw
}

/// <summary>
/// 通用攻击配置数据。纯数据容器，不包含运行逻辑。
/// 可用于玩家、敌人、Boss、召唤物等任意攻击者。
/// v0.5 可扩展 AttackAnimationType、AttackShape 与自定义角度字段。
/// </summary>
[CreateAssetMenu(fileName = "AttackData", menuName = "Combat/Attack Data")]
public class AttackData : ScriptableObject
{
    [Header("攻击形状")]
    [Tooltip("攻击判定形状，决定动画与判定范围的几何类型")]
    [SerializeField] private AttackShape attackShape = AttackShape.Fan;

    [Header("阶段时长")]
    [SerializeField] private float windupTime = 0.25f;
    [SerializeField] private float activeDuration = 0.25f;
    [SerializeField] private float recoveryTime = 0.35f;

    [Header("攻击判定")]
    [Tooltip("攻击判定半径（物理）")]
    [SerializeField] private float attackRange = 1.5f;
    [Tooltip("v0.8(M3)：判定/预警宽度覆盖（>0 替代武器宽）——Boss 大招宽横扫用")]
    [SerializeField] private float overrideWidth = 0f;

    [Tooltip("扇形全角（度）。仅用于 Arc 动画挥动幅度与预警指示器形状；v0.4.6 起近战伤害由武器矩形（WeaponHitbox）判定，不再使用此角度。")]
    [Range(0f, 360f)]
    [SerializeField] private float attackAngle = 140f;

    [SerializeField] private float attackDamage = 1f;
    [SerializeField] private float attackCooldown = 0.5f;

    [Tooltip("可命中目标的 LayerMask")]
    [SerializeField] private LayerMask targetLayer;

    [Header("投射物-墙体")]
    [Tooltip("投射物遇到此层立即销毁（墙体/障碍物等）。仅 isProjectile=true 时生效。")]
    [SerializeField] private LayerMask obstacleLayer;

    [Header("冲锋碰撞")]
    [Tooltip("冲锋时碰到此层停止位移（墙体+目标等）。仅 isCharge=true 时生效。")]
    [SerializeField] private LayerMask chargerCollisionLayer;

    [Header("动画")]
    [Tooltip("动画播放方式")]
    [SerializeField] private AttackAnimationType animationType = AttackAnimationType.Arc;

    [SerializeField] private Ease attackEase = Ease.OutQuad;

    [Header("命中触发")]
    [Tooltip("Active阶段中触发命中的时间点比例（0=开始，1=结束）")]
    [Range(0f, 1f)]
    [SerializeField] private float activeMomentRatio = 0.5f;

    [Header("招式选择（v0.5.4.1 多招系统）")]
    [Tooltip("Distance 策略：目标在此距离范围内可选用此招。x=最小距离，y=最大距离。0~0=不限距离。")]
    [SerializeField] private Vector2 distanceRange = Vector2.zero;

    [Tooltip("Weighted 策略：此招式的权重（0 = 不会被权重随机选中，但 MinCount 保底仍生效）。")]
    [SerializeField] private int weight = 1;

    [Header("特殊攻击类型（v0.5.4.2）")]
    [Tooltip("是否为投射物攻击（远程/游击用）。true时Active阶段发射投射物而非近战矩形检测。")]
    [SerializeField] private bool isProjectile;

    [Tooltip("投射物 prefab（isProjectile=true时需要）")]
    [SerializeField] private GameObject projectilePrefab;

    [Tooltip("投射物飞行速度")]
    [SerializeField] private float projectileSpeed = 8f;

    [Tooltip("是否为冲锋攻击（Charger用）。true时Active阶段敌人自身会向前移动。")]
    [SerializeField] private bool isCharge;

    [Tooltip("冲锋速度倍率（相对于 MoveSpeed）")]
    [Range(1f, 5f)]
    [SerializeField] private float chargeSpeedMultiplier = 3f;

    [Tooltip("是否为召唤攻击（Summoner用）。true时Active阶段实例化summonPrefab。")]
    [SerializeField] private bool isSummon;

    [Tooltip("召唤的敌人 prefab（isSummon=true时需要）")]
    [SerializeField] private GameObject summonPrefab;

    [Tooltip("每次召唤数量")]
    [Range(1, 5)]
    [SerializeField] private int summonCount = 2;

    [Tooltip("召唤半径（小兵出生在以自身为中心的此半径内）")]
    [SerializeField] private float summonRadius = 2f;

    public AttackShape AttackShape => attackShape;

    public float WindupTime => windupTime;
    public float ActiveDuration => activeDuration;
    public float RecoveryTime => recoveryTime;

    public float AttackRange => attackRange;
    public float OverrideWidth => overrideWidth;
    public float AttackAngle => attackAngle;
    public float AttackDamage => attackDamage;
    public float AttackCooldown => attackCooldown;
    public LayerMask TargetLayer => targetLayer;

    /// <summary>投射物遇到此层立即销毁（墙体/障碍物等）。</summary>
    public LayerMask ObstacleLayer => obstacleLayer;

    /// <summary>冲锋时碰到此层停止位移（墙体+目标等）。isCharge=true 时与 TargetLayer 做 OR 一起用。</summary>
    public LayerMask ChargerCollisionLayer => chargerCollisionLayer;

    public AttackAnimationType AnimationType => animationType;
    public Ease AttackEase => attackEase;
    public float ActiveMomentRatio => activeMomentRatio;

    /// <summary>Distance 策略：目标在此距离范围内可选用此招。x=min, y=max。0~0=不限距离。</summary>
    public Vector2 DistanceRange => distanceRange;
    public float MinDistance => distanceRange.x;
    public float MaxDistance => distanceRange.y;

    /// <summary>Weighted 策略：此招式的权重。0 = 不会被权重随机选中。</summary>
    public int Weight => weight;

    /// <summary>Distance 策略：检查目标距离是否在此招式的适用范围内。</summary>
    public bool IsInDistanceRange(float distToTarget)
    {
        if (distanceRange.x <= 0f && distanceRange.y <= 0f) return true;  // 0~0 = 不限
        if (distToTarget < distanceRange.x) return false;
        if (distanceRange.y > 0f && distToTarget > distanceRange.y) return false;
        return true;
    }

    // --- v0.5.4.2 特殊攻击类型属性 ---

    public bool IsProjectile => isProjectile;
    public GameObject ProjectilePrefab => projectilePrefab;
    public float ProjectileSpeed => projectileSpeed;

    public bool IsCharge => isCharge;
    public float ChargeSpeedMultiplier => chargeSpeedMultiplier;

    public bool IsSummon => isSummon;
    public GameObject SummonPrefab => summonPrefab;
    public int SummonCount => summonCount;
    public float SummonRadius => summonRadius;

    void OnValidate()
    {
        attackAngle = Mathf.Clamp(attackAngle, 0f, 360f);
        activeMomentRatio = Mathf.Clamp01(activeMomentRatio);
        windupTime = Mathf.Max(0.001f, windupTime);
        activeDuration = Mathf.Max(0.001f, activeDuration);
        recoveryTime = Mathf.Max(0.001f, recoveryTime);
        attackRange = Mathf.Max(0.01f, attackRange);
        attackCooldown = Mathf.Max(0f, attackCooldown);
        weight = Mathf.Max(0, weight);
        projectileSpeed = Mathf.Max(0.1f, projectileSpeed);
        chargeSpeedMultiplier = Mathf.Max(1f, chargeSpeedMultiplier);
        summonCount = Mathf.Max(1, summonCount);
    }
}
