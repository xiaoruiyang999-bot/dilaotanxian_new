using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// v2.0.9 天赋目录（任务计划 §6）：按职业过滤的静态只读目录。
/// 首发狼人四分支 16 节点（Resources/Talents）；P1/P2 扩充只加资产不加代码。
/// </summary>
public static class TalentCatalog
{
    private const string ResourceDir = "Talents";
    private static TalentDefinition[] cache;
    private static bool loaded;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() { cache = null; loaded = false; }

    /// <summary>请求职业的可用标签（P0 狼人：近战/DOT/破甲；法师/弓箭手标签随实装扩充）。</summary>
    private static readonly Dictionary<PlayableCharacterId, string[]> CharacterTags = new Dictionary<PlayableCharacterId, string[]>
    {
        { PlayableCharacterId.Werewolf, new[] { "melee", "dot", "armorbreak" } },
    };

    private static string[] TagsOf(PlayableCharacterId character)
        => CharacterTags.TryGetValue(character, out var tags) ? tags : new string[0];

    /// <summary>奖励池视角的可用节点（§3.3 兼容规则；NotReady 仅预览不入池）。</summary>
    public static IReadOnlyList<TalentDefinition> All(PlayableCharacterId character)
    {
        Load();
        var list = new List<TalentDefinition>();
        if (cache == null) return list;
        string[] charTags = TagsOf(character);
        foreach (TalentDefinition t in cache)
        {
            if (t == null || t.compatType == TalentCompat.NotReady) continue;
            if (Compatible(t, charTags)) list.Add(t);
        }
        return list;
    }

    /// <summary>预览视角（石碑树展示）：全部节点含未就绪（§3.3"可预览"）。</summary>
    public static IReadOnlyList<TalentDefinition> Preview()
    {
        Load();
        return cache ?? new TalentDefinition[0];
    }

    private static bool Compatible(TalentDefinition t, string[] charTags)
    {
        switch (t.compatType)
        {
            case TalentCompat.Universal:
            case TalentCompat.ClassAdapted:
                return true;   // 职业适配：结构通用，回报运行时映射
            case TalentCompat.TagUniversal:
                foreach (string need in t.tags)
                {
                    bool hit = false;
                    foreach (string have in charTags) if (have == need) { hit = true; break; }
                    if (!hit) return false;
                }
                return true;
            default:
                return false;
        }
    }

    public static TalentDefinition Find(string talentId)
    {
        if (string.IsNullOrEmpty(talentId)) return null;
        Load();
        if (cache == null) return null;
        foreach (TalentDefinition t in cache)
            if (t != null && t.talentId == talentId) return t;
        return null;
    }

    /// <summary>一阶节点（可作起始铭文/开拓位，任务计划 §2.2）。</summary>
    public static bool IsTierOne(TalentDefinition t)
        => t != null && (string.IsNullOrEmpty(t.prerequisiteId) || t.prerequisiteId == t.talentId);

    /// <summary>同分支下一阶候选（前驱=已有节点）。</summary>
    public static List<TalentDefinition> LegalSuccessors(RunTalentState state, PlayableCharacterId character)
    {
        var result = new List<TalentDefinition>();
        foreach (TalentDefinition t in All(character))
        {
            if (state.Owns(t.talentId)) continue;
            if (IsTierOne(t)) { result.Add(t); continue; }   // 一阶永远合法（开新分支）
            if (!string.IsNullOrEmpty(t.prerequisiteId) && state.Owns(t.prerequisiteId))
                result.Add(t);
        }
        return result;
    }

    private static void Load()
    {
        // 资产可能晚于域加载（编辑器内首次导入/guid 修复后再跑）——空结果不置 loaded，下次重试
        if (loaded) return;
        cache = Resources.LoadAll<TalentDefinition>(ResourceDir);
        if (cache == null || cache.Length == 0)
        {
            cache = null;
            return;
        }
        loaded = true;
    }
}
