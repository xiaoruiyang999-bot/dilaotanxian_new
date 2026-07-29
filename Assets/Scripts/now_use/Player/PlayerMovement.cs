using UnityEngine;
using DG.Tweening;

/// <summary>
/// 玩家移动执行层（v0.6.0）。
/// 统一管理 rb.linearVelocity 的写入：常规移动 / 奔跑（Sprint）/ 闪避（Dash）。
/// 输入仍由 PlayerController 唯一入口收集后转发，本类不直接监听 Input System。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(Health))]
public class PlayerMovement : MonoBehaviour
{
    [Header("奔跑")]
    [SerializeField] private float sprintSpeedMultiplier = 1.6f;    // 奔跑移速倍率
    [SerializeField] private float sprintStaminaPerSec = 20f;       // 奔跑体力消耗（点/秒）

    [Header("蓄力（v0.6.3）")]
    [SerializeField] private float chargeMoveSpeedMultiplier = 0.5f; // 蓄力期间移速倍率（计划书 4.6）

    [Header("闪避")]
    [SerializeField] private float dashDistance = 3f;               // 冲刺距离
    [SerializeField] private float dashDuration = 0.18f;            // 冲刺时长（速度 = 距离/时长）
    [SerializeField] private float dashStaminaCost = 35f;           // 冲刺体力消耗
    [SerializeField] private float dashCooldown = 0.5f;             // 冲刺结束后的内置冷却
    [SerializeField, Range(0f, 1f)] private float dashAlpha = 0.5f; // 冲刺期间角色透明度
    [SerializeField] private int afterimageCount = 3;               // 冲刺残影数量

    private Rigidbody2D rb;
    private PlayerStats stats;
    private Health health;
    private PlayerAimController aim;
    private SpriteRenderer sr;

    // 输入状态（由 PlayerController 转发）
    private Vector2 moveInput;
    private bool sprintHeld;

    // 蓄力状态（v0.6.3，由 PlayerCombat 设置）
    private bool chargeSlowing;

    // 闪避状态
    private bool isDashing;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector2 dashDirection;
    private int afterimagesSpawned;
    private Color colorBeforeDash;

    /// <summary>是否正在冲刺（供外部查询，如攻击/动画逻辑避让）。</summary>
    public bool IsDashing => isDashing;

