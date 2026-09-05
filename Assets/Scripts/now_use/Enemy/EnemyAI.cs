using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 敌人AI状态机核心（v0.5.4.2 多行为系统，v1.0.13 自 MCP 分支整体还原）。
/// 五种战斗行为：Melee/Ranged(风筝+横移)/Skirmisher(游击)/Charger(冲锋)/Summoner(召唤+仆从管理)。
/// 管理高层状态：Patrol/Chase/Attack/ReturnToPatrol。
/// 支持5种战斗行为：Melee/Ranged/Skirmisher/Charger/Summoner。
/// 攻击流程完全交给 EnemyCombat，本类只负责决策与移动。
/// </summary>
public class EnemyAI : MonoBehaviour
{
    public enum State { Patrol, Chase, Attack, ReturnToPatrol }

    [Header("AI配置")]
    [SerializeField] private float patrolRadius = 2f;
    [SerializeField] private float patrolWaitTime = 2f;

    [Header("行为配置（v0.5.4.2）")]
    [Tooltip("怪物战斗行为配置。为空时默认 Melee 行为。")]
    public EnemyBehaviorConfig behaviorConfig;

    // 组件引用
    private EnemyController controller;
    private EnemyStats stats;
    private EnemyCombat combat;
    private EnemyHealth health;
    private EnemyPerception perception;

    // 目标与状态
    private Transform target;
    private Vector3 patrolOrigin;
    private State currentState;
    private float patrolWaitTimer = 0f;
    private Vector3 currentPatrolTarget;

    // === v1.1.32 视觉子状态（障碍挡视野 + 50% 不丢失）与寻路跟随 ===
    private bool losBlocked;            // 当前 LOS 被墙/障碍阻挡
    private bool ignoringLos;           // 丢失时掷骰 50%"不丢失"——继续追（重见目标后复位）
    private Vector3 lastSeenPosition;   // 丢失前最后目击点
    private float lostSearchTimer;      // 丢失方向目击点搜索的剩余时长
    private const float LostSearchDuration = 3f;
    private readonly List<Vector2> path = new List<Vector2>(32);   // 复用航点缓冲
    private int pathIndex;
    private float repathTimer;
    private Vector2 pathGoal;
    private int visionMask = -1;

    // === v0.5.4.2 行为专用字段 ===
    private bool isSkirmishRetreating;
    private float skirmishRetreatTimer;
    private Vector2 skirmishRetreatDirection;
    private int aliveMinions; // 召唤师追踪存活小兵数
    private readonly List<EnemyHealth> summonedMinions = new List<EnemyHealth>(); // 登记的小兵，OnDestroy 解绑用
    private enum RangedMoveMode { Hold, Approach, Retreat, Strafe }
    private RangedMoveMode rangedMoveMode;
    private Vector2 rangedStrafeDirection;
    private float rangedRepositionTimer;
    private System.Random behaviorRng;

    // === 召唤上限检查（v0.5.4.2）===
    /// <summary>还能召唤更多小兵吗？</summary>
    public bool CanSummonMore()
    {
        if (behaviorConfig == null) return true;
        return aliveMinions < behaviorConfig.maxMinionsAlive;
    }

    public void SetBehaviorRng(System.Random rng)
    {
        if (rng != null) behaviorRng = rng;
    }

    /// <summary>登记一只小兵，钩住其死亡事件以追踪存活数。</summary>
    public void RegisterMinion(EnemyHealth minionHealth)
    {
        if (minionHealth == null || summonedMinions.Contains(minionHealth)) return;
        minionHealth.OnDeath += OnMinionDied;
        summonedMinions.Add(minionHealth);
        aliveMinions = summonedMinions.Count;
    }

    private void OnMinionDied()
    {
        for (int i = summonedMinions.Count - 1; i >= 0; i--)
        {
            EnemyHealth minion = summonedMinions[i];
            if (minion == null || minion.IsDead)
            {
                if (minion != null) minion.OnDeath -= OnMinionDied;
                summonedMinions.RemoveAt(i);
            }
        }
        aliveMinions = summonedMinions.Count;
    }

