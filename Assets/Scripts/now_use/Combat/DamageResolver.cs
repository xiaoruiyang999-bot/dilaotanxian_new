using UnityEngine;

/// <summary>
/// 伤害结算静态入口（v0.7.0，计划书 2.1）。
/// 玩家造成伤害的单点收口：Deal(target, ctx) → target.TakeDamage(ctx.Roll())。
/// v0.7.1 新增 ApplyArmor 减伤甲纯函数（玩家/怪物共用一份实现，公式文档 §三/§六）：
/// 玩家侧经 PlayerStats.ApplyArmorDamage 调用，敌人侧经 EnemyHealth.TakeDamage 调用。
/// </summary>
public static class DamageResolver
{
    /// <summary>
    /// 玩家对 EnemyHealth 造成的实际 HP 伤害事件。死亡目标、无效命中与场景物件不发送，
    /// 供怒痕等战斗资源订阅。
    /// </summary>
    public static event System.Action<float> OnPlayerDamageDealt;

    /// <summary>
    /// 结算一次伤害：Roll 出最终伤害（含暴击）并写入目标。
    /// trueDamage &gt; 0 时为真伤包（v0.7.5 裸绞）：跳过 Roll 与护甲结算直接扣血
    /// （EnemyHealth 走 TakeTrueDamage 专用入口，其余 IDamageable 原路径）。
    /// 返回实际结算伤害，供表现层/测试断言使用。
    /// </summary>
    public static float Deal(IDamageable target, DamageContext ctx)
    {
        if (target == null) return 0f;
        EnemyHealth enemy = target as EnemyHealth;
        float healthBefore = enemy != null && !enemy.IsDead ? enemy.CurrentHealth : 0f;

        if (ctx.trueDamage > 0f)
        {
            if (enemy != null)
                enemy.TakeTrueDamage(ctx.trueDamage);
            else
                target.TakeDamage(ctx.trueDamage);

            return PublishActualEnemyDamage(enemy, healthBefore, ctx.trueDamage);
        }

        float final = ctx.Roll();
        target.TakeDamage(final);
        return PublishActualEnemyDamage(enemy, healthBefore, final);
    }

    private static float PublishActualEnemyDamage(EnemyHealth enemy, float healthBefore, float fallbackDamage)
    {
        // 场景机关等旧 IDamageable 仍保留原返回语义，但绝不触发玩家怒痕事件。
        if (enemy == null) return fallbackDamage;
        if (healthBefore <= 0f) return 0f;

        float actual = Mathf.Max(0f, healthBefore - enemy.CurrentHealth);
        if (actual > 0f)
            OnPlayerDamageDealt?.Invoke(actual);
        return actual;
    }

    /// <summary>
    /// 减伤甲结算纯函数（v0.7.1，公式文档 §三/§六），玩家与怪物共用：
    /// armor &gt; 0：扣血 = damage×(1−reduceMul)，扣甲 = damage×lossMul（最低扣到 0，溢出不转嫁，本次扣血仍按有甲算完）；
    /// armor ≤ 0：全额扣血，免伤失效。
    /// R∈[0,0.9]、L&gt;0 由数据侧 OnValidate 钳制保证（公式文档 §六-8）。
    /// </summary>
    /// <param name="damage">面板伤害 D（攻击方链已算完，含暴击）</param>
    /// <param name="armor">受击方当前护甲</param>
    /// <param name="reduceMul">免伤倍率 R</param>
    /// <param name="lossMul">扣甲倍率 L</param>
    /// <param name="armorAfter">结算后的护甲值</param>
    /// <returns>应扣 HP 的伤害</returns>
    public static float ApplyArmor(float damage, float armor, float reduceMul, float lossMul, out float armorAfter)
    {
        if (armor > 0f)
        {
            armorAfter = Mathf.Max(0f, armor - damage * lossMul);
            return damage * (1f - reduceMul);
        }

        armorAfter = 0f;
        return damage;
    }
}
