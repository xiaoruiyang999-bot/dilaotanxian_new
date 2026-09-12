using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VS 第四批 本局遗物背包 + 数值聚合（V2 §8.1）：
/// 持有 = RelicDefinition 列表（同 id 不重复持有——重复遗物不进池，MVP 无升级）；
/// 聚合 = 各效果求和，供 EnemyStatus/WerewolfRage/CoinDrop 消费。
/// 生命周期：新 Run 清空（死亡/通关随 Carrier 重置——R12 口径）；数据挂 RunStateCarrier。
/// </summary>
[Serializable]
public class RelicInventory
{
    private readonly List<RelicDefinition> relics = new List<RelicDefinition>();
    public IReadOnlyList<RelicDefinition> Owned => relics;

    public bool TryAdd(RelicDefinition relic)
    {
        if (relic == null) return false;
        foreach (RelicDefinition r in relics)
            if (r.relicId == relic.relicId) return false;   // 重复不持有（不升级，MVP）
        relics.Add(relic);
        return true;
    }

    public bool Contains(string relicId)
    {
        foreach (RelicDefinition r in relics)
            if (r.relicId == relicId) return true;
        return false;
    }

    public void Clear() => relics.Clear();

    // ---------- 聚合通道（各系统消费：求和口径，同类多枚叠加） ----------

    public float Sum(RelicDefinition.EffectKind kind)
    {
        float sum = 0f;
        foreach (RelicDefinition r in relics)
            if (r.effect == kind) sum += r.value;
        return sum;
    }

    public float BleedDamageBonus => Sum(RelicDefinition.EffectKind.BleedDeepen);
    public float BleedDurationBonus => Sum(RelicDefinition.EffectKind.BleedLinger);
    public float ArmorBreakDurationBonus => Sum(RelicDefinition.EffectKind.ArmorBreakExtend);
    public float RageGainBonus => Sum(RelicDefinition.EffectKind.RageFaster);
    public float BeastDurationBonus => Sum(RelicDefinition.EffectKind.BeastLonger);
    public float CoinValueBonus => Sum(RelicDefinition.EffectKind.CoinMagnet);
}

/// <summary>全局访问点：挂 RunStateCarrier（本局真值），未初始化返回空背包（零差异）。</summary>
public static class RelicRuntime
{
    private static readonly RelicInventory empty = new RelicInventory();
    public static RelicInventory Run => RunStateCarrier.Ensure()?.Relics ?? empty;
}