    // === 属性 ===
    public State CurrentState => currentState;
    private EnemyBehaviorType Behavior
    {
        get
        {
            if (behaviorConfig != null) return behaviorConfig.behaviorType;
            return EnemyBehaviorType.Melee;
        }
    }

    void Awake()
    {
        controller = GetComponent<EnemyController>();
        stats = GetComponent<EnemyStats>();
        combat = GetComponent<EnemyCombat>();
        health = GetComponent<EnemyHealth>();
        perception = GetComponent<EnemyPerception>();
        if (perception != null && behaviorConfig != null)
            perception.Configure(behaviorConfig.lineOfSightCheckInterval);

        patrolOrigin = transform.position;
        currentPatrolTarget = patrolOrigin;
        currentState = State.Patrol;
        behaviorRng = new System.Random(transform.position.GetHashCode() ^ 0x51A7);

        TryAcquireTarget();

        if (health != null)
            health.OnTakeDamage += OnDamaged;
    }

    void OnDestroy()
    {
        if (health != null)
            health.OnTakeDamage -= OnDamaged;

        // 解绑所有登记小兵的死亡回调，避免召唤师先死时小兵死亡触发已销毁对象
        foreach (var m in summonedMinions)
        {
            if (m != null)
                m.OnDeath -= OnMinionDied;
        }
        summonedMinions.Clear();
    }

    void OnEnable()
    {
        TryAcquireTarget();
    }

    void Update()
    {
        if (this == null || gameObject == null) return;   // v1.1.37：楼层清理 Destroy 后同帧残余 Update 守卫
        if (target == null) TryAcquireTarget();
        if (health == null || health.IsDead) return;      // health fake-null（同房清理）一并拦截

        // Without target, fall back to patrol-only movement
        if (target == null)
        {
            if (currentState == State.Chase || currentState == State.Attack)
                ChangeState(State.ReturnToPatrol);
            if (currentState == State.Patrol || currentState == State.ReturnToPatrol)
                UpdatePatrolOnly();
            return;
        }

        float distToTarget = Vector2.Distance(transform.position, target.position);
        float distToOrigin = Vector2.Distance(transform.position, patrolOrigin);

        switch (currentState)
        {
            case State.Patrol:
                UpdatePatrol(distToTarget);
                break;
            case State.Chase:
                UpdateChase(distToTarget);
                break;
            case State.Attack:
                UpdateAttack(distToTarget);
                break;
            case State.ReturnToPatrol:
                UpdateReturnToPatrol(distToOrigin);
                break;
        }

        // 非攻击状态下面朝目标（远程/召唤师等需要持续面朝玩家）
        if (currentState != State.Attack && behaviorConfig != null && behaviorConfig.faceTargetWhileIdle)
        {
            Vector2 dir = (target.position - transform.position).normalized;
            if (dir.sqrMagnitude > 0.001f)
                controller.FaceTowards(dir);
        }
    }

    // ========== v1.1.32 视觉与寻路 ==========

    /// <summary>
    /// 追击视野判定：墙/障碍（Default 墙 Tilemap + Obstacle 层）阻挡即"看不见"。
    /// 刚丢失时一次性掷骰：50% 忽略丢失继续追（ignoringLos，直至重见目标复位）；
    /// 50% 进入目击点搜索（LostSearchDuration 秒内走向 lastSeenPosition，超时/到达即弃目标）。
    /// </summary>
    private void UpdateVision()
    {
        if (perception == null || target == null || this == null) return;   // this==null：销毁残余帧
        if (visionMask < 0) visionMask = LayerMask.GetMask("Default", "Obstacle");

        bool visible = perception.HasLineOfSight(target, visionMask, 0);
        if (visible)
        {
            losBlocked = false;
            ignoringLos = false;
            lastSeenPosition = target.position;
            lostSearchTimer = LostSearchDuration;
            return;
        }

        if (!losBlocked)   // 刚丢失：一次性 50% 掷骰
        {
            losBlocked = true;
            ignoringLos = behaviorRng != null && behaviorRng.Next(2) == 0;
        }
        if (!ignoringLos) lostSearchTimer -= Time.deltaTime;
    }

