using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 敌人攻击管理（v0.5.4.1 多招系统）。
/// 负责完整攻击状态机（Windup / Active / Recovery）、武器动画、范围预警、命中判定。
/// 支持多份 AttackData 配置（attackDataSet[]），按策略选择不同招式。
/// 默认配单份 AttackData 时行为与旧版本完全一致。
/// 所有攻击数值来自 AttackData。
/// </summary>
public class EnemyCombat : MonoBehaviour
{
    /// <summary>招式选择策略。</summary>
    public enum AttackSelectionMode
    {
        /// <summary>完全随机（从所有有效招式中随机选）。</summary>
        Random,
        /// <summary>按目标距离选择（AttackData.distanceRange 区间匹配）。</summary>
        Distance,
        /// <summary>按数组顺序轮换。</summary>
        Sequence,
        /// <summary>按权重随机（AttackData.weight）。</summary>
        Weighted
    }

    [Header("攻击配置（v0.5.4.1 多招系统）")]
    [Tooltip("多招数组。非空时按 selectionMode 选择招式；为空时回退使用 attackData 单招。")]
    [SerializeField] private AttackData[] attackDataSet;

    [Tooltip("招式选择策略。Random=完全随机 / Distance=按距离匹配 / Sequence=轮换 / Weighted=权重随机。")]
    [SerializeField] private AttackSelectionMode selectionMode = AttackSelectionMode.Random;

    [Tooltip("兼容旧版单招：attackDataSet 为空时使用此字段。")]
    [SerializeField] private AttackData attackData;

    [Header("组件引用")]
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private WeaponAnimator weaponAnimator;
    [SerializeField] private AttackIndicator attackIndicator;
    [SerializeField] private WeaponHitbox weaponHitbox;

    /// <summary>外部引用：EnemyController（用于冲锋位移/读取移动速度）。</summary>
    private EnemyController controller;

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

    /// <summary>本轮攻击选中的招式数据。所有阶段方法读此字段而非 attackData。</summary>
    private AttackData currentAttackData;

    /// <summary>Sequence 策略的轮换索引。</summary>
    private int sequenceIndex;

    /// <summary>招式选择随机源（由房间子 seed 派生，外部注入）。</summary>
    private System.Random combatRng;

    /// <summary>v0.5.4.2 冲锋方向缓存。</summary>
    private Vector2 currentChargeDirection;

