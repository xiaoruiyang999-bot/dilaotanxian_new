using UnityEngine;

/// <summary>
/// 玩家移动执行层（v0.7.0 重写为纯移动）。
/// 统一管理 rb.linearVelocity 的写入：常规移动 + 蓄力减速（SetChargeSlow ×0.5，与体力无耦合）
/// + Buff 移速倍率（v0.7.5，BuffManager 缺失/无 buff 时 ×1 零差异）。
/// 体力/闪避/疾跑已于 v0.7.0 下线（决策 2），Space/Shift 输入代码侧解绑，
/// Dash/Sprint action 保留在 .inputactions 备用（技能化闪避回归时复用）。
/// 输入仍由 PlayerController 唯一入口收集后转发，本类不直接监听 Input System。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(Health))]
public class PlayerMovement : MonoBehaviour
{
    [Header("蓄力（v0.6.3）")]
    [SerializeField] private float chargeMoveSpeedMultiplier = 0.5f; // 蓄力期间移速倍率（计划书 4.6）

    private Rigidbody2D rb;
    private PlayerStats stats;
    private Health health;
    private BuffManager buffManager;   // v0.7.5：延迟缓存（SkillExecutor.Awake 运行时补挂，Awake 顺序不定）

    // 输入状态（由 PlayerController 转发）
    private Vector2 moveInput;

    // 蓄力状态（v0.6.3，由 PlayerCombat 设置）
    private bool chargeSlowing;
    // 冲刺抑制（v1.1.42 WerewolfDash）：冲刺期间本组件不写速度，由冲刺组件直写——单一写速者
    private bool suspended;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        stats = GetComponent<PlayerStats>();
        health = GetComponent<Health>();
    }

    void FixedUpdate()
    {
        // 死亡：清速度
        if (health.IsDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // 冲刺期间让出写速权（WerewolfDash 直写高速位移）
        if (suspended) return;

        // 蓄力减速（v0.6.3，计划书 4.6）：蓄力中移速 ×0.5
        float speed = stats.MoveSpeed;
        if (chargeSlowing)
            speed *= chargeMoveSpeedMultiplier;

        // Buff 移速倍率（v0.7.5：屹立不倒 +20% / 虚弱 −35%）；无 BuffManager / 无 buff 为 1，零行为差异
        if (buffManager == null) buffManager = GetComponent<BuffManager>();
        if (buffManager != null)
            speed *= buffManager.MoveSpeedMultiplier;

        rb.linearVelocity = moveInput.normalized * speed;
    }

    // ========== 输入转发（PlayerController 调用）==========

    /// <summary>设置移动输入（PlayerController 从 Move action 转发）。</summary>
    public void SetMoveInput(Vector2 input)
    {
        moveInput = input;
    }

    /// <summary>设置蓄力减速状态（v0.6.3，PlayerCombat 蓄力开始/结束时调用，计划书 4.6）。</summary>
    public void SetChargeSlow(bool on)
    {
        chargeSlowing = on;
    }

    /// <summary>冲刺抑制开关（v1.1.42 WerewolfDash 冲刺起止调用）。</summary>
    public void SetSuspended(bool on) => suspended = on;

    /// <summary>立即停止所有移动（供死亡/失活/Respawn 调用）。</summary>
    public void StopImmediately()
    {
        moveInput = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
    }
}