    /// <summary>当前是否处于奔跑状态（按住奔跑键、有移动输入且体力未耗尽）。</summary>
    public bool IsSprinting =>
        sprintHeld && !isDashing && moveInput.sqrMagnitude > 0.0001f && stats.CurrentStamina > 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        stats = GetComponent<PlayerStats>();
        health = GetComponent<Health>();
        aim = GetComponent<PlayerAimController>();
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (dashCooldownTimer > 0f)
            dashCooldownTimer -= Time.deltaTime;
    }

    void FixedUpdate()
    {
        // 死亡：清速度并中断冲刺
        if (health.IsDead)
        {
            if (isDashing) EndDash();
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // 冲刺期间：直接写冲刺速度，覆盖常规移动
        if (isDashing)
        {
            dashTimer -= Time.fixedDeltaTime;
            TickAfterimages();
            rb.linearVelocity = dashDirection * (dashDistance / dashDuration);

            if (dashTimer <= 0f)
                EndDash();
            return;
        }

        // 蓄力减速优先（v0.6.3，计划书 4.6）：蓄力中不叠加奔跑提速与体力消耗，直接 ×0.5 减速
        float speed = stats.MoveSpeed;
        if (chargeSlowing)
        {
            speed *= chargeMoveSpeedMultiplier;
        }
        else if (IsSprinting)
        {
            stats.ConsumeStaminaOverTime(sprintStaminaPerSec);
            speed *= sprintSpeedMultiplier;
        }

        rb.linearVelocity = moveInput.normalized * speed;
    }

    // ========== 输入转发（PlayerController 调用）==========

    /// <summary>设置移动输入（PlayerController 从 Move action 转发）。</summary>
    public void SetMoveInput(Vector2 input)
    {
        moveInput = input;
    }

    /// <summary>设置奔跑键按住状态（PlayerController 从 Sprint action 转发）。</summary>
    public void SetSprintHeld(bool held)
    {
        sprintHeld = held;
    }

    /// <summary>设置蓄力减速状态（v0.6.3，PlayerCombat 蓄力开始/结束时调用，计划书 4.6）。</summary>
    public void SetChargeSlow(bool on)
    {
        chargeSlowing = on;
    }

    /// <summary>
    /// 尝试闪避。条件：非死亡、不在冲刺中、冷却结束、体力足够。
    /// 方向 = 当前移动输入方向；无输入时使用瞄准方向。
    /// </summary>
    public void TryDash()
    {
        if (health.IsDead || isDashing) return;
        if (dashCooldownTimer > 0f) return;
        if (!stats.TryConsumeStamina(dashStaminaCost)) return;

        Vector2 dir = moveInput.sqrMagnitude > 0.0001f
            ? moveInput.normalized
            : (aim != null ? aim.AimDirection : Vector2.right);

        BeginDash(dir);
    }

    /// <summary>立即停止所有移动（供死亡/失活/Respawn 调用）。</summary>
    public void StopImmediately()
    {
        if (isDashing) EndDash();
        moveInput = Vector2.zero;
        sprintHeld = false;
        rb.linearVelocity = Vector2.zero;
    }

    // ========== 闪避内部流程 ==========

    private void BeginDash(Vector2 dir)
    {
        isDashing = true;
        dashDirection = dir;
        dashTimer = dashDuration;
        afterimagesSpawned = 0;

        // 冲刺期间无敌
        health.SetInvincible(true);

        // 角色半透明
        if (sr != null)
        {
            colorBeforeDash = sr.color;
            sr.color = new Color(colorBeforeDash.r, colorBeforeDash.g, colorBeforeDash.b, dashAlpha);
        }
    }

    private void EndDash()
    {
        isDashing = false;
        health.SetInvincible(false);

        // 冲刺动作完整结束后才进入冷却
        dashCooldownTimer = dashCooldown;

        // 恢复透明度（死亡时由 PlayerController 的变灰逻辑接管，不覆盖）
        if (sr != null && !health.IsDead)
            sr.color = colorBeforeDash;
    }

    /// <summary>冲刺过程中按时间均匀生成残影。</summary>
    private void TickAfterimages()
    {
        float interval = dashDuration / (afterimageCount + 1);
        float elapsed = dashDuration - dashTimer;
        while (afterimagesSpawned < afterimageCount && elapsed >= interval * (afterimagesSpawned + 1))
        {
            SpawnAfterimage();
            afterimagesSpawned++;
        }
    }

    /// <summary>生成一个残影：复制当前 sprite 的临时 GameObject，DOTween 淡出后销毁。</summary>
    private void SpawnAfterimage()
    {
        if (sr == null || sr.sprite == null) return;

        GameObject ghost = new GameObject("DashAfterimage");
        ghost.transform.position = transform.position;
        ghost.transform.rotation = transform.rotation;
        ghost.transform.localScale = transform.localScale;

        SpriteRenderer ghostSr = ghost.AddComponent<SpriteRenderer>();
        ghostSr.sprite = sr.sprite;
        ghostSr.color = new Color(colorBeforeDash.r, colorBeforeDash.g, colorBeforeDash.b, dashAlpha);
        ghostSr.sortingLayerID = sr.sortingLayerID;
        ghostSr.sortingOrder = sr.sortingOrder - 1;

        ghostSr.DOFade(0f, 0.3f)
            .SetLink(ghost)   // 目标销毁时自动 kill，避免 DOTween safe mode 报 missing target
            .OnComplete(() => Destroy(ghost));
    }

    void OnDisable()
    {
        // 失活时清理冲刺状态，避免重启用后残留无敌/半透明
        if (isDashing) EndDash();
    }
}
