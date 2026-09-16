using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// v2.0.9 天赋门禁（任务计划 §8 矩阵）：16 资产合同（ID 唯一/前置合法/职业隔离）、
/// 前置过滤（未满足不出高阶）、去重、三席位保底（前两次含起始分支后继）、确定性。
/// </summary>
public class TalentSystemTests
{
    private const int Werewolf = 0;   // PlayableCharacterId.Werewolf

    [Test]
    public void WerewolfPool_SixteenNodes_StructureValid()
    {
        IReadOnlyList<TalentDefinition> pool = TalentCatalog.All((PlayableCharacterId)Werewolf);
        Assert.AreEqual(16, pool.Count, "P0 狼人 = 4 分支 × 4 节点");

        var ids = new HashSet<string>();
        var byBranch = new Dictionary<string, List<TalentDefinition>>();
        foreach (TalentDefinition t in pool)
        {
            Assert.IsTrue(ids.Add(t.talentId), $"ID 重复：{t.talentId}");
            Assert.IsNotEmpty(t.displayName);
            Assert.IsNotEmpty(t.description);
            Assert.GreaterOrEqual(t.values.Length, 1, $"{t.talentId} 缺数值载荷");
            if (!byBranch.TryGetValue(t.branchId, out var list)) byBranch[t.branchId] = list = new List<TalentDefinition>();
            list.Add(t);
        }
        Assert.AreEqual(4, byBranch.Count, "四条分支（F01/F03/F09/F14）");
        foreach (KeyValuePair<string, List<TalentDefinition>> kv in byBranch)
        {
            Assert.AreEqual(4, kv.Value.Count, $"分支 {kv.Key} 必须 4 节点");
            for (int tier = 1; tier <= 4; tier++)
            {
                int tierCount = kv.Value.Count(t => t.tier == tier);
                Assert.AreEqual(1, tierCount, $"分支 {kv.Key} 第 {tier} 阶节点数异常");
            }
            // 前置链：二阶起前驱必须同分支存在
            foreach (TalentDefinition t in kv.Value)
                if (t.tier > 1)
                {
                    Assert.IsNotNull(TalentCatalog.Find(t.prerequisiteId), $"{t.talentId} 前置悬空");
                    Assert.AreEqual(t.branchId, TalentCatalog.Find(t.prerequisiteId).branchId,
                        $"{t.talentId} 前置跨分支（禁止）");
                }
        }
    }

    [Test]
    public void Offer_PrerequisiteFilter_NoHighTierWithoutParent()
    {
        var state = new RunTalentState();
        state.Grant("Werewolf_F09_T1");   // 只有一阶

        for (int seed = 1; seed <= 30; seed++)
        {
            List<TalentOfferService.Offer> offers = TalentOfferService.Roll(seed, seed * 3, state, (PlayableCharacterId)Werewolf);
            foreach (TalentOfferService.Offer o in offers)
            {
                TalentDefinition t = o.Talent;
                if (t.tier > 1)
                {
                    Assert.IsTrue(state.Owns(t.prerequisiteId) || IsGrantedLater(offers, t, state),
                        $"seed={seed}：未满足前置的高阶 {t.talentId} 出现在候选");
                }
            }
        }
    }

    private static bool IsGrantedLater(List<TalentOfferService.Offer> offers, TalentDefinition t, RunTalentState state)
    {
        // 候选内的同分支低阶节点也在场则视为合法提案（玩家选了它之后）——严格版要求瞬时满足；
        // 本门禁取瞬时口径：前置必须在 state 中已拥有
        return false;
    }

    [Test]
    public void Offer_NoDuplicates_AlwaysThreeish()
    {
        var state = new RunTalentState();
        state.Grant("Werewolf_F03_T1");
        for (int seed = 1; seed <= 20; seed++)
        {
            List<TalentOfferService.Offer> offers = TalentOfferService.Roll(seed, 5, state, (PlayableCharacterId)Werewolf);
            var ids = new HashSet<string>();
            foreach (TalentOfferService.Offer o in offers)
            {
                Assert.IsFalse(state.Owns(o.Talent.talentId), $"seed={seed} 已拥有节点 {o.Talent.talentId} 再次出现");
                Assert.IsTrue(ids.Add(o.Talent.talentId), $"seed={seed} 候选内重复 {o.Talent.talentId}");
            }
            Assert.That(offers.Count, Is.InRange(1, 3));
        }
    }

    [Test]
    public void Offer_FirstTwoRewards_ContainStartingBranchSuccessor()
    {
        for (int seed = 1; seed <= 25; seed++)
        {
            var state = new RunTalentState { StartingInscription = "Werewolf_F14_T1" };
            state.Grant("Werewolf_F14_T1");   // 起始铭文即首个天赋
            state.RewardSequence = 0;
            for (int reward = 0; reward < 2; reward++)
            {
                state.RewardSequence = reward;
                List<TalentOfferService.Offer> offers = TalentOfferService.Roll(seed, reward * 11 + 3, state, (PlayableCharacterId)Werewolf);
                bool hasF14 = offers.Any(o => o.Talent.branchId == "F14");
                Assert.IsTrue(hasF14, $"seed={seed} reward={reward}：前两次奖励缺起始分支后继（保底失败）");
            }
        }
    }

    [Test]
    public void Offer_Deterministic_SameTriple()
    {
        var state = new RunTalentState();
        state.Grant("Werewolf_F01_T1");
        for (int seed = 1; seed <= 10; seed++)
        {
            List<TalentOfferService.Offer> a = TalentOfferService.Roll(seed, 7, state, (PlayableCharacterId)Werewolf);
            List<TalentOfferService.Offer> b = TalentOfferService.Roll(seed, 7, state, (PlayableCharacterId)Werewolf);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Talent.talentId, b[i].Talent.talentId, $"seed={seed} 候选不确定");
                Assert.AreEqual(a[i].Relation, b[i].Relation);
            }
        }
    }

    [Test]
    public void Compatibility_WerewolfSeesAllSixteen()
    {
        // §3.3 通用树：狼人(melee/dot/armorbreak 标签)可见全部 16 节点(F01 职业适配+三标签分支)
        IReadOnlyList<TalentDefinition> pool = TalentCatalog.All((PlayableCharacterId)Werewolf);
        Assert.AreEqual(16, pool.Count);
        foreach (TalentDefinition t in pool)
            Assert.AreNotEqual(TalentCompat.NotReady, t.compatType, "未就绪节点不得进入奖励池");
    }

    [Test]
    public void Naming_UniversalNoWerewolfSpecificTerms()
    {
        // §3.2 改名规则：名称不得含狼人专属词；displayName 必须是中文(防拼音占位回归)
        string[] banned = { "狼", "兽化", "怒痕", "狼爪", "刺刀" };
        IReadOnlyList<TalentDefinition> pool = TalentCatalog.Preview();
        Assert.GreaterOrEqual(pool.Count, 16);
        foreach (TalentDefinition t in pool)
        {
            foreach (string term in banned)
                Assert.IsFalse(t.displayName.Contains(term),
                    $"{t.talentId} 名称含狼人专属词「{term}」（§3.2 通用化禁令）");
            bool hasCjk = false;
            foreach (char c in t.displayName)
                if (c >= 0x4E00 && c <= 0x9FFF) { hasCjk = true; break; }
            Assert.IsTrue(hasCjk, $"{t.talentId} 名称必须为中文（拼音占位不允许交付）");
        }
    }
}
