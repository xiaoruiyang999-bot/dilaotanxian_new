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

    public float MaxHP => maxHP;
    public float MaxArmor => maxArmor;
    public float MoveSpeed => moveSpeed;
    public float CurrentArmor { get; private set; }

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
}
