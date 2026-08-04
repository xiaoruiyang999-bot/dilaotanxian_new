using DG.Tweening;
using UnityEngine;

/// <summary>
/// 技能执行器（v0.7.4 技能框架）：玩家组件，PlayerController.Awake 运行时 Get-or-Add（ItemInventory 同模式）。
/// 三槽（0=小技能 1=大招 2=武器技能），Start 装配——
///   小技能 = 分支表选中项（RunStateCarrier.ChosenSkillBranchIndex 局外读写、局内锁定，本版无切换 UI）；
///   大招 = 职业大招；武器技能 = 当前武器（未装备 = 空槽）。
/// 引用解析：ClassData/WeaponData 已接线则用接线值，null 走 SkillCatalog 兜底（本版既有资产全部 null，实际走 SkillCatalog）。
/// Update 递减 CD 并每帧推 SlotBarUI（SetSkillDisplay 技能名 / SetSkillCooldown 文本秒数；Radial360 扫面记遗留）。
/// TryCastSlot：CD 中/法力不足 → 槽位文本红闪返回；否则 TryConsumeMana + 起 CD + 按 SkillType 执行。
/// 订阅 PlayerWeaponHolder.OnWeaponChanged：换武器整套替换武器技能槽（CD 清零独立计时，F/Q 两槽不受影响）。
/// 蓄力冲突：分发在 PlayerController 层、不进 PlayerCombat 状态机，蓄力中允许放技能（占位不做打断，记遗留）。
/// </summary>
public class SkillExecutor : MonoBehaviour
{
    private const int SlotCount = 3;
    private const float FlashDuration = 0.2f;      // CD 中/法力不足红闪时长
    private const float IndicatorShowTime = 0.2f;  // 范围灰显时长

    private static readonly Color FlashColor = new Color(1f, 0.25f, 0.25f);          // 施放失败红闪
    private static readonly Color IndicatorColor = new Color(0.5f, 0.5f, 0.5f, 0.35f); // 范围灰显（近战范围显示同款灰）

    /// <summary>单个技能槽：数据 + CD/红闪计时（纯运行时状态）。</summary>
    private class SkillSlot
    {
        public SkillData Data;
        public float CooldownRemaining;
        public float FlashRemaining;
        public bool WasFlashing;
    }

    private readonly SkillSlot[] slots = new SkillSlot[SlotCount];

    private PlayerStats stats;
    private Health health;
    private PlayerWeaponHolder holder;
    private SlotBarUI slotBar;
    private AttackIndicator indicator;
    private Tween indicatorHideTween;

    private static int enemyMask = -1;   // Enemy 层（首次施放时解析，层缺失 Warning 一次后 AOE 无目标）

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        health = GetComponent<Health>();
        for (int i = 0; i < SlotCount; i++) slots[i] = new SkillSlot();

        // PlayerWeaponHolder 无 RequireComponent，运行时补挂安全（武器拾取/准备房间同为 Get-or-Add）
        holder = GetComponent<PlayerWeaponHolder>();
        if (holder == null) holder = gameObject.AddComponent<PlayerWeaponHolder>();
        holder.OnWeaponChanged += OnWeaponChanged;

