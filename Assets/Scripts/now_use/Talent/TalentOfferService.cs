using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// v2.0.9 天赋三选一生成器（任务计划 §3.2，纯 C# 可测试）：
/// 三席位 = 关联位（起始/投入最多分支的合法后继）+ 联动位（已有状态相关分支）+ 开拓位（新分支一阶）。
/// 保底：前两次主要奖励至少一个起始分支后继；候选不足三时由调用方补位（武器/遗物/恢复，
/// 不得用永久攻击百分比）。确定性：seed = runSeed/roomId/序号 三元——退出重进不刷新。
/// </summary>
public static class TalentOfferService
{
    public struct Offer
    {
        public TalentDefinition Talent;
        public string Relation;   // 后继/联动/新分支（UI 卡面关系标签）
    }

    /// <summary>生成三选一候选（不足三个时返回实际数量，调用方决定补位）。</summary>
    public static List<Offer> Roll(int runSeed, int roomId, RunTalentState state,
        PlayableCharacterId character)
    {
        var result = new List<Offer>();
        if (state == null) return result;

        var rng = new System.Random(runSeed * 7919 + roomId * 104729 + state.RewardSequence * 31);
        List<TalentDefinition> legal = TalentCatalog.LegalSuccessors(state, character);
        if (legal.Count == 0) return result;

        // 主分支判定：已投入最多节点的分支（并列取起始分支）
        string mainBranch = DominantBranch(state);

        // ① 关联位：主分支的合法后继（保底：前两次奖励必须有起始分支后继）
        TalentDefinition related = PickFromBranch(legal, mainBranch, rng);
        if (related != null)
        {
            result.Add(new Offer { Talent = related, Relation = "后继" });
            legal.Remove(related);
        }
        else if (state.RewardSequence < 2)
        {
            // 保底失败（主分支已点满/不存在）——退化为任意合法位并标记关联
            if (legal.Count > 0)
            {
                TalentDefinition fallback = legal[rng.Next(legal.Count)];
                result.Add(new Offer { Talent = fallback, Relation = "后继" });
                legal.Remove(fallback);
            }
        }

        // ② 联动位：与已投入分支（任意）相关的后继
        if (legal.Count > 0)
        {
            List<string> owned = OwnedBranches(state, character);
            TalentDefinition synergy = null;
            if (owned.Count > 0)
            {
                for (int tries = 0; tries < 4 && synergy == null; tries++)
                {
                    TalentDefinition pick = legal[rng.Next(legal.Count)];
                    foreach (string b in owned)
                        if (pick.branchId == b) { synergy = pick; break; }
                }
            }
            if (synergy == null) synergy = legal[rng.Next(legal.Count)];
            result.Add(new Offer { Talent = synergy, Relation = "联动" });
            legal.Remove(synergy);
        }

        // ③ 开拓位：未开启分支的一阶（没有则任意剩余）
        if (legal.Count > 0)
        {
            TalentDefinition pioneer = null;
            for (int tries = 0; tries < 4 && pioneer == null; tries++)
            {
                TalentDefinition pick = legal[rng.Next(legal.Count)];
                if (TalentCatalog.IsTierOne(pick)) pioneer = pick;
            }
            if (pioneer == null) pioneer = legal[rng.Next(legal.Count)];
            result.Add(new Offer { Talent = pioneer, Relation = "新分支" });
        }

        return result;
    }

    private static string DominantBranch(RunTalentState state)
    {
        var counts = new Dictionary<string, int>();
        string startingBranch = null;
        foreach (string id in state.OwnedIds)
        {
            TalentDefinition t = TalentCatalog.Find(id);
            if (t == null) continue;
            counts.TryGetValue(t.branchId, out int c);
            counts[t.branchId] = c + 1;
            if (id == state.StartingInscription) startingBranch = t.branchId;
        }
        string best = null; int bestCount = -1;
        foreach (KeyValuePair<string, int> kv in counts)
            if (kv.Value > bestCount || (kv.Value == bestCount && kv.Key == startingBranch))
            { best = kv.Key; bestCount = kv.Value; }
        return best ?? startingBranch;
    }

    private static List<string> OwnedBranches(RunTalentState state, PlayableCharacterId character)
    {
        var set = new HashSet<string>();
        foreach (string id in state.OwnedIds)
        {
            TalentDefinition t = TalentCatalog.Find(id);
            if (t != null) set.Add(t.branchId);
        }
        return new List<string>(set);
    }

    private static TalentDefinition PickFromBranch(List<TalentDefinition> legal, string branch, System.Random rng)
    {
        if (string.IsNullOrEmpty(branch)) return null;
        var pool = new List<TalentDefinition>();
        foreach (TalentDefinition t in legal)
            if (t.branchId == branch) pool.Add(t);
        return pool.Count > 0 ? pool[rng.Next(pool.Count)] : null;
    }
}
