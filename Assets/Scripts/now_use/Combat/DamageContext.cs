/// <summary>
/// 伤害上下文（v0.7.0，计划书 2.1，公式口径见《伤害计算公式文档.md》）。
/// 玩家侧一次伤害的完整参数包：
///   baseAttack  = 角色攻击 + 武器攻击（基础攻击区）；
///   multiplier  = 倍率区（蓄力/技能等，独立于基础攻击）；
///   critRate / critDamage = 暴击率 / 暴击伤害倍率。
/// Roll() 做一次暴击判定并返回最终伤害：
///   baseAttack × multiplier × (isCrit ? critDamage : 1)。
/// IsCrit 结果外露供表现层（暴击视觉 v0.7.11 接入）。
/// </summary>
public struct DamageContext
{
    /// <summary>基础攻击区：角色攻击 + 武器攻击。</summary>
    public float baseAttack;

    /// <summary>倍率区：蓄力/技能等（默认 1）。</summary>
    public float multiplier;

    /// <summary>暴击率（0~1）。</summary>
    public float critRate;

    /// <summary>暴击伤害倍率（如 1.5）。</summary>
    public float critDamage;

    /// <summary>最近一次 Roll() 是否暴击（供表现层读取）。</summary>
    public bool IsCrit { get; private set; }

    /// <summary>一次暴击判定并返回最终伤害。</summary>
    public float Roll()
    {
        IsCrit = UnityEngine.Random.value < critRate;
        return baseAttack * multiplier * (IsCrit ? critDamage : 1f);
    }
}
