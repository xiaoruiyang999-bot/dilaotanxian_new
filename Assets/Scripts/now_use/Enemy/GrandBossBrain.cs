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
    [Header("四足追猎(§4.7 批4)")]
    [SerializeField] private float timeSinceQuadHop = 99f;
    [SerializeField] private float timeSinceLastCharge = 99f;
    private System.Random quadRng;
    private Vector2 lastFaceDirection = Vector2.right;   // 四足限速转向用

    [Header("组件（自动查找）")]
    [SerializeField] private EnemyAI ai;
    [SerializeField] private EnemyCombat combat;
    [SerializeField] private EnemyHealth health;
    [SerializeField] private EnemyController controller;
    [SerializeField] private GrandBasicCombo basicCombo;
    [SerializeField] private GrandTripleLeap tripleLeap;
    [SerializeField] private GrandChargeAttack chargeAttack;   // v2.0.10 批2
    [SerializeField] private GrandMoonHunt moonHunt;           // v2.0.10 批2
    [SerializeField] private BossArenaState arena;             // v2.0.10 批2 石柱状态
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

        // v2.0.10 批2:优先从 EncounterContext 获取(§3.3 显式注入)
        GrandBossEncounterContext context = boss.GetComponentInParent<GrandBossEncounterContext>();
        BossArenaState arenaState = context != null && context.Arena != null
            ? context.Arena
            : boss.GetComponentInParent<BossArenaState>();
        if (arenaState == null)
            Debug.LogWarning("[Grand] 无 EncounterContext/BossArenaState——二阶段石柱联动降级");

        combo.WireAssets(
            Resources.Load<AttackData>("Data/AttackData_GrandRightClaw"),
            Resources.Load<AttackData>("Data/AttackData_GrandLeftClaw"),
            Resources.Load<AttackData>("Data/AttackData_GrandRightUppercut"),
            Resources.Load<AttackData>("Data/AttackData_GrandDoubleClawSlam"));
        leap.WireAssets(Resources.Load<AttackData>("Data/AttackData_GrandLeapLand"), tele);
        brain.WireModules(combo, leap, tele);
        tele.WireBrain(brain);
        if (context != null) tele.WireHazardRoot(context.WorldHazardRoot);   // §4.3

        // v2.0.10 批2:奔袭/围猎/石柱状态
        GrandChargeAttack charge = boss.GetComponent<GrandChargeAttack>();
        if (charge == null) charge = boss.AddComponent<GrandChargeAttack>();
        GrandMoonHunt hunt = boss.GetComponent<GrandMoonHunt>();
        if (hunt == null) hunt = boss.AddComponent<GrandMoonHunt>();

        charge.Wire(arenaState, tele, LayerMask.GetMask("Default"), LayerMask.GetMask("Default", "Obstacle"));   // v2.0.10:玩家在 Default(无 Player Layer)
        hunt.Wire(arenaState, tele, LayerMask.GetMask("Default"), context != null ? context.WorldHazardRoot : null);
        brain.chargeAttack = charge;
        brain.moonHunt = hunt;
        brain.arena = arenaState;

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

    /// <summary>批2 组件接线（EnsureOn 内联调用——保持 WireModules 签名兼容）。</summary>
    public void WirePhaseTwoModules(GrandChargeAttack charge, GrandMoonHunt hunt, BossArenaState arenaState)
    {
        chargeAttack = charge;
        moonHunt = hunt;
        arena = arenaState;
    }

    public void BroadcastWarning(Vector2 pos, float radius)
        => OnWarningShown?.Invoke(pos, radius);

    private void Awake()
    {
        ArtRoot = transform.Find("ArtRoot");
        quadRng = new System.Random(System.Environment.TickCount);
        ai = GetComponent<EnemyAI>();
        combat = GetComponent<EnemyCombat>();
        health = GetComponent<EnemyHealth>();
        controller = GetComponent<EnemyController>();
        basicCombo = GetComponent<GrandBasicCombo>();
        tripleLeap = GetComponent<GrandTripleLeap>();
        chargeAttack = GetComponent<GrandChargeAttack>();
        moonHunt = GetComponent<GrandMoonHunt>();
        arena = GetComponentInParent<BossArenaState>();
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
                DriveQuadruped();
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
            return distance <= attackRange + 1f && timeSinceLastCharge > 3f
            ? BossState.ChargeAttack
            : BossState.QuadrupedChase;   // P1:近距仍需冷却好才直入奔袭(否则四足调整)
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

    /// <summary>四足追猎(§4.7):四行为决策器驱动——Chase/Orbit/Reposition/SideHop。</summary>
    private void DriveQuadruped()
    {
        if (player == null) return;
        timeSinceQuadHop += Time.deltaTime;
        timeSinceLastCharge += Time.deltaTime;

        // RoomBounds 简化:取 Boss 房 ContentRoot 的第一个 Collider(或用固定包围盒)
        Rect bounds = EstimateRoomBounds();

        var d = GrandQuadLocomotion.Decide(
            transform.position, player.position,
            lastFaceDirection,
            bounds, timeSinceQuadHop, timeSinceLastCharge, quadRng);

        // 执行决策
        lastFaceDirection = d.Direction;   // 记录当前朝向(限速转向基准)
        controller?.FaceTowards(d.Direction);
        controller?.MoveTowards(d.Direction, phaseTwoMoveSpeedMultiplier * d.SpeedMultiplier);

        if (d.Mode == GrandQuadLocomotion.MoveMode.SideHop)
            timeSinceQuadHop = 0f;

        // 满足招式条件→进入
        if (d.ReadyToCharge && decisionTimer <= 0f)
        {
            timeSinceLastCharge = 0f;
            Enter(BossState.ChargeAttack);
        }
        else if (d.ReadyToMoonHunt && decisionTimer <= 0f)
        {
            timeSinceLastCharge = 0f;
            Enter(BossState.ChargeAttack);   // 围猎也走 ChargeAttack 调度(RunPhaseTwoAttack 内交替)
        }
    }

    /// <summary>估算房间包围盒(批4 简化:从 Arena 或固定范围)。</summary>
    private Rect EstimateRoomBounds()
    {
        if (arena != null)
        {
            // 从 Arena 的第一根柱估算
            var pillar = arena.FindNearest(transform.position, s => true);
            if (pillar != null)
            {
                float cx = pillar.Center.x, cy = pillar.Center.y;
                return new Rect(cx - 15f, cy - 10f, 30f, 20f);
            }
        }
        return new Rect(transform.position.x - 15f, transform.position.y - 10f, 30f, 20f);
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
                // P0-1 修复:lastAttackState 在模块完成后由 RunPhaseTwoAttack 写入实际执行的招式,
                // 不在 Enter 提前覆盖(原提前覆盖导致 lastAttackState 恒=ChargeAttack→选择器恒判 MoonHunt)
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
    /// <summary>二阶段招式：奔袭/围猎交替（不连续同招，文档 §四）;批2 真实模块接入。</summary>
    private IEnumerator RunPhaseTwoAttack()
    {
        controller?.StopMoving();

        bool chargeNext = lastAttackState != BossState.ChargeAttack;   // 交替(P0-1:看上一招,非当前进入态)
        if (chargeNext && chargeAttack != null)
        {
            lastAttackState = BossState.ChargeAttack;   // 完成后记——下次交替到围猎
            yield return chargeAttack.Run(player, this);
        }
        else if (!chargeNext && moonHunt != null)
        {
            lastAttackState = BossState.MoonHunt;       // 完成后记——下次交替到奔袭
            yield return moonHunt.Run(player, this);
        }
        else if (chargeAttack != null)
        {
            lastAttackState = BossState.ChargeAttack;
            yield return chargeAttack.Run(player, this);   // 围猎缺组件兜底
        }
        else
        {
            // 批2 组件全缺的极端兜底：回旧 AttackData 池路径
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
        }
        FinishAction();
    }

    private void FinishAction()
    {
        if (dead) return;
        // P0-6:Stunned 状态不由 FinishAction 收管——撞裂柱长眩晕走独立恢复协程
        if (State == BossState.Stunned) return;
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
        chargeAttack?.Cancel();   // P0-8:二阶段模块显式取消(防月痕/冲锋残留)
        moonHunt?.Cancel();
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