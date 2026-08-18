using UnityEngine;

/// <summary>
/// 敌人生命管理。实现IDamageable，提供受伤/治疗/死亡，被攻击时通知EnemyAI。
/// </summary>
public class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 3f;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsDead { get; private set; }

    public void ApplyMaxHealthMultiplier(float multiplier)
    {
        if (IsDead) return;
        maxHealth *= Mathf.Max(0.1f, multiplier);
        CurrentHealth = maxHealth;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    // 事件
    public System.Action<float, float> OnHealthChanged; // (current, max) 供血条监听
    public System.Action OnDeath;                        // 死亡通知
    public System.Action OnTakeDamage;                   // 被攻击时通知AI（关键！）

    void Awake()
    {
        CurrentHealth = maxHealth;
        IsDead = false;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    /// <summary>实现IDamageable接口</summary>
    public void TakeDamage(float damage)
    {
        if (IsDead) return;

        CurrentHealth = Mathf.Max(CurrentHealth - damage, 0f);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        OnTakeDamage?.Invoke(); // 通知AI：我被打了！

        if (CurrentHealth <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        CurrentHealth = Mathf.Min(CurrentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void Die()
    {
        if (IsDead) return;
        IsDead = true;
        OnDeath?.Invoke();
    }

    /// <summary>楼层 HP 缩放（v0.5.4）：上限倍乘并刷新当前血量（Awake 已初始化，必须同步刷新）。</summary>
    public void ScaleMaxHealth(float mul)
    {
        maxHealth *= mul;
        CurrentHealth = maxHealth;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
        IsDead = false;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }
}
