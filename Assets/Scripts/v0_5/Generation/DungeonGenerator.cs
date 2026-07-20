using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 纯 C# 地牢布局生成器：网格邻接生长法（v0.5 计划书 4.3）。
/// 不依赖场景/MonoBehaviour，可离线自检（DungeonManager.Validate1000Seeds）。
/// 连通性由构造保证：每个新房间都贴在已有房间上。
/// </summary>
public static class DungeonGenerator
{
    private static readonly Vector2Int[] Dirs =
        { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    /// <summary>生成一层地牢布局。同 config + 同 seed → 同一张图。</summary>
    public static DungeonLayout Generate(DungeonConfig config, int seed)
    {
        var rng = new System.Random(seed);
        int target = rng.Next(config.roomCountMin, config.roomCountMax + 1);

        // 防御性重roll：生长失败（候选格提前耗尽）或房间数不足时整张重来（算法上几乎不会触发）
        for (int attempt = 0; attempt < 50; attempt++)
        {
            DungeonLayout layout = TryGrow(config, rng, target, seed);
            if (layout != null) return layout;
        }
        Debug.LogError($"[Dungeon] Generator 50 次重roll仍失败 (seed={seed})，按下限房间数保底");
        return TryGrow(config, rng, config.roomCountMin, seed);
    }

    private static DungeonLayout TryGrow(DungeonConfig config, System.Random rng, int target, int seed)
    {
        var layout = new DungeonLayout { seed = seed };
        var placed = new Dictionary<Vector2Int, RoomNode>();
        var candidates = new List<Vector2Int>();
        var candidateSet = new HashSet<Vector2Int>();

        // 1. 起始房间放原点
        var start = new RoomNode { id = 0, gridPos = Vector2Int.zero, type = RoomType.Start };
        layout.rooms.Add(start);
        layout.startRoom = start;
        placed[start.gridPos] = start;
        AddCandidates(start.gridPos, placed, candidates, candidateSet);

        // 2. 邻接生长到目标数量
        int guard = target * 20 + 100; // 防御：候选耗尽/异常时跳出
        while (layout.rooms.Count < target && candidates.Count > 0 && guard-- > 0)
        {
            int idx = rng.Next(candidates.Count);
            Vector2Int cell = candidates[idx];
            candidates[idx] = candidates[candidates.Count - 1]; // swap-remove，O(1)
            candidates.RemoveAt(candidates.Count - 1);
            candidateSet.Remove(cell);
            if (placed.ContainsKey(cell)) continue; // 防御

            var room = new RoomNode { id = layout.rooms.Count, gridPos = cell, type = RoomType.Combat };
            layout.rooms.Add(room);
            placed[cell] = room;

            // 与所有相邻已放置房间建立连接（自然产生环路，地图不是纯树）
            foreach (Vector2Int dir in Dirs)
            {
                if (placed.TryGetValue(cell + dir, out RoomNode neighbor))
                {
                    var conn = new RoomConnection(room, neighbor);
                    layout.connections.Add(conn);
                    room.connections.Add(conn);
                    neighbor.connections.Add(conn);
                }
            }
            AddCandidates(cell, placed, candidates, candidateSet);
        }

        if (layout.rooms.Count < config.roomCountMin) return null; // 触发重roll

        ComputeDistancesFromStart(layout);
        layout.bossRoom = SelectBossRoom(layout);
        layout.bossRoom.type = RoomType.Boss;
        return layout;
    }

    private static void AddCandidates(Vector2Int cell, Dictionary<Vector2Int, RoomNode> placed,
        List<Vector2Int> candidates, HashSet<Vector2Int> candidateSet)
    {
        foreach (Vector2Int dir in Dirs)
        {
            Vector2Int next = cell + dir;
            if (!placed.ContainsKey(next) && candidateSet.Add(next))
                candidates.Add(next);
        }
    }

    /// <summary>BFS：填每个房间的 distanceFromStart（不可达 = -1，构造上不存在）。</summary>
    private static void ComputeDistancesFromStart(DungeonLayout layout)
    {
        foreach (RoomNode r in layout.rooms) r.distanceFromStart = -1;
        var queue = new Queue<RoomNode>();
        layout.startRoom.distanceFromStart = 0;
        queue.Enqueue(layout.startRoom);
        while (queue.Count > 0)
        {
            RoomNode cur = queue.Dequeue();
            foreach (RoomConnection conn in cur.connections)
            {
                RoomNode next = conn.Other(cur);
                if (next.distanceFromStart >= 0) continue;
                next.distanceFromStart = cur.distanceFromStart + 1;
                queue.Enqueue(next);
            }
        }
    }

    /// <summary>
    /// Boss 房 = BFS 距离最远的房间，并列时叶子房优先。
    /// 全局最远即「尽力满足 bossMinDistance」：若最远房都不达标，则不存在达标房间。
    /// </summary>
    private static RoomNode SelectBossRoom(DungeonLayout layout)
    {
        RoomNode best = null;
        foreach (RoomNode r in layout.rooms)
        {
            if (r == layout.startRoom) continue;
            if (best == null
                || r.distanceFromStart > best.distanceFromStart
                || (r.distanceFromStart == best.distanceFromStart && r.IsLeaf && !best.IsLeaf))
                best = r;
        }
        return best;
    }
}
