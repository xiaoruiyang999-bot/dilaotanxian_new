using UnityEngine;

/// <summary>
/// 公共伤害系统。
/// TODO v0.5+: 实现统一伤害计算（护甲穿透、暴击率、元素伤害、伤害飘字等）
/// </summary>
public static class DamageSystem
{
    /// <summary>
    /// 应用伤害。当前为直接传递，后续加入伤害计算逻辑。
    /// </summary>
    public static void ApplyDamage(IDamageable target, float rawDamage)
    {
        // TODO: 后续加入护甲减伤、暴击判定、抗性计算等
        target.TakeDamage(rawDamage);
    }

    /// <summary>
    /// 计算最终伤害。当前直接返回原始值，后续加入公式。
    /// </summary>
    public static float CalculateFinalDamage(float rawDamage, float armor, float resistance)
    {
        // TODO: 实现伤害公式，例如：final = rawDamage * (1 - armor/(armor+100))
        return rawDamage;
    }
}
