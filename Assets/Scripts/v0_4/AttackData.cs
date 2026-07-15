using UnityEngine;
using DG.Tweening;

/// <summary>
/// 通用攻击配置数据。纯数据容器，不包含运行逻辑。
/// 可用于敌人、玩家、Boss、召唤物等各种攻击者。
/// </summary>
[CreateAssetMenu(fileName = "AttackData", menuName = "Combat/Attack Data")]
public class AttackData : ScriptableObject
{
    [Header("阶段时长")]
    [SerializeField] private float windupTime = 0.25f;
    [SerializeField] private float activeDuration = 0.25f;
    [SerializeField] private float recoveryTime = 0.35f;

    [Header("武器动画")]
    [SerializeField] private float attackStartAngle = -70f;
    [SerializeField] private float attackEndAngle = 70f;
    [SerializeField] private Ease attackEase = Ease.OutQuad;

    [Header("命中触发")]
    [Tooltip("Active阶段中触发命中的时间点比例（0=开始，1=结束）")]
    [Range(0f, 1f)]
    [SerializeField] private float activeMomentRatio = 0.5f;

    public float WindupTime => windupTime;
    public float ActiveDuration => activeDuration;
    public float RecoveryTime => recoveryTime;
    public float AttackStartAngle => attackStartAngle;
    public float AttackEndAngle => attackEndAngle;
    public Ease AttackEase => attackEase;
    public float ActiveMomentRatio => activeMomentRatio;
}
