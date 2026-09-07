using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 固定 Boss 仪式厅的纯数据结果。所有坐标都由实际入口方向映射：地图只决定房间位置与旋转，
/// 房内格局不消费地牢 seed。
/// </summary>
public sealed class BossRitualRoomLayout
{
    public RectInt Interior { get; }
    /// <summary>从唯一入口指向祭坛的方向。</summary>
    public Vector2Int Forward { get; }
    /// <summary>模板的右方向。</summary>
    public Vector2Int Right { get; }
    public IReadOnlyList<Vector2Int> DoorAnchors { get; }
    /// <summary>由最终 2×2 逻辑柱基求出的中心；表现层只能消费这里，禁止另写一份锚点。</summary>
    public IReadOnlyList<Vector2> PillarCenters { get; internal set; }
    public RoomPlan Plan { get; internal set; }

    public int LateralCells => Right.x != 0 ? Interior.width : Interior.height;
    public int DepthCells => Forward.x != 0 ? Interior.width : Interior.height;
    public float RotationDegrees => Mathf.Atan2(Right.y, Right.x) * Mathf.Rad2Deg;
    public float RitualDiameter => Mathf.Clamp(Mathf.Round(0.43f * Mathf.Min(LateralCells, DepthCells)), 8f, 16f);

    internal BossRitualRoomLayout(RectInt interior, Vector2Int forward,
        Vector2Int right, IReadOnlyList<Vector2Int> doorAnchors)
    {
        Interior = interior;
        Forward = forward;
        Right = right;
        DoorAnchors = doorAnchors;
    }

    /// <summary>
    /// 模板归一化坐标转世界坐标。lateral=-0.5..0.5；depth=0 为入口侧、1 为祭坛侧。
    /// </summary>
    public Vector2 Point(float lateral, float depth)
    {
        float lateralOffset = Mathf.Clamp(lateral, -0.5f, 0.5f) * Mathf.Max(0, LateralCells - 1);
        float depthOffset = (Mathf.Clamp01(depth) - 0.5f) * Mathf.Max(0, DepthCells - 1);
        return Interior.center + (Vector2)Right * lateralOffset + (Vector2)Forward * depthOffset;
    }

    /// <summary>归一化锚点落到房内格；格 (x,y) 的世界中心为 (x+0.5,y+0.5)。</summary>
    public Vector2Int Cell(float lateral, float depth)
    {
        Vector2 p = Point(lateral, depth);
        return new Vector2Int(
            Mathf.Clamp(Mathf.FloorToInt(p.x), Interior.xMin, Interior.xMax - 1),
            Mathf.Clamp(Mathf.FloorToInt(p.y), Interior.yMin, Interior.yMax - 1));
    }
}

/// <summary>
/// v1.1.51 固定 Boss 仪式厅：四柱、入口中轴、中央法阵和祭坛朝向均为常量。
/// 同一模板只随唯一入口旋转；不调用 Random，不参与普通房随机塑形。
/// </summary>
public static class BossRitualRoomTemplate
{
    /// <summary>Boss 房内容使用独立常量随机流，地图 seed 不再改变敌人组合。</summary>
    public const int ContentSeed = 0x0B055A11;

    private static readonly Vector2[] PillarSockets =
    {
        new Vector2(-0.27f, 0.30f), new Vector2(0.27f, 0.30f),
        new Vector2(-0.27f, 0.58f), new Vector2(0.27f, 0.58f),
    };

    private static readonly Vector2[] EnemySockets =
    {
        new Vector2(0f, 0.44f),
        new Vector2(-0.16f, 0.28f), new Vector2(0.16f, 0.28f),
        new Vector2(-0.20f, 0.48f), new Vector2(0.20f, 0.48f),
        new Vector2(-0.12f, 0.62f), new Vector2(0.12f, 0.62f),
        new Vector2(-0.34f, 0.40f), new Vector2(0.34f, 0.40f),
        new Vector2(-0.27f, 0.68f), new Vector2(0.27f, 0.68f),
        new Vector2(0f, 0.70f),
    };

