using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 生成权重表（计划书五-C）：一类内容（敌人/障碍物/装饰）的候选 prefab + 权重 + 每房数量区间。
/// 纯数据 + 抽取逻辑（不碰场景）；改内容只改本资产，不改代码。
/// </summary>
[CreateAssetMenu(menuName = "Dungeon/SpawnTable")]
public class SpawnTable : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public GameObject prefab;
        public int weight = 1;
        [Tooltip("保底生成数量：无视权重，每个房间至少生成该条目 N 个（先于权重抽取，v0.5.3.1）")]
        public int minCount = 0;
        [Tooltip("仅障碍物表使用：可被玩家武器破坏")]
        public bool destructible;
        [Tooltip("仅障碍物表使用：可破坏时的血量（≈需要砍的刀数）")]
        public int hp = 3;

        [Header("Encounter Budget (enemy tables only)")]
        [Tooltip("敌人在遭遇预算中的成本；小于 1 时按 1 计算。")]
        public int encounterCost = 1;
        [Tooltip("同一房间最多生成数量；0 表示不限制。")]
        public int maxPerRoom = 0;
        [Tooltip("用于保证前后排组合的战斗职责。")]
        public EnemyEncounterRole role = EnemyEncounterRole.Frontline;
    }

    public List<Entry> entries = new List<Entry>();
    public int countMin = 2, countMax = 4;   // 每个房间生成数量区间

    [Tooltip("Random = 随机散点（默认）；Row = 房中心一列排放（条目即商品按列表顺序，忽略权重与数量区间）")]
    public SpawnLayout layoutMode = SpawnLayout.Random;
    [Tooltip("Row 模式：相邻商品间距（格）")]
    public float rowSpacing = 2.5f;

    [Header("Enemy Encounter Budget")]
    [Tooltip("大于 0 时，EnemySpawner 改用预算编排；否则保持旧版 countMin/countMax。")]
    public int encounterBudgetMin = 0;
    public int encounterBudgetMax = 0;
    [Tooltip("预算允许时，至少安排一个 Frontline，避免只有远程/召唤单位。")]
    public bool requireFrontline = true;

    [Header("Enemy Affixes")]
    [Range(0f, 1f)] public float affixChance = 0f;
    public List<EnemyAffixConfig> affixes = new List<EnemyAffixConfig>();

    public bool UsesEncounterBudget => encounterBudgetMax > 0;

    public EnemyAffixConfig RollAffix(System.Random rng)
    {
        if (rng == null || affixes == null || affixes.Count == 0
            || rng.NextDouble() >= Mathf.Clamp01(affixChance))
            return null;

        var valid = new List<EnemyAffixConfig>();
        foreach (EnemyAffixConfig affix in affixes) if (affix != null) valid.Add(affix);
        return valid.Count == 0 ? null : valid[rng.Next(valid.Count)];
    }

    /// <summary>按数量区间 roll 本房生成个数。</summary>
    public int RollCount(System.Random rng)
    {
        int safeMin = Mathf.Max(0, countMin);
        int safeMax = Mathf.Max(safeMin, countMax);
        return rng.Next(safeMin, safeMax + 1);
    }

    /// <summary>按权重抽一个条目；表空或权重全 0 返回 null。</summary>
    public Entry PickEntry(System.Random rng)
    {
        int total = 0;
        foreach (Entry e in entries) if (e != null && e.prefab != null) total += Mathf.Max(0, e.weight);
        if (total <= 0) return null;

        int roll = rng.Next(total);
        foreach (Entry e in entries)
        {
            if (e == null || e.prefab == null) continue;
            roll -= Mathf.Max(0, e.weight);
            if (roll < 0) return e;
        }
        return null;
    }

    /// <summary>按成本、职责和房间上限构建敌人组合；仅供 EnemySpawner 使用。</summary>
    public List<Entry> BuildEncounter(System.Random rng)
    {
        var result = new List<Entry>();
        var counts = new Dictionary<Entry, int>();
        int safeMin = Mathf.Max(1, encounterBudgetMin);
        int safeMax = Mathf.Max(safeMin, encounterBudgetMax);
        int budget = rng.Next(safeMin, safeMax + 1);

        // 配置的保底数量优先，并同样消耗预算。
        foreach (Entry entry in entries)
        {
            if (!IsValidEncounterEntry(entry)) continue;
            int minimum = Mathf.Max(0, entry.minCount);
            for (int i = 0; i < minimum && CanAdd(entry, counts); i++)
            {
                AddPick(entry, result, counts);
                budget -= CostOf(entry);
            }
        }

        if (requireFrontline && !ContainsRole(result, EnemyEncounterRole.Frontline))
        {
            Entry frontline = PickAffordable(rng, counts, budget, EnemyEncounterRole.Frontline);
            if (frontline != null)
            {
                AddPick(frontline, result, counts);
                budget -= CostOf(frontline);
            }
        }

        // 每轮至少花费 1 点预算；安全阈值防御错误配置。
        int safety = 64;
        while (budget > 0 && safety-- > 0)
        {
            Entry picked = PickAffordable(rng, counts, budget, null);
            if (picked == null) break;
            AddPick(picked, result, counts);
            budget -= CostOf(picked);
        }

        return result;
    }

    private Entry PickAffordable(System.Random rng, Dictionary<Entry, int> counts, int budget,
        EnemyEncounterRole? requiredRole)
    {
        int totalWeight = 0;
        foreach (Entry entry in entries)
            if (IsValidEncounterEntry(entry) && CanAdd(entry, counts)
                && CostOf(entry) <= budget
                && (!requiredRole.HasValue || entry.role == requiredRole.Value))
                totalWeight += Mathf.Max(0, entry.weight);

        if (totalWeight <= 0) return null;
        int roll = rng.Next(totalWeight);
        foreach (Entry entry in entries)
        {
            if (!IsValidEncounterEntry(entry) || !CanAdd(entry, counts)
                || CostOf(entry) > budget
                || (requiredRole.HasValue && entry.role != requiredRole.Value))
                continue;
            roll -= Mathf.Max(0, entry.weight);
            if (roll < 0) return entry;
        }
        return null;
    }

    private static bool IsValidEncounterEntry(Entry entry) =>
        entry != null && entry.prefab != null && entry.weight > 0;

    private static int CostOf(Entry entry) => Mathf.Max(1, entry.encounterCost);

    private static bool CanAdd(Entry entry, Dictionary<Entry, int> counts)
    {
        counts.TryGetValue(entry, out int count);
        return entry.maxPerRoom <= 0 || count < entry.maxPerRoom;
    }

    private static void AddPick(Entry entry, List<Entry> result, Dictionary<Entry, int> counts)
    {
        result.Add(entry);
        counts.TryGetValue(entry, out int count);
        counts[entry] = count + 1;
    }

    private static bool ContainsRole(List<Entry> picks, EnemyEncounterRole role)
    {
        foreach (Entry entry in picks) if (entry.role == role) return true;
        return false;
    }
}

/// <summary>生成布局模式（v0.5.3.1）。Random = 随机散点（默认）；Row = 房中心一列排放。</summary>
public enum SpawnLayout { Random, Row }

public enum EnemyEncounterRole { Frontline, Ranged, Flanker, Support }
