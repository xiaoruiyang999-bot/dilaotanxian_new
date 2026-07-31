using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("基础属性")]
    [SerializeField] private float maxHP = 5f;
    [SerializeField] private float maxArmor = 5f;
    [SerializeField] private float moveSpeed = 5f;

    [Header("护甲恢复")]
    [SerializeField] private float armorRegenRate = 0.5f;       // 每秒恢复0.5点
    [SerializeField] private float armorRegenDelay = 3f;        // 脱战3秒后开始恢复

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

    private float lastDamageTime = -999f;  // 上次受伤时间（负值表示开局未受伤）
    private bool isOutOfCombat => Time.time - lastDamageTime >= armorRegenDelay;

    void Awake()
    {
        CurrentArmor = maxArmor;
        CurrentMana = maxMana;
    }

    void Update()
    {
        // 脱战3秒后，护甲开始自动恢复（v0.7.1 计划删除，本版保留）
        if (CurrentArmor < maxArmor && isOutOfCombat)
        {
            CurrentArmor = Mathf.Min(CurrentArmor + armorRegenRate * Time.deltaTime, maxArmor);
            OnStatsChanged?.Invoke();
        }
    }

    /// <summary>
    /// 标记受到伤害（重置脱战计时器）
    /// </summary>
    public void OnTakeDamage()
    {
        lastDamageTime = Time.time;
    }

    /// <summary>
    /// 优先使用护甲吸收伤害，返回剩余应由生命值承担的伤害。
    /// 护甲变化时会触发 OnStatsChanged 事件以更新 UI。
    /// </summary>
    public float AbsorbDamageWithArmor(float damage)
    {
        if (damage <= 0) return 0f;

        float absorbed = Mathf.Min(CurrentArmor, damage);
        CurrentArmor -= absorbed;

        if (absorbed > 0)
        {
            OnStatsChanged?.Invoke();
        }

        return damage - absorbed;
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
}
