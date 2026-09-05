using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 职责 4/5 · 布局验证器（v1.1.31；v1.1.44 增玩家口径；v1.1.46 收紧掩体岛尺寸）。
/// Validate（格级，基础门槛）：① 门锚点从房心格级可达；② 可行走面积 ≥65%；
/// ③ 障碍连通块 ≤4；④ 障碍零接触外墙/轮廓。⑤ 全 free 格从中心格级可达。
/// ValidatePlayerGauge（v1.1.44 玩家口径，用户规格"由障碍墙构成的新的区域必须可进入，
/// 一定要留有 ≥2 格宽空间"）：玩家胶囊净宽 1.32（Player.prefab 3.65×scale0.3618），
/// 1 格净宽（1.0）物理过不去、2 格（2.0）可过——通道以"净宽 ≥2"判定：
/// 通行边 = 4 邻边两侧至少一侧的 2 格带皆 free；要求**所有** free 格与门锚点
/// 都在中心的 2 宽可达集内（含 1 宽缝也不许残留——内容生成器会往任何 free 缝里
/// 刷敌人/宝箱/大石块，刷进玩家进不去的缝即卡清房/拿不到奖励）。
/// </summary>
public static class RoomLayoutValidator
{
    private const float MinFreeRatio = 0.65f;
    private const int MaxObstacleComponent = RoomObstaclePlanner.MaxIslandCells;

    public static bool Validate(RoomPlan plan, List<Vector2Int> doorAnchors, Vector2Int center)
    {
        if (!plan.Walkable.Contains(center) || plan.Obstacles.Contains(center)) return false;

        // ③④ 障碍块尺寸 + 零贴墙
        if (!CheckObstacles(plan)) return false;

        // ① BFS：房心出发的可达集
        var reach = FloodFromCenter(plan, center);

        // ② 面积比（free = 可达即有效行走面）
        if (reach.Count < plan.Interior.width * plan.Interior.height * MinFreeRatio) return false;

        foreach (var anchor in doorAnchors)
            if (!reach.Contains(anchor)) return false;

        // ⑤ 全连通（用户要求：房内各个小空间都可达）——
        // 所有"可行走且非障碍"的格子必须全部从中心可达，出现任何孤岛即判失败重试
        int freeTotal = 0;
        foreach (var c in plan.Walkable)
            if (!plan.Obstacles.Contains(c)) freeTotal++;
        return reach.Count == freeTotal;
    }

    /// <summary>v1.1.44 玩家口径：所有 free 格 + 门锚点 ∈ 中心的 2 宽可达集。</summary>
    public static bool ValidatePlayerGauge(RoomPlan plan, List<Vector2Int> doorAnchors, Vector2Int center)
    {
        if (!IsFree(plan, center)) return false;
        var reach = Reachable2Wide(plan, center);

        foreach (var anchor in doorAnchors)
            if (!reach.Contains(anchor)) return false;

        foreach (var c in plan.Walkable)
            if (!plan.Obstacles.Contains(c) && !reach.Contains(c)) return false;
        return true;
    }

    private static bool IsFree(RoomPlan plan, Vector2Int c)
        => plan.Walkable.Contains(c) && !plan.Obstacles.Contains(c);

    /// <summary>通行边判定：4 邻边 (u,v) 两侧至少一侧的 2 格带皆 free（该处通道净宽 ≥2）。</summary>
    private static bool EdgePassable(RoomPlan plan, Vector2Int u, Vector2Int v)
    {
        if (!IsFree(plan, u) || !IsFree(plan, v)) return false;
        if (u.y == v.y)   // 水平边：上/下两侧
        {
            int x0 = Mathf.Min(u.x, v.x);
            return (IsFree(plan, new Vector2Int(x0, u.y + 1)) && IsFree(plan, new Vector2Int(x0 + 1, u.y + 1)))
                || (IsFree(plan, new Vector2Int(x0, u.y - 1)) && IsFree(plan, new Vector2Int(x0 + 1, u.y - 1)));
        }
        else              // 垂直边：左/右两侧
        {
            int y0 = Mathf.Min(u.y, v.y);
            return (IsFree(plan, new Vector2Int(u.x + 1, y0)) && IsFree(plan, new Vector2Int(u.x + 1, y0 + 1)))
                || (IsFree(plan, new Vector2Int(u.x - 1, y0)) && IsFree(plan, new Vector2Int(u.x - 1, y0 + 1)));
        }
    }

