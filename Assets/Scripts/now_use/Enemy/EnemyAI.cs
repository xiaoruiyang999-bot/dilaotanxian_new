using UnityEngine;

/// <summary>
/// 敌人AI状态机核心。管理高层状态：Patrol/Chase/Attack/ReturnToPatrol。
/// 被攻击时（OnTakeDamage）强制切换到Chase（发现玩家）。
/// 攻击流程完全交给 EnemyCombat，本类只负责决策。
/// </summary>
public class EnemyAI : MonoBehaviour
{
    public enum State { Patrol, Chase, Attack, ReturnToPatrol }

    [Header("AI配置")]
    [SerializeField] private float patrolRadius = 2f;       // 巡逻半径
    [SerializeField] private float patrolWaitTime = 2f;     // 巡逻点间等待时间

    // 组件引用
    private EnemyController controller;
    private EnemyStats stats;
    private EnemyCombat combat;
    private EnemyHealth health;

    // 目标与状态
    private Transform target;           // 玩家
    private Vector3 patrolOrigin;       // 巡逻原点
    private State currentState;
    private float patrolWaitTimer = 0f;
    private Vector3 currentPatrolTarget;

    // 移动意图（v0.6.0）：Update 只做决策并记录意图，FixedUpdate 统一写入速度，
    // 避免在渲染帧直接写 rb.linearVelocity 与物理步不同步导致移动"一闪一闪"
    private Vector2 moveIntent;
    private bool hasMoveIntent;

    void Awake()
    {
        controller = GetComponent<EnemyController>();
        stats = GetComponent<EnemyStats>();
        combat = GetComponent<EnemyCombat>();
        health = GetComponent<EnemyHealth>();

        patrolOrigin = transform.position;
        currentPatrolTarget = patrolOrigin;
        currentState = State.Patrol;

        // 查找玩家
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) target = player.transform;

        // 监听被攻击事件：被打了立刻发现玩家
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
    }

    // ========== 移动意图（FixedUpdate 统一写入） ==========

    private void SetMoveIntent(Vector2 direction)
    {
        moveIntent = direction;
        hasMoveIntent = true;
    }

    private void SetStopIntent()
    {
        moveIntent = Vector2.zero;
        hasMoveIntent = false;
    }

    void FixedUpdate()
    {
        if (controller == null) return;

        // 意图每物理帧消费一次：Update 没有重新设置意图时默认停止
        if (hasMoveIntent)
            controller.MoveTowards(moveIntent);
        else
            controller.StopMoving();

        moveIntent = Vector2.zero;
        hasMoveIntent = false;
    }

    // ========== 被攻击回调 ==========
    private void OnDamaged()
    {
        // 被攻击时，如果不在Chase/Attack状态，切换到Chase（发现攻击者）
        if (currentState == State.Patrol || currentState == State.ReturnToPatrol)
        {
            ChangeState(State.Chase);
        }
    }

    // ========== Patrol（巡逻） ==========
    private void UpdatePatrol(float distToTarget)
    {
        SetStopIntent();

        // 等待计时
        patrolWaitTimer -= Time.deltaTime;
        if (patrolWaitTimer <= 0f)
        {
            // 选新巡逻点
            currentPatrolTarget = PatrolSystem.GetRandomPatrolPoint(
                patrolOrigin, patrolRadius);
            patrolWaitTimer = patrolWaitTime;
        }

        // 向巡逻点移动（简单版：直接用MoveTowards）
        Vector2 dir = (currentPatrolTarget - transform.position).normalized;
        if (Vector2.Distance(transform.position, currentPatrolTarget) > 0.2f)
        {
            SetMoveIntent(dir);
            controller.FaceTowards(dir);
        }

        // 检测玩家
        if (distToTarget <= stats.DetectionRange)
        {
            ChangeState(State.Chase);
        }
    }

    // ========== Chase（追击） ==========
    private void UpdateChase(float distToTarget)
    {
        Vector2 dir = (target.position - transform.position).normalized;
        SetMoveIntent(dir);
        controller.FaceTowards(dir);

        // 传递目标给 EnemyCombat，用于普通状态武器朝向
        combat.SetTarget(target);

        // 进入攻击范围且冷却完毕才开始攻击流程
        if (combat.IsInAttackRange(target) && combat.CanAttack)
        {
            if (combat.TryStartAttack(target))
            {
                ChangeState(State.Attack);
            }
        }
        // 玩家跑太远，丢失
        else if (distToTarget > stats.LosePlayerRange)
        {
            combat.SetTarget(null);
            ChangeState(State.ReturnToPatrol);
        }
    }

    // ========== Attack（攻击） ==========
    private void UpdateAttack(float distToTarget)
    {
        // 攻击期间保持静止，避免扇形指示器与判定方向因位移/旋转而错位。
        SetStopIntent();

        // 攻击流程由 EnemyCombat 内部管理。
        if (combat.CanAttack)
        {
            ChangeState(State.Chase);
        }
    }

    // ========== ReturnToPatrol（返回巡逻点） ==========
    private void UpdateReturnToPatrol(float distToOrigin)
    {
        Vector2 dir = (patrolOrigin - transform.position).normalized;
        SetMoveIntent(dir);
        controller.FaceTowards(dir);

        // 回到原点
        if (distToOrigin <= 0.3f)
        {
            SetStopIntent();
            ChangeState(State.Patrol);
        }

        // 返回途中又发现玩家
        float distToTarget = Vector2.Distance(transform.position, target.position);
        if (distToTarget <= stats.DetectionRange)
        {
            ChangeState(State.Chase);
        }
    }

    // ========== 状态切换 ==========
    private void ChangeState(State newState)
    {
        if (currentState == newState) return;

        // 退出旧状态
        switch (currentState)
        {
            case State.Patrol:
                patrolWaitTimer = 0f;
                break;
            case State.Chase:
                break;
            case State.Attack:
                // 攻击流程由 EnemyCombat 管理，这里只处理 AI 层切换
                break;
            case State.ReturnToPatrol:
                break;
        }

        currentState = newState;

        // 进入新状态
        switch (newState)
        {
            case State.Patrol:
                combat.SetTarget(null);
                currentPatrolTarget = patrolOrigin;
                patrolWaitTimer = 0f;
                break;
            case State.Chase:
                break;
            case State.Attack:
                SetStopIntent();
                break;
            case State.ReturnToPatrol:
                combat.SetTarget(null);
                break;
        }
    }

    public State CurrentState => currentState;
}