        // 技能范围显示独立挂一个 AttackIndicator（与 PlayerCombat 近战范围显示互不干扰）；
        // detachOnShow = false 跟随玩家（玩家近战范围显示同规则，v0.6.3）
        GameObject indicatorGo = new GameObject("SkillIndicator");
        indicatorGo.transform.SetParent(transform, false);
        indicator = indicatorGo.AddComponent<AttackIndicator>();
        indicator.detachOnShow = false;
    }

    void OnDestroy()
    {
        if (holder != null) holder.OnWeaponChanged -= OnWeaponChanged;
    }

    void Start()
    {
        AssembleSlots();
    }

    /// <summary>三槽装配：小技能（分支选中）/ 大招（职业）/ 武器技能（当前武器）。未选职业（旧场景回归）全部留空。</summary>
    private void AssembleSlots()
    {
        // 当前职业：ApplyClass 已跑用 PlayerStats.CurrentClass；否则退 RunStateCarrier（同场景 Start 顺序不定）
        ClassData cls = stats != null ? stats.CurrentClass : null;
        if (cls == null) cls = RunStateCarrier.Ensure().LastChosenClass;
        if (cls == null) return;   // 未选职业：旧场景无法力，技能三槽维持"—"

        // 小技能 = 分支表选中项（局外 SetSkillBranch 写入，局内锁定）
        SkillBranchData branches = cls.SkillBranches != null
            ? cls.SkillBranches : SkillCatalog.GetBranches(cls.ClassType);
        slots[0].Data = branches != null
            ? branches.GetBranch(RunStateCarrier.Ensure().ChosenSkillBranchIndex) : null;

        // 大招 = 职业大招
        slots[1].Data = cls.UltimateSkill != null
            ? cls.UltimateSkill : SkillCatalog.GetUltimate(cls.ClassType);

        // 武器技能 = 当前武器
        slots[2].Data = ResolveWeaponSkill(holder != null && holder.Current != null ? holder.Current.Data : null);
    }

    /// <summary>武器技能解析：接线值优先，null 走 SkillCatalog 兜底；未装备武器 = 空槽。</summary>
    private SkillData ResolveWeaponSkill(WeaponData weapon)
    {
        if (weapon == null) return null;
        return weapon.WeaponSkill != null ? weapon.WeaponSkill : SkillCatalog.GetWeaponSkill(weapon);
    }

    /// <summary>换武器回调：武器技能槽整套替换，CD/红闪清零独立计时（F/Q 两槽不受影响）。</summary>
    private void OnWeaponChanged(WeaponData oldData, WeaponData newData)
    {
        slots[2].Data = ResolveWeaponSkill(newData);
        slots[2].CooldownRemaining = 0f;
        slots[2].FlashRemaining = 0f;
    }

    void Update()
    {
        // SlotBarUI 是 RuntimeInitializeOnLoadMethod 自举的常驻单例（DontDestroyOnLoad），找到后缓存即可
        if (slotBar == null)
            slotBar = FindAnyObjectByType<SlotBarUI>();

        for (int i = 0; i < SlotCount; i++)
        {
            SkillSlot slot = slots[i];
            if (slot.CooldownRemaining > 0f)
                slot.CooldownRemaining = Mathf.Max(0f, slot.CooldownRemaining - Time.deltaTime);
            if (slot.FlashRemaining > 0f)
                slot.FlashRemaining -= Time.deltaTime;
            PushSlotUI(i, slot);
        }
    }

    /// <summary>每帧推一个槽的显示：CD 中推秒数（SetSkillCooldown），否则推技能名（色 = iconColor；红闪优先）。</summary>
    private void PushSlotUI(int index, SkillSlot slot)
    {
        if (slotBar == null || slot.Data == null) return;   // 空槽维持 SlotBarUI 的"—"占位

        bool flashing = slot.FlashRemaining > 0f;
        if (slot.CooldownRemaining > 0f)
        {
            if (flashing)
                slotBar.SetSkillDisplay(index, slot.CooldownRemaining.ToString("0.0"), FlashColor);
            else if (slot.WasFlashing)
                // 红闪结束：SetSkillCooldown 不写颜色，补一帧恢复技能色
                slotBar.SetSkillDisplay(index, slot.CooldownRemaining.ToString("0.0"), slot.Data.IconColor);
            else
                slotBar.SetSkillCooldown(index, slot.CooldownRemaining, slot.Data.Cooldown);
        }
        else
        {
            slotBar.SetSkillDisplay(index, slot.Data.DisplayName, flashing ? FlashColor : slot.Data.IconColor);
        }
        slot.WasFlashing = flashing;
    }

    /// <summary>尝试施放一个槽（PlayerController 输入分发入口：0=F 小技能 1=Q 大招 2=R 武器技能）。</summary>
    public void TryCastSlot(int index)
    {
        if (index < 0 || index >= SlotCount) return;
        SkillSlot slot = slots[index];
        if (slot.Data == null) return;
        if (health != null && health.IsDead) return;

        if (slot.CooldownRemaining > 0f || stats == null || !stats.TryConsumeMana(slot.Data.ManaCost))
        {
            slot.FlashRemaining = FlashDuration;   // CD 中/法力不足：红闪提示，不起 CD 不扣蓝
            return;
        }

        slot.CooldownRemaining = slot.Data.Cooldown;
        Execute(slot.Data);
    }

    /// <summary>按类型执行（MeleeAoE 本版唯一实现；扩展新类型在此分发）。</summary>
    private void Execute(SkillData data)
    {
        switch (data.SkillType)
        {
            case SkillType.MeleeAoE:
                CastMeleeAoE(data);
                break;
            default:
                Debug.LogWarning($"[Skill] 未实现的技能类型：{data.SkillType}（{data.DisplayName}）。");
                break;
        }
    }

    /// <summary>
    /// 旋风斩（MeleeAoE）：Enemy 层圆形 AOE，每个 IDamageable 走 DamageResolver.Deal
    /// （DamageContext 仿 WeaponHitbox：baseAttack 只取角色攻击不并入武器攻击——技能倍率区独立，占位从简，计划 §2.5）。
    /// </summary>
    private void CastMeleeAoE(SkillData data)
    {
        if (enemyMask < 0)
        {
            enemyMask = LayerMask.GetMask("Enemy");
            if (enemyMask == 0)
                Debug.LogWarning("[Skill] Enemy 层缺失，技能 AOE 无目标。");
        }

        Vector2 center = transform.position;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, data.AoeRadius, enemyMask);
        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.isTrigger) continue;              // 跳过探测圈等 trigger（WeaponHitbox 同规则）
            if (hit.transform.IsChildOf(transform)) continue;        // 跳过自身（含子物体）
            if (hit.TryGetComponent<IDamageable>(out var damageable))
            {
                DamageContext ctx = new DamageContext
                {
                    baseAttack = stats.Attack,
                    multiplier = data.GetDamageMultiplier(),
                    critRate = stats.CritRate,
                    critDamage = stats.CritDamage
                };
                DamageResolver.Deal(damageable, ctx);
            }
        }

        // 表现（纯占位）：灰色圆形范围短显 + 技能色缩放缓圈；无伤害数字
        ShowRangeIndicator(data.AoeRadius);
        SpawnSkillRing(data);
    }

    /// <summary>灰色圆形范围短显（AttackIndicator 圆形模式，detachOnShow=false 跟随玩家，0.2s 后收起）。</summary>
    private void ShowRangeIndicator(float radius)
    {
        if (indicator == null) return;

        // 指示器挂在玩家根下（根有缩放），SetRadius 是局部值：除以 lossyScale 使渲染半径 = 世界半径（判定同源，
        // PlayerCombat.RangeDisplayScale 同思路）
        float scale = indicator.transform.lossyScale.x;
        indicator.SetColor(IndicatorColor);
        indicator.SetShape(AttackIndicator.ShapeType.Circle);
        indicator.SetRadius(radius / Mathf.Max(0.001f, scale));
        indicator.Show();

        indicatorHideTween?.Kill();
        indicatorHideTween = DOVirtual.DelayedCall(IndicatorShowTime, () => indicator.Hide())
            .SetLink(gameObject);
    }

    /// <summary>技能色缩放缓圈（SpawnHealRing 同款模式，必须 SetLink）：半径 → 直径缩放 + 淡出销毁。</summary>
    private void SpawnSkillRing(SkillData data)
    {
        Sprite circle = ProjectileVisualBuilder.GetCircleSprite();
        if (circle == null) return;

        GameObject ring = new GameObject("SkillRing");
        ring.transform.position = transform.position;
        ring.transform.localScale = Vector3.one * data.AoeRadius;   // 1×1 圆图：直径 = 缩放值，从半径起步

        SpriteRenderer sr = ring.AddComponent<SpriteRenderer>();
        sr.sprite = circle;
        Color c = data.IconColor;
        c.a = 0.5f;
        sr.color = c;
        sr.sortingOrder = 10;

        ring.transform.DOScale(data.AoeRadius * 2f, 0.3f).SetLink(ring);
        sr.DOFade(0f, 0.3f)
            .SetLink(ring)   // 目标销毁时自动 kill，避免 DOTween safe mode 报 missing target
            .OnComplete(() => Destroy(ring));
    }
}
