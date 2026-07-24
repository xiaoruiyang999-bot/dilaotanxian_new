using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 房间类型分配器（纯 C#，计划书五-D）：Generator 产出布局后运行，只改 RoomNode.type，不动布局。
/// Start=原点、Boss=最远（均由 Generator 定死，本类不碰）；
/// Treasure/Shop/Event 按 DungeonConfig 数量优先叶子房（叶子不够取 distance≥2，再不够任意余房——尽力满足）；
/// 其余 Combat，其中 distance≥2 的按 eliteChance 逐个抽 Elite。
/// 随机流独立（seed*31+7 派生）：不接布局流尾部，同 seed 类型分配稳定且不影响布局复现性。
/// </summary>
public static class RoomTypeAssigner
{
    public static void Assign(DungeonLayout layout, DungeonConfig config, System.Random rng)
    {
        // 可分配池：排除 Start 与 Boss（生成器默认全部 Combat，特殊房从中挑走）
        var pool = new List<RoomNode>();
        foreach (RoomNode r in layout.rooms)
            if (r != layout.startRoom && r != layout.bossRoom) pool.Add(r);

        // 固定顺序保证确定性：Treasure → Shop → Event
        AssignSpecial(pool, RoomType.Treasure, config.treasureCount, rng);
        AssignSpecial(pool, RoomType.Shop, config.shopCount, rng);
        AssignSpecial(pool, RoomType.Event, config.eventCount, rng);

        // 剩余 Combat：distance≥2 按 eliteChance 抽 Elite
        foreach (RoomNode r in pool)
        {
            if (r.type != RoomType.Combat) continue;   // 已被特殊房占用
            if (r.distanceFromStart >= 2 && rng.NextDouble() < config.eliteChance)
                r.type = RoomType.Elite;
        }
    }

    /// <summary>从池中挑 count 个房间填为指定类型：叶子优先 → distance≥2 兜底 → 任意余房极端兜底。</summary>
    private static void AssignSpecial(List<RoomNode> pool, RoomType type, int count, System.Random rng)
    {
        for (int i = 0; i < count; i++)
        {
            RoomNode picked = PickRandom(pool, rng, requireLeaf: true, minDistance: 0);
            if (picked == null) picked = PickRandom(pool, rng, requireLeaf: false, minDistance: 2);
            if (picked == null) picked = PickRandom(pool, rng, requireLeaf: false, minDistance: 0);
            if (picked == null) return;   // 池空：尽力满足，数量不足由 Validate 断言按 min(配置, 容量) 核对
            picked.type = type;
            pool.Remove(picked);
        }
    }

    private static RoomNode PickRandom(List<RoomNode> pool, System.Random rng, bool requireLeaf, int minDistance)
    {
        var candidates = new List<RoomNode>();
        foreach (RoomNode r in pool)
        {
            if (requireLeaf && !r.IsLeaf) continue;
            if (r.distanceFromStart < minDistance) continue;
            candidates.Add(r);
        }
        if (candidates.Count == 0) return null;
        return candidates[rng.Next(candidates.Count)];
    }
}
