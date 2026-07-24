using UnityEngine;

/// <summary>
/// 玩家攻击管理。
/// 职责：维护 Windup / Active / Recovery 阶段；调用 WeaponAnimator 播放动画；
/// Active 阶段驱动 WeaponHitbox 做武器矩形命中检测。
/// 不控制 WeaponPivot，不读取鼠标输入，不管理武器视觉。
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("攻击配置")]
    [SerializeField] private AttackData attackData;

    [Header("组件引用")]
    [SerializeField] private PlayerAimController aimController;
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private WeaponAnimator weaponAnimator;
    [SerializeField] private AttackIndicator attackIndicator;
    [SerializeField] private WeaponHitbox weaponHitbox;

    public System.Action OnAttackStart;
    public System.Action OnAttackEnd;

    private enum SubPhase { None, Windup, Active, Recovery }
    private SubPhase subPhase = SubPhase.None;
    private float windupTimer;
    private float activeTimer;
    private float recoveryTimer;
    private bool activeMomentTriggered;
    private Vector2 attackDirection;

    void Awake()
    {
        if (attackData == null)
            Debug.LogWarning("[PlayerCombat] 未配置 AttackData，攻击无法执行。");

        if (aimController == null)
            aimController = GetComponent<PlayerAimController>();

        if (weaponController == null)
            weaponController = GetComponent<WeaponController>();

        if (weaponAnimator == null)
            weaponAnimator = GetComponentInChildren<WeaponAnimator>(true);

        if (attackIndicator == null)
            attackIndicator = GetComponentInChildren<AttackIndicator>(true);

        if (weaponHitbox == null)
            weaponHitbox = GetComponent<WeaponHitbox>();
    }

    void Update()
    {
        UpdateAiming();
        UpdateAttackState();
    }

    /// <summary>
    /// 普通状态下持续更新武器朝向。
    /// 攻击期间方向已由 WeaponController 锁定，不再更新。
    /// </summary>
    private void UpdateAiming()
    {
        if (subPhase != SubPhase.None) return;
        if (aimController == null || weaponController == null) return;

        weaponController.SetAimDirection(aimController.AimDirection);
    }

    /// <summary>
    /// 尝试发起一次攻击。若已在攻击流程中或冷却未好则忽略。
    /// </summary>
    public void TryAttack()
    {
        if (attackData == null) return;
        if (subPhase != SubPhase.None) return;

        StartWindup();
    }

    /// <summary>
    /// 是否正在攻击流程中。
    /// </summary>
    public bool IsAttacking => subPhase != SubPhase.None;

    private void StartWindup()
    {
        subPhase = SubPhase.Windup;
        windupTimer = attackData.WindupTime;
        activeMomentTriggered = false;

        // 锁定当前攻击方向，由 WeaponController 负责管理 WeaponPivot
        weaponController?.LockAttackDirection();

        // 缓存攻击方向，防止攻击期间鼠标移动导致判定方向与动画方向不一致
        attackDirection = weaponController != null
            ? weaponController.GetAimDirection()
            : aimController != null ? aimController.AimDirection : Vector2.right;

        OnAttackStart?.Invoke();

        if (attackIndicator != null)
        {
            attackIndicator.SetRadius(attackData.AttackRange);
            attackIndicator.SetAngle(attackData.AttackAngle);
            attackIndicator.SetDirection(attackDirection);
            attackIndicator.SetColor(attackIndicator.WarningColor);
            attackIndicator.Show();
        }
    }

    private void UpdateAttackState()
    {
        switch (subPhase)
        {
            case SubPhase.Windup:
                UpdateWindup();
                break;
            case SubPhase.Active:
                UpdateActive();
                break;
            case SubPhase.Recovery:
                UpdateRecovery();
                break;
        }
    }

    private void UpdateWindup()
    {
        windupTimer -= Time.deltaTime;
        if (windupTimer <= 0f)
            EnterActive();
    }

    private void EnterActive()
    {
        subPhase = SubPhase.Active;
        activeTimer = attackData.ActiveDuration;
        activeMomentTriggered = false;

        // Active 开始，武器矩形检测同步启动
        weaponHitbox?.BeginSwing();

        if (weaponAnimator != null && weaponController != null)
        {
            weaponAnimator.Play(
                weaponController.GetAttackStartAngle(),
                weaponController.GetAttackEndAngle(),
                attackData.ActiveDuration,
                attackData.AttackEase,
                weaponController.GetAttackRotateMode(),
                attackData.ActiveMomentRatio,
                OnActiveMoment
            );
        }
        else
        {
            OnActiveMoment();
        }
    }

    private void UpdateActive()
    {
        activeTimer -= Time.deltaTime;
        if (activeTimer <= 0f)
        {
            EnterRecovery();
            return;
        }

        // Active 期间每帧执行一次武器矩形检测，检测窗口严格等于 Active 阶段
        weaponHitbox?.Tick();
    }

    /// <summary>
    /// 命中时刻回调。由 WeaponAnimator 在动画配置比例点触发。
    /// v0.4.6 起伤害由 WeaponHitbox 全程检测结算，此处仅负责隐藏指示器。
    /// </summary>
    private void OnActiveMoment()
    {
        if (activeMomentTriggered) return;
        activeMomentTriggered = true;

        if (attackIndicator != null)
            attackIndicator.Hide();
    }

    private void EnterRecovery()
    {
        subPhase = SubPhase.Recovery;
        recoveryTimer = attackData.RecoveryTime;

        // Active 结束即停挥，关闭武器检测（不能等到 Recovery 之后）
        weaponHitbox?.EndSwing();
    }

    private void UpdateRecovery()
    {
        recoveryTimer -= Time.deltaTime;
        if (recoveryTimer <= 0f)
        {
            subPhase = SubPhase.None;
            weaponAnimator?.Stop();
            weaponController?.UnlockAttackDirection();
            OnAttackEnd?.Invoke();
        }
    }

    /// <summary>
    /// 外部切换攻击配置（用于 v0.5 技能/武器切换）。
    /// </summary>
    public void SetAttackData(AttackData data)
    {
        attackData = data;
    }
}
