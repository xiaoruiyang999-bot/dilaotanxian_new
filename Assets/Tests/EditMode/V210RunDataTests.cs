using System.Collections.Generic;
using NUnit.Framework;

public class V210RunDataTests
{
    [Test]
    public void FirstCatalog_HasThirteenStableDistinctIdsAndFixedTiers()
    {
        var ids = new HashSet<string>();
        int swift = 0, rift = 0, oath = 0;
        int tierI = 0, tierII = 0, tierIII = 0;
        foreach (SageInscriptionDefinition definition in SageInscriptionCatalog.All)
        {
            Assert.IsTrue(ids.Add(definition.AbilityId), "刻印 ID 必须唯一");
            Assert.IsTrue(definition.IsCompatible(PlayableCharacterId.Werewolf));
            switch (definition.Lineage)
            {
                case InscriptionLineage.SwiftBlade: swift++; break;
                case InscriptionLineage.StarRift: rift++; break;
                case InscriptionLineage.BloodOath: oath++; break;
            }
            switch (definition.Tier)
            {
                case InscriptionTier.I: tierI++; break;
                case InscriptionTier.II: tierII++; break;
                case InscriptionTier.III: tierIII++; break;
                default: Assert.Fail("v2.1 不投放 IV～VI 空品阶"); break;
            }
        }
        Assert.AreEqual(13, ids.Count);
        Assert.AreEqual(4, swift);
        Assert.AreEqual(5, rift);
        Assert.AreEqual(4, oath);
        Assert.AreEqual(4, tierI);
        Assert.AreEqual(6, tierII);
        Assert.AreEqual(3, tierIII);
    }

    [Test]
    public void Build_UpgradeSameIdDoesNotInflateResonance()
    {
        var build = new RunBuildState();
        Assert.IsTrue(build.TryAcquire("swift_edge", PlayableCharacterId.Werewolf, out bool upgraded));
        Assert.IsFalse(upgraded);
        Assert.IsTrue(build.TryAcquire("swift_edge", PlayableCharacterId.Werewolf, out upgraded));
        Assert.IsTrue(upgraded);
        Assert.IsTrue(build.TryAcquire("swift_edge", PlayableCharacterId.Werewolf, out upgraded));
        Assert.IsFalse(build.TryAcquire("swift_edge", PlayableCharacterId.Werewolf, out _), "第 4 次不得越过 3 级");
        Assert.AreEqual(3, build.inscriptions[0].level);
        Assert.AreEqual(1, build.CountDistinct(InscriptionLineage.SwiftBlade));
        Assert.AreEqual(0, build.GetResonanceStage(InscriptionLineage.SwiftBlade));

        Assert.IsTrue(build.TryAcquire("swift_peril_step", PlayableCharacterId.Werewolf, out _));
        Assert.AreEqual(1, build.GetResonanceStage(InscriptionLineage.SwiftBlade));
        build.TryAcquire("swift_overflow", PlayableCharacterId.Werewolf, out _);
        build.TryAcquire("swift_combo", PlayableCharacterId.Werewolf, out _);
        Assert.AreEqual(4, build.CountDistinct(InscriptionLineage.SwiftBlade));
        Assert.AreEqual(2, build.GetResonanceStage(InscriptionLineage.SwiftBlade));
    }

    [Test]
    public void RankMatrix_OnlyPositiveEligibleTiersReceiveWeight()
    {
        for (int rank = 1; rank <= 5; rank++)
        {
            int total = 0;
            for (int tier = 1; tier <= 6; tier++)
                total += InscriptionRankRules.GetBaseWeight(rank, (InscriptionTier)tier);
            Assert.AreEqual(100, total, "六档原始概率每行合计 100");
        }

        Assert.IsTrue(InscriptionRankRules.TryGetEffectiveWeights(1,
            new[] { true, true, true, false, false, false }, out float[] rank1));
        Assert.AreEqual(0.6f, rank1[0], 0.0001f);
        Assert.AreEqual(0.4f, rank1[1], 0.0001f);
        Assert.AreEqual(0f, rank1[2]);

        Assert.IsTrue(InscriptionRankRules.TryGetEffectiveWeights(4,
            new[] { true, true, true, false, false, false }, out float[] rank4));
        Assert.AreEqual(0f, rank4[0], "4 级 I 的 0% 不得回流");
        Assert.AreEqual(20f / 55f, rank4[1], 0.0001f);
        Assert.AreEqual(35f / 55f, rank4[2], 0.0001f);
        Assert.IsFalse(InscriptionRankRules.TryGetEffectiveWeights(1,
            new[] { false, false, true, false, false, false }, out _), "合法池只有 0% 品阶时失败");
    }

    [Test]
    public void RankPurchase_IsSequentialAndIdempotent()
    {
        var run = new SaveService.ActiveRunData
        {
            runId = "test-run", inscriptionRank = 1, runCoins = 100, currentNodeId = 7,
            dungeonGraph = new DungeonGraphData
            {
                Nodes = { new DungeonGraphNode
                    { NodeId = 7, Type = NodeType.Shop, Discovered = true, Selected = true, Visited = true } },
            },
        };
        Assert.AreEqual(RunTransactionResult.Applied,
            RunTransactionRules.TryUpgradeRank(run, 7, 1));
        Assert.AreEqual(2, run.inscriptionRank);
        Assert.AreEqual(75, run.runCoins);
        Assert.AreEqual(RunTransactionResult.AlreadyApplied,
            RunTransactionRules.TryUpgradeRank(run, 7, 1));
        Assert.AreEqual(75, run.runCoins, "重复回调不能重复扣费");
        Assert.AreEqual(1, run.transactions.Count);

        Assert.AreEqual(RunTransactionResult.InvalidState,
            RunTransactionRules.TryUpgradeRank(run, 9, 3), "不可跳级");
        Assert.AreEqual(RunTransactionResult.Applied,
            RunTransactionRules.TryUpgradeRank(run, 7, 2));
        Assert.AreEqual(3, run.inscriptionRank);
        Assert.AreEqual(30, run.runCoins);
        Assert.AreEqual(RunTransactionResult.InsufficientFunds,
            RunTransactionRules.TryUpgradeRank(run, 7, 3));
        Assert.AreEqual(2, run.transactions.Count);

        run.currentNodeId = 8;
        Assert.AreEqual(RunTransactionResult.InvalidState,
            RunTransactionRules.TryUpgradeRank(run, 7, 3), "离开商店后不能购买升阶");
    }
}