    /// <summary>当前有效追击目标点：看得见/忽略丢失→玩家实时位置；丢失搜索期→最后目击点。</summary>
    private Vector2 ChaseGoal => (losBlocked && !ignoringLos) ? (Vector2)lastSeenPosition : (Vector2)target.position;

    /// <summary>丢失搜索是否已到头（超时或已抵达目击点）。</summary>
    private bool LostSearchExhausted =>
        losBlocked && !ignoringLos &&
        (lostSearchTimer <= 0f || Vector2.Distance(transform.position, lastSeenPosition) < 0.6f);

    /// <summary>
    /// v1.1.32 寻路追击移动：直线体宽通畅→直接走（原行为）；被挡→A* 求最短绕行路径
    /// （0.35s 节流重算，目标点漂移 >1.5 强制重算），沿航点推进——不再贴墙滑行。
    /// </summary>
    private void MoveWithPathing(Vector2 goal)
    {
        Vector2 pos = transform.position;
        Vector2 delta = goal - pos;
        float dist = delta.magnitude;
        if (dist < 0.05f) { controller.StopMoving(); return; }
        Vector2 dir = delta / dist;

        if (EnemyPathfinder.BodyLineClear(pos, goal, transform))
        {
            controller.MoveTowards(dir);
            controller.FaceTowards(dir);
            repathTimer = 0f;
            path.Clear();
            return;
        }

        repathTimer -= Time.deltaTime;
        bool goalMoved = (goal - pathGoal).sqrMagnitude > 1.5f * 1.5f;
        if (repathTimer <= 0f || path.Count == 0 || goalMoved || pathIndex >= path.Count)
        {
            if (EnemyPathfinder.FindPath(pos, goal, transform, path))
            {
                pathGoal = goal;
                pathIndex = 1;   // 0 号是自身起点附近
                repathTimer = 0.35f;
            }
            else
            {
                controller.MoveTowards(dir);   // 寻路失败回退直线（体面降级）
                controller.FaceTowards(dir);
                repathTimer = 0.2f;
                return;
            }
        }

        // 沿航点推进（v1.1.35 防御钳制：索引越界即整路径作废重算，绝不向上抛）
        if (pathIndex < 0 || pathIndex >= path.Count)
        {
            pathIndex = 0;
            path.Clear();
            repathTimer = 0f;
            controller.MoveTowards(dir);
            controller.FaceTowards(dir);
            return;
        }
        while (pathIndex < path.Count && Vector2.Distance(pos, path[pathIndex]) < 0.35f) pathIndex++;
        if (pathIndex >= path.Count) { controller.MoveTowards(dir); controller.FaceTowards(dir); return; }
        Vector2 wp = path[pathIndex];
        Vector2 wdir = (wp - pos).normalized;
        controller.MoveTowards(wdir);
        controller.FaceTowards(wdir);
    }

    /// <summary>追击公共前置：视觉判定 + 丢失弃目标（各行为 UpdateChase_* 开头调用）。</summary>
    private void ChasePrelude(float distToTarget)
    {
        UpdateVision();
        if (LostSearchExhausted)
        {
            combat.SetTarget(null);
            losBlocked = false;
            ignoringLos = false;
            ChangeState(State.ReturnToPatrol);
        }
    }

    // ========== 被攻击回调 ==========
    private void OnDamaged()
    {
        if (currentState == State.Patrol || currentState == State.ReturnToPatrol)
            ChangeState(State.Chase);
    }

    // ========== Patrol ==========
    private void UpdatePatrol(float distToTarget)
    {
        controller.StopMoving();
        patrolWaitTimer -= Time.deltaTime;
        if (patrolWaitTimer <= 0f)
        {
            currentPatrolTarget = PatrolSystem.GetRandomPatrolPoint(patrolOrigin, patrolRadius);
            patrolWaitTimer = patrolWaitTime;
        }

        Vector2 dir = (currentPatrolTarget - transform.position).normalized;
        if (Vector2.Distance(transform.position, currentPatrolTarget) > 0.2f)
        {
            controller.MoveTowards(dir);
            controller.FaceTowards(dir);
        }

        if (distToTarget <= stats.DetectionRange)
            ChangeState(State.Chase);
    }

