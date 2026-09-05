using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 职责 3/5 · 房内障碍规划器（v1.1.46 重做）：
/// 以“多个分散的小掩体岛”取代围绕房心的固定长墙模板。随机只负责提出候选，
/// RoomPlanner 负责用连通性/玩家口径验证并按密度、分布、间距评分择优。
///
/// 硬约束：
/// - 外墙前保留 2~3 格呼吸带，距挖除轮廓至少 2 格；
/// - 门→房心骨架与中心战斗区禁放；
/// - 单岛 2~4 格，岛与岛之间至少留 2 个完整地板格；
/// - 所有随机均消费调用方传入的 System.Random，同 seed 可复现。
/// </summary>
public static class RoomObstaclePlanner
{
    private const int CandidateCount = 14;
    private const int PlacementAttemptsPerIsland = 32;
    private const float EmptyRoomChance = 0.08f;
    private const int OutlineClearance = 2;
    private const int CenterClearHalfWidth = 3;
    private const int CenterClearHalfHeight = 2;

    public const int MaxIslandCells = 4;
    /// <summary>不同岛最近格的切比雪夫距离下限；3 表示两岛之间至少有 2 格空地。</summary>
    public const int MinimumIslandCellDistance = 3;

    // 小掩体形状。旋转/镜像后再落位；不使用长墙，避免切割竞技场或形成大片墙团。
    private static readonly Vector2Int[][] CoverShapes =
    {
        new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) },
        new[] { new Vector2Int(-1, 0), new Vector2Int(0, 0), new Vector2Int(1, 0) },
        new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1) },
        new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(0, 1) },
        new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) },
    };

    /// <summary>保留少量真正的空旷竞技场；结果仍由房间 seed 决定。</summary>
    public static bool ShouldLeaveEmpty(System.Random rng) => rng.NextDouble() < EmptyRoomChance;

    /// <summary>
    /// 生成若干完整候选。每个候选都在临时集合中一次建成，不对 RoomPlan 做增量 Add/Remove，
    /// 因而不会发生重叠段回滚时误删旧障碍格的问题。
    /// </summary>
    public static List<HashSet<Vector2Int>> PlanCandidates(RectInt interior,
        HashSet<Vector2Int> walkable, HashSet<Vector2Int> outline,
        HashSet<Vector2Int> protect, System.Random rng)
    {
        var candidates = new List<HashSet<Vector2Int>>(CandidateCount);
        for (int i = 0; i < CandidateCount; i++)
        {
            HashSet<Vector2Int> candidate = BuildCandidate(interior, walkable, outline, protect, rng);
            if (candidate.Count > 0) candidates.Add(candidate);
        }
        return candidates;
    }

    /// <summary>观感评分：接近面积自适应密度、覆盖更多象限、横纵展开更均衡者优先。</summary>
    public static float ScoreCandidate(RectInt interior, int walkableCount,
        HashSet<Vector2Int> obstacles)
    {
        if (obstacles == null || obstacles.Count == 0) return float.NegativeInfinity;

        int targetCells = TargetObstacleCells(walkableCount);
        float score = 50f - Mathf.Abs(obstacles.Count - targetCells) * 3f;

        Vector2Int center = CenterOf(interior);
        bool[] quadrants = new bool[4];
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        foreach (var c in obstacles)
        {
            int qx = c.x < center.x ? 0 : 1;
            int qy = c.y < center.y ? 0 : 1;
            quadrants[qx + qy * 2] = true;
            minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
            minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y);
        }

        int usedQuadrants = 0;
        for (int i = 0; i < quadrants.Length; i++) if (quadrants[i]) usedQuadrants++;
        score += usedQuadrants * 4f;

        float spanX = (maxX - minX + 1f) / Mathf.Max(1, interior.width);
        float spanY = (maxY - minY + 1f) / Mathf.Max(1, interior.height);
        score += (spanX + spanY) * 8f;
        return score;
    }

    /// <summary>按可走面积计算障碍目标量：约 2.8%，小房不少于 6，大房不超过 26。</summary>
    public static int TargetObstacleCells(int walkableCount)
        => Mathf.Clamp(Mathf.RoundToInt(walkableCount * 0.028f), 6, 26);

    /// <summary>小房保留 2 格外圈，常规/大房保留 3 格外圈。</summary>
    public static int OuterWallClearance(RectInt interior)
        => Mathf.Min(interior.width, interior.height) >= 14 ? 3 : 2;

    /// <summary>单格落位硬约束，供候选生成与测试同源复用。</summary>
    public static bool HardConstraint(RectInt interior, HashSet<Vector2Int> walkable,
        HashSet<Vector2Int> outline, HashSet<Vector2Int> protect, Vector2Int cell)
    {
        int outer = OuterWallClearance(interior);
        if (cell.x < interior.xMin + outer || cell.x >= interior.xMax - outer) return false;
        if (cell.y < interior.yMin + outer || cell.y >= interior.yMax - outer) return false;
        if (!walkable.Contains(cell) || outline.Contains(cell) || protect.Contains(cell)) return false;

        Vector2Int center = CenterOf(interior);
        if (Mathf.Abs(cell.x - center.x) <= CenterClearHalfWidth
            && Mathf.Abs(cell.y - center.y) <= CenterClearHalfHeight) return false;

        // 内轮廓也按“墙”处理，保留 2 格完整地板带，防止视觉粘连与 1 格夹缝。
        for (int dy = -OutlineClearance; dy <= OutlineClearance; dy++)
            for (int dx = -OutlineClearance; dx <= OutlineClearance; dx++)
                if (outline.Contains(new Vector2Int(cell.x + dx, cell.y + dy))) return false;
        return true;
    }

    private static HashSet<Vector2Int> BuildCandidate(RectInt interior,
        HashSet<Vector2Int> walkable, HashSet<Vector2Int> outline,
        HashSet<Vector2Int> protect, System.Random rng)
    {
        var result = new HashSet<Vector2Int>();
        int targetCells = TargetObstacleCells(walkable.Count);
        int baseIslands = Mathf.Clamp(Mathf.RoundToInt(walkable.Count / 120f), 2, 8);
        int targetIslands = Mathf.Clamp(baseIslands + rng.Next(-1, 2), 2, 8);
        bool pairedStyle = rng.NextDouble() < 0.35;
        Vector2Int center = CenterOf(interior);
        int islandCount = 0;

        while (islandCount < targetIslands && result.Count < targetCells)
        {
            Vector2Int[] shape = CoverShapes[rng.Next(CoverShapes.Length)];
            int rotation = rng.Next(4);
            bool mirror = rng.NextDouble() < 0.5;
            List<Vector2Int> island = TryBuildIsland(interior, walkable, outline, protect,
                result, shape, rotation, mirror, rng);
            if (island == null) break;

            AddAll(result, island);
            islandCount++;

            // 部分候选采用中心对置的小岛，形成有设计感但不完全固定的掩体布局。
            if (pairedStyle && islandCount < targetIslands && result.Count < targetCells)
            {
                var opposite = new List<Vector2Int>(island.Count);
                for (int i = 0; i < island.Count; i++)
                    opposite.Add(new Vector2Int(center.x * 2 - island[i].x, center.y * 2 - island[i].y));
                if (CanPlaceIsland(opposite, interior, walkable, outline, protect, result))
                {
                    AddAll(result, opposite);
                    islandCount++;
                }
            }
        }

        return result;
    }

    private static List<Vector2Int> TryBuildIsland(RectInt interior,
        HashSet<Vector2Int> walkable, HashSet<Vector2Int> outline,
        HashSet<Vector2Int> protect, HashSet<Vector2Int> placed,
        Vector2Int[] shape, int rotation, bool mirror, System.Random rng)
    {
        for (int attempt = 0; attempt < PlacementAttemptsPerIsland; attempt++)
        {
            var anchor = new Vector2Int(
                rng.Next(interior.xMin, interior.xMax),
                rng.Next(interior.yMin, interior.yMax));
            var island = new List<Vector2Int>(shape.Length);
            for (int i = 0; i < shape.Length; i++)
                island.Add(anchor + Transform(shape[i], rotation, mirror));

            if (CanPlaceIsland(island, interior, walkable, outline, protect, placed)) return island;
        }
        return null;
    }

    private static bool CanPlaceIsland(List<Vector2Int> island, RectInt interior,
        HashSet<Vector2Int> walkable, HashSet<Vector2Int> outline,
        HashSet<Vector2Int> protect, HashSet<Vector2Int> placed)
    {
        for (int i = 0; i < island.Count; i++)
        {
            Vector2Int c = island[i];
            if (!HardConstraint(interior, walkable, outline, protect, c)) return false;
            for (int j = 0; j < i; j++) if (island[j] == c) return false;

            foreach (var existing in placed)
                if (Chebyshev(c, existing) < MinimumIslandCellDistance) return false;
        }
        return true;
    }

    private static void AddAll(HashSet<Vector2Int> target, List<Vector2Int> cells)
    {
        for (int i = 0; i < cells.Count; i++) target.Add(cells[i]);
    }

    private static Vector2Int CenterOf(RectInt interior)
        => new Vector2Int(interior.xMin + interior.width / 2, interior.yMin + interior.height / 2);

    private static int Chebyshev(Vector2Int a, Vector2Int b)
        => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

    private static Vector2Int Transform(Vector2Int p, int rotation, bool mirror)
    {
        if (mirror) p.x = -p.x;
        for (int i = 0; i < rotation; i++) p = new Vector2Int(-p.y, p.x);
        return p;
    }
}
