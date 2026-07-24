using UnityEngine;
using System.Collections;

/// <summary>
/// 敌人AI状态机核心（v0.5.4.2 多行为系统）。
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

    [Header("攻击配置（兼容旧版单招）")]
    [SerializeField] private AttackData attackData;

    [Header("行为配置（v0.5.4.2）")]
    [Tooltip("怪物战斗行为配置。为空时默认 Melee 行为。")]
    public EnemyBehaviorConfig behaviorConfig;

    // 组件引用
    private EnemyController controller;
    private EnemyStats stats;
    private EnemyCombat combat;
    private EnemyHealth health;

    // 目标与状态
    private Transform target;
    private Vector3 patrolOrigin;
    private State currentState;
    private float patrolWaitTimer = 0f;
    private Vector3 currentPatrolTarget;

    // === v0.5.4.2 行为专用字段 ===
    private bool isSkirmishRetreating;
    private float skirmishRetreatTimer;
    private Vector2 skirmishRetreatDirection;
    private int aliveMinions; // 召唤师追踪存活小兵数

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

        patrolOrigin = transform.position;
        currentPatrolTarget = patrolOrigin;
        currentState = State.Patrol;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) target = player.transform;

        if (health != null)
            health.OnTakeDamage += OnDamaged;
    }

    void OnDestroy()
    {
        if (health != null)
            health.OnTakeDamage -= OnDamaged;
    }

    void Update()
    {
        if (target == null) return;
        if (health != null && health.IsDead) return;

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
        Vector2 dir = (target.position - transform.position).normalized;
        controller.MoveTowards(dir);
        controller.FaceTowards(dir);
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

        Vector2 dirToTarget = (target.position - transform.position).normalized;
        controller.FaceTowards(dirToTarget);

        if (distToTarget < minDist)
        {
            // 太近：后退
            controller.MoveTowards(-dirToTarget * retreatMul);
        }
        else if (distToTarget > maxDist)
        {
            // 太远：靠近
            controller.MoveTowards(dirToTarget);
        }
        else
        {
            // 在理想距离区间内，停住射击
            controller.StopMoving();

            if (combat.CanAttack)
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
            // 进入攻击范围：突进→攻击→标记后退
            controller.MoveTowards(dirToTarget);
            if (combat.TryStartAttack(target))
            {
                ChangeState(State.Attack);
                // 攻击完成后触发后退（在 Attack 状态退出时标记）
                MarkSkirmishRetreat();
            }
        }
        else
        {
            // 不在攻击范围：靠近
            controller.MoveTowards(dirToTarget);
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
            // 不在范围：直线追击
            controller.MoveTowards(dirToTarget);
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
            // 保持距离，召唤
            controller.StopMoving();
            if (combat.CanAttack)
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

        if (combat.CanAttack)
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
                break;
            case State.Chase:
                break;
            case State.Attack:
                controller.StopMoving();
                break;
            case State.ReturnToPatrol:
                combat.SetTarget(null);
                isSkirmishRetreating = false;
                break;
        }
    }
}