    /// <summary>玩家口径可达集：从中心沿"净宽 ≥2 通行边"BFS。</summary>
    public static HashSet<Vector2Int> Reachable2Wide(RoomPlan plan, Vector2Int center)
    {
        var reach = new HashSet<Vector2Int>();
        if (!IsFree(plan, center)) return reach;
        reach.Add(center);
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(center);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            Step(plan, reach, queue, c, new Vector2Int(c.x + 1, c.y));
            Step(plan, reach, queue, c, new Vector2Int(c.x - 1, c.y));
            Step(plan, reach, queue, c, new Vector2Int(c.x, c.y + 1));
            Step(plan, reach, queue, c, new Vector2Int(c.x, c.y - 1));
        }
        return reach;
    }

    private static void Step(RoomPlan plan, HashSet<Vector2Int> reach, Queue<Vector2Int> queue,
        Vector2Int c, Vector2Int n)
    {
        if (reach.Contains(n) || !EdgePassable(plan, c, n)) return;
        reach.Add(n);
        queue.Enqueue(n);
    }

    private static bool CheckObstacles(RoomPlan plan)
    {
        var visited = new HashSet<Vector2Int>();
        foreach (var seed in plan.Obstacles)
        {
            if (visited.Contains(seed)) continue;
            int size = 0;
            var stack = new Stack<Vector2Int>();
            stack.Push(seed);
            visited.Add(seed);
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                size++;
                if (size > MaxObstacleComponent) return false;
                // ④ 贴墙复核：4 邻接轮廓墙 / 内部边界（外墙）即违规
                if (plan.Outline.Contains(c)) return false;
                if (c.x <= plan.Interior.xMin + 1 || c.x >= plan.Interior.xMax - 2) return false;
                if (c.y <= plan.Interior.yMin + 1 || c.y >= plan.Interior.yMax - 2) return false;
                PushNeighbor(stack, visited, plan, new Vector2Int(c.x + 1, c.y));
                PushNeighbor(stack, visited, plan, new Vector2Int(c.x - 1, c.y));
                PushNeighbor(stack, visited, plan, new Vector2Int(c.x, c.y + 1));
                PushNeighbor(stack, visited, plan, new Vector2Int(c.x, c.y - 1));
            }
        }
        return true;
    }

    private static void PushNeighbor(Stack<Vector2Int> stack, HashSet<Vector2Int> visited, RoomPlan plan, Vector2Int c)
    {
        if (plan.Obstacles.Contains(c) && !visited.Contains(c)) { visited.Add(c); stack.Push(c); }
    }

    private static HashSet<Vector2Int> FloodFromCenter(RoomPlan plan, Vector2Int center)
    {
        var reach = new HashSet<Vector2Int> { center };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(center);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            TryStep(plan, reach, queue, new Vector2Int(c.x + 1, c.y));
            TryStep(plan, reach, queue, new Vector2Int(c.x - 1, c.y));
            TryStep(plan, reach, queue, new Vector2Int(c.x, c.y + 1));
            TryStep(plan, reach, queue, new Vector2Int(c.x, c.y - 1));
        }
        return reach;
    }

    private static void TryStep(RoomPlan plan, HashSet<Vector2Int> reach, Queue<Vector2Int> queue, Vector2Int c)
    {
        if (reach.Contains(c) || !plan.Walkable.Contains(c) || plan.Obstacles.Contains(c)) return;
        reach.Add(c);
        queue.Enqueue(c);
    }
}
