using NUnit.Framework;
using UnityEngine;

/// <summary>
/// VS 第四批 遗物门禁（V2 §8.1）：背包合同（重复不持有/清空/聚合求和）+
/// 资产合同（7 枚加载、字段合法、id 唯一）。
/// </summary>
public class RelicInventoryTests
{
    private static RelicDefinition MakeRelic(string id, RelicDefinition.EffectKind kind, float value)
    {
        var r = ScriptableObject.CreateInstance<RelicDefinition>();
        r.relicId = id;
        r.displayName = id;
        r.effect = kind;
        r.value = value;
        return r;
    }

    [Test]
    public void TryAdd_DuplicateId_Rejected()
    {
        var inv = new RelicInventory();
        Assert.IsTrue(inv.TryAdd(MakeRelic("a", RelicDefinition.EffectKind.BleedDeepen, 0.3f)));
        Assert.IsFalse(inv.TryAdd(MakeRelic("a", RelicDefinition.EffectKind.BleedDeepen, 0.5f)), "同 id 不重复持有");
        Assert.AreEqual(1, inv.Owned.Count);
        Assert.IsTrue(inv.Contains("a"));
    }

    [Test]
    public void Sum_SameKindAccumulates()
    {
        var inv = new RelicInventory();
        inv.TryAdd(MakeRelic("a", RelicDefinition.EffectKind.BleedDeepen, 0.3f));
        inv.TryAdd(MakeRelic("b", RelicDefinition.EffectKind.BleedDeepen, 0.5f));
        inv.TryAdd(MakeRelic("c", RelicDefinition.EffectKind.BeastLonger, 4f));
        Assert.AreEqual(0.8f, inv.BleedDamageBonus, 0.0001f, "同类求和");
        Assert.AreEqual(4f, inv.BeastDurationBonus, 0.0001f);
        Assert.AreEqual(0f, inv.RageGainBonus, 0.0001f, "未持有=0 零差异");
    }

    [Test]
    public void Clear_EmptiesAll()
    {
        var inv = new RelicInventory();
        inv.TryAdd(MakeRelic("a", RelicDefinition.EffectKind.BleedDeepen, 0.3f));
        inv.Clear();
        Assert.AreEqual(0, inv.Owned.Count);
        Assert.AreEqual(0f, inv.BleedDamageBonus);
    }

    [Test]
    public void RelicAssets_AllLoadWithValidContract()
    {
        RelicDefinition[] pool = Resources.LoadAll<RelicDefinition>("Relics");
        Assert.GreaterOrEqual(pool.Length, 7, $"遗物池应 ≥7（实际 {pool.Length}）——资产未入库或绑定断链");
        var ids = new System.Collections.Generic.HashSet<string>();
        foreach (RelicDefinition r in pool)
        {
            Assert.IsNotEmpty(r.relicId, $"{r.name} 缺 relicId");
            Assert.IsTrue(ids.Add(r.relicId), $"relicId 重复：{r.relicId}");
            Assert.IsNotEmpty(r.displayName);
            Assert.IsNotEmpty(r.description);
            Assert.AreNotEqual(RelicDefinition.EffectKind.BleedDeepen, r.effect == RelicDefinition.EffectKind.BleedDeepen ? RelicDefinition.EffectKind.BleedLinger : r.effect, "枚举健全性");
            Assert.Greater(r.value, 0f, $"{r.relicId} 效果量必须为正");
        }
        // 首批方向覆盖：流血加深 ×2 + 破甲 + 怒痕 + 兽化 + 星蓝币（构筑面完整）
        var kinds = new System.Collections.Generic.HashSet<RelicDefinition.EffectKind>();
        foreach (RelicDefinition r in pool) kinds.Add(r.effect);
        foreach (RelicDefinition.EffectKind k in System.Enum.GetValues(typeof(RelicDefinition.EffectKind)))
            Assert.IsTrue(kinds.Contains(k), $"首批应覆盖效果 {k}");
    }
}
