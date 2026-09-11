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
/// V2 攻击方向合同。Horizontal 只允许左右；SelfCentered 为以施法者为中心的无方向范围。
/// 召唤攻击天然无方向，不读取此字段。
/// </summary>
public enum AttackDirectionMode
{
    Horizontal,
    SelfCentered
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

    [Tooltip("V2 横向攻击带的 Y 轴宽度。个别跨 Y 轴招式可单独增大；预警与实际判定共用。")]
    [SerializeField, Min(0.05f)] private float attackLaneWidth = 1f;

    [Tooltip("横向攻击从角色中心沿攻击方向前移的距离。预警、近战 Hitbox 与远程弹体出生点共用。")]
    [SerializeField, Min(0f)] private float attackOriginOffset = 0.35f;

    [Tooltip("有方向攻击固定为 Horizontal（Left/Right）；自身中心范围技选择 SelfCentered。")]
    [SerializeField] private AttackDirectionMode directionMode = AttackDirectionMode.Horizontal;

    [Tooltip("扇形全角（度）。仅用于 Arc 动画挥动幅度与预警指示器形状；v0.4.6 起近战伤害由武器矩形（WeaponHitbox）判定，不再使用此角度。")]
    [Range(0f, 360f)]
    [SerializeField] private float attackAngle = 140f;

    [SerializeField] private float attackDamage = 1f;
    [SerializeField] private float attackCooldown = 0.5f;

    [Tooltip("可命中目标的 LayerMask")]
    [SerializeField] private LayerMask targetLayer;

    [Tooltip("投射物/冲锋判定视为障碍的 LayerMask（LOS 与挡弹用；资产里字段一直在）")]
    [SerializeField] private LayerMask obstacleLayer;

    [Header("动画")]
    [Tooltip("动画播放方式")]
    [SerializeField] private AttackAnimationType animationType = AttackAnimationType.Arc;

    [SerializeField] private Ease attackEase = Ease.OutQuad;

    [Header("命中触发")]
    [Tooltip("Active阶段中触发命中的时间点比例（0=开始，1=结束）")]
    [Range(0f, 1f)]
    [SerializeField] private float activeMomentRatio = 0.5f;

    [Header("敌人多招选择")]
    [Tooltip("Distance 模式下的适用距离。x=最小、y=最大；0~0 表示不限距离。")]
    [SerializeField] private Vector2 distanceRange = Vector2.zero;
    [Tooltip("Weighted 模式下的抽取权重；0 表示不参与权重随机。")]
    [SerializeField] private int weight = 1;

    [Header("特殊攻击类型（v1.0.13 自 MCP 分支还原：数据资产里的同名字段一直都在，只是类字段曾被合并砍掉）")]
    [Tooltip("召唤攻击：Active 开始时生成小兵（EnemyCombat.SummonMinions）")]
    [SerializeField] private bool isSummon;
    [Tooltip("召唤的敌人 prefab（isSummon=true 时需要）")]
    [SerializeField] private GameObject summonPrefab;
    [Tooltip("每次召唤数量")]
    [Range(1, 5)]
    [SerializeField] private int summonCount = 2;
    [Tooltip("召唤半径（小兵出生在以自身为中心的此半径内）")]
    [SerializeField] private float summonRadius = 2f;

    [Tooltip("冲锋攻击：Active 期间朝锁定方向高速位移，撞墙/撞目标停止（近战判定照常）")]
    [SerializeField] private bool isCharge;
    [Tooltip("冲锋在无碰撞时的最大水平位移（世界单位）。预警总长度 = 本值 + Attack Range。")]
    [SerializeField, Min(0f)] private float chargeDistance = 2.5f;
    [Tooltip("冲锋时碰到此层停止位移（墙体/障碍等），与 TargetLayer 一起 OR 用")]
    [SerializeField] private LayerMask chargerCollisionLayer;

    public AttackShape AttackShape => attackShape;

    public float WindupTime => windupTime;
    public float ActiveDuration => activeDuration;
    public float RecoveryTime => recoveryTime;

    public float AttackRange => attackRange;
    public float AttackLaneWidth => attackLaneWidth;
    public float AttackOriginOffset => attackOriginOffset;
    public AttackDirectionMode DirectionMode => directionMode;
    public float AttackAngle => attackAngle;
    public float AttackDamage => attackDamage;
    public float AttackCooldown => attackCooldown;
    public LayerMask TargetLayer => targetLayer;

