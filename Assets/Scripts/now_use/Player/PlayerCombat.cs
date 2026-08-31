using UnityEngine;
using DG.Tweening;

/// <summary>
/// 玩家攻击管理。
/// 职责：维护 Windup / Active / Recovery 阶段；调用 WeaponAnimator 播放动画；
/// Active 阶段驱动 WeaponHitbox 做武器矩形命中检测。
/// 不控制 WeaponPivot，不读取鼠标输入，不管理武器视觉。
/// v0.6.3：扩展为三模式（近战 / 远程 / 自身施法）+ 蓄力状态机 + 弹夹换弹计时。
/// 默认近战（weapon == null）行为与 v0.6.2 前完全一致。
/// v0.7.6：攻击开始（StartWindup）通知 FrameAnimator 播攻击序列帧（组缺失零干预）。
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("攻击配置")]
    [SerializeField] private AttackData attackData;

    [Header("组件引用")]
    [SerializeField] private PlayerAimController aimController;
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private WeaponAnimator weaponAnimator;
    [SerializeField] private AttackIndicator attackIndicator;
    [SerializeField] private WeaponHitbox weaponHitbox;

    public System.Action OnAttackStart;
    public System.Action OnAttackEnd;

    // ===== v0.6.3：弹药/换弹/武器展示事件（AmmoUI 订阅）=====
    /// <summary>弹药变化（current, max）。max = 0 表示无弹夹，UI 隐藏。</summary>
    public System.Action<int, int> OnAmmoChanged;
    /// <summary>换弹进度（remaining, total）。换弹结束发一次 (0, total)。</summary>
    public System.Action<float, float> OnReloadProgress;
    /// <summary>武器展示变化（武器名, 染色）。name 为 null/空表示默认近战，UI 隐藏。</summary>
    public System.Action<string, Color> OnWeaponDisplayChanged;

    private enum SubPhase { None, Windup, Active, Recovery }
    private SubPhase subPhase = SubPhase.None;
    private float windupTimer;
    private float activeTimer;
    private float recoveryTimer;
    private bool activeMomentTriggered;
    private Vector2 attackDirection;

    // ===== v0.6.3：三模式与武器实例 =====
    private enum CombatMode { Melee, Ranged, SelfCast }
    private CombatMode mode = CombatMode.Melee;
    private WeaponInstance weapon;              // null = 默认近战（现状）
    private AttackData meleeRuntimeCopy;        // 近战装备武器的 AttackData 运行时副本（磁盘资产不动）
    private float meleeBaseRange;               // 副本基准值缓存（蓄力还原用）
    private float meleeBaseAngle;

    // ===== v0.6.3：蓄力状态 =====
    private bool isCharging;
    private float chargeTimer;
    private bool attackHeld;                    // 连弩连发用
    private SpriteRenderer[] chargeGlowRenderers;   // 手持视觉 "Effect" 部件（蓄力发光）
    private Color[] chargeGlowColors;               // Effect 部件原色缓存

    // ===== v0.6.3：远程/换弹计时 =====
    private float fireCooldownTimer;
    private float idleTimer;                    // 闲置自动换弹
    private bool reloadEventActive;
    private float reloadTotal;

    // ===== v0.6.3：近战范围显示（灰色实时扇形/矩形）与戳击 =====
    [Header("近战范围显示（v0.6.3）")]
    [SerializeField] private Color rangeDisplayColor = new Color(0.75f, 0.75f, 0.75f, 0.35f);
    [Tooltip("枪矛戳击：Windup 后拉距离（pivot 局部单位）")]
    [SerializeField] private float thrustPullBack = 0.45f;
    [Tooltip("枪矛戳击：Active 前冲幅度（pivot 局部单位）")]
    [SerializeField] private float thrustTravel = 0.5f;
    [Tooltip("蓄力触发阈值（秒）：按住攻击键超过该时长才进入蓄力；之前松开 = 普通攻击/直接射击")]
    [SerializeField] private float chargeHoldThreshold = 0.2f;
    [Tooltip("近战满蓄后自动释放延迟（秒）：满蓄后继续握住超过该时长自动出手，0 = 不自动释放（仅近战，弓箭不受限）")]
    [SerializeField] private float chargeAutoReleaseDelay = 1f;
    private bool rangeDisplayActive;
    private bool isThrustAttack;                // 当前挥击是否为戳击（Thrust 动画类型）
    private bool pendingCharge;                 // 已按下、等待超过阈值进入蓄力
    private float pressHoldTimer;
    private float overchargeTimer;              // 满蓄后的超时计时（自动释放用）

    // 组件缓存（Awake GetComponent）
    private PlayerMovement playerMovement;
    private Health health;
    private BuffManager buffManager;    // v0.7.5：延迟缓存（SkillExecutor.Awake 运行时补挂，Awake 顺序不定）
    private FrameAnimator frameAnimator; // v0.7.6：延迟缓存（PlayerController.Awake 运行时补挂，同 buffManager 模式）

    // v0.7.5 攻速倍率：本次挥击实际前摇/判定时长（= 配置值 ÷ 攻速倍率），戳击进度比值同源使用
    private float windupDuration;
    private float activeDuration;

    /// <summary>是否正在蓄力（v0.6.3）。</summary>
    public bool IsCharging => isCharging;

    /// <summary>攻速倍率（v0.7.5 Buff 通道）：攻击间隔 ÷ 此值；无 BuffManager / 无 buff 返回 1，零行为差异。</summary>
    private float AttackSpeedMul()
    {
        if (buffManager == null) buffManager = GetComponent<BuffManager>();
        return buffManager != null ? Mathf.Max(0.01f, buffManager.AttackSpeedMultiplier) : 1f;
    }

    void Awake()
    {
        if (attackData == null)
            Debug.LogWarning("[PlayerCombat] 未配置 AttackData，攻击无法执行。");

        if (aimController == null)
            aimController = GetComponent<PlayerAimController>();

        if (weaponController == null)
            weaponController = GetComponent<WeaponController>();

        if (weaponAnimator == null)
            weaponAnimator = GetComponentInChildren<WeaponAnimator>(true);

        if (attackIndicator == null)
            attackIndicator = GetComponentInChildren<AttackIndicator>(true);

        // v0.6.3：Player prefab 上没有 AttackIndicator（旧指示器代码因此从未生效）——运行时补建
        if (attackIndicator == null)
        {
            GameObject indicatorGo = new GameObject("AttackIndicator");
            indicatorGo.transform.SetParent(transform, false);
            attackIndicator = indicatorGo.AddComponent<AttackIndicator>();
        }

        if (weaponHitbox == null)
            weaponHitbox = GetComponent<WeaponHitbox>();

        playerMovement = GetComponent<PlayerMovement>();
        health = GetComponent<Health>();

        // v0.6.3：玩家近战范围显示跟随玩家（敌人预警仍用脱离父物体模式）
        if (attackIndicator != null)
            attackIndicator.detachOnShow = false;
    }

    void Update()
    {
        // 死亡时取消蓄力/待蓄力（不发攻击，纯复位）
        if (health != null && health.IsDead)
        {
            pendingCharge = false;
            if (isCharging)
                EndCharge();
        }

        UpdateAiming();
        UpdatePendingCharge();
        UpdateAttackState();
        UpdateCharge();
        UpdateRanged();
        UpdateReload();
    }

    void OnDisable()
    {
        // 失活时取消蓄力状态（不发攻击，纯复位），避免重启用后残留减速/发光
        pendingCharge = false;
        if (isCharging)
            EndCharge();
    }

    /// <summary>待蓄力计时：按住攻击键超过 chargeHoldThreshold 才真正进入蓄力（v0.6.3 点按/长按分离）。</summary>
    private void UpdatePendingCharge()
    {
        if (!pendingCharge || isCharging || !attackHeld) return;

        pressHoldTimer += Time.deltaTime;
        if (pressHoldTimer >= chargeHoldThreshold)
        {
            pendingCharge = false;
            BeginCharge();
        }
    }

    /// <summary>
    /// 普通状态下持续更新武器朝向。
    /// 攻击期间方向已由 WeaponController 锁定，不再更新。
    /// </summary>
    private void UpdateAiming()
    {
        if (subPhase != SubPhase.None) return;
        if (aimController == null || weaponController == null) return;

        weaponController.SetAimDirection(aimController.AimDirection);
    }

    /// <summary>
    /// 尝试发起一次攻击。若已在攻击流程中或冷却未好则忽略。
    /// </summary>
    public void TryAttack()
    {
        if (attackData == null) return;
        if (subPhase != SubPhase.None) return;

        StartWindup();
    }

    /// <summary>
    /// 是否正在攻击流程中。
    /// </summary>
    public bool IsAttacking => subPhase != SubPhase.None;

    // ============================================================
    // v0.6.3：输入 API（PlayerController 按 started/canceled 转发）
    // ============================================================

    /// <summary>
    /// 攻击键按下。按当前模式分发：可蓄力武器先进入"待蓄力"（按住超过 chargeHoldThreshold 才真正蓄力，
    /// 之前松开 = 普通攻击/直接射击）；无蓄力武器立即出手。
    /// </summary>
    public void OnAttackPressed()
    {
        attackHeld = true;   // 所有模式都记录按住状态（连弩连发用）

        switch (mode)
        {
            case CombatMode.Melee:
                if (weapon != null && weapon.Data != null && weapon.Data.ChargeRule != ChargeRule.None)
                {
                    // 可蓄力近战（长剑/长枪）：非攻击中才进入待蓄力，长按过阈值才 BeginCharge
                    if (subPhase == SubPhase.None && !isCharging)
                    {
                        pendingCharge = true;
                        pressHoldTimer = 0f;
                    }
                }
                else
                {
                    // 默认近战 / 无蓄力规则：行为与 v0.6.2 前一致
                    TryAttack();
                }
                break;

            case CombatMode.Ranged:
                if (weapon != null && weapon.Data != null && weapon.Data.ChargeRule == ChargeRule.ProjectileBoost)
                {
                    // 弓箭：待蓄力（点按直射，长按蓄力）
                    if (!isCharging)
                    {
                        pendingCharge = true;
                        pressHoldTimer = 0f;
                    }
                }
                else
                {
                    TryFire();       // 连弩/能量法杖：直接开火
                }
                break;

            case CombatMode.SelfCast:
                TrySelfCast();
                break;
        }
    }

    /// <summary>
    /// 攻击键松开。待蓄力中松开 = 点按（普通攻击/直接射击）；蓄力中松开 = 释放蓄力。
    /// </summary>
    public void OnAttackReleased()
    {
        attackHeld = false;

        if (pendingCharge && !isCharging)
        {
            // 点按：未到蓄力阈值，按无蓄力处理
            pendingCharge = false;
            if (mode == CombatMode.Melee)
                TryAttack();
            else if (mode == CombatMode.Ranged)
                TryFire();
            return;
        }

        pendingCharge = false;
        if (isCharging)
            ReleaseCharge();
    }

    // ============================================================
    // v0.6.3：模式入口（WeaponBehavior.Apply 分发调用）
    // ============================================================

    /// <summary>
    /// 装备近战武器（v0.6.3）：AttackData 运行时副本接入三件套链路 + 蓄力规则 + 手持视觉。
    /// </summary>
    public void SetMeleeWeapon(WeaponInstance inst)
    {
        if (inst == null || inst.Data == null) return;

        ResetCombatState();
        mode = CombatMode.Melee;
        weapon = inst;

        // 运行时副本：蓄力缩放只作用于副本，磁盘资产零改动（卸下时销毁）
        DestroyMeleeRuntimeCopy();
        if (inst.Data.AttackData != null)
        {
            meleeRuntimeCopy = inst.Data.AttackData.CreateRuntimeCopy();
            meleeBaseRange = meleeRuntimeCopy.AttackRange;
            meleeBaseAngle = meleeRuntimeCopy.AttackAngle;
            ApplyAttackDataToChain(meleeRuntimeCopy);
        }

        MountWeaponVisual(inst.Data);
        EmitWeaponDisplay(inst.Data);
        OnAmmoChanged?.Invoke(inst.CurrentClip, inst.Data.ClipSize);   // 近战 clipSize=0 → (0,0)，UI 隐藏
    }

    /// <summary>
    /// 装备远程武器（v0.6.3）：Projectile.Launch 开火，不碰近战 attackData 链。
    /// </summary>
    public void SetRangedWeapon(WeaponInstance inst)
    {
        if (inst == null || inst.Data == null) return;

        ResetCombatState();
        mode = CombatMode.Ranged;
        weapon = inst;
        DestroyMeleeRuntimeCopy();   // 远程不用近战判定，丢弃近战副本

        MountWeaponVisual(inst.Data);
        EmitWeaponDisplay(inst.Data);
        OnAmmoChanged?.Invoke(inst.CurrentClip, inst.Data.ClipSize);
    }

    /// <summary>
    /// 装备自身施法武器（v0.6.3：治疗法杖）：Heal + 绿环特效 + 弹夹/换弹。
    /// </summary>
    public void SetSelfCastWeapon(WeaponInstance inst)
    {
        if (inst == null || inst.Data == null) return;

        ResetCombatState();
        mode = CombatMode.SelfCast;
        weapon = inst;
        DestroyMeleeRuntimeCopy();

        MountWeaponVisual(inst.Data);
        EmitWeaponDisplay(inst.Data);
        OnAmmoChanged?.Invoke(inst.CurrentClip, inst.Data.ClipSize);
    }

    /// <summary>
    /// 外部切换攻击配置（PlayerWeaponHolder.Unequip 卸下武器时调用）。
    /// v0.6.3：重置为默认近战——清模式/清实例/销毁运行时副本/还原视觉与宽度/发 UI 隐藏事件。
    /// </summary>
    public void SetAttackData(AttackData data)
    {
        ResetCombatState();
        mode = CombatMode.Melee;
        weapon = null;
        DestroyMeleeRuntimeCopy();

        if (weaponController != null)
            weaponController.ClearCustomVisual();
        chargeGlowRenderers = null;
        chargeGlowColors = null;

        ApplyAttackDataToChain(data);

        OnAmmoChanged?.Invoke(0, 0);
        OnWeaponDisplayChanged?.Invoke(null, Color.white);
    }

    /// <summary>当前攻击配置（v0.6.2：PlayerWeaponHolder 缓存默认近战用）。</summary>
    public AttackData CurrentAttackData => attackData;

    /// <summary>
    /// 换装时把攻击数据同步到三件套链路：自身 + WeaponHitbox + WeaponController（v0.6.3 视觉长度同源）。
    /// </summary>
    private void ApplyAttackDataToChain(AttackData data)
    {
        attackData = data;
        if (weaponHitbox != null) weaponHitbox.SetAttackData(data);
        if (weaponController != null) weaponController.SetAttackData(data);
    }

    /// <summary>
    /// 模式切换公共复位：取消蓄力、清换弹/冷却/闲置计时、还原宽度倍率、收范围显示与视觉状态。
    /// </summary>
    private void ResetCombatState()
    {
        if (isCharging)
            EndCharge();

        attackHeld = false;
        pendingCharge = false;
        fireCooldownTimer = 0f;
        idleTimer = 0f;
        isThrustAttack = false;
        if (reloadEventActive)
        {
            reloadEventActive = false;
            OnReloadProgress?.Invoke(0f, reloadTotal);   // 中断换弹：通知 UI 收起进度条
        }

        HideRangeDisplay();
        if (weaponController != null)
        {
            weaponController.SetWidthMultiplier(1f);
            weaponController.SetCustomVisualScale(1f, 1f);
            weaponController.SetCustomVisualThrustOffset(0f);
        }
        if (weaponHitbox != null)
            weaponHitbox.LengthMultiplier = 1f;
    }

    private void DestroyMeleeRuntimeCopy()
    {
        if (meleeRuntimeCopy != null)
        {
            Destroy(meleeRuntimeCopy);
            meleeRuntimeCopy = null;
        }
    }

    /// <summary>
    /// 挂载程序化手持视觉（WeaponVisualBuilder 构建），并缓存 "Effect" 蓄力发光部件及其原色。
    /// </summary>
    private void MountWeaponVisual(WeaponData data)
    {
        chargeGlowRenderers = null;
        chargeGlowColors = null;
        if (weaponController == null || data == null) return;

        GameObject visual = WeaponVisualBuilder.BuildHeldVisual(data);
        if (visual == null) return;
        weaponController.SetCustomVisual(visual);

        Transform effect = visual.transform.Find("Effect");
        if (effect != null)
        {
            chargeGlowRenderers = effect.GetComponentsInChildren<SpriteRenderer>(true);
            chargeGlowColors = new Color[chargeGlowRenderers.Length];
            for (int i = 0; i < chargeGlowRenderers.Length; i++)
                chargeGlowColors[i] = chargeGlowRenderers[i].color;
        }
    }

    private void EmitWeaponDisplay(WeaponData data)
    {
        OnWeaponDisplayChanged?.Invoke(data != null ? data.DisplayName : null,
                                       data != null ? data.WeaponColor : Color.white);
    }

    // ============================================================
    // v0.6.3：近战范围显示（灰色实时扇形/矩形，与判定同源）
    // ============================================================

    /// <summary>
    /// 范围显示缩放换算：指示器挂在玩家根下（根缩放 0.6），而需求是世界单位 = 数据 × pivot lossyScale。
    /// 返回值 × 数据范围 = 指示器局部尺寸（经父级缩放渲染后恰为世界尺寸）。
    /// </summary>
    private float RangeDisplayScale()
    {
        float pivotScale = 1f;
        if (weaponController != null && weaponController.WeaponPivot != null)
            pivotScale = weaponController.WeaponPivot.lossyScale.x;
        float indicatorScale = attackIndicator != null ? attackIndicator.transform.lossyScale.x : 1f;
        return indicatorScale > 0.001f ? pivotScale / indicatorScale : pivotScale;
    }

    private Vector2 CurrentAimDirection()
    {
        return aimController != null ? aimController.AimDirection : Vector2.right;
    }

    /// <summary>显示/刷新灰色扇形范围（刀等扇形近战）。dir 为空时取实时瞄准方向。</summary>
    private void ShowFanRangeDisplay(float range, float angle, Vector2? dir = null)
    {
        if (attackIndicator == null) return;
        attackIndicator.SetColor(rangeDisplayColor);
        attackIndicator.SetShape(AttackIndicator.ShapeType.Sector);
        attackIndicator.SetRadius(range * RangeDisplayScale());
        attackIndicator.SetAngle(angle);
        attackIndicator.SetDirection(dir ?? CurrentAimDirection());
        attackIndicator.Show();
        rangeDisplayActive = true;
    }

    /// <summary>显示/刷新灰色矩形范围（枪矛戳击，长=当前判定长度、宽=武器宽度）。</summary>
    private void ShowThrustRangeDisplay(float length, Vector2? dir = null)
    {
        if (attackIndicator == null) return;
        attackIndicator.SetColor(rangeDisplayColor);
        float scale = RangeDisplayScale();
        float width = (weaponController != null ? weaponController.WeaponWidth : 0.15f) * scale;
        attackIndicator.SetDirection(dir ?? CurrentAimDirection());
        attackIndicator.SetBox(length * scale, width);
        attackIndicator.Show();
        rangeDisplayActive = true;
    }

    private void HideRangeDisplay()
    {
        if (!rangeDisplayActive) return;
        rangeDisplayActive = false;
        attackIndicator?.Hide();
    }

    // ============================================================
    // v0.6.3：蓄力状态机（计划书 4.6；v0.7.0 起闪避已下线，本状态机无外部打断方）
    // ============================================================

    private void BeginCharge()
    {
        if (isCharging) return;
        if (weapon == null || weapon.Data == null) return;
        if (weapon.IsReloading) return;   // 换弹中禁止蓄力

        isCharging = true;
        chargeTimer = 0f;
        if (playerMovement != null)
            playerMovement.SetChargeSlow(true);   // 蓄力移动 ×0.5（计划书 4.6）
    }

    private void UpdateCharge()
    {
        if (!isCharging) return;

        chargeTimer += Time.deltaTime;
        float t = CurrentChargeT();
        ApplyChargeGlow(t);   // Effect 部件随蓄力等级 lerp → 白

        // 近战满蓄超时自动释放（v0.6.3 用户追加，仅近战；弓箭 ProjectileBoost 不受限）
        if (mode == CombatMode.Melee && chargeAutoReleaseDelay > 0f && t >= 1f)
        {
            overchargeTimer += Time.deltaTime;
            if (overchargeTimer >= chargeAutoReleaseDelay)
            {
                overchargeTimer = 0f;
                ReleaseCharge();   // 等同松手：满蓄伤害 ×ChargeFullDamageMul 照常生效
                return;
            }
        }
        else
        {
            overchargeTimer = 0f;
        }

        // 近战蓄力：范围显示随蓄力等级实时变化 + 武器模型长度/宽度同步适配（计划书 §四）
        if (mode == CombatMode.Melee && weapon != null && weapon.Data != null && meleeRuntimeCopy != null)
        {
            if (weapon.Data.ChargeRule == ChargeRule.FanScale)
            {
                float rangeMul = Mathf.Lerp(1f, weapon.Data.ChargeRangeMul, t);
                float angleMul = Mathf.Lerp(1f, weapon.Data.ChargeAngleMul, t);
                ShowFanRangeDisplay(meleeBaseRange * rangeMul, meleeBaseAngle * angleMul);
                if (weaponController != null)
                    weaponController.SetCustomVisualScale(1f, rangeMul);   // 模型长度适配扇形半径
            }
            else if (weapon.Data.ChargeRule == ChargeRule.RectScale)
            {
                float lengthMul = Mathf.Lerp(1f, weapon.Data.ChargeLengthMul, t);
                float widthMul = Mathf.Lerp(1f, weapon.Data.ChargeWidthMul, t);
                ShowThrustRangeDisplay(meleeBaseRange * lengthMul);
                if (weaponController != null)
                    weaponController.SetCustomVisualScale(widthMul, lengthMul);
            }
        }
    }

    private float CurrentChargeT()
    {
        if (weapon == null || weapon.Data == null) return 0f;
        float max = weapon.Data.ChargeMaxTime;
        return max > 0f ? Mathf.Clamp01(chargeTimer / max) : 1f;
    }

    private void ReleaseCharge()
    {
        if (weapon == null || weapon.Data == null)
        {
            EndCharge();
            return;
        }

        float t = CurrentChargeT();

        // 近战满蓄（v0.7.0 蓄力倍率归位）：倍率走 WeaponHitbox.DamageMultiplier（挥击结束随 BeginSwing 复位），
        // 不再 SetDamage 改副本——倍率区独立于基础攻击，避免角色攻击力被一起放大（伤害计算公式文档 §2.1）
        if (t >= 0.999f && weaponHitbox != null
            && (weapon.Data.ChargeRule == ChargeRule.FanScale || weapon.Data.ChargeRule == ChargeRule.RectScale))
            weaponHitbox.DamageMultiplier = weapon.Data.ChargeFullDamageMul;

        switch (weapon.Data.ChargeRule)
        {
            case ChargeRule.FanScale:
                // 刀：范围/角度 ×(1→chargeRangeMul/chargeAngleMul)，只改运行时副本
                if (meleeRuntimeCopy != null)
                    meleeRuntimeCopy.SetRangeAngle(
                        meleeBaseRange * Mathf.Lerp(1f, weapon.Data.ChargeRangeMul, t),
                        meleeBaseAngle * Mathf.Lerp(1f, weapon.Data.ChargeAngleMul, t));
                TryAttack();
                break;

            case ChargeRule.RectScale:
                // 枪矛：长度 ×(1→chargeLengthMul)（副本）+ 宽度 ×(1→chargeWidthMul)（WeaponController 倍率）
                if (meleeRuntimeCopy != null)
                    meleeRuntimeCopy.SetRangeAngle(
                        meleeBaseRange * Mathf.Lerp(1f, weapon.Data.ChargeLengthMul, t),
                        meleeBaseAngle);
                if (weaponController != null)
                    weaponController.SetWidthMultiplier(Mathf.Lerp(1f, weapon.Data.ChargeWidthMul, t));
                TryAttack();
                break;

            case ChargeRule.ProjectileBoost:
                // 弓箭：伤害/弹速 ×(1→chargeDamageMul/chargeSpeedMul)，Launch 参数，不动数据
                TryFire(Mathf.Lerp(1f, weapon.Data.ChargeDamageMul, t),
                        Mathf.Lerp(1f, weapon.Data.ChargeSpeedMul, t));
                break;
        }

        EndCharge(true);   // 释放转攻击：范围显示与放大视觉保留到挥击结束（RestoreMeleeChargeBase 还原）
    }

    /// <summary>
    /// 结束蓄力：清状态、解除减速、发光熄灭。
    /// keepRangeDisplay = false（取消/死亡/换装）时收起范围显示并还原视觉缩放；
    /// true（释放转攻击）时保留，由挥击结束处的 RestoreMeleeChargeBase 统一还原。
    /// </summary>
    private void EndCharge(bool keepRangeDisplay = false)
    {
        isCharging = false;
        chargeTimer = 0f;
        overchargeTimer = 0f;
        if (playerMovement != null)
            playerMovement.SetChargeSlow(false);
        RestoreChargeGlow();

        if (!keepRangeDisplay)
        {
            HideRangeDisplay();
            if (weaponController != null)
                weaponController.SetCustomVisualScale(1f, 1f);
        }
    }

    private void ApplyChargeGlow(float t)
    {
        if (chargeGlowRenderers == null) return;
        for (int i = 0; i < chargeGlowRenderers.Length; i++)
            if (chargeGlowRenderers[i] != null)
                chargeGlowRenderers[i].color = Color.Lerp(chargeGlowColors[i], Color.white, t);
    }

    private void RestoreChargeGlow()
    {
        if (chargeGlowRenderers == null) return;
        for (int i = 0; i < chargeGlowRenderers.Length; i++)
            if (chargeGlowRenderers[i] != null)
                chargeGlowRenderers[i].color = chargeGlowColors[i];
    }

    /// <summary>近战挥击结束后（subPhase 回 None）：副本参数/宽度倍率/视觉缩放/戳击位移还原基准值；伤害倍率随下次 BeginSwing 复位（v0.7.0）。</summary>
    private void RestoreMeleeChargeBase()
    {
        if (meleeRuntimeCopy != null)
        {
            meleeRuntimeCopy.SetRangeAngle(meleeBaseRange, meleeBaseAngle);
        }
        if (weaponController != null)
        {
            weaponController.SetWidthMultiplier(1f);
            weaponController.SetCustomVisualScale(1f, 1f);
            weaponController.SetCustomVisualThrustOffset(0f);
        }
        if (weaponHitbox != null)
            weaponHitbox.LengthMultiplier = 1f;
    }

    // ============================================================
    // v0.6.3：远程开火 / 弹夹 / 换弹
    // ============================================================

    /// <summary>
    /// 尝试发射一次子弹。验射击冷却 / 换弹中 / 弹药，弹空自动换弹。
    /// </summary>
    private void TryFire(float damageMul = 1f, float speedMul = 1f)
    {
        if (weapon == null || weapon.Data == null) return;
        if (weapon.Data.ProjectileData == null) return;
        if (fireCooldownTimer > 0f) return;
        if (weapon.IsReloading) return;   // 换弹中禁止射击
        if (!weapon.TryConsumeAmmo())
        {
            BeginReload();   // 弹空：自动换弹
            return;
        }

        Vector2 aimDir = aimController != null
            ? aimController.AimDirection
            : (weaponController != null ? weaponController.GetAimDirection() : Vector2.right);

        // 枪口 = WeaponPivot + aimDir×0.6；weaponPivot 未接线（老 prefab）时退化为角色位置
        Transform pivot = weaponController != null ? weaponController.WeaponPivot : null;
        Vector2 origin = (pivot != null ? (Vector2)pivot.position : (Vector2)transform.position) + aimDir * 0.6f;

        Projectile.Launch(weapon.Data.ProjectileData, origin, aimDir, gameObject, damageMul, speedMul);

        // 弓箭等 FireInterval=0 的单发：冷却下限 0.05s，射速主要由装填限制
        // v0.7.5：射击间隔 ÷ 攻速倍率（Buff 通道）
        fireCooldownTimer = Mathf.Max(weapon.Data.FireInterval, 0.05f) / AttackSpeedMul();
        idleTimer = 0f;
        OnAmmoChanged?.Invoke(weapon.CurrentClip, weapon.Data.ClipSize);

        if (weapon.Data.ClipSize > 0 && weapon.CurrentClip == 0)
            BeginReload();
    }

    /// <summary>
    /// 远程每帧：冷却递减、连弩长按连发、闲置自动换弹。
    /// </summary>
    private void UpdateRanged()
    {
        if (fireCooldownTimer > 0f)
            fireCooldownTimer -= Time.deltaTime;

        if (mode != CombatMode.Ranged || weapon == null || weapon.Data == null) return;

        // 连弩连发：按住期间每到 fireInterval 自动开火
        // （限无蓄力规则的远程：弓箭是 ProjectileBoost，否则待蓄力窗口内连发会抢射打空弹夹，
        //   到点 BeginCharge 因装填中返回 → 蓄力"消失"，v0.6.3 补丁 5）
        if (attackHeld && !isCharging && !pendingCharge
            && weapon.Data.ChargeRule == ChargeRule.None
            && weapon.Data.FireInterval > 0f && fireCooldownTimer <= 0f)
            TryFire();

        // 闲置自动换弹（连弩 autoReloadIdleTime）：未开火闲置到点且弹夹不满 → 自动换弹
        if (weapon.Data.AutoReloadIdleTime > 0f
            && weapon.Data.ClipSize > 0
            && weapon.CurrentClip < weapon.Data.ClipSize
            && !weapon.IsReloading)
        {
            idleTimer += Time.deltaTime;
            if (idleTimer >= weapon.Data.AutoReloadIdleTime)
                BeginReload();
        }
        else
        {
            idleTimer = 0f;
        }
    }

    private void BeginReload()
    {
        if (weapon == null || weapon.Data == null) return;
        if (weapon.Data.ClipSize <= 0 || weapon.Data.ReloadTime <= 0f) return;
        if (weapon.IsReloading) return;

        weapon.ReloadTimer = weapon.Data.ReloadTime;
        reloadTotal = weapon.Data.ReloadTime;
        reloadEventActive = true;
    }

    /// <summary>换弹计时（ReloadTimer 归 WeaponInstance 持有，本类每帧递减并广播进度）。</summary>
    private void UpdateReload()
    {
        if (weapon == null || !weapon.IsReloading) return;

        weapon.ReloadTimer -= Time.deltaTime;
        if (reloadEventActive)
            OnReloadProgress?.Invoke(Mathf.Max(weapon.ReloadTimer, 0f), reloadTotal);

        if (weapon.ReloadTimer <= 0f)
        {
            weapon.FinishReload();
            reloadEventActive = false;
            OnReloadProgress?.Invoke(0f, reloadTotal);   // 结束发一次 (0,total)
            OnAmmoChanged?.Invoke(weapon.CurrentClip, weapon.Data.ClipSize);
        }
    }

    // ============================================================
    // v0.6.3：自身施法（治疗法杖）
    // ============================================================

    /// <summary>
    /// 自身施法：验弹药 → Heal + 绿环特效 → 弹空自动换弹。无攻击判定，不动三阶段状态机。
    /// </summary>
    private void TrySelfCast()
    {
        if (weapon == null || weapon.Data == null) return;
        if (weapon.IsReloading) return;   // 换弹中禁止施法
        if (!weapon.TryConsumeAmmo())
        {
            BeginReload();
            return;
        }

        if (health != null)
            health.Heal(weapon.Data.HealAmount);

        SpawnHealRing();

        idleTimer = 0f;
        OnAmmoChanged?.Invoke(weapon.CurrentClip, weapon.Data.ClipSize);

        if (weapon.Data.ClipSize > 0 && weapon.CurrentClip == 0)
            BeginReload();
    }

    /// <summary>治疗绿环特效：翠绿 #2ECC71 低透明圆环，0.4s 缩放 0.5→1.2 + 淡出销毁。</summary>
    private void SpawnHealRing()
    {
        Sprite circle = ProjectileVisualBuilder.GetCircleSprite();
        if (circle == null) return;

        GameObject ring = new GameObject("HealRing");
        ring.transform.position = transform.position;
        ring.transform.localScale = Vector3.one * 0.5f;

        SpriteRenderer sr = ring.AddComponent<SpriteRenderer>();
        sr.sprite = circle;
        sr.color = new Color(0.1804f, 0.8f, 0.4431f, 0.5f);   // #2ECC71 低透明
        sr.sortingOrder = 10;

        ring.transform.DOScale(1.2f, 0.4f).SetLink(ring);
        sr.DOFade(0f, 0.4f)
            .SetLink(ring)   // 目标销毁时自动 kill，避免 DOTween safe mode 报 missing target
            .OnComplete(() => Destroy(ring));
    }

    // ============================================================
    // 近战三阶段状态机（逻辑与 v0.6.2 前一致）
    // ============================================================

    private void StartWindup()
    {
        subPhase = SubPhase.Windup;
        windupDuration = attackData.WindupTime / AttackSpeedMul();   // v0.7.5：前摇 ÷ 攻速倍率
        windupTimer = windupDuration;
        activeMomentTriggered = false;

        // v0.6.3：戳击类武器（枪矛）走活塞动画，不走 WeaponAnimator 旋转挥击
        isThrustAttack = mode == CombatMode.Melee
            && attackData.AnimationType == AttackAnimationType.Thrust;

        // 锁定当前攻击方向，由 WeaponController 负责管理 WeaponPivot
        weaponController?.LockAttackDirection();

        // 缓存攻击方向，防止攻击期间鼠标移动导致判定方向与动画方向不一致
        attackDirection = weaponController != null
            ? weaponController.GetAimDirection()
            : aimController != null ? aimController.AimDirection : Vector2.right;

        OnAttackStart?.Invoke();

        // v0.7.6 美术线：通知 FrameAnimator 播攻击序列帧（attack_sword/attack_spear 组）。
        // 时长 = 三阶段合计 ÷ 攻速倍率，fps 由 FrameAnimator 按帧数自动对齐；
        // 组缺失时 PlayAttack 返回 false 完全不干预（WeaponAnimator 挥砍照旧，零回归）。
        if (frameAnimator == null) frameAnimator = GetComponent<FrameAnimator>();
        if (frameAnimator != null)
        {
            bool isSpear = weapon != null && weapon.Data != null
                && weapon.Data.ChargeRule == ChargeRule.RectScale;   // FanScale=剑、RectScale=枪
            float totalDuration = (attackData.WindupTime + attackData.ActiveDuration + attackData.RecoveryTime)
                / AttackSpeedMul();
            frameAnimator.PlayAttack(isSpear, totalDuration);
        }

        // v0.6.3：灰色实时范围显示（蓄力释放进挥击时数值已是缩放后的副本值，重刷等于保持）
        if (attackIndicator != null)
        {
            if (isThrustAttack)
                ShowThrustRangeDisplay(attackData.AttackRange, attackDirection);
            else
                ShowFanRangeDisplay(attackData.AttackRange, attackData.AttackAngle, attackDirection);
        }
    }

    private void UpdateAttackState()
    {
        switch (subPhase)
        {
            case SubPhase.Windup:
                UpdateWindup();
                break;
            case SubPhase.Active:
                UpdateActive();
                break;
            case SubPhase.Recovery:
                UpdateRecovery();
                break;
        }
    }

    private void UpdateWindup()
    {
        windupTimer -= Time.deltaTime;

        // 戳击（v0.6.3）：Windup 阶段武器逐帧后拉，为前冲蓄势
        if (isThrustAttack && weaponController != null && windupDuration > 0.001f)
        {
            float pull = 1f - Mathf.Max(windupTimer, 0f) / windupDuration;
            weaponController.SetCustomVisualThrustOffset(-thrustPullBack * pull);
        }

        if (windupTimer <= 0f)
            EnterActive();
    }

    private void EnterActive()
    {
        subPhase = SubPhase.Active;
        activeDuration = attackData.ActiveDuration / AttackSpeedMul();   // v0.7.5：判定/动画时长 ÷ 攻速倍率
        activeTimer = activeDuration;
        activeMomentTriggered = false;

        // Active 开始，武器矩形检测同步启动
        weaponHitbox?.BeginSwing();

        if (isThrustAttack)
        {
            // 戳击（v0.6.3）：不播放旋转挥击动画，活塞位移与判定门控在 UpdateActive 逐帧驱动
            OnActiveMoment();
            return;
        }

        if (weaponAnimator != null && weaponController != null)
        {
            weaponAnimator.Play(
                weaponController.GetAttackStartAngle(),
                weaponController.GetAttackEndAngle(),
                activeDuration,
                attackData.AttackEase,
                weaponController.GetAttackRotateMode(),
                attackData.ActiveMomentRatio,
                OnActiveMoment
            );
        }
        else
        {
            OnActiveMoment();
        }
    }

    private void UpdateActive()
    {
        activeTimer -= Time.deltaTime;
        if (activeTimer <= 0f)
        {
            EnterRecovery();
            return;
        }

        if (isThrustAttack)
        {
            // 戳击（v0.6.3 方案一：戳出段有判定、收回段无判定）：
            // 伸展度 0→1→0；判定长度 = AttackRange × 伸展度；仅前半程（戳出）执行判定
            float progress = 1f - activeTimer / activeDuration;
            float extension = Mathf.Sin(progress * Mathf.PI);

            if (weaponController != null)
            {
                // 活塞位移：从后拉位置前冲再回位（视觉）
                float offset = -thrustPullBack * (1f - progress) + thrustTravel * extension;
                weaponController.SetCustomVisualThrustOffset(offset);
            }
            if (weaponHitbox != null)
                weaponHitbox.LengthMultiplier = extension;

            // 范围矩形跟随判定长度实时伸缩
            if (attackIndicator != null)
                ShowThrustRangeDisplay(attackData.AttackRange * Mathf.Max(extension, 0.05f), attackDirection);

            if (progress < 0.5f)
                weaponHitbox?.Tick();
            return;
        }

        // Active 期间每帧执行一次武器矩形检测，检测窗口严格等于 Active 阶段
        weaponHitbox?.Tick();
    }

    /// <summary>
    /// 命中时刻回调。由 WeaponAnimator 在动画配置比例点触发。
    /// v0.4.6 起伤害由 WeaponHitbox 全程检测结算；v0.6.3 起范围显示在 EnterRecovery 统一收起，
    /// 本回调仅保留触发去重（供后续命中特效扩展）。
    /// </summary>
    private void OnActiveMoment()
    {
        if (activeMomentTriggered) return;
        activeMomentTriggered = true;
    }

    private void EnterRecovery()
    {
        subPhase = SubPhase.Recovery;
        recoveryTimer = attackData.RecoveryTime / AttackSpeedMul();   // v0.7.5：后摇 ÷ 攻速倍率

        // Active 结束即停挥，关闭武器检测（不能等到 Recovery 之后）
        weaponHitbox?.EndSwing();

        // v0.6.3：攻击动画结束，收起范围显示；戳击视觉与判定倍率归位
        HideRangeDisplay();
        if (isThrustAttack)
        {
            if (weaponController != null)
                weaponController.SetCustomVisualThrustOffset(0f);
            if (weaponHitbox != null)
                weaponHitbox.LengthMultiplier = 1f;
        }
    }

    private void UpdateRecovery()
    {
        recoveryTimer -= Time.deltaTime;
        if (recoveryTimer <= 0f)
        {
            subPhase = SubPhase.None;
            weaponAnimator?.Stop();
            weaponController?.UnlockAttackDirection();
            OnAttackEnd?.Invoke();

            // v0.6.3：蓄力挥击结束，副本参数与宽度倍率还原基准值
            RestoreMeleeChargeBase();
        }
    }
}
