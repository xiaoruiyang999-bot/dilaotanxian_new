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
        weaponPivot.localRotation = Quaternion.Euler(0f, 0f, baseAngle + startAngle);

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

        // 旋转动画：基准 + 结束偏移
        rotationTween = weaponPivot
            .DOLocalRotate(new Vector3(0f, 0f, baseAngle + endAngle), duration, rotateMode)
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
