using DG.Tweening;
using UnityEngine;

/// <summary>
/// 技能执行器（v0.7.4 技能框架）：玩家组件，PlayerController.Awake 运行时 Get-or-Add（ItemInventory 同模式）。
/// 三槽（0=小技能 1=大招 2=武器技能），Start 装配 + 订阅 PlayerStats.OnClassApplied 重装配
/// （准备房间选职业晚于场景 Start：选完职业三槽立即装填，CD/红闪清零）——
///   小技能 = 分支表选中项（RunStateCarrier.ChosenSkillBranchIndex 局外读写、局内锁定，本版无切换 UI）；
///   大招 = 职业大招；武器技能 = 当前武器（未装备 = 空槽）。
/// 引用解析：ClassData/WeaponData 已接线则用接线值，null 走 SkillCatalog 兜底（本版既有资产全部 null，实际走 SkillCatalog）。
/// Update 递减 CD 并每帧推 SlotBarUI（SetSkillDisplay 技能名 / SetSkillCooldown 文本秒数；Radial360 扫面记遗留）。
/// TryCastSlot：CD 中/法力不足 → 槽位文本红闪返回；否则 TryConsumeMana + 起 CD + 按 SkillType 执行。
/// 订阅 PlayerWeaponHolder.OnWeaponChanged：换武器整套替换武器技能槽（CD 清零独立计时，F/Q 两槽不受影响）。
/// 蓄力冲突：分发在 PlayerController 层、不进 PlayerCombat 状态机，蓄力中允许放技能（占位不做打断，记遗留）。
/// v0.7.5 二期：DashExecute 裸绞（冲刺 + 阈值斩杀/真伤，先判阈值后结算）与 BurnLife 燃命
/// （清 Buff + 免疫窗口 + empowerRemaining 联动窗口，窗口内下一次施放小技能分支改用大招资产的强化数值）。
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
    private BuffManager buffManager;   // v0.7.5：Buff 运行时（Awake Get-or-Add，Buff 型技能与输出倍率通道用）

    // v0.7.5 二期：裸绞冲刺用（瞄准方向 + 刚体位移）
    private PlayerAimController aimController;
    private Rigidbody2D rb;

    // v0.7.5 二期燃命：联动窗口剩余秒数（>0 期间下一次施放小技能分支用强化数值，施放即消耗）
    private float empowerRemaining;

    private static int enemyMask = -1;   // Enemy 层（首次施放时解析，层缺失 Warning 一次后 AOE 无目标）

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        health = GetComponent<Health>();
        aimController = GetComponent<PlayerAimController>();
        rb = GetComponent<Rigidbody2D>();
        for (int i = 0; i < SlotCount; i++) slots[i] = new SkillSlot();

        // 职业应用后重装配（准备房间选职业晚于本场景 Start，OnWeaponChanged 同模式订阅）
        if (stats != null) stats.OnClassApplied += OnClassApplied;

        // PlayerWeaponHolder 无 RequireComponent，运行时补挂安全（武器拾取/准备房间同为 Get-or-Add）
        holder = GetComponent<PlayerWeaponHolder>();
        if (holder == null) holder = gameObject.AddComponent<PlayerWeaponHolder>();
        holder.OnWeaponChanged += OnWeaponChanged;

        // v0.7.5：BuffManager 同模式 Get-or-Add（无 RequireComponent；四通道消费方均按缺失即 1 处理，
        // 未选职业的旧场景即使不挂也零行为差异）
        buffManager = GetComponent<BuffManager>();
        if (buffManager == null) buffManager = gameObject.AddComponent<BuffManager>();

        // v0.7.5 二期：武器被动（长剑连击加速/长枪贯穿）同模式 Get-or-Add（无 RequireComponent；
        // 未装备武器/默认近战不生效，零行为差异）
        if (GetComponent<WeaponPassives>() == null)
            gameObject.AddComponent<WeaponPassives>();

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
        if (stats != null) stats.OnClassApplied -= OnClassApplied;
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

    /// <summary>职业应用回调（准备房间选职业/换职业后）：三槽按当前职业/分支/武器重装配，CD/红闪全清零。</summary>
    private void OnClassApplied(ClassData cls)
    {
        AssembleSlots();
        for (int i = 0; i < SlotCount; i++)
        {
            slots[i].CooldownRemaining = 0f;
            slots[i].FlashRemaining = 0f;
        }
    }

    void Update()
    {
        // SlotBarUI 是 RuntimeInitializeOnLoadMethod 自举的常驻单例（DontDestroyOnLoad），找到后缓存即可
        if (slotBar == null)
            slotBar = FindAnyObjectByType<SlotBarUI>();

        if (empowerRemaining > 0f)
            empowerRemaining = Mathf.Max(0f, empowerRemaining - Time.deltaTime);

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

    /// <summary>按类型执行（MeleeAoE 旋风斩 / Buff 自身增益，v0.7.5；二期：DashExecute 裸绞 / BurnLife 燃命）。</summary>
    private void Execute(SkillData data)
    {
        switch (data.SkillType)
        {
            case SkillType.MeleeAoE:
                CastMeleeAoE(data);
                break;
            case SkillType.Buff:
                CastBuff(data);
                break;
            case SkillType.DashExecute:
                CastDashExecute(data);
                break;
            case SkillType.BurnLife:
                CastBurnLife(data);
                break;
            default:
                Debug.LogWarning($"[Skill] 未实现的技能类型：{data.SkillType}（{data.DisplayName}）。");
                break;
        }
    }

    /// <summary>
    /// Buff 型技能（v0.7.5 屹立不倒/强力一击）：按 SkillData 的 Buff 区字段挂 Buff，
    /// 同技能再施放 = 刷新替换；持续结束后若配置了虚弱参数则自动挂虚弱 Buff（BuffManager 虚弱链）。
    /// v0.7.5 二期：燃命联动窗口内且当前分支为 0/1 时，本次施放改用大招资产里的强化数值并消耗窗口。
    /// </summary>
    private void CastBuff(SkillData data)
    {
        if (buffManager == null) return;

        float duration = data.BuffDuration;
        float weaknessDuration = data.WeaknessDuration;
        float damageDealtMul = data.BuffDamageDealtMul;

        SkillData ult = slots[1].Data;
        if (empowerRemaining > 0f && ult != null && ult.SkillType == SkillType.BurnLife)
        {
            int branch = RunStateCarrier.Ensure().ChosenSkillBranchIndex;
            if (branch == 0)        // 屹立不倒：持续 5→6.5、虚弱 3→1.5
            {
                duration = ult.EmpowerStandFirmDuration;
                weaknessDuration = ult.EmpowerStandFirmWeaknessDuration;
                empowerRemaining = 0f;
            }
            else if (branch == 1)   // 强力一击：持续 5→7.5、倍率 ×2.2
            {
                duration = ult.EmpowerPowerStrikeDuration;
                damageDealtMul = ult.EmpowerPowerStrikeDamageDealtMul;
                empowerRemaining = 0f;
            }
        }

        buffManager.AddBuff(data.DisplayName, duration,
            attackSpeedMul: data.BuffAttackSpeedMul,
            moveSpeedMul: data.BuffMoveSpeedMul,
            damageTakenMul: data.BuffDamageTakenMul,
            damageDealtMul: damageDealtMul,
            weaknessDuration: weaknessDuration,
            weaknessAttackSpeedMul: data.WeaknessAttackSpeedMul,
            weaknessMoveSpeedMul: data.WeaknessMoveSpeedMul);
    }

    /// <summary>
    /// 燃命（v0.7.5 二期大招）：清除自身全部 Buff（不触发虚弱链）+ 免疫窗口（期间负面 Buff 挂不上）
    /// + 联动窗口（窗口内下一次施放小技能分支用强化数值，数值存在大招资产的燃命区）。
    /// </summary>
    private void CastBurnLife(SkillData data)
    {
        if (buffManager != null)
        {
            buffManager.ClearAll();
            buffManager.SetImmune(data.ImmuneDuration);
        }
        empowerRemaining = data.EmpowerWindow;

        // 表现（纯占位）：技能色缩放缓圈（半径沿用 aoeRadius 字段作视觉半径）
        SpawnSkillRing(data);
    }

    /// <summary>
    /// 裸绞（v0.7.5 二期斩杀技）：朝瞄准方向冲刺（rb.DOMove，冲刺期间挂受击减伤短 buff），
    /// 终点小范围取最近敌人结算——先判斩杀阈值（普通 ≤30% / 精英 ≤15%，Boss 不可斩杀），
    /// 达标直接处决，未达标结算真实伤害（trueDamage 通道绕过护甲）。
    /// 燃命联动窗口内：真伤与斩杀回血改用大招资产的强化数值并消耗窗口。
    /// </summary>
    private void CastDashExecute(SkillData data)
    {
        float trueDamage = data.TrueDamage;
        float executeHeal = 0f;

        SkillData ult = slots[1].Data;
        if (empowerRemaining > 0f && ult != null && ult.SkillType == SkillType.BurnLife)
        {
            trueDamage = ult.EmpowerExecuteTrueDamage;
            executeHeal = ult.EmpowerExecuteHeal;
            empowerRemaining = 0f;
        }

        Vector2 dir = aimController != null ? aimController.AimDirection : Vector2.right;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        Vector2 start = transform.position;
        Vector2 end = start + dir.normalized * data.DashDistance;

        // 冲刺期间受击减伤 70%（0.3）：短 buff 随冲刺时长自然到期
        buffManager?.AddBuff(data.DisplayName + "_Dash", data.DashDuration,
            damageTakenMul: data.DashDamageTakenMul);

        // rb.DOMove 走 FixedUpdate MovePosition（比直写 transform 稳）；SetLink 防玩家销毁后 tween 悬空
        if (rb != null)
            rb.DOMove(end, data.DashDuration).SetEase(Ease.OutQuad).SetLink(gameObject);
        else
            transform.position = end;

        StartCoroutine(DashExecuteHitRoutine(data, end, trueDamage, executeHeal));
    }

    /// <summary>裸绞冲刺结束后的命中结算（协程等冲刺时长；玩家中途死亡则不结算）。</summary>
    private System.Collections.IEnumerator DashExecuteHitRoutine(SkillData data, Vector2 end, float trueDamage, float executeHeal)
    {
        yield return new WaitForSeconds(data.DashDuration);
        if (health != null && health.IsDead) yield break;

        if (enemyMask < 0)
        {
            enemyMask = LayerMask.GetMask("Enemy");
            if (enemyMask == 0)
                Debug.LogWarning("[Skill] Enemy 层缺失，裸绞无目标。");
        }

        // 终点小范围 OverlapCircle 取最近敌人（跳过 trigger 与自身，MeleeAoE 同规则）
        Collider2D[] hits = Physics2D.OverlapCircleAll(end, data.DashHitRadius, enemyMask);
        IDamageable nearest = null;
        float nearestSqr = float.MaxValue;
        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.isTrigger) continue;
            if (hit.transform.IsChildOf(transform)) continue;
            if (!hit.TryGetComponent<IDamageable>(out var damageable)) continue;
            float sqr = ((Vector2)hit.transform.position - end).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = damageable;
            }
        }

        if (nearest != null)
            ResolveGarroteHit(nearest, data, trueDamage, executeHeal);

        // 表现（纯占位）：终点技能色缩放缓圈（半径 = 命中判定半径）
        SpawnSkillRing(data, data.DashHitRadius);
    }

    /// <summary>
    /// 裸绞单体结算：先判斩杀阈值再结算真伤。
    /// Boss（prefab 名含 "Boss"，Enemy_Boss 实例）不可斩杀，一律真伤；
    /// 精英判据 = EnemyStats.MaxArmor &gt; 0（现有唯一区分方式，EnemyHealth/WorldSpaceHealthBar 同口径）。
    /// </summary>
    private void ResolveGarroteHit(IDamageable target, SkillData data, float trueDamage, float executeHeal)
    {
        EnemyHealth eh = target as EnemyHealth;
        if (eh == null)
        {
            // 非 EnemyHealth 目标（可破坏障碍等）：直接真伤原路径
            DamageResolver.Deal(target, new DamageContext { trueDamage = trueDamage });
            return;
        }

        if (eh.IsDead) return;
        Vector2 executePosition = eh.transform.position;

        bool isBoss = eh.name.Contains("Boss");
        float threshold = eh.MaxArmor > 0f ? data.ExecuteThresholdElite : data.ExecuteThresholdNormal;
        float hpRatio = eh.MaxHealth > 0f ? eh.CurrentHealth / eh.MaxHealth : 0f;

        if (!isBoss && hpRatio <= threshold)
        {
            // 处决：直接击杀（不走伤害结算），燃命强化时回血
            eh.Die();
            if (executeHeal > 0f && health != null)
                health.Heal(executeHeal);
        }
        else
        {
            // 未达阈值 / Boss：真实伤害（绕过护甲结算）
            DamageResolver.Deal(eh, new DamageContext { trueDamage = trueDamage });
        }

        // 击杀震颤只属于战士裸绞：阈值处决或本次真伤致死均触发；
        // 普攻、其他技能及 EnemyHealth 全局死亡链路不消费该表现。
        if (eh.IsDead)
            ExecuteFeedback.Play(executePosition);
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
                    // v0.7.5：输出倍率通道（强力一击期间技能也吃 ×1.75）；无 buff 时为 1 零差异
                    multiplier = data.GetDamageMultiplier() * (buffManager != null ? buffManager.DamageDealtMultiplier : 1f),
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

    /// <summary>技能色缩放缓圈（SpawnHealRing 同款模式，必须 SetLink）：半径 → 直径缩放 + 淡出销毁。
    /// radius &lt; 0 时取 data.AoeRadius（MeleeAoE/燃命视觉）；裸绞传命中判定半径。</summary>
    private void SpawnSkillRing(SkillData data, float radius = -1f)
    {
        Sprite circle = ProjectileVisualBuilder.GetCircleSprite();
        if (circle == null) return;

        float r = radius >= 0f ? radius : data.AoeRadius;
        GameObject ring = new GameObject("SkillRing");
        ring.transform.position = transform.position;
        ring.transform.localScale = Vector3.one * r;   // 1×1 圆图：直径 = 缩放值，从半径起步

        SpriteRenderer sr = ring.AddComponent<SpriteRenderer>();
        sr.sprite = circle;
        Color c = data.IconColor;
        c.a = 0.5f;
        sr.color = c;
        sr.sortingOrder = 10;

        ring.transform.DOScale(r * 2f, 0.3f).SetLink(ring);
        sr.DOFade(0f, 0.3f)
            .SetLink(ring)   // 目标销毁时自动 kill，避免 DOTween safe mode 报 missing target
            .OnComplete(() => Destroy(ring));
    }
}
