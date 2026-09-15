using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// v2.0.10 格兰总状态机（开发文档 §一/§十）：唯一允许启动招式的对象。
/// 职责：阶段(一/二)与行为状态、动作互斥（同时只跑一个）、动作间间隔、
/// 同招不连续无限复用、受控/死亡/阶段转换时取消旧动作与预警。
/// 批1 范围（文档 §十一 顺序 1~6）：Idle/Approach/BasicCombo/TripleLeap/Stunned 闭环；
/// 四段连击/奔袭/围猎/狼嚎等二阶段状态预留枚举与调度入口，模块后续批次接入。
/// BossPhaseController 保留生命监听，升级为向本脑发送阶段命令（不再直改攻击池）。
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

    [Header("一阶段参数（文档 §二/§三 测试值）")]
    [SerializeField, Min(0.1f)] private float decisionInterval = 0.6f;   // 动作间最小间隔
    [SerializeField, Min(0f)] private float attackRange = 2.6f;          // 双爪起手距离
    [SerializeField, Min(0f)] private float leapTriggerRange = 9f;       // 跃击偏好距离（冷却好且掷中）
    [SerializeField, Min(0f)] private float tripleLeapCooldown = 14f;    // 跃击招冷却
    [Range(0f, 1f)] [SerializeField] private float leapPickChance = 0.4f;

    [Header("组件（自动查找）")]
    [SerializeField] private EnemyAI ai;
    [SerializeField] private EnemyCombat combat;
    [SerializeField] private EnemyHealth health;
    [SerializeField] private GrandBasicCombo basicCombo;
    [SerializeField] private GrandTripleLeap tripleLeap;

    private Transform player;
    private float decisionTimer;
    private float leapCooldownTimer;
    private BossState lastAttackState;         // 同招不连续复用（轻约束：连 Idle/Approach 后可复用）
    private Coroutine activeRoutine;
    private bool dead;

    /// <summary>运行时自举（Boss 变体 Prefab 不做 YAML 手术——EnsureOn 同 WerewolfTransformation 先例）：
    /// Boss 房敌人生成后调用；幂等。组件/子模块/爪击资产一并接线，资产走 Resources。</summary>
    public static GrandBossBrain EnsureOn(GameObject boss)
    {
        GrandBossBrain brain = boss.GetComponent<GrandBossBrain>();
        if (brain != null) return brain;

        brain = boss.AddComponent<GrandBossBrain>();
        var combo = boss.AddComponent<GrandBasicCombo>();
        var leap = boss.AddComponent<GrandTripleLeap>();
        var tele = boss.AddComponent<BossTelegraphController>();

        // 资产接线（Resources 加载——项目打包态契约；缺资产静默降级为现有攻击池行为）
        combo.WireAssets(
            Resources.Load<AttackData>("Data/AttackData_GrandRightClaw"),
            Resources.Load<AttackData>("Data/AttackData_GrandLeftClaw"));
        leap.WireAssets(
            Resources.Load<AttackData>("Data/AttackData_GrandLeapLand"),
            tele,
            LayerMask.GetMask("Player"));

        // 预制体变体路径不在 Resources 下——双路径兜底（编辑器 AssetDatabase 由调用环境保证;
        // 这里用完整路径 Load 尝试：Assets/Data 在 Resources 外,故复制到 Resources/Data 一份见资产步骤）
        brain.WireModules(combo, leap);
        return brain;
    }

    /// <summary>自举后接线（Inspector 路径的替代——字段全私有保持封装）。</summary>
    public void WireModules(GrandBasicCombo combo, GrandTripleLeap leap)
    {
        basicCombo = combo;
        tripleLeap = leap;
    }

    private void Awake()
    {
        ai = GetComponent<EnemyAI>();
        combat = GetComponent<EnemyCombat>();
        health = GetComponent<EnemyHealth>();
        basicCombo = GetComponent<GrandBasicCombo>();
        tripleLeap = GetComponent<GrandTripleLeap>();
        if (health != null) health.OnDeath += OnDead;
    }

    private void OnDestroy()
    {
        if (health != null) health.OnDeath -= OnDead;
    }

    private void OnEnable()
    {
        // Boss 房休眠唤醒/复用：复位到一阶段待机
        StopActiveRoutine();
        State = BossState.Idle;
        decisionTimer = decisionInterval;
        leapCooldownTimer = tripleLeapCooldown * 0.5f;   // 开局半冷却后可用
    }

    private void Update()
    {
        if (dead || player == null && !TryFindPlayer()) return;
        leapCooldownTimer -= Time.deltaTime;
        if (decisionTimer > 0f) decisionTimer -= Time.deltaTime;

        switch (State)
        {
            case BossState.Idle:
                if (decisionTimer <= 0f) Enter(DecideNext());
                break;
            case BossState.Approach:
                // 向玩家移动；到攻击距离即接双爪连击（移动由既有 EnemyAI 追击速度承担）
                if (DistanceToPlayer() <= attackRange && decisionTimer <= 0f)
                    Enter(BossState.BasicCombo);
                break;
            // 执行态由协程模块回调 ExitState 收管
            case BossState.BasicCombo:
            case BossState.Retreat:
            case BossState.TripleLeap:
            case BossState.Stunned:
            case BossState.PhaseWarning:
            case BossState.PhaseTransition:
                break;
            default:
                // 二阶段状态批2+接入前的安全兜底：回 Idle
                Enter(BossState.Idle);
                break;
        }
    }

    /// <summary>行为选择（一阶段）：近距双爪 / 远距偏好跃击 / 其余接近。</summary>
    private BossState DecideNext()
    {
        float dist = DistanceToPlayer();
        bool leapReady = leapCooldownTimer <= 0f && lastAttackState != BossState.TripleLeap;
        if (leapReady && dist >= attackRange + 1.5f && UnityEngine.Random.value < leapPickChance)
            return BossState.TripleLeap;
        if (dist <= attackRange && lastAttackState != BossState.BasicCombo)
            return BossState.BasicCombo;
        return BossState.Approach;
    }

    private void Enter(BossState next)
    {
        StopActiveRoutine();
        State = next;

        switch (next)
        {
            case BossState.BasicCombo:
                lastAttackState = next;
                activeRoutine = StartCoroutine(RunModule(basicCombo.Run(player, this)));
                break;
            case BossState.TripleLeap:
                lastAttackState = next;
                leapCooldownTimer = tripleLeapCooldown;
                activeRoutine = StartCoroutine(RunModule(tripleLeap.Run(player, this)));
                break;
            default:
                Enter(BossState.Approach);
                break;
        }
    }

    /// <summary>执行招式协程：结束后统一回 Idle 并计时决策间隔（模块内异常/中断也走此收口）。</summary>
    private IEnumerator RunModule(System.Collections.IEnumerator module)
    {
        SetAILocomotion(false);   // 招式执行期接管移动
        while (true)
        {
            object result;
            try { if (!module.MoveNext()) break; result = module.Current; }
            catch (Exception) { break; }   // 模块崩溃不拖死状态机
            if (result != null && result is YieldInstruction yi)
                yield return yi;
            else
                yield return result;
        }
        SetAILocomotion(true);
        State = BossState.Idle;
        decisionTimer = decisionInterval;
    }

    /// <summary>模块回调：进入子状态（后退/眩晕等非招式执行态）或直接结束。</summary>
    public void EnterSubState(BossState sub)
    {
        State = sub;
        if (sub == BossState.Stunned)
        {
            SetAILocomotion(false);
            activeRoutine = StartCoroutine(RecoverFromStun());
        }
        else if (sub == BossState.Retreat)
        {
            SetAILocomotion(true);   // 后退用既有移动（反向由模块自驱，此处只保证可动）
        }
    }

    private IEnumerator RecoverFromStun()
    {
        yield return tripleLeap != null ? tripleLeap.StunDurationWait() : null;
        SetAILocomotion(true);
        State = BossState.Idle;
        decisionTimer = decisionInterval;
    }

    /// <summary>阶段命令（BossPhaseController 调用；批2 接四段连击/狼嚎，当前仅记录）。</summary>
    public void EnterPhaseTwo()
    {
        if (PhaseTwo) return;
        PhaseTwo = true;
        StopActiveRoutine();
        // 批2：狼嚎演出→四段连击池/四足参数切换；当前保守回 Idle 继续一阶段循环
        Enter(BossState.Idle);
    }

    private void StopActiveRoutine()
    {
        if (activeRoutine != null) { StopCoroutine(activeRoutine); activeRoutine = null; }
        basicCombo?.Cancel();
        tripleLeap?.Cancel();
        SetAILocomotion(true);
    }

    private void OnDead()
    {
        dead = true;
        StopActiveRoutine();
        State = BossState.Dead;
    }

    private void SetAILocomotion(bool on)
    {
        if (ai != null) ai.enabled = on;
        if (combat != null) combat.enabled = on;   // 招式期禁普通攻击防双启动
    }

    private bool TryFindPlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return false;
        player = p.transform;
        return true;
    }

    private float DistanceToPlayer()
        => player != null ? Vector2.Distance(transform.position, player.position) : float.MaxValue;
}
