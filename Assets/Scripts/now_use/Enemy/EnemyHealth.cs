using UnityEngine;

/// <summary>
/// 敌人生命管理。实现IDamageable，提供受伤/治疗/死亡，被攻击时通知EnemyAI。
/// v0.7.1 减伤甲：护甲三字段读 EnemyStats（仅精英/Boss 配置，普通怪 MaxArmor=0 天然全额扣血），
/// 结算走 DamageResolver.ApplyArmor（玩家/怪物共用一份实现），护甲变化走独立 OnArmorChanged 事件。
/// </summary>
public class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 3f;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsDead { get; private set; }

    // 护甲（v0.7.1）：数据源在 EnemyStats，本类只持当前值
    private EnemyStats stats;
    private float currentArmor = float.NaN; // NaN=未初始化（懒读 MaxArmor，保证组件 Awake 顺序无关）

    private EnemyStats Stats => stats != null ? stats : (stats = GetComponent<EnemyStats>());

    /// <summary>护甲上限（直读 EnemyStats，0=无甲）。不随楼层缩放（ApplyFloorScale 只缩 HP）。</summary>
    public float MaxArmor => Stats != null ? Stats.MaxArmor : 0f;
    /// <summary>当前护甲。</summary>
    public float CurrentArmor => float.IsNaN(currentArmor) ? MaxArmor : currentArmor;

    // 事件
    public System.Action<float, float> OnHealthChanged; // (current, max) 供血条监听
    public System.Action<float, float> OnArmorChanged;  // (current, max) 供护甲条监听（v0.7.1）
    public System.Action OnDeath;                        // 死亡通知
    public System.Action OnTakeDamage;                   // 被攻击时通知AI（关键！）

    void Awake()
    {
        CurrentHealth = maxHealth;
        IsDead = false;
        currentArmor = MaxArmor;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    /// <summary>实现IDamageable接口</summary>
    public void TakeDamage(float damage)
    {
        if (IsDead) return;

        // 减伤甲结算（v0.7.1）：无甲敌人 armor=0，ApplyArmor 原样返回全额伤害，普通怪路径数值与旧版一致
        EnemyStats s = Stats;
        float hpDamage = DamageResolver.ApplyArmor(damage, CurrentArmor,
            s != null ? s.ArmorReduceMul : 0f, s != null ? s.ArmorLossMul : 1f, out float armorAfter);
        if (!Mathf.Approximately(armorAfter, CurrentArmor))
        {
            currentArmor = Mathf.Max(0f, armorAfter);
            OnArmorChanged?.Invoke(currentArmor, MaxArmor);
        }

        CurrentHealth = Mathf.Max(CurrentHealth - hpDamage, 0f);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        OnTakeDamage?.Invoke(); // 通知AI：我被打了！

        if (CurrentHealth <= 0f)
        {
            Die();
        }
    }

    /// <summary>
    /// 真实伤害入口（v0.7.5 裸绞，DamageContext.trueDamage 通道）：绕过减伤甲结算直接扣血。
    /// 仅 DamageResolver.Deal 的真伤分支调用；普通受伤路径（TakeDamage）不受影响。
    /// </summary>
    public void TakeTrueDamage(float damage)
    {
        if (IsDead) return;
        if (damage <= 0f) return;

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

    /// <summary>回复护甲（v0.7.1 预留，不超上限）。</summary>
    public void AddArmor(float amount)
    {
        if (IsDead) return;
        if (amount <= 0f || MaxArmor <= 0f) return;
        currentArmor = Mathf.Min(CurrentArmor + amount, MaxArmor);
        OnArmorChanged?.Invoke(currentArmor, MaxArmor);
    }

    public void Die()
    {
        if (IsDead) return;
        IsDead = true;
        OnDeath?.Invoke();
    }

    /// <summary>楼层 HP 缩放（v0.5.4）：上限倍乘并刷新当前血量（Awake 已初始化，必须同步刷新）。护甲不缩放。</summary>
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
        currentArmor = MaxArmor; // 护甲同步回满（v0.7.1）
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        OnArmorChanged?.Invoke(currentArmor, MaxArmor);
    }
}
