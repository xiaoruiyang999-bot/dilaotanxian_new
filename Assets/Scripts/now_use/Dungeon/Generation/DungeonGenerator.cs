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
            if (layout != null)
            {
                // v0.5.3 类型分配 + v0.5.3.1 尺寸扩展：共用独立随机流（seed*31+7），布局流零接触
                var typeRng = new System.Random(seed * 31 + 7);
                RoomTypeAssigner.Assign(layout, config, typeRng);
                RoomSizeExpander.Expand(layout, config, typeRng);
                // v1.1.52 固定仪式厅必须是南入口叶子且完整 2×2；不允许尺寸/朝向随 seed 改变。
                int requiredBossSpan = BossRitualRoomTemplate.CoarseSpan;
                if (layout.bossRoom.IsLeaf
                    && layout.bossRoom.spanX == requiredBossSpan
                    && layout.bossRoom.spanY == requiredBossSpan)
                    return layout;
            }
        }
        Debug.LogError($"[Dungeon] Generator 50 次重roll仍未找到完整单入口 Boss 厅 (seed={seed})，使用确定性直线保底图");
        DungeonLayout fallback = BuildLinearFallback(config, seed);
        var fallbackTypeRng = new System.Random(seed * 31 + 7);
        RoomTypeAssigner.Assign(fallback, config, fallbackTypeRng);
        RoomSizeExpander.Expand(fallback, config, fallbackTypeRng);
        return fallback;
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
        layout.bossRoom = SelectBossRoom(layout, BossRitualRoomTemplate.CoarseSpan,
            Mathf.Max(0, config.bossMinDistance));
        if (layout.bossRoom == null) return null;   // 没有可扩成固定单入口厅的节点，整图重 roll
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
    /// Boss 房 = 达到最小距离且能在北侧预留完整 2×2 区域的最远南入口叶子房。
    /// 若本次图没有合格节点则整图重 roll；极端情况下由直线保底图满足可满足的最小距离。
    /// </summary>
    private static RoomNode SelectBossRoom(DungeonLayout layout, int requiredSpan, int minimumDistance)
    {
        var occupied = new HashSet<Vector2Int>();
        foreach (RoomNode room in layout.rooms) occupied.Add(room.gridPos);

        RoomNode best = null;
        foreach (RoomNode r in layout.rooms)
        {
            if (r == layout.startRoom || !r.IsLeaf || r.distanceFromStart < minimumDistance
                || !CanReserveBossSquare(r, requiredSpan, occupied)) continue;
            if (best == null || r.distanceFromStart > best.distanceFromStart)
                best = r;
        }
        return best;
    }

    /// <summary>与 RoomSizeExpander 的四角扩张口径一致；此时所有房仍是 1×1。</summary>
    private static bool CanReserveBossSquare(RoomNode room, int span, HashSet<Vector2Int> occupied)
    {
        if (span != BossRitualRoomTemplate.CoarseSpan
            || !BossRitualRoomTemplate.TryGetCoarseAnchor(room, out Vector2Int anchor)) return false;
        for (int x = 0; x < span; x++)
            for (int y = 0; y < span; y++)
            {
                Vector2Int cell = anchor + new Vector2Int(x, y);
                if (cell != room.gridPos && occupied.Contains(cell)) return false;
            }
        return true;
    }

    /// <summary>
    /// 极端配置/随机流保底：直线图最后一个节点天然为南入口叶子，北侧必有完整 2×2 空间。
    /// 仍经过统一类型分配与尺寸扩展，不引入第二套运行时构建路径。
    /// </summary>
    private static DungeonLayout BuildLinearFallback(DungeonConfig config, int seed)
    {
        int count = Mathf.Max(2, config.roomCountMin);
        var layout = new DungeonLayout { seed = seed };
        RoomNode previous = null;
        for (int i = 0; i < count; i++)
        {
            var room = new RoomNode
            {
                id = i,
                gridPos = new Vector2Int(0, i),
                type = i == 0 ? RoomType.Start : RoomType.Combat,
            };
            layout.rooms.Add(room);
            if (previous != null)
            {
                var connection = new RoomConnection(previous, room);
                layout.connections.Add(connection);
                previous.connections.Add(connection);
                room.connections.Add(connection);
            }
            previous = room;
        }

        layout.startRoom = layout.rooms[0];
        layout.bossRoom = layout.rooms[layout.rooms.Count - 1];
        layout.bossRoom.type = RoomType.Boss;
        ComputeDistancesFromStart(layout);
        return layout;
    }
}