    public AttackAnimationType AnimationType => animationType;
    public Ease AttackEase => attackEase;
    public float ActiveMomentRatio => activeMomentRatio;
    public Vector2 DistanceRange => distanceRange;
    public float MinDistance => distanceRange.x;
    public float MaxDistance => distanceRange.y;
    public int Weight => weight;

    public bool IsInDistanceRange(float distance)
    {
        if (distanceRange.x <= 0f && distanceRange.y <= 0f) return true;
        if (distance < distanceRange.x) return false;
        return distanceRange.y <= 0f || distance <= distanceRange.y;
    }

    // v1.0.13 特殊攻击类型（自 MCP 分支还原）
    public bool IsSummon => isSummon;
    public GameObject SummonPrefab => summonPrefab;
    public int SummonCount => summonCount;
    public float SummonRadius => summonRadius;
    public bool IsCharge => isCharge;
    public float ChargeDistance => chargeDistance;
    public LayerMask ChargerCollisionLayer => chargerCollisionLayer;
    public LayerMask ObstacleLayer => obstacleLayer;

    /// <summary>
    /// V2 最终方向合同：显式自身中心、圆形、360° 或旋转攻击视为无方向范围；
    /// 其余有方向攻击一律解析为 Left/Right。
    /// </summary>
    public bool IsSelfCenteredArea => !isSummon &&
        (directionMode == AttackDirectionMode.SelfCentered
         || attackShape == AttackShape.Circle
         || attackAngle >= 359.9f
         || animationType == AttackAnimationType.FullCircle
         || animationType == AttackAnimationType.Spin);

    public bool UsesHorizontalDirection => !isSummon && !IsSelfCenteredArea;

    /// <summary>
    /// 创建运行时副本（v0.6.3 蓄力系统：近战装备武器的参数缩放只作用于副本）。
    /// 仅供运行时副本使用，禁止改磁盘资产。
    /// </summary>
    public AttackData CreateRuntimeCopy()
    {
        AttackData copy = Instantiate(this);
        copy.name += "_Runtime";
        return copy;
    }

    /// <summary>
    /// 设置攻击范围与扇形角度。仅供运行时副本使用，禁止改磁盘资产。
    /// </summary>
    public void SetRangeAngle(float range, float angle)
    {
        attackRange = Mathf.Max(0.01f, range);
        attackAngle = Mathf.Clamp(angle, 0f, 360f);
    }

    /// <summary>设置 V2 横向攻击几何。仅供运行时副本与测试使用，禁止改磁盘资产。</summary>
    public void SetHorizontalGeometry(float range, float laneWidth, float originOffset)
    {
        attackRange = Mathf.Max(0.01f, range);
        attackLaneWidth = Mathf.Max(0.05f, laneWidth);
        attackOriginOffset = Mathf.Max(0f, originOffset);
        directionMode = AttackDirectionMode.Horizontal;
    }

    /// <summary>设置冲锋走廊长度。仅供运行时副本与测试使用，禁止改磁盘资产。</summary>
    public void SetChargeGeometry(float distance)
    {
        isCharge = true;
        chargeDistance = Mathf.Max(0f, distance);
    }

    /// <summary>
    /// 设置攻击伤害。仅供运行时副本使用，禁止改磁盘资产。
    /// </summary>
    public void SetDamage(float damage)
    {
        attackDamage = damage;
    }

    void OnValidate()
    {
        attackAngle = Mathf.Clamp(attackAngle, 0f, 360f);
        activeMomentRatio = Mathf.Clamp01(activeMomentRatio);
        windupTime = Mathf.Max(0.001f, windupTime);
        activeDuration = Mathf.Max(0.001f, activeDuration);
        recoveryTime = Mathf.Max(0.001f, recoveryTime);
        attackRange = Mathf.Max(0.01f, attackRange);
        attackLaneWidth = Mathf.Max(0.05f, attackLaneWidth);
        attackOriginOffset = Mathf.Max(0f, attackOriginOffset);
        attackCooldown = Mathf.Max(0f, attackCooldown);
        chargeDistance = Mathf.Max(0f, chargeDistance);
        distanceRange.x = Mathf.Max(0f, distanceRange.x);
        distanceRange.y = Mathf.Max(0f, distanceRange.y);
        weight = Mathf.Max(0, weight);
    }
}
