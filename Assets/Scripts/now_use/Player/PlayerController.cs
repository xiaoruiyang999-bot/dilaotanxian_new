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
    // v1.1.48 失落城堡式：只保留左右水平朝向（±1,0），攻击/技能/冲刺全部消费
    private Vector2 facingDirection = Vector2.right;
    /// <summary>水平朝向（v1.1.48 公开只读：±1,0）——近战攻击带方向/踏步方向消费。</summary>
    public Vector2 FacingDirection => facingDirection;
    private PlayerMovement movement;
    private PlayerInteractor interactor;
    private PlayerInput playerInput;
    private ItemInventory itemInventory;
    private SkillExecutor skillExecutor;
    private WerewolfTransformation werewolfTransformation;

    // 输入
    private Vector2 moveInput;
    /// <summary>当前移动输入（只读，v1.2.2 WerewolfAnimatorDriver 消费）。</summary>
    public Vector2 MoveInput => moveInput;

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
        werewolfTransformation = GetComponent<WerewolfTransformation>();

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
        GameModalService.StateChanged += OnModalStateChanged;
    }

    void OnDisable()
    {
        if (playerInput != null)
            playerInput.onActionTriggered -= OnActionTriggered;
        GameModalService.StateChanged -= OnModalStateChanged;

        moveInput = Vector2.zero;
        if (movement != null) movement.StopImmediately();
    }

    // ========== Input System 回调 ==========

    private void OnActionTriggered(InputAction.CallbackContext context)
    {
        string actionName = context.action?.name;
        if (string.IsNullOrEmpty(actionName)) return;

        // v2.1.2：Cancel 只走顶层模态→暂停菜单→世界取消的单一链路，
        // 避免 PausePanel/PlayerController 同帧重复消费导致面板闪开闪关。
        if (actionName == "Cancel" && context.performed)
        {
            if (GameModalService.TryCancelTop()) return;
            if (PausePanel.Instance != null && PausePanel.Instance.TryOpenFromInput()) return;
            interactor.OnCancelPressed();
            return;
        }

        // 任何模态界面打开时阻断全部世界输入，包括移动、攻击、技能、交互和 Dash。
        if (GameModalService.BlocksWorldInput)
            return;

        if (actionName == "Move")
        {
            moveInput = context.ReadValue<Vector2>();
            // v1.1.48 失落城堡式朝向：只保留左右（上下移动只负责对齐纵深站位）。
            // 有水平输入时更新朝向符号，纯竖直输入/静止时保持上次朝向——技能/冲刺/攻击全部消费此水平朝向。
            if (Mathf.Abs(moveInput.x) > 0.01f)
                facingDirection = new Vector2(Mathf.Sign(moveInput.x), 0f);
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
            // V2：狼人 Q 消耗满怒痕进入兽化；非狼人仍走通用大招槽。
            if (health.IsDead) return;
            if (werewolfTransformation == null)
                werewolfTransformation = GetComponent<WerewolfTransformation>();

            if (werewolfTransformation != null)
                werewolfTransformation.TryActivateUltimate();
            else
                skillExecutor.TryCastSlot(1);
        }
        // V2 首轮 MVP 不启用旧 R 武器技与 C 道具键；旧系统保留到替代链完成后再归档。
    }

    // ========== 更新循环 ==========

    void Update()
    {
        // 鼠标瞄准与武器朝向由 PlayerAimController + WeaponController 负责，
        // PlayerController 不再直接旋转角色，避免与 WeaponPivot 叠加导致武器转得比鼠标快。
        // 移动速度写入已迁移至 PlayerMovement（v0.6.0），本类不再持有 FixedUpdate。

        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return;
        // v2.0.8 用户定案：叙事碎片 J 键逐段推进（Esc 不再消费；点击推进保留）
        if (keyboard.jKey.wasPressedThisFrame && GameModalService.IsTop(GameModalKind.Narrative))
            NarrativePanelUI.Advance();
        if (keyboard.tabKey.wasPressedThisFrame
            && (GameModalService.IsTop(GameModalKind.MapPreview)
                || GameModalService.IsTop(GameModalKind.RouteChoice)))
        {
            DungeonMapUI.Close();
            return;
        }
        if (GameModalService.BlocksWorldInput) return;
        // v2.0.5 Tab 开关 DAG 地图预览（Input System 设备直读——action 表暂无 Map 键，随 T-07 重绑 action 化）
        if (keyboard.tabKey.wasPressedThisFrame) DungeonMapUI.Toggle();
    }

    private void OnModalStateChanged()
    {
        if (!GameModalService.BlocksWorldInput) return;
        moveInput = Vector2.zero;
        movement?.StopImmediately();
        combat?.OnAttackReleased();
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
        GetComponent<WerewolfTransformation>()?.ResetTransformation();
        GetComponent<WerewolfRage>()?.ResetRage();
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
