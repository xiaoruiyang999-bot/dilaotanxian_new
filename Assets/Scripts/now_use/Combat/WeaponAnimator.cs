using UnityEngine;
using DG.Tweening;
using System;

/// <summary>
/// 纯武器旋转动画播放器。
/// 攻击期间唯一负责修改 WeaponPivot.localRotation。
/// 不读取 AttackData，不处理输入/AI/判定，不修改 WeaponSprite，不管理武器朝向。
/// 所有动画参数由调用方（WeaponController + PlayerCombat/EnemyAI）传入。
/// </summary>
public class WeaponAnimator : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("武器挂载点，攻击期间通过 Tween 修改其 localRotation")]
    [SerializeField] private Transform weaponPivot;

    private Tween rotationTween;
    private Tween momentTween;
    private bool activeMomentTriggered;

    /// <summary>
    /// 是否正在播放攻击动画。
    /// </summary>
    public bool IsPlaying => rotationTween != null
        && rotationTween.IsActive()
        && rotationTween.IsPlaying();

    /// <summary>
    /// 播放一次武器旋转动画。
    /// 此方法为 WeaponPivot.localRotation 在攻击期间的唯一修改入口。
    /// 传入的 startAngle/endAngle 为相对当前 WeaponPivot.localRotation 的局部偏移，
    /// 由 WeaponController 在 LockAttackDirection 后提供。
    /// </summary>
    /// <param name="startAngle">起始角度偏移（本地空间 Z 轴，度）</param>
    /// <param name="endAngle">结束角度偏移（本地空间 Z 轴，度）</param>
    /// <param name="duration">动画持续时间</param>
    /// <param name="ease">动画曲线</param>
    /// <param name="rotateMode">旋转模式，FullCircle 应传入 RotateMode.FastBeyond360</param>
    /// <param name="activeMomentRatio">命中触发时间点比例（0=开始，1=结束）</param>
    /// <param name="onActiveMoment">命中时刻回调</param>
    public void Play(
        float startAngle,
        float endAngle,
        float duration,
        Ease ease,
        RotateMode rotateMode,
        float activeMomentRatio,
        Action onActiveMoment = null)
    {
        Stop();

        if (weaponPivot == null)
        {
            onActiveMoment?.Invoke();
            return;
        }

        activeMomentTriggered = false;

        // 以当前 WeaponPivot.localRotation 的 Z 角度作为基准方向
        float baseAngle = weaponPivot.localRotation.eulerAngles.z;

        if (duration <= 0f)
        {
            weaponPivot.localRotation = Quaternion.Euler(0f, 0f, baseAngle + endAngle);
            onActiveMoment?.Invoke();
            return;
        }

        // 设置起始角度：基准 + 偏移
        float startZ = baseAngle + startAngle;
        weaponPivot.localRotation = Quaternion.Euler(0f, 0f, startZ);

        // 命中时刻回调
        if (onActiveMoment != null)
        {
            float momentDelay = duration * Mathf.Clamp01(activeMomentRatio);
            momentTween = DOVirtual.DelayedCall(momentDelay, () =>
            {
                if (!activeMomentTriggered)
                {
                    activeMomentTriggered = true;
                    onActiveMoment?.Invoke();
                }
                momentTween = null;
            }).SetLink(gameObject);   // 攻击者死亡销毁时自动 kill，避免回调打进已销毁对象
        }

        // 旋转动画：浮点 tween 每帧直接写 localRotation（startZ → startZ+扫幅）。
        // v0.6.3 修复：欧拉角 tween 会被 Unity 归一化到 [0,360)，起始角为负时方向翻转
        // （瞄准右上方时普通/蓄力挥击从背后扫过）；浮点直写彻底绕开归一化，
        // 任何瞄准方向、任何扫幅（含蓄力 >180°）都按挥击方向正向扫满。
        // rotateMode 参数保留兼容调用方，实际路径由扫幅符号唯一决定。
        float sweep = endAngle - startAngle;
        rotationTween = DOVirtual
            .Float(0f, sweep, duration,
                v => { if (weaponPivot != null) weaponPivot.localRotation = Quaternion.Euler(0f, 0f, startZ + v); })
            .SetEase(ease)
            .SetLink(weaponPivot.gameObject)   // 目标（敌人/玩家）销毁时自动 kill，避免 DOTween safe mode 报 missing target
            .OnComplete(() => rotationTween = null);
    }

    /// <summary>
    /// 停止当前动画。
    /// 只负责 Kill Tween，不修改 WeaponPivot 的 rotation / localRotation / position / scale。
    /// 攻击结束后的朝向恢复由 WeaponController 负责。
    /// </summary>
    public void Stop()
    {
        rotationTween?.Kill(complete: false);
        momentTween?.Kill(complete: false);
        rotationTween = null;
        momentTween = null;
        activeMomentTriggered = false;
    }
}
