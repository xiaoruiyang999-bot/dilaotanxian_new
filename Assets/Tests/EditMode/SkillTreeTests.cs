using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 技能树门禁（v1.1.47）：树定义结构合法性（id 唯一/前置指向同支线上一层/线首无前置）、
/// 效果聚合正确性、SkillTreeSave 读写往返。PlayerPrefs 测试污染用 Setup/TearDown 清键。
/// </summary>
public class SkillTreeTests
{
    [SetUp]
    public void SetUp() => SkillTreeSave.ResetAll();

    [TearDown]
    public void TearDown() => SkillTreeSave.ResetAll();

    [Test]
    public void TreeDefinition_IdsUnique_AndBranchTierCoverAllCells()
    {
        var seen = new HashSet<int>();
        for (int branch = 0; branch < SkillTreeDef.BranchCount; branch++)
            for (int tier = 0; tier < SkillTreeDef.TierCount; tier++)
            {
                var node = FindByCell(branch, tier);
                Assert.NotNull(node, $"支线{branch} 层{tier} 缺节点");
                Assert.IsTrue(seen.Add(node.id), $"节点 id {node.id} 重复");
                Assert.AreEqual(branch, node.branch, $"节点 {node.name} branch 字段与坐标不符");
                Assert.AreEqual(tier, node.tier, $"节点 {node.name} tier 字段与坐标不符");
            }
    }

    [Test]
    public void TreeDefinition_RequirementChain_LinearPerBranch()
    {
        foreach (var node in SkillTreeDef.Nodes)
        {
            if (node.tier == 0)
            {
                Assert.AreEqual(-1, node.requiresId, $"线首 {node.name} 不应有前置");
                continue;
            }
            var req = SkillTreeDef.Find(node.requiresId);
            Assert.NotNull(req, $"{node.name} 前置 id={node.requiresId} 不存在");
            Assert.AreEqual(node.branch, req.branch, $"{node.name} 前置跨线（不允许）");
            Assert.AreEqual(node.tier - 1, req.tier, $"{node.name} 前置应为同线上一层");
        }
    }

    [Test]
    public void Aggregate_AllNodesUnlocked_SumsEveryBranch()
    {
        var all = new List<int>();
        foreach (var n in SkillTreeDef.Nodes) all.Add(n.id);

        SkillTreeDef.Aggregate(all, out float dmg, out float move, out float atkSpeed,
            out float hp, out float armor, out float critDmg);

        Assert.AreEqual(30f, dmg, 0.001f);         // 8+10+12
        Assert.AreEqual(14f, move, 0.001f);         // 6+8
        Assert.AreEqual(20f, atkSpeed, 0.001f);     // 8+12
        Assert.AreEqual(50f, hp, 0.001f);           // 20+30
        Assert.AreEqual(25f, armor, 0.001f);        // 10+15
        Assert.AreEqual(25f, critDmg, 0.001f);      // 25
    }

    [Test]
    public void RequirementMet_OnlyWorksAfterParentUnlocked()
    {
        var node = FindByCell(0, 1);   // 重击 II，前置 重击 I(0)
        Assert.IsFalse(SkillTreeDef.RequirementMet(node, new List<int>()), "未解锁前置时应不可点");
        Assert.IsTrue(SkillTreeDef.RequirementMet(node, new List<int> { 0 }), "解锁前置后应可点");
    }

    [Test]
    public void Save_EssenceRoundtrip_AndUnlockPersistence()
    {
        SkillTreeSave.AddEssence(7);
        Assert.AreEqual(7, SkillTreeSave.Essence, "AddEssence 后余额应一致");

        Assert.IsTrue(SkillTreeSave.TrySpendEssence(3));
        Assert.AreEqual(4, SkillTreeSave.Essence);
        Assert.IsFalse(SkillTreeSave.TrySpendEssence(99), "超支应被拒绝");

        SkillTreeSave.Unlock(10);
        SkillTreeSave.Unlock(11);
        Assert.IsTrue(SkillTreeSave.IsUnlocked(10));
        Assert.IsTrue(SkillTreeSave.IsUnlocked(11));
        Assert.IsFalse(SkillTreeSave.IsUnlocked(12));
    }

    private static SkillTreeNodeDef FindByCell(int branch, int tier)
    {
        foreach (var n in SkillTreeDef.Nodes)
            if (n.branch == branch && n.tier == tier) return n;
        return null;
    }
}
