using UnityEngine;
using DG.Tweening;

/// <summary>
/// 武器控制器。
/// 负责 WeaponPivot 普通状态朝向、WeaponSprite 视觉管理、攻击方向锁定、攻击起止角度计算。
/// 是 WeaponPivot 与 WeaponSprite 的唯一管理者。
/// 攻击期间不直接修改 WeaponPivot.localRotation，只向 WeaponAnimator 提供角度参数。
/// </summary>
public class WeaponController : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("武器挂载点，必须位于角色中心")]
    [SerializeField] private Transform weaponPivot;

    [Tooltip("武器 Sprite 对象")]
    [SerializeField] private Transform weaponSprite;

    [Tooltip("当前武器使用的攻击数据")]
    [SerializeField] private AttackData attackData;

    [Header("视觉")]
    [Tooltip("武器 Sprite 的宽度（垂直于攻击方向的缩放）")]
    [SerializeField] private float weaponWidth = 0.15f;

    private float aimAngle;
    private bool isDirectionLocked;

    public bool IsDirectionLocked => isDirectionLocked;

    /// <summary>武器矩形宽度。供 WeaponHitbox 读取，保证判定宽度与视觉宽度同源。</summary>
    public float WeaponWidth => weaponWidth;

    /// <summary>武器挂载点。供 WeaponHitbox 读取检测姿态。</summary>
    public Transform WeaponPivot => weaponPivot;

    /// <summary>
    /// 获取当前瞄准方向（归一化 Vector2）。
    /// 攻击锁定期间返回锁定瞬间的方向。
    /// </summary>
    public Vector2 GetAimDirection()
    {
        float rad = aimAngle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private void Awake()
    {
        RefreshWeaponVisual();
    }

    private void OnValidate()
    {
        RefreshWeaponVisual();
    }

    /// <summary>
    /// 普通状态下更新武器朝向。
    /// 攻击期间（方向已锁定）不会修改 WeaponPivot。
    /// </summary>
    /// <param name="direction">瞄准方向</param>
    /// <param name="applyRotation">是否立即将角度应用到 WeaponPivot.localRotation。
    /// Player 应传 true（Player transform 不旋转，由 WeaponPivot 直接朝向鼠标）。
    /// Enemy 应传 false（Enemy transform 已朝向目标，WeaponPivot 保持 identity 跟随父物体，
    /// 避免父物体旋转与 localRotation 叠加导致武器方向翻倍）。</param>
    public void SetAimDirection(Vector2 direction, bool applyRotation = true)
    {
        if (isDirectionLocked) return;
        if (direction.sqrMagnitude < 0.0001f) return;

        aimAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (applyRotation)
            ApplyAimRotation();
    }

    /// <summary>
    /// 锁定当前攻击方向。
    /// 调用后 SetAimDirection 不再生效，直到 UnlockAttackDirection。
    /// 同时刷新 WeaponSprite 视觉。
    /// </summary>
    public void LockAttackDirection()
    {
        isDirectionLocked = true;
        RefreshWeaponVisual();
    }

    /// <summary>
    /// 解锁攻击方向，恢复普通状态朝向更新。
    /// </summary>
    public void UnlockAttackDirection()
    {
        isDirectionLocked = false;
        ApplyAimRotation();
    }

    /// <summary>
    /// 重置武器朝向为父物体正前方（localRotation = identity）。
    /// 用于 Enemy 攻击结束后让武器自然跟随自身 transform 旋转。
    /// </summary>
    public void ResetAimToForward()
    {
        isDirectionLocked = false;
        aimAngle = 0f;
        ApplyAimRotation();
    }

    /// <summary>
    /// 获取攻击动画起始角度的本地偏移。
    /// 返回值是相对于当前锁定朝向的局部旋转偏移，由 WeaponAnimator 叠加到 WeaponPivot.localRotation 上。
    /// </summary>
    public float GetAttackStartAngle()
    {
        if (attackData == null)
        {
            Debug.LogWarning($"[{nameof(WeaponController)}] attackData is null on {gameObject.name}", this);
            return 0f;
        }

        switch (attackData.AnimationType)
        {
            case AttackAnimationType.FullCircle:
            case AttackAnimationType.Spin:
                return 0f;
            case AttackAnimationType.Arc:
            case AttackAnimationType.Thrust:
            case AttackAnimationType.Throw:
            default:
                return -attackData.AttackAngle * 0.5f;
        }
    }

    /// <summary>
    /// 获取攻击动画结束角度的本地偏移。
    /// 返回值是相对于当前锁定朝向的局部旋转偏移。
    /// </summary>
    public float GetAttackEndAngle()
    {
        if (attackData == null)
        {
            Debug.LogWarning($"[{nameof(WeaponController)}] attackData is null on {gameObject.name}", this);
            return 0f;
        }

        switch (attackData.AnimationType)
        {
            case AttackAnimationType.FullCircle:
                return 360f;
            case AttackAnimationType.Spin:
                return 720f;
            case AttackAnimationType.Arc:
            case AttackAnimationType.Thrust:
            case AttackAnimationType.Throw:
            default:
                return attackData.AttackAngle * 0.5f;
        }
    }

    /// <summary>
    /// 获取攻击动画旋转模式。
    /// </summary>
    public RotateMode GetAttackRotateMode()
    {
        if (attackData == null) return RotateMode.Fast;

        return attackData.AnimationType == AttackAnimationType.FullCircle
            || attackData.AnimationType == AttackAnimationType.Spin
                ? RotateMode.FastBeyond360
                : RotateMode.Fast;
    }

    /// <summary>
    /// 刷新武器 Sprite 视觉：长度、位置、宽度、初始旋转。
    /// </summary>
    public void RefreshWeaponVisual()
    {
        if (weaponSprite == null) return;

        weaponSprite.localRotation = Quaternion.Euler(0f, 0f, -90f);

        float length = attackData != null ? attackData.AttackRange : 1f;
        weaponSprite.localScale = new Vector3(weaponWidth, length, 1f);
        weaponSprite.localPosition = new Vector3(length * 0.5f, 0f, 0f);
    }

    /// <summary>
    /// 设置武器长度，供外部动态调整（如 Buff、换武器）。
    /// </summary>
    public void SetWeaponLength(float length)
    {
        if (weaponSprite == null) return;
        weaponSprite.localScale = new Vector3(weaponWidth, length, 1f);
        weaponSprite.localPosition = new Vector3(length * 0.5f, 0f, 0f);
    }

    private void ApplyAimRotation()
    {
        if (weaponPivot == null) return;
        weaponPivot.localRotation = Quaternion.Euler(0f, 0f, aimAngle);
    }
}
