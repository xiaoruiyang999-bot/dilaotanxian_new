using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(PlayerCombat))]
[RequireComponent(typeof(PlayerMovement))]
public class PlayerController : MonoBehaviour
{
    // 子组件
    private PlayerStats stats;
    private Health health;
    private PlayerCombat combat;
    private PlayerMovement movement;
    private PlayerInteractor interactor;
    private PlayerInput playerInput;

    // 输入
    private Vector2 moveInput;

    // 初始颜色缓存（死亡变灰后 Respawn 恢复用，v0.5.4）
    private Color initialColor;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        health = GetComponent<Health>();
        combat = GetComponent<PlayerCombat>();
        movement = GetComponent<PlayerMovement>();
        playerInput = GetComponent<PlayerInput>();

        // v0.6.1：交互器运行时挂载（编辑器运行期间不改 prefab YAML；prefab 已挂则直接用）
        interactor = GetComponent<PlayerInteractor>();
        if (interactor == null)
            interactor = gameObject.AddComponent<PlayerInteractor>();

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

        moveInput = Vector2.zero;
        if (movement != null) movement.StopImmediately();
    }

    // ========== Input System 回调 ==========

    private void OnActionTriggered(InputAction.CallbackContext context)
    {
        string actionName = context.action?.name;
        if (string.IsNullOrEmpty(actionName)) return;

        // 职业选择 UI 打开期间（v0.6.2）：屏蔽攻击/技能/交互输入——
        // 鼠标点 UI 按钮会触发左键 Attack action，必须拦在分发前；移动不受限（出生房安全）
        // （v0.7.0：Dash/Sprint 已下线，分发分支移除，.inputactions 中 action 保留备用）
        if (ClassSelectUI.IsOpen &&
            (actionName == "Attack" || actionName == "Skill" || actionName == "Interact"))
            return;

        if (actionName == "Move")
        {
            moveInput = context.ReadValue<Vector2>();
            movement.SetMoveInput(moveInput);
        }
        else if (actionName == "Attack")
        {
            // v0.6.3：started/canceled 转发按下/松开，支持长按蓄力与连发
            if (health.IsDead) return;
            if (context.started) combat.OnAttackPressed();
            else if (context.canceled) combat.OnAttackReleased();
        }
        else if (actionName == "Interact" && context.performed)
        {
            interactor.OnInteractPressed();
        }
        else if (actionName == "Cancel" && context.performed)
        {
            // 职业选择 UI 打开时 Esc 优先关 UI（未确认不生效，可再开），否则关拾取列表
            if (ClassSelectUI.IsOpen)
                ClassSelectUI.Close();
            else
                interactor.OnCancelPressed();
        }
        else if (actionName == "Skill" && context.performed)
        {
            // TODO(v0.6.4)：接 SkillExecutor（旋风斩/后跃射击/奥术法阵），此处仅占位
            Debug.Log("[Skill] 技能键占位（v0.6.4 接 SkillExecutor）");
        }
    }

    // ========== 更新循环 ==========

    void Update()
    {
        // 鼠标瞄准与武器朝向由 PlayerAimController + WeaponController 负责，
        // PlayerController 不再直接旋转角色，避免与 WeaponPivot 叠加导致武器转得比鼠标快。
        // 移动速度写入已迁移至 PlayerMovement（v0.6.0），本类不再持有 FixedUpdate。
    }

    // ========== 死亡处理 ==========

    private void OnPlayerDeath()
    {
        moveInput = Vector2.zero;
        if (movement != null) movement.StopImmediately();

        // 变灰表现
        if (TryGetComponent<SpriteRenderer>(out var sr))
            sr.color = new Color(0.3f, 0.3f, 0.3f, 1f);
    }

    /// <summary>死亡重开状态恢复（v0.5.4 死亡重开流程）：颜色还原 + 速度清零（IsDead 由 Health.ResetHealth 解除）。</summary>
    public void Respawn()
    {
        if (TryGetComponent<SpriteRenderer>(out var sr)) sr.color = initialColor;
        moveInput = Vector2.zero;
        if (movement != null) movement.StopImmediately();
    }

    // 外部访问接口
    public PlayerStats GetStats() => stats;
    public Health GetHealth() => health;
    public PlayerCombat GetCombat() => combat;

    public void TakeDamage(float damage) => health.TakeDamage(damage);
}
