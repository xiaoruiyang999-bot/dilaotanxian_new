using UnityEngine;

/// <summary>
/// 敌人AI状态机核心。管理四个状态：Patrol/Chase/Attack/ReturnToPatrol。
/// 被攻击时（OnTakeDamage）强制切换到Chase（发现玩家）。
/// Attack 状态内部拆分为：Windup（前摇）→ Active（攻击释放）→ Recovery（后摇）。
/// 攻击阶段由 AttackData 驱动，武器动画由 WeaponAnimator 播放。
/// </summary>
public class EnemyAI : MonoBehaviour
{
    public enum State { Patrol, Chase, Attack, ReturnToPatrol }
    private enum AttackSubPhase { Windup, Active, Recovery }

    [Header("AI配置")]
    [SerializeField] private float patrolRadius = 2f;       // 巡逻半径
    [SerializeField] private float patrolWaitTime = 2f;     // 巡逻点间等待时间

    [Header("攻击配置")]
    [SerializeField] private AttackData attackData;         // 攻击阶段与动画配置
    [SerializeField] private float dangerThreshold = 0.1f;  // 前摇最后 X 秒指示器变红

    // 组件引用
    private EnemyController controller;
    private EnemyStats stats;
    private EnemyCombat combat;
    private EnemyHealth health;
    private AttackIndicator indicator;
    private WeaponAnimator weaponAnimator;

    // 目标与状态
    private Transform target;           // 玩家
    private Vector3 patrolOrigin;       // 巡逻原点
    private State currentState;
    private float patrolWaitTimer = 0f;
    private Vector3 currentPatrolTarget;

    // Attack 状态内部子阶段与计时器
    private AttackSubPhase attackSubPhase;
    private float windupTimer;
    private float activeTimer;
    private float recoveryTimer;
    private bool activeMomentTriggered;

    // 从 AttackData 读取阶段时长（未配置时使用默认值兜底）
    private float WindupTime     => attackData != null ? attackData.WindupTime     : 0.25f;
    private float ActiveDuration => attackData != null ? attackData.ActiveDuration : 0.25f;
    private float RecoveryTime   => attackData != null ? attackData.RecoveryTime   : 0.35f;

    void Awake()
    {
        controller = GetComponent<EnemyController>();
        stats = GetComponent<EnemyStats>();
        combat = GetComponent<EnemyCombat>();
        health = GetComponent<EnemyHealth>();
        indicator = GetComponentInChildren<AttackIndicator>(true);
        weaponAnimator = GetComponentInChildren<WeaponAnimator>(true);

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
        dangerThreshold = Mathf.Max(0f, dangerThreshold);
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

        switch (attackSubPhase)
        {
            case AttackSubPhase.Windup:
                UpdateWindup();
                break;
            case AttackSubPhase.Active:
                UpdateActive();
                break;
            case AttackSubPhase.Recovery:
                UpdateRecovery();
                break;
        }
    }

    private void UpdateWindup()
    {
        controller.StopMoving();

        windupTimer -= Time.deltaTime;

        // 预警颜色：最后 dangerThreshold 秒变红
        if (indicator != null)
        {
            if (windupTimer <= dangerThreshold)
                indicator.SetColor(indicator.DangerColor);
            else
                indicator.SetColor(indicator.WarningColor);
        }

        if (windupTimer <= 0f)
        {
            EnterActive();
        }
    }

    private void EnterActive()
    {
        attackSubPhase = AttackSubPhase.Active;
        activeTimer = ActiveDuration;
        activeMomentTriggered = false;

        // 播放武器动画，命中回调由 WeaponAnimator 在配置时间点触发
        if (weaponAnimator != null)
            weaponAnimator.PlayAttack(OnActiveMoment);
        else
            OnActiveMoment();
    }

    private void UpdateActive()
    {
        controller.StopMoving();

        activeTimer -= Time.deltaTime;
        if (activeTimer <= 0f)
        {
            EnterRecovery();
        }
    }

    /// <summary>
    /// 命中时刻回调。由 WeaponAnimator 在动画配置比例点触发。
    /// 隐藏指示器并执行一次伤害判定。
    /// </summary>
    private void OnActiveMoment()
    {
        if (activeMomentTriggered) return;
        activeMomentTriggered = true;

        // 真正挥击瞬间隐藏攻击范围指示器
        if (indicator != null)
            indicator.Hide();

        // 仍由 EnemyCombat 做范围判定与伤害
        if (combat.IsInAttackRange(target))
        {
            if (target.TryGetComponent<IDamageable>(out var damageable))
            {
                combat.TryAttack(damageable);
            }
        }
    }

    private void EnterRecovery()
    {
        attackSubPhase = AttackSubPhase.Recovery;
        recoveryTimer = RecoveryTime;
    }

    private void UpdateRecovery()
    {
        controller.StopMoving();

        recoveryTimer -= Time.deltaTime;
        if (recoveryTimer <= 0f)
        {
            ChangeState(State.Chase);
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
                // 退出攻击时确保指示器隐藏、动画停止、子阶段重置
                if (indicator != null) indicator.Hide();
                if (weaponAnimator != null) weaponAnimator.Stop();
                activeMomentTriggered = false;
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
                windupTimer = WindupTime;
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
