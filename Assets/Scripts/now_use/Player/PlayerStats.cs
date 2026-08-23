using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("基础属性")]
    [SerializeField] private float maxHP = 110f;
    [SerializeField] private float maxArmor = 50f;
    [SerializeField] private float moveSpeed = 6.5f;

    [Header("护甲恢复")]
    [SerializeField] private float armorRegenRate = 0.5f;       // 每秒恢复0.5点
    [SerializeField] private float armorRegenDelay = 3f;        // 脱战3秒后开始恢复

    public float MaxHP => maxHP;
    public float MaxArmor => maxArmor + upgradeArmorBonus;
    public float MoveSpeed => moveSpeed * MoveSpeedMultiplier;
    public float CurrentArmor { get; private set; }

    [Header("乘数体系（M2·v0.7.1：三选一升级 × 兽化加成 叠乘）")]
    private float upgradeDamageBonus;        // 三选一累积（0.2 = +20%）
    private float upgradeAttackSpeedBonus;
    private float upgradeMoveSpeedBonus;
    private float upgradeArmorBonus;         // 护甲上限加成（M2·v0.7.1）
    /// <summary>兽化临时乘数（WerewolfTransformation 进/出设置，默认 1）。</summary>
    public float BeastDamageMult { get; set; } = 1f;
    /// <summary>M4·v0.9.0 永久伤害乘数（魂商店"老练之魂"注入，默认 1）。</summary>
    public float PermanentDamageMult { get; set; } = 1f;

    [Header("暴击（M4·v0.9.0，数值书 §12.1：狼人 CR 12% / CD 150%）")]
    [SerializeField, Range(0f, 1f)] private float critChance = 0.12f;
    [SerializeField, Min(1f)] private float critDamage = 1.5f;
    public float CritChance => critChance;
    public float CritDamage => critDamage;
    public float BeastMoveSpeedMult { get; set; } = 1f;   // v0.8 兽化移速
    public float BeastAttackSpeedMult { get; set; } = 1f;

    public float DamageMultiplier => (1f + upgradeDamageBonus) * BeastDamageMult * PermanentDamageMult;
    public float AttackSpeedMultiplier => (1f + upgradeAttackSpeedBonus) * BeastAttackSpeedMult;
    public float MoveSpeedMultiplier => (1f + upgradeMoveSpeedBonus) * BeastMoveSpeedMult;

    public void AddDamageBonus(float v) { upgradeDamageBonus += v; OnStatsChanged?.Invoke(); }
    public void AddAttackSpeedBonus(float v) { upgradeAttackSpeedBonus += v; OnStatsChanged?.Invoke(); }
    public void AddMoveSpeedBonus(float v) { upgradeMoveSpeedBonus += v; OnStatsChanged?.Invoke(); }
    public void AddMaxArmor(float v) { upgradeArmorBonus += v; OnStatsChanged?.Invoke(); }

    [Header("钱包（M2·v0.7.0）")]
    private int coins;
    public int Coins => coins;
    /// <summary>金币变化事件（HUD/商店订阅；死亡重开时由 RunManager 重置场景自动归零——金币是局内资产）。</summary>
    public System.Action<int> OnCoinsChanged;

    public System.Action OnStatsChanged;

    private float lastDamageTime = -999f;  // 上次受伤时间（负值表示开局未受伤）
    private bool isOutOfCombat => Time.time - lastDamageTime >= armorRegenDelay;

    void Awake()
    {
        CurrentArmor = maxArmor;
    }

    void Update()
    {
        // 脱战3秒后，护甲开始自动恢复
        if (CurrentArmor < MaxArmor && isOutOfCombat)
        {
            CurrentArmor = Mathf.Min(CurrentArmor + armorRegenRate * Time.deltaTime, MaxArmor);
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
        CurrentArmor = Mathf.Clamp(CurrentArmor + delta, 0, MaxArmor);
        OnStatsChanged?.Invoke();
    }

    /// <summary>获得金币（M2·v0.7.0：敌人掉落/宝箱/卖物共用入口）。</summary>
    public void AddCoins(int amount)
    {
        if (amount <= 0) return;
        coins += amount;
        OnCoinsChanged?.Invoke(coins);
    }

    /// <summary>花费金币；不足返回 false 且不扣（商店购买用）。</summary>
    public bool TrySpendCoins(int amount)
    {
        if (amount < 0 || coins < amount) return false;
        coins -= amount;
        OnCoinsChanged?.Invoke(coins);
        return true;
    }
}