    public static BossRitualRoomLayout Build(RectInt interior,
        IReadOnlyList<Vector2Int> doorCells)
    {
        Vector2Int forward = ResolveForward(interior, doorCells);
        Vector2Int right = new Vector2Int(forward.y, -forward.x);
        List<Vector2Int> anchors = BuildDoorAnchors(interior, doorCells);
        var layout = new BossRitualRoomLayout(interior, forward, right, anchors);

        RoomPlan plan = RoomPlan.Plain(interior);
        var pillarCenters = new List<Vector2>(PillarSockets.Length);
        foreach (Vector2 socket in PillarSockets)
            if (AddPillarIsland(plan, layout.Cell(socket.x, socket.y), out Vector2 pillarCenter))
                pillarCenters.Add(pillarCenter);

        Vector2Int ritualCenter = layout.Cell(0f, 0.44f);
        AddSafeAreaToSkeleton(plan, ritualCenter, 2);
        foreach (Vector2Int anchor in anchors)
            foreach (Vector2Int step in LineSteps(anchor, ritualCenter))
                AddSafeAreaToSkeleton(plan, step, 1);

        plan.Skeleton.ExceptWith(plan.Obstacles);
        plan.RefreshSpawnCells();
        layout.PillarCenters = pillarCenters;
        layout.Plan = plan;
        return layout;
    }

