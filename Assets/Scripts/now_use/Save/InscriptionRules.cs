using System;
using System.Collections.Generic;

/// <summary>刻印谱系；枚举值进入存档，不得重排。</summary>
public enum InscriptionLineage
{
    SwiftBlade = 0,
    StarRift = 1,
    BloodOath = 2,
}

/// <summary>品阶 I～VI；v2.1 首批只投放 I～III。</summary>
public enum InscriptionTier
{
    I = 1, II = 2, III = 3, IV = 4, V = 5, VI = 6,
}

public sealed class SageInscriptionDefinition
{
    public readonly string AbilityId;
    public readonly string DisplayName;
    public readonly InscriptionLineage Lineage;
    public readonly InscriptionTier Tier;
    public readonly PlayableCharacterId[] CompatibleCharacters;
    public readonly string[] RequiredTags;

    public SageInscriptionDefinition(string abilityId, string displayName,
        InscriptionLineage lineage, InscriptionTier tier,
        PlayableCharacterId[] compatibleCharacters = null, string[] requiredTags = null)
    {
        AbilityId = abilityId;
        DisplayName = displayName;
        Lineage = lineage;
        Tier = tier;
        CompatibleCharacters = compatibleCharacters ?? Array.Empty<PlayableCharacterId>();
        RequiredTags = requiredTags ?? Array.Empty<string>();
    }

    public bool IsCompatible(PlayableCharacterId characterId, ICollection<string> tags = null)
    {
        if (CompatibleCharacters.Length > 0)
        {
            bool matched = false;
            foreach (PlayableCharacterId id in CompatibleCharacters)
                if (id == characterId) { matched = true; break; }
            if (!matched) return false;
        }
        foreach (string tag in RequiredTags)
            if (tags == null || !tags.Contains(tag)) return false;
        return true;
    }
}

/// <summary>v2.1 首批 13 张刻印的稳定 ID 与固定品阶；数值效果在 v2.1.3 接入。</summary>
public static class SageInscriptionCatalog
{
    public const int MaxOwnedLevel = 3;
    private static readonly IReadOnlyList<SageInscriptionDefinition> definitions =
        Array.AsReadOnly(new[]
        {
            new SageInscriptionDefinition("rift_sharp_eye", "裂星·锐瞳", InscriptionLineage.StarRift, InscriptionTier.I),
            new SageInscriptionDefinition("rift_heavy_scar", "裂星·重痕", InscriptionLineage.StarRift, InscriptionTier.I),
            new SageInscriptionDefinition("rift_refraction", "裂星·折光", InscriptionLineage.StarRift, InscriptionTier.II),
            new SageInscriptionDefinition("rift_gathered_gap", "裂星·蓄隙", InscriptionLineage.StarRift, InscriptionTier.II),
            new SageInscriptionDefinition("rift_desperation", "裂星·绝境", InscriptionLineage.StarRift, InscriptionTier.III),
            new SageInscriptionDefinition("swift_edge", "疾刃·快锋", InscriptionLineage.SwiftBlade, InscriptionTier.I),
            new SageInscriptionDefinition("swift_peril_step", "疾刃·危步", InscriptionLineage.SwiftBlade, InscriptionTier.II),
            new SageInscriptionDefinition("swift_overflow", "疾刃·余势", InscriptionLineage.SwiftBlade, InscriptionTier.III),
            new SageInscriptionDefinition("swift_combo", "疾刃·连锋", InscriptionLineage.SwiftBlade, InscriptionTier.II),
            new SageInscriptionDefinition("oath_drinking_blade", "血誓·饮刃", InscriptionLineage.BloodOath, InscriptionTier.I),
            new SageInscriptionDefinition("oath_overlife", "血誓·溢生", InscriptionLineage.BloodOath, InscriptionTier.III),
            new SageInscriptionDefinition("oath_hunt", "血誓·猎获", InscriptionLineage.BloodOath, InscriptionTier.II),
            new SageInscriptionDefinition("oath_final_devour", "血誓·终噬", InscriptionLineage.BloodOath, InscriptionTier.II),
        });

    public static IReadOnlyList<SageInscriptionDefinition> All => definitions;

    public static SageInscriptionDefinition Find(string abilityId)
    {
        if (string.IsNullOrEmpty(abilityId)) return null;
        foreach (SageInscriptionDefinition definition in definitions)
            if (definition.AbilityId == abilityId) return definition;
        return null;
    }
}

