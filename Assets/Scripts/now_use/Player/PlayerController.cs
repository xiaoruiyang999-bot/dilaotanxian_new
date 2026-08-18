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

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
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
    }

    // ========== 更新循环 ==========

    void FixedUpdate()
    {
        if (health != null && health.IsDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        rb.linearVelocity = moveInput.normalized * stats.MoveSpeed;
    }

    // ========== 死亡处理 ==========

    private void OnPlayerDeath()
    {
        rb.linearVelocity = Vector2.zero;
        moveInput = Vector2.zero;

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
    }

    // 外部访问接口
    public PlayerStats GetStats() => stats;
    public Health GetHealth() => health;
    public PlayerCombat GetCombat() => combat;

    public void TakeDamage(float damage) => health.TakeDamage(damage);
}
