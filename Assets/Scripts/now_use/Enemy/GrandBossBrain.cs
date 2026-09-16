using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 格兰唯一行为状态机。EnemyAI 在该组件启用期间保持关闭，所有移动与招式只从这里发起。
/// 第一阶段负责双爪、三重跃击和命中/落空分支；阶段控制器只发送临界与二阶段命令。
/// </summary>
public class GrandBossBrain : MonoBehaviour
{
    public enum BossState
    {
        Idle, Approach, BasicCombo, Retreat, TripleLeap, PhaseWarning,
        FourHitCombo, PhaseTransition, QuadrupedChase, ChargeAttack, MoonHunt,
        Stunned, Dead,
    }

    public BossState State { get; private set; } = BossState.Idle;
    public bool PhaseTwo { get; private set; }
    public bool PhaseWarningTriggered { get; private set; }

    public event Action<BossState> OnStateEntered;
    public event Action<bool> OnPhaseTwoEntered;
    public event Action<Vector2, float> OnWarningShown;
    public event Action<Vector2> OnDeathPosition;

    public Transform ArtRoot { get; private set; }

    [Header("状态机")]
    [SerializeField, Min(0.1f)] private float decisionInterval = 0.6f;
    [SerializeField, Min(0f)] private float attackRange = 2.6f;
    [SerializeField, Min(0f)] private float tripleLeapCooldown = 14f;
    [Range(0f, 1f)] [SerializeField] private float leapPickChance = 0.4f;

    [Header("阶段过渡")]
    [SerializeField, Min(0f)] private float phaseWarningDuration = 1.8f;
    [SerializeField, Min(0f)] private float phaseTransitionDuration = 0.9f;
    [SerializeField, Min(0.1f)] private float phaseTwoMoveSpeedMultiplier = 1.35f;

    [Header("组件（自动查找）")]
    [SerializeField] private EnemyAI ai;
    [SerializeField] private EnemyCombat combat;
    [SerializeField] private EnemyHealth health;
    [SerializeField] private EnemyController controller;
    [SerializeField] private GrandBasicCombo basicCombo;
    [SerializeField] private GrandTripleLeap tripleLeap;
    [SerializeField] private BossTelegraphController telegraph;

    private Transform player;
    private float decisionTimer;
    private float leapCooldownTimer;
    private BossState lastAttackState;
    private Coroutine activeRoutine;
    private bool dead;
    private bool criticalFrenzy;

    public static GrandBossBrain EnsureOn(GameObject boss)
    {
        GrandBossBrain brain = boss.GetComponent<GrandBossBrain>();
        if (brain == null) brain = boss.AddComponent<GrandBossBrain>();

        GrandBasicCombo combo = boss.GetComponent<GrandBasicCombo>();
        if (combo == null) combo = boss.AddComponent<GrandBasicCombo>();
        GrandTripleLeap leap = boss.GetComponent<GrandTripleLeap>();
        if (leap == null) leap = boss.AddComponent<GrandTripleLeap>();
        BossTelegraphController tele = boss.GetComponent<BossTelegraphController>();
        if (tele == null) tele = boss.AddComponent<BossTelegraphController>();

        combo.WireAssets(
            Resources.Load<AttackData>("Data/AttackData_GrandRightClaw"),
            Resources.Load<AttackData>("Data/AttackData_GrandLeftClaw"));
        leap.WireAssets(Resources.Load<AttackData>("Data/AttackData_GrandLeapLand"), tele);
        brain.WireModules(combo, leap, tele);
        tele.WireBrain(brain);

        BossPhaseController phase = boss.GetComponent<BossPhaseController>();
        if (phase != null) phase.BindBrain(brain);
        GrandBossArtAdapter.EnsureOn(boss, brain);

        // 显式夺取行为控制权，不依赖 AddComponent 时 Awake/OnEnable 的编辑器调用顺序。
        EnemyAI legacyAi = boss.GetComponent<EnemyAI>();
        if (legacyAi != null) legacyAi.enabled = false;
        return brain;
    }

    public void WireModules(GrandBasicCombo combo, GrandTripleLeap leap, BossTelegraphController warningController)
    {
        basicCombo = combo;
        tripleLeap = leap;
        telegraph = warningController;
    }

    public void BroadcastWarning(Vector2 pos, float radius)
        => OnWarningShown?.Invoke(pos, radius);

