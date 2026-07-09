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

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        stats = GetComponent<PlayerStats>();
        health = GetComponent<Health>();
        combat = GetComponent<PlayerCombat>();
        playerInput = GetComponent<PlayerInput>();

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

        rb.velocity = Vector2.zero;
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

    void Update()
    {
        if (!health.IsDead)
        {
            HandleMouseAiming();
        }
    }

    void FixedUpdate()
    {
        if (health.IsDead)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        rb.velocity = moveInput.normalized * stats.MoveSpeed;
    }

    // ========== 鼠标瞄准 ==========

    private void HandleMouseAiming()
    {
        if (Mouse.current == null || Camera.main == null) return;

        Vector3 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
        Vector2 direction = mouseWorldPos - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    // ========== 死亡处理 ==========

    private void OnPlayerDeath()
    {
        rb.velocity = Vector2.zero;
        moveInput = Vector2.zero;

        // 变灰表现
        if (TryGetComponent<SpriteRenderer>(out var sr))
            sr.color = new Color(0.3f, 0.3f, 0.3f, 1f);
    }

    // 外部访问接口
    public PlayerStats GetStats() => stats;
    public Health GetHealth() => health;
    public PlayerCombat GetCombat() => combat;

    public void TakeDamage(float damage) => health.TakeDamage(damage);
}
