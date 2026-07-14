using UnityEngine;

/// <summary>
/// 敌人AI状态机核心。管理四个状态：Patrol/Chase/Attack/ReturnToPatrol。
/// 被攻击时（OnTakeDamage）强制切换到Chase（发现玩家）。
/// Attack 状态内部拆分为：Windup（前摇）→ Execute（执行判定）→ Recovery（后摇）。
/// </summary>
public class EnemyAI : MonoBehaviour
{
    public enum State { Patrol, Chase, Attack, ReturnToPatrol }

    [Header("AI配置")]
    [SerializeField] private float patrolRadius = 2f;       // 巡逻半径
    [SerializeField] private float patrolWaitTime = 2f;     // 巡逻点间等待时间

    [Header("攻击手感")]
    [SerializeField] private float windupTime = 0.4f;       // 攻击前摇
    [SerializeField] private float recoveryTime = 0.35f;    // 攻击后摇
    [SerializeField] private float dangerThreshold = 0.1f;  // 前摇最后 X 秒变红

    // 组件引用
    private EnemyController controller;
    private EnemyStats stats;
    private EnemyCombat combat;
    private EnemyHealth health;
    private AttackIndicator indicator;

    // 目标与状态
    private Transform target;           // 玩家
    private Vector3 patrolOrigin;       // 巡逻原点
    private State currentState;
    private float patrolWaitTimer = 0f;
    private Vector3 currentPatrolTarget;

    // Attack 状态内部子阶段
    private enum AttackSubPhase { Windup, Recovery }
    private AttackSubPhase attackSubPhase;
    private float attackTimer;

    void Awake()
    {
        controller = GetComponent<EnemyController>();
        stats = GetComponent<EnemyStats>();
        combat = GetComponent<EnemyCombat>();
        health = GetComponent<EnemyHealth>();
        indicator = GetComponentInChildren<AttackIndicator>(true);

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

    void OnValidate()
    {
        windupTime = Mathf.Max(0f, windupTime);
        recoveryTime = Mathf.Max(0f, recoveryTime);
        dangerThreshold = Mathf.Clamp(dangerThreshold, 0f, windupTime);
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
        controller.StopMoving();

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
            controller.MoveTowards(dir);
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
        controller.MoveTowards(dir);
        controller.FaceTowards(dir);

        // 进入攻击范围且冷却完毕才开始攻击流程
        if (combat.IsInAttackRange(target) && combat.CanAttack)
        {
            ChangeState(State.Attack);
        }
        // 玩家跑太远，丢失
        else if (distToTarget > stats.LosePlayerRange)
        {
            ChangeState(State.ReturnToPatrol);
        }
    }

    // ========== Attack（攻击） ==========
    private void UpdateAttack(float distToTarget)
    {
        // 攻击期间保持面向玩家
        Vector2 dir = (target.position - transform.position).normalized;
        controller.FaceTowards(dir);

        if (attackSubPhase == AttackSubPhase.Windup)
        {
            attackTimer -= Time.deltaTime;

            // 预警颜色：最后 dangerThreshold 秒变红
            if (indicator != null)
            {
                if (attackTimer <= dangerThreshold)
                    indicator.SetColor(indicator.DangerColor);
                else
                    indicator.SetColor(indicator.WarningColor);
            }

            if (attackTimer <= 0f)
            {
                // 前摇结束，再次检测范围后执行一次攻击判定
                if (combat.IsInAttackRange(target))
                {
                    if (target.TryGetComponent<IDamageable>(out var damageable))
                    {
                        combat.TryAttack(damageable);
                    }
                }

                if (indicator != null)
                    indicator.Hide();

                attackSubPhase = AttackSubPhase.Recovery;
                attackTimer = recoveryTime;
            }
        }
        else if (attackSubPhase == AttackSubPhase.Recovery)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                ChangeState(State.Chase);
            }
        }
    }

    // ========== ReturnToPatrol（返回巡逻点） ==========
    private void UpdateReturnToPatrol(float distToOrigin)
    {
        Vector2 dir = (patrolOrigin - transform.position).normalized;
        controller.MoveTowards(dir);
        controller.FaceTowards(dir);

        // 回到原点
        if (distToOrigin <= 0.3f)
        {
            controller.StopMoving();
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
                // 退出攻击时确保指示器隐藏、子阶段重置
                if (indicator != null) indicator.Hide();
                attackSubPhase = AttackSubPhase.Windup;
                break;
            case State.ReturnToPatrol:
                break;
        }

        currentState = newState;

        // 进入新状态
        switch (newState)
        {
            case State.Patrol:
                currentPatrolTarget = patrolOrigin;
                patrolWaitTimer = 0f;
                break;
            case State.Chase:
                break;
            case State.Attack:
                attackSubPhase = AttackSubPhase.Windup;
                attackTimer = windupTime;
                controller.StopMoving(); // 攻击期间只停止一次移动
                if (indicator != null)
                {
                    indicator.SetRadius(stats.AttackRange);
                    indicator.Show();
                    indicator.SetColor(indicator.WarningColor);
                }
                break;
            case State.ReturnToPatrol:
                break;
        }
    }

    public State CurrentState => currentState;
}
