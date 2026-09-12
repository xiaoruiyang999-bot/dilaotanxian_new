using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    // 数值真值在 PlayableCharacterDefinition，由 ApplyPlayableCharacter 全权写入。
    // 未选职业（旧场景 v0_4/v0_5）时保持下列安全常量：UI 显示 0/占位，结算不崩（无除零、无空引用），移速兜底 5 保证旧场景可动。
    private float moveSpeed = 5f;                               // 基础移速（七维之一；无职业兜底 5，与任务书 §一 暂定值一致）
    private float maxHP = 0f;                                   // HP 上限（显示走 Health）
    private float maxArmor = 0f;                                // 护甲上限：0 → 护甲条显示 0，ApplyArmorDamage 全额扣血
    private float attack = 0f;                                  // 角色攻击力（基础攻击区的一半，另一半为武器攻击）
    private float critRate = 0f;                                // 暴击率
    private float critDamage = 1f;                              // 暴击伤害倍率（1 = 不暴击数值不变）
    private float armorReduceMul = 0f;                          // 护甲免伤率 R（v0.7.1 接线结算）
    private float armorLossMul = 1f;                            // 护甲扣减率 L（v0.7.1 接线结算，须 > 0）
    private float maxMana = 0f;                                 // 未选职业时为 0，回复只能靠法力瓶/击杀法力球/技能宠物

    public float MaxHP => maxHP + PermHpBonus;
    public float MaxArmor => maxArmor + PermArmorBonus;
    public float MoveSpeed => moveSpeed * BeastMoveSpeedMult * PermMoveSpeedMult;
    public float CurrentArmor { get; private set; }
    public float MaxMana => maxMana;
    public float CurrentMana { get; private set; }

    public float Attack => attack * BeastDamageMult * PermDamageMult;
    public float CritRate => critRate;
    public float CritDamage => critDamage + PermCritDamageBonus;
    public float ArmorReduceMul => armorReduceMul;
    public float ArmorLossMul => armorLossMul;

    // v1.0.9 兽化乘数（WerewolfTransformation 写入，默认 1 = 不影响任何数值；
    // 伤害乘进 Attack、移速乘进 MoveSpeed、攻速由 PlayerCombat.AttackSpeedMul 消费）
    public float BeastDamageMult { get; set; } = 1f;
    public float BeastMoveSpeedMult { get; set; } = 1f;
    public float BeastAttackSpeedMult { get; set; } = 1f;

    // v1.1.47 技能树永久加成（局外解锁，RefreshSkillTreeBonuses 从 SkillTreeDef 聚合写入；
    // 默认 0 加成 = 行为与旧版一致。攻速由 PlayerCombat.AttackSpeedMul 消费；HP 上限经 Health.Initialize）
    public float PermDamageMult { get; set; } = 1f;
    public float PermMoveSpeedMult { get; set; } = 1f;
    public float PermAttackSpeedMult { get; set; } = 1f;
    public float PermCritDamageBonus { get; set; } = 0f;
    public float PermHpBonus { get; set; } = 0f;
    public float PermArmorBonus { get; set; } = 0f;

    /// <summary>从存档聚合技能树加成并立即生效（HP 上限经 Health.Initialize，含回满——设计选择：加点视为休整）。UI 解锁成功后调用。</summary>
    public void RefreshSkillTreeBonuses()
    {
        ApplySkillTreeBonuses();
        if (TryGetComponent<Health>(out var h))
            h.Initialize(MaxHP);
        CurrentArmor = MaxArmor;
        OnStatsChanged?.Invoke();
    }

    /// <summary>聚合局外加成通道；职业角色应用随后统一初始化当前值。</summary>
    private void ApplySkillTreeBonuses()
    {
        SkillTreeDef.Aggregate(SkillTreeSave.UnlockedIds,
            out float dmg, out float move, out float atkSpeed,
            out float hp, out float armor, out float critDmg);
        PermDamageMult = 1f + dmg / 100f;
        PermMoveSpeedMult = 1f + move / 100f;
        PermAttackSpeedMult = 1f + atkSpeed / 100f;
        PermCritDamageBonus = critDmg / 100f;
        PermHpBonus = hp;
        PermArmorBonus = armor;
    }

    /// <summary>v2.0.2 当前职业角色定义；新运行链的唯一身份来源。</summary>
    public PlayableCharacterDefinition CurrentPlayableCharacter { get; private set; }

    public System.Action OnStatsChanged;

    public System.Action<PlayableCharacterDefinition> OnPlayableCharacterApplied;

    void Awake()
    {
        CurrentArmor = maxArmor;
        CurrentMana = maxMana;
    }

    /// <summary>
    /// 减伤甲结算（v0.7.1，公式文档 §三）：有甲时 扣血=伤害×(1−R)、扣甲=伤害×L（溢出不转嫁）；
    /// 护甲归零后全额扣血。公式走 DamageResolver.ApplyArmor（玩家/怪物共用一份实现）。
    /// 护甲变化时触发 OnStatsChanged 刷新 UI。返回应扣 HP 的伤害。
    /// </summary>
    public float ApplyArmorDamage(float damage)
    {
        if (damage <= 0) return 0f;

        float hpDamage = DamageResolver.ApplyArmor(damage, CurrentArmor, armorReduceMul, armorLossMul, out float armorAfter);
        if (!Mathf.Approximately(armorAfter, CurrentArmor))
        {
            CurrentArmor = armorAfter;
            OnStatsChanged?.Invoke();
        }

        return hpDamage;
    }

    public void ModifyArmor(float delta)
    {
        CurrentArmor = Mathf.Clamp(CurrentArmor + delta, 0, MaxArmor);   // MaxArmor 属性（含技能树加成）
        OnStatsChanged?.Invoke();
    }

    /// <summary>
    /// 回复法力（不超上限）。法力不可自动回复，来源仅：法力瓶 / 击杀法力球 / 技能宠物（计划书 4.4）。
    /// </summary>
    public void AddMana(float amount)
    {
        if (amount <= 0f || CurrentMana >= maxMana) return;

        CurrentMana = Mathf.Min(CurrentMana + amount, maxMana);
        OnStatsChanged?.Invoke();
    }

    /// <summary>尝试消耗法力（技能用）。不足时不扣减并返回 false。</summary>
    public bool TryConsumeMana(float amount)
    {
        if (amount <= 0f) return true;
        if (CurrentMana < amount) return false;

        CurrentMana -= amount;
        OnStatsChanged?.Invoke();
        return true;
    }

    // ========== 金币钱包（v1.1.3 自 MCP 分支还原） ==========
    // 单局货币：死亡重开 = 整场景重载 + 新玩家实例 → 天然清零；
    // 楼层过渡为原地重建（RunManager.NextFloor 不换场景）→ 局内跨层累积。
    // 变更走专用 OnCoinsChanged（CoinHUD 订阅），不并进 OnStatsChanged 以免整块属性 UI 逐币重刷。

    private int coins;

    /// <summary>当前金币数。</summary>
    public int Coins => coins;

    /// <summary>金币变化事件（参数 = 变化后的余额）。</summary>
    public System.Action<int> OnCoinsChanged;

    /// <summary>获得金币（≤0 忽略）。</summary>
    /// <summary>清零随身星蓝币（v2.0.6 死亡/Boss 封存结算后调用——新 Run 从 0 开始）。</summary>
        public void ResetCoins(){ coins = 0; OnCoinsChanged?.Invoke(coins); }

        public void AddCoins(int amount)
    {
        if (amount <= 0) return;
        coins += amount;
        OnCoinsChanged?.Invoke(coins);
    }

    /// <summary>尝试花费金币：不足时不扣减并返回 false（未来商店/付费交互的消费入口）。</summary>
    public bool TrySpendCoins(int amount)
    {
        if (amount < 0 || coins < amount) return false;
        coins -= amount;
        OnCoinsChanged?.Invoke(coins);
        return true;
    }

    /// <summary>
    /// 应用职业角色定义：完整覆盖基础属性并回满 HP/护甲/法力。
    /// 这是运行时唯一的职业角色属性应用入口。
    /// </summary>
    public void ApplyPlayableCharacter(PlayableCharacterDefinition definition)
    {
        if (definition == null) return;

        CurrentPlayableCharacter = definition;
        maxHP = definition.maxHp;
        maxArmor = definition.maxArmor;
        maxMana = definition.maxMana;
        moveSpeed = definition.moveSpeed;
        attack = definition.attack;
        critRate = definition.critRate;
        critDamage = definition.critDamage;
        armorReduceMul = Mathf.Clamp(definition.armorReduceMul, 0f, 0.9f);
        armorLossMul = Mathf.Max(definition.armorLossMul, 0.01f);

        ApplySkillTreeBonuses();
        CurrentArmor = MaxArmor;
        CurrentMana = maxMana;

        if (TryGetComponent<Health>(out var h))
            h.Initialize(definition.maxHp + PermHpBonus);

        OnStatsChanged?.Invoke();
        OnPlayableCharacterApplied?.Invoke(definition);
    }
}