[Serializable]
public sealed class OwnedInscriptionData
{
    public string abilityId;
    public InscriptionTier tier;
    public int level;
}

/// <summary>单局刻印持有真值。共鸣只由不同 AbilityId 计算，效果运行时由后续版本实现。</summary>
[Serializable]
public sealed class RunBuildState
{
    public List<OwnedInscriptionData> inscriptions = new List<OwnedInscriptionData>();

    public bool TryAcquire(string abilityId, PlayableCharacterId characterId, out bool upgraded,
        ICollection<string> tags = null)
    {
        upgraded = false;
        SageInscriptionDefinition definition = SageInscriptionCatalog.Find(abilityId);
        if (definition == null || !definition.IsCompatible(characterId, tags)) return false;
        if (inscriptions == null) inscriptions = new List<OwnedInscriptionData>();

        foreach (OwnedInscriptionData owned in inscriptions)
        {
            if (owned == null || owned.abilityId != abilityId) continue;
            if (owned.tier != definition.Tier || owned.level < 1 || owned.level >= SageInscriptionCatalog.MaxOwnedLevel)
                return false;
            owned.level++;
            upgraded = true;
            return true;
        }

        inscriptions.Add(new OwnedInscriptionData
        {
            abilityId = definition.AbilityId,
            tier = definition.Tier,
            level = 1,
        });
        return true;
    }

    public int CountDistinct(InscriptionLineage lineage)
    {
        if (inscriptions == null) return 0;
        var seen = new HashSet<string>();
        foreach (OwnedInscriptionData owned in inscriptions)
        {
            if (owned == null || owned.level < 1) continue;
            SageInscriptionDefinition definition = SageInscriptionCatalog.Find(owned.abilityId);
            if (definition != null && definition.Lineage == lineage && definition.Tier == owned.tier)
                seen.Add(owned.abilityId);
        }
        return seen.Count;
    }

    /// <returns>0=未激活，1=初鸣（2 张），2=初鸣+全鸣（4 张）。</returns>
    public int GetResonanceStage(InscriptionLineage lineage)
    {
        int count = CountDistinct(lineage);
        return count >= 4 ? 2 : count >= 2 ? 1 : 0;
    }
}

/// <summary>唯一五级六档矩阵。2～5 级与价格为 v2.1 首轮测试基线。</summary>
public static class InscriptionRankRules
{
    public const int MinRank = 1;
    public const int MaxRank = 5;
    public const int TierCount = 6;

    private static readonly int[,] weights =
    {
        { 60, 40,  0,  0,  0,  0 },
        { 40, 45, 15,  0,  0,  0 },
        { 20, 35, 30, 15,  0,  0 },
        {  0, 20, 35, 30, 15,  0 },
        {  0, 10, 20, 30, 25, 15 },
    };
    private static readonly int[] priceToRank = { 0, 0, 25, 45, 70, 95 };

    public static int GetBaseWeight(int rank, InscriptionTier tier)
    {
        if (rank < MinRank || rank > MaxRank || (int)tier < 1 || (int)tier > TierCount)
            throw new ArgumentOutOfRangeException();
        return weights[rank - 1, (int)tier - 1];
    }

    public static int GetPriceToReach(int targetRank)
    {
        if (targetRank < MinRank || targetRank > MaxRank)
            throw new ArgumentOutOfRangeException(nameof(targetRank));
        return priceToRank[targetRank];
    }

    /// <summary>过滤无合法候选品阶后归一化；0% 永不回流。eligible 按 I～VI 排列。</summary>
    public static bool TryGetEffectiveWeights(int rank, bool[] eligible, out float[] normalized)
    {
        normalized = new float[TierCount];
        if (rank < MinRank || rank > MaxRank || eligible == null || eligible.Length != TierCount)
            return false;

        int total = 0;
        for (int i = 0; i < TierCount; i++)
            if (eligible[i] && weights[rank - 1, i] > 0) total += weights[rank - 1, i];
        if (total == 0) return false;

        for (int i = 0; i < TierCount; i++)
            if (eligible[i] && weights[rank - 1, i] > 0)
                normalized[i] = (float)weights[rank - 1, i] / total;
        return true;
    }
}
