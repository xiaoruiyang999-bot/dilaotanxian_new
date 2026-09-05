using UnityEngine;
using System.Collections.Generic;

public enum AttackSelectionMode
{
    Random,
    Distance,
    Sequence,
    Weighted
}

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

    [Header("多招选择（数组为空时保持单招路径）")]
    [SerializeField] private AttackData[] attackDataSet;
    [SerializeField] private AttackSelectionMode selectionMode = AttackSelectionMode.Random;

    [Tooltip("进入攻击触发范围的额外缓冲距离（v0.6.0）：触发判定 = AttackData.AttackRange + 该缓冲，避免敌人在范围边缘继续贴近才出手")]
    [SerializeField] private float attackRangeBuffer = 0.3f;

    [Header("远程（v1.0.11 自 MCP 分支还原）")]
    [Tooltip("非空 = 远程攻击：预警照常走 AttackData（同源），Active 开始时向目标发射投射物，不进近战挥砍链")]
    [SerializeField] private ProjectileData projectileData;

    [Header("组件引用")]
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private WeaponAnimator weaponAnimator;
    [SerializeField] private AttackIndicator attackIndicator;
    [SerializeField] private WeaponHitbox weaponHitbox;

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

    // v1.0.13 特殊攻击（自 MCP 分支还原）：冲锋位移与召唤
    private EnemyController controller;
    private EnemyPerception perception;
    private EnemyBehaviorConfig behaviorConfig;
    private bool isCharging;
    private Vector2 currentChargeDirection;
    private System.Random combatRng;
    private float lineOfSightLostTimer;

    /// <summary>是否在攻击流程中（EnemyAI 状态机用；v1.0.13 还原）。</summary>
    public bool IsAttacking => currentState != AttackState.None;

    /// <summary>v0.8(M3)·v1.0.13 还原：攻击冷却全局乘数（Boss P2 攻速加成用；1=默认）。</summary>
    public float CooldownScale { get; set; } = 1f;

    private int sequenceIndex;
    private readonly List<AttackData> attackCandidates = new List<AttackData>(8);
    private readonly List<AttackData> distanceCandidates = new List<AttackData>(8);

    public AttackSelectionMode SelectionMode => selectionMode;
    public int AttackPoolCount => attackDataSet != null ? attackDataSet.Length : 0;

    /// <summary>注入该敌人的独立战斗随机源，保证同一地牢 seed 下招式与召唤落点可复现。</summary>
    public void SetCombatRng(System.Random rng)
    {
        if (rng != null) combatRng = rng;
    }

    /// <summary>v0.8(M3)·v1.0.13 还原：运行时替换招式池（Boss 阶段切换用）。
    /// 最小实现：每次攻击从池随机选一张写入 attackData（后续流程零改动）。</summary>
    public void SetAttackPool(AttackData[] pool) => attackDataSet = pool;

    /// <summary>
    /// 远程弹道 LOS（v1.0.13 自 MCP 分支还原）：EnemyAI 远程行为在开火前问询；
    /// 无感知组件时保守返回 false（分支约定）。近战返回 true（不适用）。
    /// </summary>
    public bool HasProjectileLineOfSight(Transform target, bool forceRefresh = false)
    {
        if (!IsRanged) return true;
        if (perception == null) return false;
        return perception.HasLineOfSight(target, attackData != null ? attackData.TargetLayer : 0,
            attackData != null ? attackData.ObstacleLayer : 0, forceRefresh);
    }

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
        if (weaponHitbox == null)
            weaponHitbox = GetComponent<WeaponHitbox>();

        // v1.0.13：特殊攻击依赖（冲锋位移/LOS）
        controller = GetComponent<EnemyController>();
        perception = GetComponent<EnemyPerception>();
        EnemyAI enemyAI = GetComponent<EnemyAI>();
        behaviorConfig = enemyAI != null ? enemyAI.behaviorConfig : null;
        combatRng = new System.Random(transform.position.GetHashCode() ^ 0xC0FFEE);
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

    /// <summary>当前 AttackData 的攻击范围（供 EnemyAI 等外部决策读取）。</summary>
    public float CurrentAttackRange => attackData != null ? attackData.AttackRange : 0f;

    /// <summary>当前基础攻击配置，只读公开给接线验证与 Boss 阶段系统。</summary>
    public AttackData CurrentAttackData => attackData;

    /// <summary>是否远程攻击（v1.0.11：projectileData 已配置）。</summary>
    public bool IsRanged => projectileData != null;

    /// <summary>
    /// 检查目标是否在攻击范围内（距离判定，供 EnemyAI 决策使用）。
    /// v0.6.0：判定距离 = 当前 AttackData.AttackRange + attackRangeBuffer，
    /// 保证精英/Boss 等大范围攻击在玩家进入打击圈后即触发，不再继续贴近。
    /// </summary>
    public bool IsInAttackRange(Transform target)
    {
        if (target == null) return false;

        float maxRange = attackData != null ? attackData.AttackRange : 0f;
        if (attackDataSet != null)
            for (int i = 0; i < attackDataSet.Length; i++)
                if (attackDataSet[i] != null)
                    maxRange = Mathf.Max(maxRange, attackDataSet[i].AttackRange);

        return maxRange > 0f
            && Vector2.Distance(transform.position, target.position) <= maxRange + attackRangeBuffer;
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
        AttackData picked = PickAttack(target);
        if (picked == null) return false;

        currentTarget = target;
        attackData = picked;
        EnterWindup();
        return true;
    }

    private AttackData PickAttack(Transform target)
    {
        if (attackDataSet == null || attackDataSet.Length == 0)
            return attackData;

        attackCandidates.Clear();
        for (int i = 0; i < attackDataSet.Length; i++)
            if (attackDataSet[i] != null) attackCandidates.Add(attackDataSet[i]);

        if (attackCandidates.Count == 0) return attackData;
        if (attackCandidates.Count == 1) return attackCandidates[0];

        switch (selectionMode)
        {
            case AttackSelectionMode.Distance:
                distanceCandidates.Clear();
                float distance = Vector2.Distance(transform.position, target.position);
                for (int i = 0; i < attackCandidates.Count; i++)
                    if (attackCandidates[i].IsInDistanceRange(distance))
                        distanceCandidates.Add(attackCandidates[i]);
                return distanceCandidates.Count > 0
                    ? distanceCandidates[combatRng.Next(distanceCandidates.Count)]
                    : attackCandidates[combatRng.Next(attackCandidates.Count)];

            case AttackSelectionMode.Sequence:
                int index = sequenceIndex % attackCandidates.Count;
                sequenceIndex = (sequenceIndex + 1) % attackCandidates.Count;
                return attackCandidates[index];

            case AttackSelectionMode.Weighted:
                int totalWeight = 0;
                for (int i = 0; i < attackCandidates.Count; i++)
                    totalWeight += Mathf.Max(0, attackCandidates[i].Weight);
                if (totalWeight <= 0)
                    return attackCandidates[combatRng.Next(attackCandidates.Count)];
                int roll = combatRng.Next(totalWeight);
                for (int i = 0; i < attackCandidates.Count; i++)
                {
                    roll -= Mathf.Max(0, attackCandidates[i].Weight);
                    if (roll < 0) return attackCandidates[i];
                }
                return attackCandidates[attackCandidates.Count - 1];

            case AttackSelectionMode.Random:
            default:
                return attackCandidates[combatRng.Next(attackCandidates.Count)];
        }
    }

    private void EnterWindup()
    {
        currentState = AttackState.Windup;
        windupTimer = attackData.WindupTime;
        activeMomentTriggered = false;
        lineOfSightLostTimer = 0f;

        // 攻击方向：远程朝目标（发射+预警同向）；近战用 Enemy 当前朝向（动画与判定同源）。
        attackDirection = transform.right;
        if (IsRanged && currentTarget != null)
        {
            Vector2 to = (Vector2)currentTarget.position - (Vector2)transform.position;
            if (to.sqrMagnitude > 0.0001f) attackDirection = to.normalized;
        }

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
        if (IsRanged)
        {
            bool visible = currentTarget != null && perception != null
                && perception.HasLineOfSight(currentTarget, attackData.TargetLayer,
                    attackData.ObstacleLayer);
            lineOfSightLostTimer = visible ? 0f : lineOfSightLostTimer + Time.deltaTime;

            float grace = behaviorConfig != null ? behaviorConfig.lineOfSightGraceTime : 0.15f;
            if (lineOfSightLostTimer > grace)
            {
                CancelAttackWithCooldown(0.5f);
                return;
            }

            // Windup 期间预警持续跟手；发射沿最后一次同源方向，不在 Active 再做第二套 LOS 判定。
            if (currentTarget != null)
            {
                Vector2 toTarget = (Vector2)currentTarget.position - (Vector2)transform.position;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    attackDirection = toTarget.normalized;
                    if (attackIndicator != null)
                    {
                        attackIndicator.SetDirection(attackDirection);
                        attackIndicator.SetRadius(toTarget.magnitude);
                    }
                }
            }
        }

        windupTimer -= Time.deltaTime;
        if (windupTimer <= 0f)
            EnterActive();
    }

    /// <summary>中断尚未生效的攻击并进入部分冷却，避免 LOS 边缘反复蓄力抖动。</summary>
    private void CancelAttackWithCooldown(float cooldownScale)
    {
        currentState = AttackState.None;
        isCharging = false;
        currentChargeDirection = Vector2.zero;
        weaponHitbox?.EndSwing();
        attackIndicator?.Hide();
        weaponAnimator?.Stop();
        weaponController?.ResetAimToForward();
        controller?.StopMoving();

        canAttack = false;
        cooldownTimer = attackData != null
            ? attackData.AttackCooldown * Mathf.Clamp01(cooldownScale)
            : 0f;
    }

    private void EnterActive()
    {
        currentState = AttackState.Active;
        activeTimer = attackData.ActiveDuration;
        activeMomentTriggered = false;

        // v1.0.11 远程：Active 开始即发射（预警=发射同向同时刻，无二次 LOS 复查——MCP 分支
        // a7f358d 的修复结论：门控不一致正是当年远程 AI 打不出弹的根因）；
        // 不 BeginSwing/不播挥砍动画，UpdateActive 的 Tick 对非摆动状态为 no-op。
        if (IsRanged)
        {
            Vector2 origin = (Vector2)transform.position + attackDirection * 0.6f;
            Projectile.Launch(projectileData, origin, attackDirection, gameObject);
            OnActiveMoment();
            return;
        }

        // v1.0.13 召唤攻击：Active 开始生成小兵（不入近战链；落点实体碰撞校验防嵌墙卡清房）
        if (attackData != null && attackData.IsSummon)
        {
            attackIndicator?.Hide();
            SummonMinions();
            OnActiveMoment();
            return;
        }

        // v1.0.13 冲锋攻击：锁定方向高速位移（近战判定照常——冲锋本身就是撞人结算）
        if (attackData != null && attackData.IsCharge)
        {
            currentChargeDirection = attackDirection;
            isCharging = true;
        }

        // Active 开始，武器矩形检测同步启动
        weaponHitbox?.BeginSwing();

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

        // v1.0.13 冲锋位移（自 MCP 分支还原）：Active 期间沿锁定方向高速冲，
        // 撞墙（ChargerCollisionLayer|TargetLayer）即停
        if (isCharging && currentChargeDirection.sqrMagnitude > 0.001f)
        {
            float speed = controller != null
                ? controller.GetStats().MoveSpeed * attackData.ChargeSpeedMultiplier
                : 3f * attackData.ChargeSpeedMultiplier;
            Vector2 chargeVelocity = currentChargeDirection * speed;
            if (controller == null)
            {
                isCharging = false;
                return;
            }
            controller.SetChargeVelocity(chargeVelocity);

            float checkDist = chargeVelocity.magnitude * Time.deltaTime + 0.1f;
            int stopLayer = attackData.ChargerCollisionLayer | attackData.TargetLayer;
            RaycastHit2D hit = Physics2D.Raycast(transform.position, currentChargeDirection, checkDist, stopLayer);
            // v1.1.37：射线可能命中本帧已销毁对象（清理竞态），hit.collider 对 fake-null 判空即拦
            if (hit.collider != null && hit.transform != null && !hit.transform.IsChildOf(transform))
            {
                currentChargeDirection = Vector2.zero;
                isCharging = false;
                controller.StopMoving();
            }
        }

        if (activeTimer <= 0f)
        {
            EnterRecovery();
            return;
        }

        // Active 期间每帧执行一次武器矩形检测，检测窗口严格等于 Active 阶段
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
        recoveryTimer = attackData.RecoveryTime;

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

        canAttack = false;
        cooldownTimer = attackData.AttackCooldown * CooldownScale;
    }

    /// <summary>
    /// 死亡/失活时中断攻击流程：关闭武器检测并隐藏攻击预警。
    /// 预警显示时会脱离父物体挂到场景根部，若不在此清理，Enemy 被销毁后指示器会成为孤儿常驻显示。
    /// </summary>
    void OnDisable()
    {
        if (currentState == AttackState.None) return;

        currentState = AttackState.None;
        weaponHitbox?.EndSwing();
        attackIndicator?.Hide();
    }

    /// <summary>
    /// 外部切换攻击配置（用于 v0.5 技能/武器切换）。
    /// </summary>
    public void SetAttackData(AttackData data)
    {
        attackData = data;
    }

    // ========== v1.0.13 召唤（自 MCP 分支还原） ==========

    private void SummonMinions()
    {
        if (attackData.SummonPrefab == null)
        {
            Debug.LogWarning($"[EnemyCombat] isSummon=true 但 summonPrefab 为空，{name} 无法召唤。", this);
            return;
        }

        EnemyAI ai = GetComponent<EnemyAI>();
        Room ownerRoom = GetComponentInParent<Room>();

        for (int i = 0; i < attackData.SummonCount; i++)
        {
            // 每只生成前检查上限，避免一次召唤多只超上限
            if (ai != null && !ai.CanSummonMore())
                break;

            // v0.5.4.4.4 教训：落点必须通过实体碰撞校验——召唤师贴墙时纯随机点会把小兵
            // 嵌进墙里，而小兵已计入清房计数，会导致房间永不清空、房门永锁。
            if (!TryGetMinionSpawnPosition(attackData.SummonRadius, out Vector2 spawnPos))
                continue;

            GameObject minion = Instantiate(attackData.SummonPrefab, spawnPos, Quaternion.identity, transform.parent);
            minion.name = $"{attackData.SummonPrefab.name}_{name}_{i}";
            // v1.1.3 召唤物不掉金币（防无限刷币）
            EnemyController minionCtrl = minion.GetComponent<EnemyController>();
            if (minionCtrl != null) minionCtrl.DropCoins = false;
            EnemyHealth mHealth = minion.GetComponent<EnemyHealth>();
            if (mHealth != null)
            {
                if (ai != null) ai.RegisterMinion(mHealth);
                if (ownerRoom != null) ownerRoom.RegisterEnemy(mHealth);
            }
        }
    }

    // 与 SpawnPositionHelper 一致的占位判定：Default(墙)/Enemy/Obstacle 实体层，
    // useTriggers=false 自动跳过 RoomTrigger 与探测圈；缓冲复用避免 GC。
    private static readonly Collider2D[] minionOverlapBuffer = new Collider2D[1];
    private const int MinionPlacementAttempts = 8;
    private const float MinionOverlapRadius = 0.4f;

    /// <summary>在召唤师周围找不嵌墙的落点；最多重摇 8 次，失败返回 false。</summary>
    private bool TryGetMinionSpawnPosition(float summonRadius, out Vector2 spawnPos)
    {
        var filter = new ContactFilter2D
        {
            layerMask = LayerMask.GetMask("Default", "Enemy", "Obstacle"),
            useLayerMask = true,
            useTriggers = false
        };

        for (int attempt = 0; attempt < MinionPlacementAttempts; attempt++)
        {
            float angle = (float)combatRng.NextDouble() * 360f;
            float radius = (float)combatRng.NextDouble() * summonRadius;
            Vector2 offset = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;
            Vector2 candidate = (Vector2)transform.position + offset;

            if (Physics2D.OverlapCircle(candidate, MinionOverlapRadius, filter, minionOverlapBuffer) == 0)
            {
                spawnPos = candidate;
                return true;
            }
        }

        spawnPos = default;
        return false;
    }
}
