using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 房间布置计划（五职责架构的数据载体，RoomPlanner 产出、DungeonBuilder 消费）：
/// Walkable=可铺地/可行走格；Outline=挖除区与地面的单格交界墙；
/// Obstacles=房内小型掩体岛；Skeleton=中心区与门→中心的安全路网。
/// SpawnCells=最终内容生成白名单（自身及周围 8 格均为无墙地面）。
/// 深层挖除格既不属于 Walkable，也不属于 Outline；DungeonBuilder 必须真正留空。
/// </summary>
public class RoomPlan
{
    public RectInt Interior;
    public readonly HashSet<Vector2Int> Walkable = new HashSet<Vector2Int>();
    public readonly HashSet<Vector2Int> Outline = new HashSet<Vector2Int>();
    public readonly HashSet<Vector2Int> Obstacles = new HashSet<Vector2Int>();
    public readonly HashSet<Vector2Int> Skeleton = new HashSet<Vector2Int>();
    public readonly List<Vector2Int> SpawnCells = new List<Vector2Int>();

    public bool IsWalkable(Vector2Int c) => Walkable.Contains(c);
    public bool IsWall(Vector2Int c) => Outline.Contains(c) || Obstacles.Contains(c);

    public static RoomPlan Plain(RectInt interior)
    {
        var plan = new RoomPlan { Interior = interior };
        for (int y = interior.yMin; y < interior.yMax; y++)
            for (int x = interior.xMin; x < interior.xMax; x++)
                plan.Walkable.Add(new Vector2Int(x, y));
        plan.RefreshSpawnCells();
        return plan;
    }

    /// <summary>
    /// 从最终布局重建内容生成白名单。生成点使用格心并只做小幅抖动，因此要求 3×3 邻域
    /// 全部为无墙可走地面：既避开外墙/挖除空洞，也不依赖 TilemapCollider 同帧刷新来识别障碍。
    /// </summary>
    public void RefreshSpawnCells()
    {
        SpawnCells.Clear();
        for (int y = Interior.yMin; y < Interior.yMax; y++)
            for (int x = Interior.xMin; x < Interior.xMax; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!HasFreeHalo(cell)) continue;
                SpawnCells.Add(cell);
            }
    }

    private bool HasFreeHalo(Vector2Int center)
    {
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                var cell = new Vector2Int(center.x + dx, center.y + dy);
                if (!Walkable.Contains(cell) || IsWall(cell)) return false;
            }
        return true;
    }
}

/// <summary>
/// 房间布置编排器（v1.1.46）：
/// ShapeGenerator → BoundaryBuilder → 基础/玩家口径验证 → 14 组小掩体候选验证与评分择优。
/// 失败尝试使用 baseProtect 的独立副本，轮廓保护不再污染后续重试；障碍候选整体提交，
/// 不再采用逐段 Add/Remove 的贪心回滚。最终布局始终满足所有 free 格与门锚点净宽 ≥2 可达。
/// </summary>
public static class RoomPlanner
{
    private const int MaxShapeAttempts = 8;

    public static RoomPlan CreatePlan(RectInt interior, List<Vector2Int> doorCells, System.Random rng)
    {
        if (interior.width < 10 || interior.height < 8) return RoomPlan.Plain(interior);

        Vector2Int center = new Vector2Int(
            interior.xMin + interior.width / 2,
            interior.yMin + interior.height / 2);
        var doorList = doorCells ?? new List<Vector2Int>();
        var anchors = new List<Vector2Int>(doorList.Count);
        var baseProtect = BuildBaseProtect(interior, doorList, anchors, center);

        for (int attempt = 0; attempt < MaxShapeAttempts; attempt++)
        {
            // 每次尝试独享保护集。Assemble 会把本次轮廓邻圈写入，绝不能泄漏给下一次。
            var attemptProtect = new HashSet<Vector2Int>(baseProtect);
            HashSet<Vector2Int> carved = RoomShapeGenerator.Generate(interior, rng, attemptProtect);
            RoomPlan plan = Assemble(interior, carved, attemptProtect);

            if (!RoomLayoutValidator.Validate(plan, anchors, center)
                || !RoomLayoutValidator.ValidatePlayerGauge(plan, anchors, center))
                continue;

            PlaceBestObstacleCandidate(plan, interior, attemptProtect, anchors, center, rng);
            plan.RefreshSpawnCells();
            return plan;
        }

        // 任意异常输入仍回退完整矩形房，绝不把不可达形状交给表现层。
        return RoomPlan.Plain(interior);
    }