    void Awake()
    {
        if (attackDataSet == null || attackDataSet.Length == 0)
        {
            // 回退：单招模式，行为与旧版完全一致
            if (attackData == null)
                Debug.LogWarning("[EnemyCombat] 未配置 AttackData。", this);
        }

        controller = GetComponent<EnemyController>();

        if (weaponController == null)
            weaponController = GetComponent<WeaponController>();
        if (weaponAnimator == null)
            weaponAnimator = GetComponentInChildren<WeaponAnimator>(true);
        if (attackIndicator == null)
            attackIndicator = GetComponentInChildren<AttackIndicator>(true);
        if (weaponHitbox == null)
            weaponHitbox = GetComponent<WeaponHitbox>();

        // 默认随机源：基于 Transform 位置哈希的保底种子。
        // 实际运行时 EnemySpawner 会在生成后立即通过 SetCombatRng() 注入房间子 seed，
        // 此默认值仅在场景中直接摆放敌人（非地牢生成）时作为兜底。
        combatRng = new System.Random(transform.position.GetHashCode());
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
    /// 注入战斗专用随机源（由房间子 seed 派生，确保同 seed 招式可复现）。
    /// EnemySpawner 生成敌人后、或 Room 激活时调用。
    /// </summary>
    public void SetCombatRng(System.Random rng)
    {
        if (rng != null) combatRng = rng;
    }

    /// <summary>
    /// 检查目标是否在攻击范围内（取所有有效招式中最远的那份范围，供 EnemyAI 决策使用）。
    /// </summary>
    public bool IsInAttackRange(Transform target)
    {
        if (target == null) return false;

        // 多招模式：取最远范围（最长的攻击能打到就算"在范围内"）
        var attacks = GetEffectiveAttacks();
        if (attacks != null && attacks.Length > 0)
        {
            float maxRange = 0f;
            foreach (AttackData a in attacks)
                if (a != null && a.AttackRange > maxRange)
                    maxRange = a.AttackRange;
            if (maxRange > 0f)
                return Vector2.Distance(transform.position, target.position) <= maxRange;
        }

        // 回退：单招
        if (attackData != null)
            return Vector2.Distance(transform.position, target.position) <= attackData.AttackRange;

        return false;
    }

    /// <summary>
    /// 是否能攻击（冷却已好且未在攻击流程中）。
    /// </summary>
    public bool CanAttack => canAttack && currentState == AttackState.None;

    /// <summary>
    /// 获取生效的招式数组。多招数组非空则返回之；否则返回单招的退路。
    /// </summary>
    private AttackData[] GetEffectiveAttacks()
    {
        if (attackDataSet != null && attackDataSet.Length > 0)
            return attackDataSet;
        return null;
    }

    /// <summary>
    /// 按 selectionMode 从有效招式中选一份。无效招式（null）会被过滤。
    /// </summary>
    private AttackData PickAttack(Transform target)
    {
        if (target == null) return null;

        var attacks = GetEffectiveAttacks();
        if (attacks == null || attacks.Length == 0)
            return attackData;  // 回退单招

        // 过滤有效招式
        var valid = new List<AttackData>();
        foreach (AttackData a in attacks)
            if (a != null) valid.Add(a);

        if (valid.Count == 0) return null;
        if (valid.Count == 1) return valid[0];

        float distToTarget = Vector2.Distance(transform.position, target.position);

        switch (selectionMode)
        {
            case AttackSelectionMode.Random:
                return valid[combatRng.Next(valid.Count)];

            case AttackSelectionMode.Distance:
                // 过滤出距离匹配的招式
                var matched = new List<AttackData>();
                foreach (AttackData a in valid)
                    if (a.IsInDistanceRange(distToTarget))
                        matched.Add(a);
                if (matched.Count == 0) matched = valid; // 无匹配则全部候选
                return matched[combatRng.Next(matched.Count)];

            case AttackSelectionMode.Sequence:
                sequenceIndex = (sequenceIndex + 1) % valid.Count;
                return valid[sequenceIndex];

            case AttackSelectionMode.Weighted:
            default:
                int total = 0;
                foreach (AttackData a in valid) total += Mathf.Max(0, a.Weight);
                if (total <= 0) return valid[combatRng.Next(valid.Count)];
                int roll = combatRng.Next(total);
                foreach (AttackData a in valid)
                {
                    roll -= Mathf.Max(0, a.Weight);
                    if (roll < 0) return a;
                }
                return valid[valid.Count - 1]; // 保底
        }
    }

    /// <summary>
    /// 尝试开始攻击。由 EnemyAI 在满足条件时调用。
    /// 多招模式按 selectionMode 选一招；单招模式行为与旧版完全一致。
    /// </summary>
    public bool TryStartAttack(Transform target)
    {
        if (target == null) return false;
        if (!CanAttack) return false;

        // 选招
        AttackData picked = PickAttack(target);
        if (picked == null)
        {
            if (attackData == null) return false;
            picked = attackData;
        }

        // === 额外距离校验 ===
        // IsInAttackRange 取所有招式中最远范围，但具体招式可能有更短的距离限制。
        // 这里做二次校验：如果选中的招式有 minDistance 限制且目标太近，静默拒绝本回合攻击。
        float distToTarget = Vector2.Distance(transform.position, target.position);
        if (picked.MinDistance > 0f && distToTarget < picked.MinDistance)
        {
            // 目标比此招式要求的最小距离还近，等下次 AI tick 再选别的招或让目标拉开距离
            return false;
        }
        if (picked.MaxDistance > 0f && distToTarget > picked.MaxDistance)
        {
            return false;
        }

        currentAttackData = picked;
        currentTarget = target;
        EnterWindup();
        return true;
    }

    private void EnterWindup()
    {
        currentState = AttackState.Windup;
        windupTimer = currentAttackData.WindupTime;
        activeMomentTriggered = false;

        // 攻击方向使用 Enemy 当前朝向，确保动画与判定都基于同一方向。
        attackDirection = transform.right;

        // 同步 AttackData 到 WeaponController 和 WeaponHitbox，确保武器视觉/伤害匹配当前招式。
        if (weaponController != null)
        {
            weaponController.SetAttackData(currentAttackData);
            // Enemy 的 transform 已经由 EnemyAI/EnemyController 旋转朝向目标，
            // WeaponPivot 作为子物体保持 identity 即可自然跟随，
            // 不需要再把世界角度写入 localRotation，否则会导致双重旋转。
            weaponController.SetAimDirection(attackDirection, applyRotation: false);
            weaponController.LockAttackDirection();
        }

        if (weaponHitbox != null)
            weaponHitbox.SetAttackData(currentAttackData);

        if (attackIndicator != null)
        {
            attackIndicator.SetRadius(currentAttackData.AttackRange);
            attackIndicator.SetAngle(currentAttackData.AttackAngle);
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
        activeTimer = currentAttackData.ActiveDuration;
        activeMomentTriggered = false;

        // === v0.5.4.2：特殊攻击类型处理 ===

        if (currentAttackData.IsProjectile)
        {
            // 投射物攻击：在 Active 开始时发射一发投射物
            FireProjectile();
        }

        if (currentAttackData.IsSummon)
        {
            // 召唤攻击：在 Active 开始时生成小兵
            SummonMinions();
        }

        if (currentAttackData.IsCharge)
        {
            // 冲锋攻击：开始冲锋位移
            currentChargeDirection = attackDirection;
        }

        // --- 普通近战流程 ---
        if (!currentAttackData.IsProjectile && !currentAttackData.IsSummon)
        {
            // Active 开始，武器矩形检测同步启动（投射物/召唤不需要）
            weaponHitbox?.BeginSwing();
        }

        // Active 阶段将指示器切换为危险色
        if (attackIndicator != null)
            attackIndicator.SetColor(attackIndicator.DangerColor);

        if (weaponAnimator != null && weaponController != null)
        {
            weaponAnimator.Play(
                weaponController.GetAttackStartAngle(),
                weaponController.GetAttackEndAngle(),
                currentAttackData.ActiveDuration,
                currentAttackData.AttackEase,
                weaponController.GetAttackRotateMode(),
                currentAttackData.ActiveMomentRatio,
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

        // === v0.5.4.2：冲锋位移 ===
        if (currentAttackData.IsCharge && currentChargeDirection.sqrMagnitude > 0.001f)
        {
            float speed = controller != null
                ? controller.GetStats().MoveSpeed * currentAttackData.ChargeSpeedMultiplier
                : 3f * currentAttackData.ChargeSpeedMultiplier;
            Vector2 moveDelta = currentChargeDirection * speed * Time.deltaTime;
            transform.Translate(moveDelta, Space.World);
        }

        if (activeTimer <= 0f)
        {
            EnterRecovery();
            return;
        }

        // Active 期间每帧执行一次武器矩形检测
        // 投射物/召唤不需要近战检测
        if (!currentAttackData.IsProjectile && !currentAttackData.IsSummon)
            weaponHitbox?.Tick();
    }

    /// <summary>
    /// v0.4.6 起伤害由 WeaponHitbox 全程检测结算，此处仅负责隐藏指示器。
    /// </summary>
    private void OnActiveMoment()
    {
        if (activeMomentTriggered) return;
        activeMomentTriggered = true;

        attackIndicator?.Hide();
    }

    private void EnterRecovery()
    {
        currentState = AttackState.Recovery;
        recoveryTimer = currentAttackData.RecoveryTime;

        // Active 结束即停挥，关闭武器检测（不能等到 Recovery 之后）
        weaponHitbox?.EndSwing();
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

        AttackData endedAttack = currentAttackData;
        currentAttackData = null;
        currentChargeDirection = Vector2.zero;
        canAttack = false;
        cooldownTimer = endedAttack.AttackCooldown;
    }

    /// <summary>
    /// 死亡/失活时中断攻击流程：关闭武器检测并隐藏攻击预警。
    /// 预警显示时会脱离父物体挂到场景根部，若不在此清理，Enemy 被销毁后指示器会成为孤儿常驻显示。
    /// </summary>
    void OnDisable()
    {
        if (currentState == AttackState.None) return;

        currentState = AttackState.None;
        currentAttackData = null;
        weaponHitbox?.EndSwing();
        attackIndicator?.Hide();
    }

    /// <summary>
    /// 外部切换攻击配置（用于手动设置单招，如训练假人等特殊场景）。
    /// </summary>
    public void SetAttackData(AttackData data)
    {
        attackData = data;
    }

    // ========== v0.5.4.2：特殊攻击类型 ==========

    /// <summary>
    /// 发射一枚投射物（由 EnterActive 在 isProjectile=true 时调用）。
    /// </summary>
    private void FireProjectile()
    {
        if (currentAttackData.ProjectilePrefab == null)
        {
            Debug.LogWarning($"[EnemyCombat] isProjectile=true 但 projectilePrefab 为空，{name} 无法发射。", this);
            return;
        }

        Vector2 spawnPos = (Vector2)transform.position + attackDirection * 0.8f;
        GameObject projGo = Object.Instantiate(currentAttackData.ProjectilePrefab, spawnPos, Quaternion.identity);
        Projectile projectile = projGo.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.Launch(
                attackDirection,
                currentAttackData.AttackDamage,
                currentAttackData.ProjectileSpeed,
                currentAttackData.TargetLayer,
                transform
            );
        }
    }

    /// <summary>
    /// 召唤小兵（由 EnterActive 在 isSummon=true 时调用）。
    /// </summary>
    private void SummonMinions()
    {
        if (currentAttackData.SummonPrefab == null)
        {
            Debug.LogWarning($"[EnemyCombat] isSummon=true 但 summonPrefab 为空，{name} 无法召唤。", this);
            return;
        }

        for (int i = 0; i < currentAttackData.SummonCount; i++)
        {
            float angle = (float)combatRng.NextDouble() * 360f;
            float radius = (float)combatRng.NextDouble() * currentAttackData.SummonRadius;
            Vector2 offset = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;
            Vector2 spawnPos = (Vector2)transform.position + offset;

            Object.Instantiate(currentAttackData.SummonPrefab, spawnPos, Quaternion.identity, transform.parent);
        }
    }
}
