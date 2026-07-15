using UnityEngine;
using DG.Tweening;
using System;

/// <summary>
/// 通用武器动画播放器。
/// 只负责按 AttackData 旋转 WeaponPivot，并在配置时间点触发命中回调。
/// 不参与状态机、伤害判定、目标选择、冷却判断，可复用于敌人/玩家/Boss/召唤物。
/// </summary>
public class WeaponAnimator : MonoBehaviour
{
    [SerializeField] private AttackData attackData;
    [SerializeField] private Transform weaponPivot;

    private Tween rotationTween;
    private Tween momentTween;

    public bool IsPlaying => rotationTween != null && rotationTween.IsActive() && rotationTween.IsPlaying();

    /// <summary>
    /// 播放一次攻击动画。
    /// onActiveMoment 会在 ActiveDuration * ActiveMomentRatio 时触发。
    /// 动画结束后不会主动通知调用方，调用方应自己计时。
    /// </summary>
    public void PlayAttack(Action onActiveMoment = null)
    {
        Stop();

        if (weaponPivot == null)
        {
            onActiveMoment?.Invoke();
            return;
        }

        // 没有配置时仍然允许安全 fallback，但正常应通过 Inspector 配置 AttackData
        if (attackData == null)
        {
            weaponPivot.localRotation = Quaternion.Euler(0, 0, -70f);
            onActiveMoment?.Invoke();
            rotationTween = weaponPivot
                .DOLocalRotate(new Vector3(0, 0, 70f), 0.25f, RotateMode.FastBeyond360)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => rotationTween = null);
            return;
        }

        weaponPivot.localRotation = Quaternion.Euler(0, 0, attackData.AttackStartAngle);

        if (onActiveMoment != null)
        {
            float momentDelay = attackData.ActiveDuration * attackData.ActiveMomentRatio;
            momentTween = DOVirtual.DelayedCall(momentDelay, () =>
            {
                onActiveMoment?.Invoke();
                momentTween = null;
            });
        }

        rotationTween = weaponPivot
            .DOLocalRotate(new Vector3(0, 0, 360), attackData.ActiveDuration, RotateMode.FastBeyond360)
            .SetRelative()
            .SetEase(attackData.AttackEase)
            .OnComplete(() => rotationTween = null);
    }

    /// <summary>
    /// 立即停止动画并重置武器角度。
    /// </summary>
    public void Stop()
    {
        rotationTween?.Kill(complete: false);
        momentTween?.Kill(complete: false);
        rotationTween = null;
        momentTween = null;
        if (weaponPivot != null)
            weaponPivot.localRotation = Quaternion.identity;
    }
}