    // ========== Chase ==========
    private void UpdateChase(float distToTarget)
    {
        ChasePrelude(distToTarget);   // v1.1.32 视觉判定 + 丢失弃目标（状态可能已切走）
        if (currentState != State.Chase) return;

        switch (Behavior)
        {
            case EnemyBehaviorType.Ranged:
                UpdateChase_Ranged(distToTarget);
                break;
            case EnemyBehaviorType.Skirmisher:
                UpdateChase_Skirmisher(distToTarget);
                break;
            case EnemyBehaviorType.Charger:
                UpdateChase_Charger(distToTarget);
                break;
            case EnemyBehaviorType.Summoner:
                UpdateChase_Summoner(distToTarget);
                break;
            case EnemyBehaviorType.Melee:
            default:
                UpdateChase_Melee(distToTarget);
                break;
        }
    }

    // --- Melee 近战追击 ---
    private void UpdateChase_Melee(float distToTarget)
    {
        MoveWithPathing(ChaseGoal);   // v1.1.32：挡路时 A* 绕行，不贴墙滑
        combat.SetTarget(target);

        if (combat.IsInAttackRange(target) && combat.CanAttack)
        {
            if (combat.TryStartAttack(target))
                ChangeState(State.Attack);
        }
        else if (distToTarget > stats.LosePlayerRange)
        {
            combat.SetTarget(null);
            ChangeState(State.ReturnToPatrol);
        }
    }

    // --- Ranged 远程保持距离 ---
    private void UpdateChase_Ranged(float distToTarget)
    {
        combat.SetTarget(target);

        float minDist = behaviorConfig != null ? behaviorConfig.preferredDistance.x : 4f;
        float maxDist = behaviorConfig != null ? behaviorConfig.preferredDistance.y : 7f;
        float retreatMul = behaviorConfig != null ? behaviorConfig.retreatSpeedMultiplier : 1f;
        float retreatExit = behaviorConfig != null ? behaviorConfig.retreatExitDistance : minDist + 0.8f;
        float approachExit = behaviorConfig != null ? behaviorConfig.approachExitDistance : maxDist - 0.8f;

        Vector2 dirToTarget = (target.position - transform.position).normalized;
        controller.FaceTowards(dirToTarget);

        if (rangedMoveMode == RangedMoveMode.Retreat && distToTarget >= retreatExit)
            rangedMoveMode = RangedMoveMode.Hold;
        else if (rangedMoveMode == RangedMoveMode.Approach && distToTarget <= approachExit)
            rangedMoveMode = RangedMoveMode.Hold;

        if (rangedMoveMode == RangedMoveMode.Hold || rangedMoveMode == RangedMoveMode.Strafe)
        {
            if (distToTarget < minDist) rangedMoveMode = RangedMoveMode.Retreat;
            else if (distToTarget > maxDist) rangedMoveMode = RangedMoveMode.Approach;
        }

        if (rangedMoveMode == RangedMoveMode.Retreat)
        {
            Vector2 retreatDirection = -dirToTarget;
            float probeDistance = behaviorConfig != null ? behaviorConfig.movementProbeDistance : 0.75f;
            if (perception == null || perception.IsDirectionClear(retreatDirection, probeDistance))
            {
                controller.MoveTowards(retreatDirection, retreatMul);
            }
            else
            {
                UpdateRangedStrafe(dirToTarget, probeDistance);
            }
        }
        else if (rangedMoveMode == RangedMoveMode.Approach)
        {
            MoveWithPathing(target.position);   // v1.1.32 寻路绕行（保持距离型只在接近段需要绕障）
        }
        else
        {
            // 在理想距离区间内，停住射击
            controller.StopMoving();

            bool hasLineOfSight = perception == null || combat.HasProjectileLineOfSight(target);
            if (combat.CanAttack && hasLineOfSight)
            {
                if (combat.TryStartAttack(target))
                    ChangeState(State.Attack);
            }
        }

        if (distToTarget > stats.LosePlayerRange)
        {
            combat.SetTarget(null);
            ChangeState(State.ReturnToPatrol);
        }
    }

