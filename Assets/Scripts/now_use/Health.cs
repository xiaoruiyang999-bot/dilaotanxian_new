using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 5f;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public float HealthRatio => maxHealth > 0 ? CurrentHealth / maxHealth : 0f;
    public bool IsDead { get; private set; }

    public System.Action<float, float> OnHealthChanged;
    public System.Action OnDeath;

    void Awake()
    {
        Initialize(maxHealth);
    }

    public void Initialize(float health)
    {
        maxHealth = health;
        CurrentHealth = maxHealth;
        IsDead = false;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void TakeDamage(float damage)
    {
        if (IsDead) return;
        if (damage <= 0) return;

        float damageToHealth = damage;

        // 玩家（有 PlayerStats）：优先由护甲承受伤害，剩余部分再扣 HP
        if (TryGetComponent<PlayerStats>(out var stats))
        {
            damageToHealth = stats.AbsorbDamageWithArmor(damage);
            stats.OnTakeDamage(); // 重置脱战计时
        }

        // 应用剩余伤害到生命值
        if (damageToHealth > 0)
        {
            CurrentHealth = Mathf.Max(CurrentHealth - damageToHealth, 0f);
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        if (CurrentHealth <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        CurrentHealth = Mathf.Min(CurrentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
        IsDead = false;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    private void Die()
    {
        IsDead = true;
        OnDeath?.Invoke();
    }
}
