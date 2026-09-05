using UnityEngine;

/// <summary>
/// 狼人冲刺（v1.1.42，自 MCP 分支 M1·v0.6.1 Dash 移植 + v0.7.x 架构适配）：
/// 仅狼人外形可用（ChosenCharacter==Werewolf 时由选择页/FrameAnimator 装上本组件，
/// 改选战士随组件销毁自动下线）。触发键 = 输入资产既有 Dash 动作（Space，v0.7.0 下线时保留备用，
/// 正好复用）；也可代码调用 TryDash()。
/// 参数与无敌帧口径完全沿用 MCP 分支原版：18 速 / 0.15s / 0.9s CD / 收招无敌 +0.06s
///（Health.SetInvincible 实现无敌窗口，冲刺结束自动解除；重复授予取更晚截止语义由本类自管）。
/// 移动写入不走 PlayerMovement（常规移速管线），冲刺期间由本组件直写 rb.linearVelocity 并抑制
/// PlayerMovement 写入（SetSuspended），结束归还——单一写速者原则（v0.7.0 PlayerMovement 注释合同）。
/// 高速隧穿防护：Awake 置 rb.collisionDetectionMode = Continuous（MCP 分支 v0.7.1 教训）。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public class WerewolfDash : MonoBehaviour
{
    [Header("冲刺参数（MCP 原版）")]
    [Tooltip("冲刺速度（远高于移速）")]
    [SerializeField] private float dashSpeed = 18f;
    [Tooltip("冲刺持续时间（秒）")]
    [SerializeField] private float dashDuration = 0.15f;
    [Tooltip("冲刺冷却（秒），从冲刺结束起算")]
    [SerializeField] private float dashCooldown = 0.9f;
    [Tooltip("无敌帧额外延长：冲刺结束后仍免伤一小段，避免收招瞬间被弹道擦中")]
    [SerializeField] private float iFrameBonus = 0.06f;

    private Rigidbody2D rb;
    private Health health;
    private PlayerMovement movement;        // 冲刺期间抑制常规移速写入
    private FrameAnimator animator;         // 冲刺期间挂起动画（视觉残影感的廉价替代：定格帧）

    private bool isDashing;
    private float dashTimer;
    private float invincibleUntil;          // Time.time 口径（无敌窗口截止）
    private Vector2 dashDirection;

    /// <summary>是否在冲刺中（PlayerController 可查，防冲刺中重复触发）。</summary>
    public bool IsDashing => isDashing;
    /// <summary>冷却剩余秒（UI/调试用）。</summary>
    public float CooldownRemaining => Mathf.Max(0f, dashCooldownUntil - Time.time);
    private float dashCooldownUntil;

    /// <summary>给狼人玩家装上冲刺（CharacterSelectUI.Pick 确认狼人时调用；改选战士组件销毁即下线）。</summary>
    public static WerewolfDash EnsureOn(GameObject player)
    {
        var d = player.GetComponent<WerewolfDash>();
        if (d == null) d = player.AddComponent<WerewolfDash>();
        return d;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        movement = GetComponent<PlayerMovement>();
        animator = GetComponent<FrameAnimator>();
        // v0.7.1 MCP 教训：18/s 高速下 Discrete 碰撞检测概率隧穿薄墙——连续检测兜底
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void OnDestroy()
    {
        if (health != null && health.IsInvincible && Time.time >= invincibleUntil)
            health.SetInvincible(false);   // 冲刺无敌窗口未自然走完时还原（对象销毁防御）
        if (movement != null) movement.SetSuspended(false);
    }

    void Update()
    {
        // 无敌窗口到点自动解除（SetInvincible 是布尔，自管截止时刻）
        if (!isDashing && health.IsInvincible && Time.time >= invincibleUntil)
            health.SetInvincible(false);

        if (!isDashing) return;

        dashTimer -= Time.deltaTime;
        rb.linearVelocity = dashDirection * dashSpeed;
        if (dashTimer <= 0f) EndDash();
    }

    /// <summary>尝试冲刺（PlayerController Dash 动作转发 / 代码调用）。方向 = 当前输入，无输入用面朝向。</summary>
    public bool TryDash(Vector2 inputDir, Vector2 fallbackFacing)
    {
        if (health == null || health.IsDead) return false;
        if (isDashing || Time.time < dashCooldownUntil) return false;

        dashDirection = inputDir.sqrMagnitude > 0.01f ? inputDir.normalized : fallbackFacing.normalized;
        isDashing = true;
        dashTimer = dashDuration;
        health.SetInvincible(true);
        invincibleUntil = Time.time + dashDuration + iFrameBonus;
        if (movement != null) movement.SetSuspended(true);   // 抑制常规移速写入（单一写速者）
        return true;
    }

    private void EndDash()
    {
        isDashing = false;
        dashCooldownUntil = Time.time + dashCooldown;
        if (movement != null) movement.SetSuspended(false);
        // 无敌帧自然到期由 Update 解除；此处不提前撤（收招缓冲语义）
    }

    /// <summary>死亡/重生复位（PlayerController.Respawn 链路调用；冷却清零）。</summary>
    public void ResetDash()
    {
        isDashing = false;
        dashCooldownUntil = 0f;
        if (health != null && health.IsInvincible) health.SetInvincible(false);
        if (movement != null) movement.SetSuspended(false);
    }
}