    /// <summary>
    /// 固定厅的粗格锚点：只接受南/北入口。南入口把原 Boss 格放在左下角；
    /// 北入口放在右上角，因此两者经 180° 旋转后连门偏移与房内构图完全一致。
    /// </summary>
    public static bool TryGetCoarseAnchor(RoomNode room, int span, out Vector2Int anchor)
    {
        anchor = room != null ? room.gridPos : default;
        if (room == null || !room.IsLeaf) return false;
        RoomNode neighbor = room.connections[0].Other(room);
        Vector2Int delta = neighbor.gridPos - room.gridPos;
        int offset = Mathf.Max(1, span) - 1;
        if (delta == Vector2Int.down) return true;
        if (delta == Vector2Int.up)
        {
            anchor = room.gridPos - new Vector2Int(offset, offset);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 固定敌人插槽：Boss 总在法阵中心，其余单位依次占据对称插槽；最多覆盖现有 12 敌人大房上限。
    /// 每个插槽吸附到最终 Plan 的 3×3 安全白名单，且不重复。
    /// </summary>
    public static List<Vector3> BuildEnemySpawnPositions(BossRitualRoomLayout layout)
    {
        var result = new List<Vector3>(EnemySockets.Length);
        if (layout == null || layout.Plan == null || layout.Plan.SpawnCells.Count == 0) return result;

        var used = new HashSet<Vector2Int>();
        foreach (Vector2 socket in EnemySockets)
        {
            Vector2Int desired = layout.Cell(socket.x, socket.y);
            Vector2Int best = default;
            int bestDistance = int.MaxValue;
            bool found = false;
            foreach (Vector2Int candidate in layout.Plan.SpawnCells)
            {
                if (used.Contains(candidate)) continue;
                int dx = candidate.x - desired.x;
                int dy = candidate.y - desired.y;
                int distance = dx * dx + dy * dy;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = candidate;
                found = true;
            }
            if (!found) break;
            used.Add(best);
            result.Add(new Vector3(best.x + 0.5f, best.y + 0.5f, 0f));
        }
        return result;
    }

    /// <summary>清房宝箱与传送门的两个固定安全插槽，同样吸附最终 SpawnCells。</summary>
    public static List<Vector3> BuildRewardSpawnPositions(BossRitualRoomLayout layout)
    {
        var result = new List<Vector3>(2);
        if (layout == null || layout.Plan == null || layout.Plan.SpawnCells.Count == 0) return result;
        var used = new HashSet<Vector2Int>();
        AddNearestSafePosition(layout, -0.08f, 0.44f, used, result);
        AddNearestSafePosition(layout, 0.08f, 0.44f, used, result);
        return result;
    }

    private static Vector2Int ResolveForward(RectInt interior,
        IReadOnlyList<Vector2Int> doorCells)
    {
        if (doorCells == null || doorCells.Count == 0) return Vector2Int.up;

        Vector2 average = Vector2.zero;
        for (int i = 0; i < doorCells.Count; i++) average += (Vector2)doorCells[i];
        average /= doorCells.Count;

        float best = Mathf.Abs(average.y - (interior.yMin - 1));
        Vector2Int forward = Vector2Int.up;       // 南入口
        float north = Mathf.Abs(average.y - interior.yMax);
        if (north < best) { best = north; forward = Vector2Int.down; }
        float west = Mathf.Abs(average.x - (interior.xMin - 1));
        if (west < best) { best = west; forward = Vector2Int.right; }
        float east = Mathf.Abs(average.x - interior.xMax);
        if (east < best) forward = Vector2Int.left;
        return forward;
    }

    private static List<Vector2Int> BuildDoorAnchors(RectInt interior,
        IReadOnlyList<Vector2Int> doorCells)
    {
        var result = new List<Vector2Int>();
        if (doorCells == null) return result;
        for (int i = 0; i < doorCells.Count; i++)
        {
            Vector2Int d = doorCells[i];
            var anchor = new Vector2Int(
                Mathf.Clamp(d.x, interior.xMin, interior.xMax - 1),
                Mathf.Clamp(d.y, interior.yMin, interior.yMax - 1));
            if (!result.Contains(anchor)) result.Add(anchor);
        }
        return result;
    }

    private static bool AddPillarIsland(RoomPlan plan, Vector2Int center, out Vector2 visualCenter)
    {
        // 2×2 逻辑柱基 = 4 格，严格满足 RoomLayoutValidator 的单岛上限；
        // 表现层可画得更高更宽，但碰撞/寻路仍以这一份 Plan 为真源。
        var cells = new[]
        {
            center + new Vector2Int(-1, -1), center + new Vector2Int(0, -1),
            center + new Vector2Int(-1, 0), center,
        };
        visualCenter = Vector2.zero;
        for (int i = 0; i < cells.Length; i++)
        {
            Vector2Int c = cells[i];
            if (!plan.Interior.Contains(c)
                || c.x <= plan.Interior.xMin + 1 || c.x >= plan.Interior.xMax - 2
                || c.y <= plan.Interior.yMin + 1 || c.y >= plan.Interior.yMax - 2
                || plan.Obstacles.Contains(c))
                return false;
        }
        for (int i = 0; i < cells.Length; i++)
        {
            plan.Obstacles.Add(cells[i]);
            visualCenter += (Vector2)cells[i] + new Vector2(0.5f, 0.5f);
        }
        visualCenter /= cells.Length;
        return true;
    }

    private static void AddNearestSafePosition(BossRitualRoomLayout layout, float lateral, float depth,
        HashSet<Vector2Int> used, List<Vector3> result)
    {
        Vector2Int desired = layout.Cell(lateral, depth);
        Vector2Int best = default;
        int bestDistance = int.MaxValue;
        bool found = false;
        foreach (Vector2Int candidate in layout.Plan.SpawnCells)
        {
            if (used.Contains(candidate)) continue;
            int dx = candidate.x - desired.x;
            int dy = candidate.y - desired.y;
            int distance = dx * dx + dy * dy;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = candidate;
            found = true;
        }
        if (!found) return;
        used.Add(best);
        result.Add(new Vector3(best.x + 0.5f, best.y + 0.5f, 0f));
    }

    private static void AddSafeAreaToSkeleton(RoomPlan plan, Vector2Int center, int radius)
    {
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                var cell = center + new Vector2Int(dx, dy);
                if (plan.Interior.Contains(cell) && plan.Walkable.Contains(cell)
                    && !plan.IsWall(cell))
                    plan.Skeleton.Add(cell);
            }
    }

    private static IEnumerable<Vector2Int> LineSteps(Vector2Int from, Vector2Int to)
    {
        Vector2Int current = from;
        yield return current;
        while (current != to)
        {
            current = new Vector2Int(
                current.x + System.Math.Sign(to.x - current.x),
                current.y + System.Math.Sign(to.y - current.y));
            yield return current;
        }
    }
}
