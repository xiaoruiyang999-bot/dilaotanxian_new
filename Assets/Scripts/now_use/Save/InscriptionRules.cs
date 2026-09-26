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

/// <summary>效果编号为静态配置索引，不写入存档；持有真值仍为 AbilityId。</summary>
public enum InscriptionEffect
{
    SharpEye, HeavyScar, Refraction, GatheredGap, Desperation,
    SwiftEdge, PerilStep, Overflow, SwiftCombo,
    DrinkingBlade, Overlife, Hunt, FinalDevour,
}

public sealed class SageInscriptionDefinition
{
    public readonly string AbilityId;
    public readonly string DisplayName;
    public readonly InscriptionLineage Lineage;
    public readonly InscriptionTier Tier;
    public readonly PlayableCharacterId[] CompatibleCharacters;
    public readonly string[] RequiredTags;
    public readonly InscriptionEffect Effect;
    private readonly float[] levelValues;
    private readonly float[] secondaryValues;

    public SageInscriptionDefinition(string abilityId, string displayName,
        InscriptionLineage lineage, InscriptionTier tier,
        InscriptionEffect effect, float[] levelValues, float[] secondaryValues = null,
        PlayableCharacterId[] compatibleCharacters = null, string[] requiredTags = null)
    {
        AbilityId = abilityId;
        DisplayName = displayName;
        Lineage = lineage;
        Tier = tier;
        Effect = effect;
        this.levelValues = levelValues ?? throw new ArgumentNullException(nameof(levelValues));
        if (levelValues.Length != 3) throw new ArgumentException("每张刻印须配置三级数值");
        this.secondaryValues = secondaryValues;
        if (secondaryValues != null && secondaryValues.Length != 3)
            throw new ArgumentException("附加参数须配置三级数值");
        CompatibleCharacters = compatibleCharacters ?? Array.Empty<PlayableCharacterId>();
        RequiredTags = requiredTags ?? Array.Empty<string>();
    }


    public float ValueAt(int level) => level >= 1 && level <= 3 ? levelValues[level - 1] : 0f;
    public float SecondaryAt(int level) => level >= 1 && level <= 3 && secondaryValues != null
        ? secondaryValues[level - 1] : 0f;

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

/// <summary>v2.1 首批 13 张刻印的稳定 ID、固定品阶和逐级数值。</summary>
public static class SageInscriptionCatalog
{
    public const int MaxOwnedLevel = 3;
    private static readonly IReadOnlyList<SageInscriptionDefinition> definitions =
        Array.AsReadOnly(new[]
        {
            new SageInscriptionDefinition("rift_sharp_eye", "裂星·锐瞳", InscriptionLineage.StarRift, InscriptionTier.I, InscriptionEffect.SharpEye, new[] { .05f, .075f, .10f }),
            new SageInscriptionDefinition("rift_heavy_scar", "裂星·重痕", InscriptionLineage.StarRift, InscriptionTier.I, InscriptionEffect.HeavyScar, new[] { .15f, .22f, .30f }),
            new SageInscriptionDefinition("rift_refraction", "裂星·折光", InscriptionLineage.StarRift, InscriptionTier.II, InscriptionEffect.Refraction, new[] { .08f, .12f, .16f }, new[] { .40f, .50f, .60f }),
            new SageInscriptionDefinition("rift_gathered_gap", "裂星·蓄隙", InscriptionLineage.StarRift, InscriptionTier.II, InscriptionEffect.GatheredGap, new[] { .04f, .05f, .06f }, new[] { .20f, .25f, .30f }),
            new SageInscriptionDefinition("rift_desperation", "裂星·绝境", InscriptionLineage.StarRift, InscriptionTier.III, InscriptionEffect.Desperation, new[] { .45f, .55f, .65f }),
            new SageInscriptionDefinition("swift_edge", "疾刃·快锋", InscriptionLineage.SwiftBlade, InscriptionTier.I, InscriptionEffect.SwiftEdge, new[] { .06f, .09f, .12f }),
            new SageInscriptionDefinition("swift_peril_step", "疾刃·危步", InscriptionLineage.SwiftBlade, InscriptionTier.II, InscriptionEffect.PerilStep, new[] { .20f, .25f, .30f }),
            new SageInscriptionDefinition("swift_overflow", "疾刃·余势", InscriptionLineage.SwiftBlade, InscriptionTier.III, InscriptionEffect.Overflow, new[] { 1f, 1.25f, 1.5f }),
            new SageInscriptionDefinition("swift_combo", "疾刃·连锋", InscriptionLineage.SwiftBlade, InscriptionTier.II, InscriptionEffect.SwiftCombo, new[] { .02f, .025f, .03f }),
            new SageInscriptionDefinition("oath_drinking_blade", "血誓·饮刃", InscriptionLineage.BloodOath, InscriptionTier.I, InscriptionEffect.DrinkingBlade, new[] { .02f, .03f, .04f }),
            new SageInscriptionDefinition("oath_overlife", "血誓·溢生", InscriptionLineage.BloodOath, InscriptionTier.III, InscriptionEffect.Overlife, new[] { .15f, .18f, .20f }, new[] { 6f, 8f, 10f }),
            new SageInscriptionDefinition("oath_hunt", "血誓·猎获", InscriptionLineage.BloodOath, InscriptionTier.II, InscriptionEffect.Hunt, new[] { .015f, .02f, .025f }),
            new SageInscriptionDefinition("oath_final_devour", "血誓·终噬", InscriptionLineage.BloodOath, InscriptionTier.II, InscriptionEffect.FinalDevour, new[] { .03f, .04f, .05f }),
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
