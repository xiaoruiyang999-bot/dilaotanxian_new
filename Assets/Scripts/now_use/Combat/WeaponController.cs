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

    [Tooltip("自定义手持视觉沿攻击方向的外移量（v0.6.3）：让武器从角色圆球边缘伸出、展示全貌。≈角色半径")]
    [SerializeField] private float customVisualGripOffset = 0.35f;

    private float aimAngle;
    private bool isDirectionLocked;

    // v0.6.3：宽度倍率（枪矛 RectScale 蓄力缩放用），1 = 基准宽度
    private float widthMultiplier = 1f;

    // v0.6.3：默认 weaponSprite 的渲染器缓存 + 当前自定义手持视觉（WeaponVisualBuilder 构建）
    private SpriteRenderer weaponSpriteRenderer;
    private GameObject customVisual;
    private float customVisualBaseLocalX;   // 自定义视觉基准外移（戳击动画以此为原点）

    public bool IsDirectionLocked => isDirectionLocked;

    /// <summary>武器矩形宽度。供 WeaponHitbox 读取，保证判定宽度与视觉宽度同源。</summary>
    public float WeaponWidth => weaponWidth * widthMultiplier;

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
        // 缓存默认武器色块的渲染器，自定义视觉挂载时隐藏/卸下时恢复
        if (weaponSprite != null)
            weaponSpriteRenderer = weaponSprite.GetComponent<SpriteRenderer>();

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
        float width = weaponWidth * widthMultiplier;   // v0.6.3：宽度应用倍率（蓄力宽度缩放）
        weaponSprite.localScale = new Vector3(width, length, 1f);
        weaponSprite.localPosition = new Vector3(length * 0.5f, 0f, 0f);
    }

    /// <summary>
    /// 设置武器长度，供外部动态调整（如 Buff、换武器）。
    /// </summary>
    public void SetWeaponLength(float length)
    {
        if (weaponSprite == null) return;
        weaponSprite.localScale = new Vector3(weaponWidth * widthMultiplier, length, 1f);
        weaponSprite.localPosition = new Vector3(length * 0.5f, 0f, 0f);
    }

    /// <summary>
    /// 设置宽度倍率（v0.6.3 枪矛 RectScale 蓄力：武器宽度 ×(1→chargeWidthMul)），并刷新视觉。
    /// WeaponHitbox 每帧实时读 WeaponWidth，判定宽度同步生效。
    /// </summary>
    public void SetWidthMultiplier(float mul)
    {
        widthMultiplier = mul;
        RefreshWeaponVisual();
    }

    /// <summary>
    /// 切换攻击数据（v0.6.3 换装同步视觉长度），并刷新视觉。
    /// </summary>
    public void SetAttackData(AttackData data)
    {
        attackData = data;
        RefreshWeaponVisual();
    }

    /// <summary>
    /// 挂载自定义手持视觉（v0.6.3 WeaponVisualBuilder 构建的程序化多色块）。
    /// 父级设到 weaponPivot，localRotation -90°（与 weaponSprite 同约定），同时隐藏默认武器色块。
    /// 重复设置时先清除旧视觉。
    /// </summary>
    public void SetCustomVisual(GameObject visual)
    {
        ClearCustomVisual();
        if (visual == null) return;

        customVisual = visual;
        Transform parent = weaponPivot != null ? weaponPivot : transform;
        visual.transform.SetParent(parent, false);
        // 沿攻击方向外移：武器从角色圆球边缘伸出（原点在球心会被球身遮住后半截）。
        // localPosition 会被父级缩放（Player 根 0.6）压缩，除以 lossyScale 保证外移量是世界单位
        float parentScale = parent.lossyScale.x > 0.001f ? parent.lossyScale.x : 1f;
        customVisualBaseLocalX = customVisualGripOffset / parentScale;
        visual.transform.localPosition = new Vector3(customVisualBaseLocalX, 0f, 0f);
        visual.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);

        if (weaponSpriteRenderer != null)
        {
            // 层级对齐默认武器色块（11）：builder 部件基准层级为 3，整体平移保持部件间相对层次，
            // 否则部件层级（3~5）低于角色身体（10），会被球身盖住
            int orderDelta = weaponSpriteRenderer.sortingOrder - 3;
            foreach (SpriteRenderer sr in visual.GetComponentsInChildren<SpriteRenderer>(true))
                sr.sortingOrder += orderDelta;

            weaponSpriteRenderer.enabled = false;
        }
    }

    /// <summary>
    /// 缩放自定义手持视觉（v0.6.3 蓄力模型长度适配）：lengthMul 沿武器方向、widthMul 垂直方向。
    /// 只影响视觉，判定由 AttackData 副本与宽度倍率决定。
    /// </summary>
    public void SetCustomVisualScale(float widthMul, float lengthMul)
    {
        if (customVisual == null) return;
        customVisual.transform.localScale = new Vector3(
            Mathf.Max(0.01f, widthMul), Mathf.Max(0.01f, lengthMul), 1f);
    }

    /// <summary>
    /// 自定义视觉沿攻击方向的附加位移（v0.6.3 枪矛戳击活塞动画）：
    /// 实际位置 = 基准外移 + extraLocalX（负值 = 后拉）。只影响视觉。
    /// </summary>
    public void SetCustomVisualThrustOffset(float extraLocalX)
    {
        if (customVisual == null) return;
        customVisual.transform.localPosition = new Vector3(customVisualBaseLocalX + extraLocalX, 0f, 0f);
    }

    /// <summary>
    /// 清除自定义手持视觉，恢复默认武器色块显示（卸下武器时用）。
    /// </summary>
    public void ClearCustomVisual()
    {
        if (customVisual != null)
        {
            Destroy(customVisual);
            customVisual = null;
        }

        if (weaponSpriteRenderer != null)
            weaponSpriteRenderer.enabled = true;
    }

    private void ApplyAimRotation()
    {
        if (weaponPivot == null) return;
        weaponPivot.localRotation = Quaternion.Euler(0f, 0f, aimAngle);
    }
}
