using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(PlayerCombat))]
public class PlayerController : MonoBehaviour
{
    // 子组件
    private Rigidbody2D rb;
    private PlayerStats stats;
    private Health health;
    private PlayerCombat combat;
    private PlayerInput playerInput;

    // 输入
    private Vector2 moveInput;

    // 初始颜色缓存（死亡变灰后 Respawn 恢复用，v0.5.4）
    private Color initialColor;

    [Header("闪避 Dash（M1·v0.6.1）")]
    [Tooltip("冲刺速度（远高于移速）")]
    [SerializeField] private float dashSpeed = 18f;
    [Tooltip("冲刺持续时间（秒）")]
    [SerializeField] private float dashDuration = 0.15f;
    [Tooltip("冲刺冷却（秒），从冲刺结束起算")]
    [SerializeField] private float dashCooldown = 0.9f;
    [Tooltip("无敌帧额外延长：冲刺结束后仍免伤一小段，避免收招瞬间被弹道擦中")]
    [SerializeField] private float iFrameBonus = 0.06f;

    // Dash 运行时状态
    private bool isDashing;
    private float dashTimer;
    private float dashCooldownUntil;
    private Vector2 dashDirection;
    // 最近一次非零移动方向：无输入触发 Dash 时的兜底朝向
    private Vector2 facingDirection = Vector2.right;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // v0.7.1 修复：Dash 高速（18/s）下默认离散碰撞检测会概率隧穿薄墙——连续检测兜底
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        stats = GetComponent<PlayerStats>();
        health = GetComponent<Health>();
        combat = GetComponent<PlayerCombat>();
        playerInput = GetComponent<PlayerInput>();

        if (TryGetComponent<SpriteRenderer>(out var sr0)) initialColor = sr0.color;

        // 监听死亡事件
        health.OnDeath += OnPlayerDeath;

        // 确保 PlayerInput 使用 C# 事件模式，避免 SendMessage / UnityEvent 绑定问题
        if (playerInput != null)
        {
            playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            playerInput.defaultActionMap = "Player";
        }
    }

    void OnDestroy()
    {
        if (health != null) health.OnDeath -= OnPlayerDeath;
    }

    void OnEnable()
    {
        if (playerInput != null)
            playerInput.onActionTriggered += OnActionTriggered;
    }

    void OnDisable()
    {
        if (playerInput != null)
            playerInput.onActionTriggered -= OnActionTriggered;

        rb.linearVelocity = Vector2.zero;
        moveInput = Vector2.zero;
    }

    // ========== Input System 回调 ==========

    private void OnActionTriggered(InputAction.CallbackContext context)
    {
        string actionName = context.action?.name;
        if (string.IsNullOrEmpty(actionName)) return;

        if (actionName == "Move")
        {
            moveInput = context.ReadValue<Vector2>();
        }
        else if (actionName == "Attack" && context.performed)
        {
            if (!health.IsDead)
                combat.TryAttack();
        }
        else if (actionName == "Dash" && context.performed)
        {
            TryStartDash();
        }
    }

    // ========== 闪避 Dash（M1·v0.6.1）==========

    private void TryStartDash()
    {
        if (health == null || health.IsDead) return;
        if (isDashing || Time.time < dashCooldownUntil) return;

        dashDirection = moveInput.sqrMagnitude > 0.01f ? moveInput.normalized : facingDirection;
        isDashing = true;
        dashTimer = dashDuration;
        // 无敌帧覆盖冲刺全程 + 收招缓冲（M1.2）
        health.GrantIFrames(dashDuration + iFrameBonus);
    }

    // ========== 更新循环 ==========

    void FixedUpdate()
    {
        if (health != null && health.IsDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isDashing)
        {
            dashTimer -= Time.fixedDeltaTime;
            rb.linearVelocity = dashDirection * dashSpeed;
            if (dashTimer <= 0f)
            {
                isDashing = false;
                dashCooldownUntil = Time.time + dashCooldown;
            }
            return;
        }

        if (moveInput.sqrMagnitude > 0.01f)
            facingDirection = moveInput.normalized;
        rb.linearVelocity = moveInput.normalized * stats.MoveSpeed;
    }

    // ========== 死亡处理 ==========

    private void OnPlayerDeath()
    {
        rb.linearVelocity = Vector2.zero;
        moveInput = Vector2.zero;
        isDashing = false;   // 死亡中断冲刺，冷却由 Respawn 重置

        // 变灰表现
        if (TryGetComponent<SpriteRenderer>(out var sr))
            sr.color = new Color(0.3f, 0.3f, 0.3f, 1f);
    }

    /// <summary>死亡重开状态恢复（v0.5.4 死亡重开流程）：颜色还原 + 速度清零（IsDead 由 Health.ResetHealth 解除）。</summary>
    public void Respawn()
    {
        if (TryGetComponent<SpriteRenderer>(out var sr)) sr.color = initialColor;
        rb.linearVelocity = Vector2.zero;
        moveInput = Vector2.zero;
        isDashing = false;
        dashCooldownUntil = 0f;
    }

    // 外部访问接口
    public PlayerStats GetStats() => stats;
    public Health GetHealth() => health;
    public PlayerCombat GetCombat() => combat;
    /// <summary>最近一次非零移动方向（狼人形态等视觉系统用，v0.6.3）。</summary>
    public Vector2 FacingDirection => facingDirection;

    public void TakeDamage(float damage) => health.TakeDamage(damage);
}