    private void UpdateRangedStrafe(Vector2 dirToTarget, float probeDistance)
    {
        rangedRepositionTimer -= Time.deltaTime;
        if (rangedRepositionTimer <= 0f || rangedStrafeDirection.sqrMagnitude <= 0.001f
            || (perception != null && !perception.IsDirectionClear(rangedStrafeDirection, probeDistance)))
        {
            Vector2 left = new Vector2(-dirToTarget.y, dirToTarget.x);
            Vector2 right = -left;
            bool preferLeft = behaviorRng.Next(2) == 0;
            Vector2 first = preferLeft ? left : right;
            Vector2 second = preferLeft ? right : left;

            if (perception == null || perception.IsDirectionClear(first, probeDistance))
                rangedStrafeDirection = first;
            else if (perception.IsDirectionClear(second, probeDistance))
                rangedStrafeDirection = second;
            else
                rangedStrafeDirection = Vector2.zero;

            rangedRepositionTimer = behaviorConfig != null ? behaviorConfig.repositionInterval : 0.25f;
        }

        if (rangedStrafeDirection.sqrMagnitude > 0.001f)
        {
            rangedMoveMode = RangedMoveMode.Strafe;
            float strafeMultiplier = behaviorConfig != null ? behaviorConfig.strafeSpeedMultiplier : 0.75f;
            controller.MoveTowards(rangedStrafeDirection, strafeMultiplier);
        }
        else
        {
            controller.StopMoving();
        }
    }

    // --- Skirmisher 游击：突进→攻击→后撤 ---
    private void UpdateChase_Skirmisher(float distToTarget)
    {
        Vector2 dirToTarget = (target.position - transform.position).normalized;
        controller.FaceTowards(dirToTarget);
        combat.SetTarget(target);

        if (isSkirmishRetreating)
        {
            // 攻击后强制后退阶段
            skirmishRetreatTimer -= Time.deltaTime;
            float retreatSpeed = behaviorConfig != null ? behaviorConfig.skirmishRetreatSpeed : 1.5f;
            controller.MoveTowards(skirmishRetreatDirection * retreatSpeed);

            if (skirmishRetreatTimer <= 0f)
            {
                isSkirmishRetreating = false;
            }
        }
        else if (combat.IsInAttackRange(target) && combat.CanAttack)
        {
            // 进入攻击范围：突进→攻击→标记后退（近距短冲，直线即可）
            controller.MoveTowards(dirToTarget);
            if (combat.TryStartAttack(target))
            {
                ChangeState(State.Attack);
            }
        }
        else
        {
            // 不在攻击范围：靠近（v1.1.32 寻路绕行）
            MoveWithPathing(ChaseGoal);
        }

        if (distToTarget > stats.LosePlayerRange)
        {
            isSkirmishRetreating = false;
            combat.SetTarget(null);
            ChangeState(State.ReturnToPatrol);
        }
    }

    private void MarkSkirmishRetreat()
    {
        isSkirmishRetreating = true;
        skirmishRetreatTimer = behaviorConfig != null ? behaviorConfig.retreatDuration : 0.5f;
        skirmishRetreatDirection = -transform.right; // 面朝玩家的反方向
    }

    // --- Charger 冲锋型：进入范围直接蓄力冲锋 ---
    private void UpdateChase_Charger(float distToTarget)
    {
        Vector2 dirToTarget = (target.position - transform.position).normalized;
        controller.FaceTowards(dirToTarget);
        combat.SetTarget(target);

        if (combat.IsInAttackRange(target) && combat.CanAttack)
        {
            // 进入范围：直接发起冲锋攻击
            controller.StopMoving();
            if (combat.TryStartAttack(target))
                ChangeState(State.Attack);
        }
        else
        {
            // 不在范围：追击（v1.1.32 寻路绕行；锁定冲锋仍是直线，由 EnemyCombat 驱动）
            MoveWithPathing(ChaseGoal);
        }

        if (distToTarget > stats.LosePlayerRange)
        {
            combat.SetTarget(null);
            ChangeState(State.ReturnToPatrol);
        }
    }