    private void Awake()
    {
        ArtRoot = transform.Find("ArtRoot");
        ai = GetComponent<EnemyAI>();
        combat = GetComponent<EnemyCombat>();
        health = GetComponent<EnemyHealth>();
        controller = GetComponent<EnemyController>();
        basicCombo = GetComponent<GrandBasicCombo>();
        tripleLeap = GetComponent<GrandTripleLeap>();
        telegraph = GetComponent<BossTelegraphController>();
        if (health != null) health.OnDeath += OnDead;
    }

    private void OnDestroy()
    {
        if (health != null) health.OnDeath -= OnDead;
    }

    private void OnEnable()
    {
        ResetRuntimeState();
    }

    private void OnDisable()
    {
        StopActiveRoutine();
        controller?.StopMoving();
    }

    private void ResetRuntimeState()
    {
        StopActiveRoutine();
        dead = health != null && health.IsDead;
        PhaseTwo = false;
        PhaseWarningTriggered = false;
        criticalFrenzy = false;
        State = dead ? BossState.Dead : BossState.Idle;
        lastAttackState = BossState.Idle;
        decisionTimer = decisionInterval;
        leapCooldownTimer = tripleLeapCooldown * 0.5f;
        player = null;
        if (ai != null) ai.enabled = false;
        if (combat != null) combat.enabled = !dead;
        controller?.StopMoving();
    }

    private void Update()
    {
        if (dead) return;
        if (player == null && !TryFindPlayer()) return;

        if (leapCooldownTimer > 0f) leapCooldownTimer -= Time.deltaTime;
        if (decisionTimer > 0f) decisionTimer -= Time.deltaTime;

        switch (State)
        {
            case BossState.Idle:
                controller?.StopMoving();
                if (decisionTimer <= 0f) Enter(DecideNext());
                break;
            case BossState.Approach:
                DriveApproach(1f, criticalFrenzy ? BossState.FourHitCombo : BossState.BasicCombo);
                break;
            case BossState.QuadrupedChase:
                DriveApproach(phaseTwoMoveSpeedMultiplier, BossState.ChargeAttack);
                break;
            case BossState.BasicCombo:
            case BossState.Retreat:
            case BossState.TripleLeap:
            case BossState.PhaseWarning:
            case BossState.FourHitCombo:
            case BossState.PhaseTransition:
            case BossState.ChargeAttack:
            case BossState.MoonHunt:
            case BossState.Stunned:
                break;
        }
    }

    private BossState DecideNext()
    {
        float distance = DistanceToPlayer();
        if (PhaseTwo)
            return distance <= attackRange + 1f ? BossState.ChargeAttack : BossState.QuadrupedChase;
        if (criticalFrenzy)
            return distance <= attackRange ? BossState.FourHitCombo : BossState.Approach;

        bool leapReady = leapCooldownTimer <= 0f && lastAttackState != BossState.TripleLeap;
        if (leapReady && distance >= attackRange + 1.5f && UnityEngine.Random.value < leapPickChance)
            return BossState.TripleLeap;
        if (distance <= attackRange && lastAttackState != BossState.BasicCombo)
            return BossState.BasicCombo;
        return BossState.Approach;
    }

    private void DriveApproach(float speedMultiplier, BossState attackState)
    {
        if (player == null) return;
        Vector2 delta = (Vector2)player.position - (Vector2)transform.position;
        if (delta.sqrMagnitude > 0.001f)
        {
            controller?.FaceTowards(delta.normalized);
            controller?.MoveTowards(delta.normalized, speedMultiplier);
        }
        if (delta.magnitude <= attackRange && decisionTimer <= 0f)
            Enter(attackState);
    }

    private void Enter(BossState next)
    {
        StopActiveRoutine();
        State = next;
        OnStateEntered?.Invoke(next);

        switch (next)
        {
            case BossState.BasicCombo:
                lastAttackState = next;
                activeRoutine = StartCoroutine(RunStandardAction(basicCombo.Run(player, this)));
                break;
            case BossState.FourHitCombo:
                lastAttackState = next;
                activeRoutine = StartCoroutine(RunStandardAction(basicCombo.RunCritical(player)));
                break;
            case BossState.TripleLeap:
                lastAttackState = next;
                leapCooldownTimer = tripleLeapCooldown;
                activeRoutine = StartCoroutine(RunTripleLeap());
                break;
            case BossState.ChargeAttack:
                lastAttackState = next;
                activeRoutine = StartCoroutine(RunPhaseTwoAttack());
                break;
        }
    }

