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

    [Header("体力（v0.6.0）")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaRegenRate = 30f;      // 每秒回复30点
    [SerializeField] private float staminaRegenDelay = 0.8f;    // 停止消耗0.8秒后开始回复

    public float MaxHP => maxHP;
    public float MaxArmor => maxArmor;
    public float MoveSpeed => moveSpeed;
    public float CurrentArmor { get; private set; }
    public float MaxStamina => maxStamina;
    public float CurrentStamina { get; private set; }

    public System.Action OnStatsChanged;

    private float lastDamageTime = -999f;  // 上次受伤时间（负值表示开局未受伤）
    private bool isOutOfCombat => Time.time - lastDamageTime >= armorRegenDelay;

    private float lastStaminaConsumeTime = -999f;  // 上次体力消耗时间（负值表示开局未消耗）
    private bool canRegenStamina => Time.time - lastStaminaConsumeTime >= staminaRegenDelay;

    void Awake()
    {
        CurrentArmor = maxArmor;
        CurrentStamina = maxStamina;
    }

    void Update()
    {
        // 脱战3秒后，护甲开始自动恢复
        if (CurrentArmor < maxArmor && isOutOfCombat)
        {
            CurrentArmor = Mathf.Min(CurrentArmor + armorRegenRate * Time.deltaTime, maxArmor);
            OnStatsChanged?.Invoke();
        }

        // 停止消耗0.8秒后，体力开始自动回复（回复满为止）
        if (CurrentStamina < maxStamina && canRegenStamina)
        {
            CurrentStamina = Mathf.Min(CurrentStamina + staminaRegenRate * Time.deltaTime, maxStamina);
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
            Debug.Log($"[PlayerStats] Armor absorbed {absorbed}, remainingArmor={CurrentArmor}/{maxArmor}");
        }

        return damage - absorbed;
    }

    /// <summary>
    /// 尝试一次性消耗体力（如闪避）。体力不足时不扣减并返回 false。
    /// 任何消耗都会重置体力回复延迟计时。
    /// </summary>
    public bool TryConsumeStamina(float amount)
    {
        if (amount <= 0f) return true;
        if (CurrentStamina < amount) return false;

        CurrentStamina -= amount;
        lastStaminaConsumeTime = Time.time;
        OnStatsChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 按速率持续消耗体力（如奔跑，在 Update/FixedUpdate 中每帧调用）。
    /// 消耗到 0 为止；任何消耗都会重置体力回复延迟计时。
    /// </summary>
    public void ConsumeStaminaOverTime(float ratePerSec)
    {
        if (ratePerSec <= 0f || CurrentStamina <= 0f) return;

        CurrentStamina = Mathf.Max(CurrentStamina - ratePerSec * Time.deltaTime, 0f);
        lastStaminaConsumeTime = Time.time;
        OnStatsChanged?.Invoke();
    }

    public void ModifyArmor(float delta)
    {
        CurrentArmor = Mathf.Clamp(CurrentArmor + delta, 0, maxArmor);
        OnStatsChanged?.Invoke();
    }
}
