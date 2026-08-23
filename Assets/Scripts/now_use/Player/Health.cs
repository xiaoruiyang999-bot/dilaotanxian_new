using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 5f;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public float HealthRatio => maxHealth > 0 ? CurrentHealth / maxHealth : 0f;
    public bool IsDead { get; private set; }

    /// <summary>无敌帧截止时刻（Time.time 口径）；Dash 等系统通过 GrantIFrames 授予（M1·v0.6.1）。</summary>
    private float iFrameUntil;
    public bool IsInvulnerable => Time.time < iFrameUntil;

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
        iFrameUntil = 0f;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    /// <summary>授予无敌帧（M1.2·v0.6.1 Dash 用）：duration 秒内 TakeDamage 直接跳过。重复授予取更晚截止。</summary>
    public void GrantIFrames(float duration)
    {
        if (duration <= 0f) return;
        iFrameUntil = Mathf.Max(iFrameUntil, Time.time + duration);
    }

    /// <summary>
    /// 等比缩放血量上限（v0.6.9 兽化用）：maxHealth 与当前血同乘，血条比例不变但绝对量增减。
    /// multiplier < 1 时当前血按同比例回落（不截断为满血，保留受伤状态）。
    /// </summary>
    public void ScaleMaxHealth(float multiplier)
    {
        if (multiplier <= 0f) return;
        maxHealth = Mathf.Max(1f, maxHealth * multiplier);
        CurrentHealth = Mathf.Clamp(CurrentHealth * multiplier, 0f, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    /// <summary>永久增加上限（M2 升级用）：alsoHeal=true 时当前血同步加等量（+2 上限送 2 血）。</summary>
    public void AddMaxHealth(float amount, bool alsoHeal)
    {
        if (amount <= 0f) return;
        maxHealth += amount;
        if (alsoHeal) CurrentHealth = Mathf.Min(CurrentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void TakeDamage(float damage)
    {
        if (IsDead) return;
        if (damage <= 0) return;
        if (Time.time < iFrameUntil) return;

        // 玩家受击反馈：音效 + 屏幕震动（M1.5·v0.6.1，一行式挂点）
        AudioManager.PlaySFX("hurt");
        CameraFollow.ShakeMain(0.18f, 0.2f);

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
        iFrameUntil = 0f;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    private void Die()
    {
        IsDead = true;
        OnDeath?.Invoke();
    }
}
