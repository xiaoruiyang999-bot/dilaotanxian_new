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
        [Tooltip("仅障碍物表使用：可被玩家武器破坏")]
        public bool destructible;
        [Tooltip("仅障碍物表使用：可破坏时的血量（≈需要砍的刀数）")]
        public int hp = 3;
    }

    public List<Entry> entries = new List<Entry>();
    public int countMin = 2, countMax = 4;   // 每个房间生成数量区间

    /// <summary>按数量区间 roll 本房生成个数。</summary>
    public int RollCount(System.Random rng)
    {
        if (countMax < countMin) countMax = countMin;
        return rng.Next(countMin, countMax + 1);
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
}