    private IEnumerator RunStandardAction(IEnumerator module)
    {
        controller?.StopMoving();
        yield return module;
        FinishAction();
    }

    private IEnumerator RunTripleLeap()
    {
        controller?.StopMoving();
        yield return tripleLeap.Run(player);
        if (dead || !isActiveAndEnabled) yield break;

        if (GrandTripleLeap.ResolveOutcome(tripleLeap.LastRunHitPlayer) == GrandTripleLeap.Outcome.FollowUpCombo)
        {
            State = BossState.BasicCombo;
            OnStateEntered?.Invoke(State);
            yield return basicCombo.Run(player, this);
            FinishAction();
            yield break;
        }

        State = BossState.Stunned;
        OnStateEntered?.Invoke(State);
        controller?.StopMoving();
        yield return new WaitForSeconds(tripleLeap.StunDurationSeconds);
        FinishAction();
    }

    /// <summary>
    /// 二阶段专项模块接入前的兼容执行器：只消费 BossPhaseController 配置的 P2 AttackData 池，
    /// 不会回退到一阶段双爪或三重跃击。
    /// </summary>
    private IEnumerator RunPhaseTwoAttack()
    {
        controller?.StopMoving();
        if (player != null)
        {
            Vector2 direction = (Vector2)player.position - (Vector2)transform.position;
            controller?.FaceTowards(direction);
        }

        if (combat == null || player == null || !combat.TryStartAttack(player))
        {
            FinishAction();
            yield break;
        }

        float timeout = 8f;
        while (combat.IsAttacking && timeout > 0f && !dead)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        FinishAction();
    }

    private void FinishAction()
    {
        if (dead) return;
        activeRoutine = null;
        controller?.StopMoving();
        State = BossState.Idle;
        OnStateEntered?.Invoke(State);
        decisionTimer = decisionInterval;
    }

    public void EnterSubState(BossState sub)
    {
        State = sub;
        OnStateEntered?.Invoke(sub);
        if (sub != BossState.Retreat) controller?.StopMoving();
    }

    public void EnterPhaseWarning()
    {
        if (dead || PhaseTwo || PhaseWarningTriggered) return;
        PhaseWarningTriggered = true;
        StopActiveRoutine();
        activeRoutine = StartCoroutine(RunPhaseWarning());
    }

    private IEnumerator RunPhaseWarning()
    {
        State = BossState.PhaseWarning;
        OnStateEntered?.Invoke(State);
        controller?.StopMoving();
        yield return new WaitForSeconds(phaseWarningDuration);
        if (dead || PhaseTwo) yield break;
        criticalFrenzy = true;
        State = BossState.Idle;
        OnStateEntered?.Invoke(State);
        decisionTimer = 0f;
        activeRoutine = null;
    }

    public void EnterPhaseTwo()
    {
        if (dead || PhaseTwo) return;
        PhaseTwo = true;
        criticalFrenzy = false;
        StopActiveRoutine();
        activeRoutine = StartCoroutine(RunPhaseTransition());
    }

    private IEnumerator RunPhaseTransition()
    {
        State = BossState.PhaseTransition;
        OnStateEntered?.Invoke(State);
        OnPhaseTwoEntered?.Invoke(true);
        controller?.StopMoving();
        yield return new WaitForSeconds(phaseTransitionDuration);
        if (dead) yield break;
        activeRoutine = null;
        State = BossState.Idle;
        OnStateEntered?.Invoke(State);
        decisionTimer = 0f;
    }

    private void StopActiveRoutine()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }
        basicCombo?.Cancel();
        tripleLeap?.Cancel();
        combat?.CancelCurrentAttack();
        telegraph?.HideAll();
        controller?.StopMoving();
    }

    private void OnDead()
    {
        if (dead) return;
        dead = true;
        StopActiveRoutine();
        State = BossState.Dead;
        OnStateEntered?.Invoke(State);
        OnDeathPosition?.Invoke(transform.position);
        if (ai != null) ai.enabled = false;
        if (combat != null) combat.enabled = false;
    }

    private bool TryFindPlayer()
    {
        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found == null) return false;
        player = found.transform;
        return true;
    }

    private float DistanceToPlayer()
        => player != null ? Vector2.Distance(transform.position, player.position) : float.MaxValue;
}