using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 敌人攻击管理。
/// 负责完整攻击状态机（Windup / Active / Recovery）、武器动画、范围预警、命中判定。
/// 所有攻击数值来自 AttackData。
/// v0.5 技能系统：替换 AttackData asset 即可改变攻击表现，无需修改本类。
/// </summary>
public class EnemyCombat : MonoBehaviour
{
    [Header("攻击配置")]
    [SerializeField] private AttackData attackData;

    [Header("组件引用")]
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private WeaponAnimator weaponAnimator;
    [SerializeField] private AttackIndicator attackIndicator;
    [SerializeField] private AttackQuery attackQuery;

    private enum AttackState { None, Windup, Active, Recovery }
    private AttackState currentState = AttackState.None;

    private float windupTimer;
    private float activeTimer;
    private float recoveryTimer;
    private bool activeMomentTriggered;

    private float cooldownTimer = 0f;
    private bool canAttack = true;

    private Transform currentTarget;
    private Vector2 attackDirection;

    void Awake()
    {
        if (attackData == null)
            Debug.LogWarning("[EnemyCombat] 未配置 AttackData。", this);

        if (weaponController == null)
            weaponController = GetComponent<WeaponController>();
        if (weaponAnimator == null)
            weaponAnimator = GetComponentInChildren<WeaponAnimator>(true);
        if (attackIndicator == null)
            attackIndicator = GetComponentInChildren<AttackIndicator>(true);
        if (attackQuery == null)
            attackQuery = GetComponent<AttackQuery>();
    }

    void Update()
    {
        UpdateCooldown();
        UpdateAiming();
        UpdateAttackState();
    }

    private void UpdateCooldown()
    {
        if (!canAttack)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f)
                canAttack = true;
        }
    }

    /// <summary>
    /// 普通状态下持续朝向目标。
    /// 武器方向由 Enemy 自身 transform 控制，不再通过 WeaponController 实时瞄准，避免追击时武器漂移。
    /// </summary>
    private void UpdateAiming()
    {
        if (currentState != AttackState.None) return;
        if (currentTarget == null) return;

        // Enemy 武器方向跟随自身 transform，不再每帧调用 weaponController.SetAimDirection。
        // 攻击开始时会在 EnterWindup 中根据当前 transform.right 锁定方向。
    }

    private void UpdateAttackState()
    {
        switch (currentState)
        {
            case AttackState.Windup:
                UpdateWindup();
                break;
            case AttackState.Active:
                UpdateActive();
                break;
            case AttackState.Recovery:
                UpdateRecovery();
                break;
        }
    }

    /// <summary>
    /// 设置当前目标。EnemyAI 在 Chase 状态每帧调用。
    /// </summary>
    public void SetTarget(Transform target)
    {
        currentTarget = target;
    }

    /// <summary>
    /// 检查目标是否在攻击范围内（距离判定，供 EnemyAI 决策使用）。
    /// </summary>
    public bool IsInAttackRange(Transform target)
    {
        if (target == null || attackData == null) return false;
        return Vector2.Distance(transform.position, target.position) <= attackData.AttackRange;
    }

    /// <summary>
    /// 是否能攻击（冷却已好且未在攻击流程中）。
    /// </summary>
    public bool CanAttack => canAttack && currentState == AttackState.None;

    /// <summary>
    /// 尝试开始攻击。由 EnemyAI 在满足条件时调用。
    /// </summary>
    public bool TryStartAttack(Transform target)
    {
        if (target == null) return false;
        if (!CanAttack) return false;
        if (attackData == null) return false;

        currentTarget = target;
        EnterWindup();
        return true;
    }

    private void EnterWindup()
    {
        currentState = AttackState.Windup;
        windupTimer = attackData.WindupTime;
        activeMomentTriggered = false;

        // 攻击方向使用 Enemy 当前朝向，确保动画与判定都基于同一方向。
        attackDirection = transform.right;

        if (weaponController != null)
        {
            // Enemy 的 transform 已经由 EnemyAI/EnemyController 旋转朝向目标，
            // WeaponPivot 作为子物体保持 identity 即可自然跟随，
            // 不需要再把世界角度写入 localRotation，否则会导致双重旋转。
            weaponController.SetAimDirection(attackDirection, applyRotation: false);
            weaponController.LockAttackDirection();
        }

        if (attackIndicator != null && attackData != null)
        {
            attackIndicator.SetRadius(attackData.AttackRange);
            attackIndicator.SetAngle(attackData.AttackAngle);
            attackIndicator.SetDirection(attackDirection);
            attackIndicator.SetColor(attackIndicator.WarningColor);
            attackIndicator.Show();
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
        currentState = AttackState.Active;
        activeTimer = attackData.ActiveDuration;
        activeMomentTriggered = false;

        // Active 阶段将指示器切换为危险色
        if (attackIndicator != null)
            attackIndicator.SetColor(attackIndicator.DangerColor);

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
            EnterRecovery();
    }

    private void OnActiveMoment()
    {
        if (activeMomentTriggered) return;
        activeMomentTriggered = true;

        attackIndicator?.Hide();

        if (attackQuery != null)
        {
            attackQuery.ExecuteAttack(transform.position, attackDirection, out _);
        }
        else
        {
            Debug.LogWarning("[EnemyCombat] attackQuery is null, cannot perform attack.", this);
        }
    }

    private void EnterRecovery()
    {
        currentState = AttackState.Recovery;
        recoveryTimer = attackData.RecoveryTime;
    }

    private void UpdateRecovery()
    {
        recoveryTimer -= Time.deltaTime;
        if (recoveryTimer <= 0f)
        {
            EndAttack();
        }
    }

    private void EndAttack()
    {
        currentState = AttackState.None;
        weaponAnimator?.Stop();

        // 攻击结束后重置武器朝向，使其跟随 Enemy 自身 transform 旋转。
        weaponController?.ResetAimToForward();

        canAttack = false;
        cooldownTimer = attackData.AttackCooldown;
    }

    /// <summary>
    /// 外部切换攻击配置（用于 v0.5 技能/武器切换）。
    /// </summary>
    public void SetAttackData(AttackData data)
    {
        attackData = data;
    }
}
