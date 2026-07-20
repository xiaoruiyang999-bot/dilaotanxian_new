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

    [Tooltip("扇形全角（度）。仅用于 Arc 动画挥动幅度与预警指示器形状；v0.4.6 起近战伤害由武器矩形（WeaponHitbox）判定，不再使用此角度。")]
    [Range(0f, 360f)]
    [SerializeField] private float attackAngle = 140f;

    [SerializeField] private float attackDamage = 1f;
    [SerializeField] private float attackCooldown = 0.5f;

    [Tooltip("可命中目标的 LayerMask")]
    [SerializeField] private LayerMask targetLayer;

    [Header("动画")]
    [Tooltip("动画播放方式")]
    [SerializeField] private AttackAnimationType animationType = AttackAnimationType.Arc;

    [SerializeField] private Ease attackEase = Ease.OutQuad;

    [Header("命中触发")]
    [Tooltip("Active阶段中触发命中的时间点比例（0=开始，1=结束）")]
    [Range(0f, 1f)]
    [SerializeField] private float activeMomentRatio = 0.5f;

    public AttackShape AttackShape => attackShape;

    public float WindupTime => windupTime;
    public float ActiveDuration => activeDuration;
    public float RecoveryTime => recoveryTime;

    public float AttackRange => attackRange;
    public float AttackAngle => attackAngle;
    public float AttackDamage => attackDamage;
    public float AttackCooldown => attackCooldown;
    public LayerMask TargetLayer => targetLayer;

    public AttackAnimationType AnimationType => animationType;
    public Ease AttackEase => attackEase;
    public float ActiveMomentRatio => activeMomentRatio;

    void OnValidate()
    {
        attackAngle = Mathf.Clamp(attackAngle, 0f, 360f);
        activeMomentRatio = Mathf.Clamp01(activeMomentRatio);
        windupTime = Mathf.Max(0.001f, windupTime);
        activeDuration = Mathf.Max(0.001f, activeDuration);
        recoveryTime = Mathf.Max(0.001f, recoveryTime);
        attackRange = Mathf.Max(0.01f, attackRange);
        attackCooldown = Mathf.Max(0f, attackCooldown);
    }
}
