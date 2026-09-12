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
            Assert.AreEqual(3, opts.Count, $"seed={seed} 必须三选一");
            Assert.AreEqual(3, opts.Select(o => o.Kind).Distinct().Count(), $"seed={seed} 三类不得重复");
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
}
