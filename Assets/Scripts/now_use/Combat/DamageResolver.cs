/// <summary>
/// 伤害结算静态入口（v0.7.0，计划书 2.1）。
/// 玩家造成伤害的单点收口：Deal(target, ctx) → target.TakeDamage(ctx.Roll())。
/// v0.7.1 在此处分流 Health 的减伤甲结算（armorReduceMul/armorLossMul）。
/// 敌人侧伤害本版不走此管线，保持 AttackData 直扣原路径。
/// </summary>
public static class DamageResolver
{
    /// <summary>
    /// 结算一次伤害：Roll 出最终伤害（含暴击）并写入目标。
    /// 返回实际结算伤害，供表现层/测试断言使用。
    /// </summary>
    public static float Deal(IDamageable target, DamageContext ctx)
    {
        if (target == null) return 0f;
        float final = ctx.Roll();
        target.TakeDamage(final);
        return final;
    }
}
