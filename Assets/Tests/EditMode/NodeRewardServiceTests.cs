using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// v2.0.6 第二批 奖励池门禁：三类各一/不重复/精英倍率/数值自洽（描述值=应用值）/seed 复现。
/// </summary>
public class NodeRewardServiceTests
{
    [Test]
    public void Roll_ThreeOptions_AllKindsUnique()
    {
        for (int seed = 1; seed <= 50; seed++)
        {
            List<NodeRewardService.RewardOption> opts = NodeRewardService.Roll(seed, elite: false);
            // VS 第二~四批：基础三类 + 35% 武器变体位 + 25% 遗物位（3~5 张，类别不重复）
            Assert.That(opts.Count, Is.InRange(3, 5), $"seed={seed} 必须三~五选一");
            Assert.AreEqual(opts.Count, opts.Select(o => o.Kind).Distinct().Count(), $"seed={seed} 类别不得重复");
        }
    }

    [Test]
    public void Roll_EliteValues_DoubledOrHigher()
    {
        List<NodeRewardService.RewardOption> normal = NodeRewardService.Roll(7, elite: false);
        List<NodeRewardService.RewardOption> elite = NodeRewardService.Roll(7, elite: true);
        float nCoins = normal.Find(o => o.Kind == NodeRewardService.RewardKind.Coins).Value;
        float eCoins = elite.Find(o => o.Kind == NodeRewardService.RewardKind.Coins).Value;
        Assert.GreaterOrEqual(eCoins, nCoins, "精英币袋不低于普通");
        Assert.Greater(
            elite.Find(o => o.Kind == NodeRewardService.RewardKind.AttackUp).Value,
            normal.Find(o => o.Kind == NodeRewardService.RewardKind.AttackUp).Value,
            "精英攻强必须更高");
    }

    [Test]
    public void Roll_DescriptionMatchesValue()
    {
        // 数值自洽（标题/描述展示值=应用值——曾出现两次独立随机导致不一致的实现缺陷）
        List<NodeRewardService.RewardOption> opts = NodeRewardService.Roll(42, elite: false);
        var coins = opts.Find(o => o.Kind == NodeRewardService.RewardKind.Coins);
        StringAssert.Contains(((int)coins.Value).ToString(), coins.Description);
        var heal = opts.Find(o => o.Kind == NodeRewardService.RewardKind.Heal);
        StringAssert.Contains(((int)heal.Value).ToString(), heal.Description);
    }

    [Test]
    public void Roll_SameSeed_Deterministic()
    {
        List<NodeRewardService.RewardOption> a = NodeRewardService.Roll(99, elite: true);
        List<NodeRewardService.RewardOption> b = NodeRewardService.Roll(99, elite: true);
        for (int i = 0; i < 3; i++)
        {
            Assert.AreEqual(a[i].Kind, b[i].Kind);
            Assert.AreEqual(a[i].Value, b[i].Value);
        }
    }

    [Test]
    public void Roll_SometimesOffersWeaponVariant_SlotWellFormed()
    {
        int withVariant = 0;
        for (int seed = 1; seed <= 60; seed++)
        {
            List<NodeRewardService.RewardOption> opts = NodeRewardService.Roll(seed, elite: false);
            var variant = opts.Find(o => o.Kind == NodeRewardService.RewardKind.WeaponVariant);
            if (variant.Kind == NodeRewardService.RewardKind.WeaponVariant)
            {
                withVariant++;
                Assert.AreEqual(default(WeaponData), variant.Weapon, "纯函数不填武器——由调用方 Fill");
            }
            // 基础三类始终齐全（变体是追加位）
            foreach (NodeRewardService.RewardKind kind in new[]
                     { NodeRewardService.RewardKind.Coins, NodeRewardService.RewardKind.Heal,
                       NodeRewardService.RewardKind.AttackUp })
                Assert.IsTrue(opts.Exists(o => o.Kind == kind), $"seed={seed} 基础项 {kind} 缺失");
        }
        Assert.Greater(withVariant, 5, "60 次至少若干次出现变体位（35% 概率）");
        Assert.Less(withVariant, 60, "不应每次都出（概率位）");
    }
}
