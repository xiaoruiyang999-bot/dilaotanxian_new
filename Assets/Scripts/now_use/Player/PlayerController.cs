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
    private WerewolfDash werewolfDash;   // v1.1.42 狼人冲刺（选择狼人时 EnsureOn 装上）
    // 最近非零移动方向：无输入冲刺时的兜底朝向（MCP 同款）
    private Vector2 facingDirection = Vector2.right;
    private PlayerMovement movement;
    private PlayerInteractor interactor;
    private PlayerInput playerInput;
    private ItemInventory itemInventory;
    private SkillExecutor skillExecutor;

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

        // v0.7.2：道具背包运行时挂载（同交互器模式；ItemPickup 拾取时兜底再查一次）
        itemInventory = GetComponent<ItemInventory>();
        if (itemInventory == null)
            itemInventory = gameObject.AddComponent<ItemInventory>();

        // v0.7.4：技能执行器运行时挂载（同 ItemInventory 模式；SkillExecutor 无 RequireComponent，补挂安全）
        skillExecutor = GetComponent<SkillExecutor>();
        if (skillExecutor == null)
            skillExecutor = gameObject.AddComponent<SkillExecutor>();

        // v0.7.5：序列帧动画器运行时挂载（同 SkillExecutor 模式；FrameAnimator 无 RequireComponent，补挂安全）
        // 纯表现层：组件自身驱动行走/停帧/镜像与置白，本类不持有引用
        if (GetComponent<FrameAnimator>() == null)
            gameObject.AddComponent<FrameAnimator>();

        // v1.1.42 狼人冲刺：懒查（选择页可能在 Awake 之后才 EnsureOn 挂组件，Dash 触发时现查最稳）
        werewolfDash = GetComponent<WerewolfDash>();

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

        // 选择类 UI 打开期间：屏蔽攻击/技能/交互输入——鼠标点 UI 按钮会触发左键 Attack action，必须拦在分发前；
        // 移动不受限（出生房安全）。角色选择页（v1.0.8）与职业选择页同规则。
        if ((ClassSelectUI.IsOpen || CharacterSelectUI.IsOpen) &&
            (actionName == "Attack" || actionName == "Skill" || actionName == "Interact" || actionName == "UseItem"
                || actionName == "Ultimate" || actionName == "WeaponSkill"))
            return;

        if (actionName == "Move")
        {
            moveInput = context.ReadValue<Vector2>();
            if (moveInput.sqrMagnitude > 0.01f) facingDirection = moveInput.normalized;
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
            // 选择类 UI 打开时 Esc 优先逐级关 UI（未确认不生效），否则关拾取列表
            if (ClassSelectUI.IsOpen)
                ClassSelectUI.Close();
            else if (CharacterSelectUI.IsOpen)
                CharacterSelectUI.Close();
            else
                interactor.OnCancelPressed();
        }

        else if (actionName == "Dash" && context.performed)
        {
            // v1.1.42 狼人冲刺（MCP Dash 动作复用 Space）：仅狼人挂了 WerewolfDash 时生效
            if (werewolfDash == null) werewolfDash = GetComponent<WerewolfDash>();   // 懒兜底（后挂）
            if (werewolfDash != null && !health.IsDead)
                werewolfDash.TryDash(moveInput, facingDirection);
        }
        else if (actionName == "Skill" && context.performed)
        {
            // v0.7.4：F = 小技能（分支选中项，SkillExecutor 槽 0）
            if (health.IsDead) return;
            skillExecutor.TryCastSlot(0);
        }
        else if (actionName == "Ultimate" && context.performed)
        {
            // v0.7.4：Q = 大招（SkillExecutor 槽 1，仿 UseItem 分支先判死亡）
            if (health.IsDead) return;
            skillExecutor.TryCastSlot(1);
        }
        else if (actionName == "WeaponSkill" && context.performed)
        {
            // v0.7.4：R = 武器技能（SkillExecutor 槽 2）
            if (health.IsDead) return;
            skillExecutor.TryCastSlot(2);
        }
        else if (actionName == "UseItem" && context.performed)
        {
            // v0.7.2：使用道具栏激活项（未装备消耗品时无副作用）
            if (health.IsDead) return;
            itemInventory.UseActive();
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
        if (werewolfDash != null) werewolfDash.ResetDash();   // v1.1.42 冲刺状态/冷却复位
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