    // --- Summoner 召唤师：远离玩家、定期召唤 ---
    private void UpdateChase_Summoner(float distToTarget)
    {
        combat.SetTarget(target);

        float minDist = behaviorConfig != null ? behaviorConfig.preferredDistance.x : 5f;
        float retreatMul = behaviorConfig != null ? behaviorConfig.retreatSpeedMultiplier : 1f;

        Vector2 dirToTarget = (target.position - transform.position).normalized;
        controller.FaceTowards(dirToTarget);

        if (distToTarget < minDist)
        {
            // 远离玩家
            controller.MoveTowards(-dirToTarget * retreatMul);
        }
        else
        {
            // 保持距离，召唤（检查上限）
            controller.StopMoving();
            if (combat.CanAttack && CanSummonMore())
            {
                if (combat.TryStartAttack(target))
                    ChangeState(State.Attack);
            }
        }

        if (distToTarget > stats.LosePlayerRange * 1.5f)
        {
            combat.SetTarget(null);
            ChangeState(State.ReturnToPatrol);
        }
    }

    // ========== Attack ==========
    private void UpdateAttack(float distToTarget)
    {
        // 攻击期间停止移动（冲锋除外，冲锋位移在 EnemyCombat.UpdateActive 中处理）
        if (Behavior != EnemyBehaviorType.Charger)
            controller.StopMoving();

        if (!combat.IsAttacking)
        {
            ChangeState(State.Chase);
        }
    }

    // ========== ReturnToPatrol ==========
    private void UpdateReturnToPatrol(float distToOrigin)
    {
        isSkirmishRetreating = false;
        Vector2 dir = (patrolOrigin - transform.position).normalized;
        controller.MoveTowards(dir);
        controller.FaceTowards(dir);

        if (distToOrigin <= 0.3f)
        {
            controller.StopMoving();
            ChangeState(State.Patrol);
        }

        float distToTarget = Vector2.Distance(transform.position, target.position);
        if (distToTarget <= stats.DetectionRange)
            ChangeState(State.Chase);
    }

    // ========== 状态切换 ==========
    private void ChangeState(State newState)
    {
        if (currentState == newState) return;

        switch (currentState)
        {
            case State.Patrol:
                patrolWaitTimer = 0f;
                break;
            case State.Chase:
                break;
            case State.Attack:
                // 攻击结束切回 Chase → Skirmisher 触发一次后退
                if (Behavior == EnemyBehaviorType.Skirmisher)
                    MarkSkirmishRetreat();
                break;
            case State.ReturnToPatrol:
                break;
        }

        currentState = newState;

        switch (newState)
        {
            case State.Patrol:
                combat.SetTarget(null);
                currentPatrolTarget = patrolOrigin;
                patrolWaitTimer = 0f;
                isSkirmishRetreating = false;
                rangedMoveMode = RangedMoveMode.Hold;
                rangedStrafeDirection = Vector2.zero;
                break;
            case State.Chase:
                break;
            case State.Attack:
                controller.StopMoving();
                break;
            case State.ReturnToPatrol:
                combat.SetTarget(null);
                isSkirmishRetreating = false;
                rangedMoveMode = RangedMoveMode.Hold;
                rangedStrafeDirection = Vector2.zero;
                break;
        }
    }

    private void TryAcquireTarget()
    {
        if (target != null) return;
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) target = player.transform;
    }

    private void UpdatePatrolOnly()
    {
        patrolWaitTimer -= Time.deltaTime;
        if (patrolWaitTimer <= 0f)
        {
            currentPatrolTarget = PatrolSystem.GetRandomPatrolPoint(patrolOrigin, patrolRadius);
            patrolWaitTimer = patrolWaitTime;
        }

        Vector2 dir = (currentPatrolTarget - transform.position).normalized;
        if (Vector2.Distance(transform.position, currentPatrolTarget) > 0.2f)
        {
            controller.MoveTowards(dir);
            controller.FaceTowards(dir);
        }
        else
        {
            controller.StopMoving();
        }
    }
}