    private static HashSet<Vector2Int> BuildBaseProtect(RectInt interior,
        List<Vector2Int> doorCells, List<Vector2Int> anchors, Vector2Int center)
    {
        var protect = new HashSet<Vector2Int>();
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                AddIfInside(protect, interior, new Vector2Int(center.x + dx, center.y + dy));

        foreach (var door in doorCells)
        {
            Vector2Int anchor = ClampIn(interior, door);
            anchors.Add(anchor);
            foreach (var step in LineSteps(anchor, center))
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        AddIfInside(protect, interior, new Vector2Int(step.x + dx, step.y + dy));
        }
        return protect;
    }

    /// <summary>
    /// 以临时集合评估完整候选：合法性由 Validator 决定，观感由 Planner 评分决定。
    /// 只在全部候选评估结束后提交最高分方案，避免贪心顺序与回滚污染。
    /// </summary>
    private static void PlaceBestObstacleCandidate(RoomPlan plan, RectInt interior,
        HashSet<Vector2Int> protect, List<Vector2Int> anchors,
        Vector2Int center, System.Random rng)
    {
        if (RoomObstaclePlanner.ShouldLeaveEmpty(rng)) return;

        List<HashSet<Vector2Int>> candidates = RoomObstaclePlanner.PlanCandidates(
            interior, plan.Walkable, plan.Outline, protect, rng);
        HashSet<Vector2Int> best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < candidates.Count; i++)
        {
            HashSet<Vector2Int> candidate = candidates[i];
            plan.Obstacles.Clear();
            plan.Obstacles.UnionWith(candidate);

            if (!RoomLayoutValidator.Validate(plan, anchors, center)
                || !RoomLayoutValidator.ValidatePlayerGauge(plan, anchors, center))
                continue;

            // 小幅 seed 随机仅用于同质量候选破同分，质量规则仍占主导。
            float score = RoomObstaclePlanner.ScoreCandidate(interior, plan.Walkable.Count, candidate)
                        + (float)rng.NextDouble() * 0.75f;
            if (score <= bestScore) continue;
            bestScore = score;
            best = new HashSet<Vector2Int>(candidate);
        }

        plan.Obstacles.Clear();
        if (best != null) plan.Obstacles.UnionWith(best);
    }

    private static RoomPlan Assemble(RectInt interior, HashSet<Vector2Int> carved,
        HashSet<Vector2Int> attemptProtect)
    {
        var plan = new RoomPlan { Interior = interior };
        for (int y = interior.yMin; y < interior.yMax; y++)
            for (int x = interior.xMin; x < interior.xMax; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!carved.Contains(cell)) plan.Walkable.Add(cell);
            }

        foreach (var cell in RoomBoundaryBuilder.Build(carved, plan.Walkable))
            plan.Outline.Add(cell);
        foreach (var cell in attemptProtect)
            if (plan.Walkable.Contains(cell)) plan.Skeleton.Add(cell);

        // 障碍不得贴挖除轮廓；本次尝试独立扩张，不污染其他形状重试。
        foreach (var outlineCell in plan.Outline)
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var nearby = new Vector2Int(outlineCell.x + dx, outlineCell.y + dy);
                    if (plan.Walkable.Contains(nearby)) attemptProtect.Add(nearby);
                }
        return plan;
    }

    private static void AddIfInside(HashSet<Vector2Int> set, RectInt area, Vector2Int cell)
    {
        if (area.Contains(cell)) set.Add(cell);
    }

    private static Vector2Int ClampIn(RectInt area, Vector2Int cell)
        => new Vector2Int(
            Mathf.Clamp(cell.x, area.xMin, area.xMax - 1),
            Mathf.Clamp(cell.y, area.yMin, area.yMax - 1));

    private static List<Vector2Int> LineSteps(Vector2Int from, Vector2Int to)
    {
        var cells = new List<Vector2Int> { from };
        Vector2Int current = from;
        while (current != to)
        {
            int dx = System.Math.Sign(to.x - current.x);
            int dy = System.Math.Sign(to.y - current.y);
            current = new Vector2Int(
                current.x + (current.x != to.x ? dx : 0),
                current.y + (current.y != to.y ? dy : 0));
            cells.Add(current);
        }
        return cells;
    }
}
