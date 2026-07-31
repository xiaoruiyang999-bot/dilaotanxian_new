using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("基础属性")]
    [SerializeField] private float maxHP = 5f;
    [SerializeField] private float maxArmor = 5f;
    [SerializeField] private float moveSpeed = 5f;

    [Header("六维（v0.7.0，决策 6；占位数值待设计 D 定稿）")]
    [SerializeField] private float attack = 5f;                 // 角色攻击力（基础攻击区的一半，另一半为武器攻击）
    [SerializeField, Range(0f, 1f)] private float critRate = 0.2f;  // 暴击率
    [SerializeField] private float critDamage = 1.5f;           // 暴击伤害倍率
    [SerializeField] private float armorReduceMul = 0.3f;       // 护甲免伤率 R（v0.7.1 接线结算）
    [SerializeField] private float armorLossMul = 1.0f;         // 护甲扣减率 L（v0.7.1 接线结算）

    [Header("法力（v0.6.2，不可自动回复）")]
    [SerializeField] private float maxMana = 0f;                // 未选职业时为 0（旧场景兼容），回复只能靠法力瓶/击杀法力球/技能宠物

    public float MaxHP => maxHP;
    public float MaxArmor => maxArmor;
    public float MoveSpeed => moveSpeed;
    public float CurrentArmor { get; private set; }
    public float MaxMana => maxMana;
    public float CurrentMana { get; private set; }

    public float Attack => attack;
    public float CritRate => critRate;
    public float CritDamage => critDamage;
    public float ArmorReduceMul => armorReduceMul;
    public float ArmorLossMul => armorLossMul;

    /// <summary>当前职业（v0.6.2；未选择时为 null，旧场景保持现状）。</summary>
    public ClassData CurrentClass { get; private set; }

    public System.Action OnStatsChanged;

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
        CurrentArmor = Mathf.Clamp(CurrentArmor + delta, 0, maxArmor);
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

    /// <summary>
    /// 应用职业配置（v0.6.2 / v0.7.0 六维）：写入 HP/护甲/法力上限与攻击/暴击/暴伤/护甲双倍率，
    /// 回满当前值，记录 CurrentClass。HP 上限经 Health.Initialize 写入（Health 是 HP 唯一数据源）。
    /// 全部变更走 OnStatsChanged 刷新 UI。
    /// </summary>
    public void ApplyClass(ClassData classData)
    {
        if (classData == null) return;

        CurrentClass = classData;
        maxHP = classData.MaxHP;
        maxArmor = classData.MaxArmor;
        maxMana = classData.MaxMana;

        attack = classData.Attack;
        critRate = classData.CritRate;
        critDamage = classData.CritDamage;
        armorReduceMul = classData.ArmorReduceMul;
        armorLossMul = classData.ArmorLossMul;

        CurrentArmor = maxArmor;
        CurrentMana = maxMana;

        if (TryGetComponent<Health>(out var h))
            h.Initialize(classData.MaxHP);

        OnStatsChanged?.Invoke();
    }

    void OnValidate()
    {
        // 减伤甲数值下限（v0.7.1，公式文档 §六-8）：R∈[0,0.9] 防 100% 免伤，L>0
        armorReduceMul = Mathf.Clamp(armorReduceMul, 0f, 0.9f);
        armorLossMul = Mathf.Max(armorLossMul, 0.01f);
    }
}
