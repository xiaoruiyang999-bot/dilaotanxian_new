using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 敌人局部 A* 寻路（v1.1.32）：贴近墙/障碍时更新最短接近路径，不再贴墙滑行。
/// 局部网格：以起终点包围盒外扩 10 格（上限 44×44），按需逐格查询可行走性——
/// 静态阻挡 = 墙 TilemapCollider2D + Obstacle 层（石块/障碍物），玩家/敌人等动态体不算阻挡；
/// 8 向（对角需两邻格均可走），octile 启发式；结果拉绳简化（体宽双射线验证可跳段）。
/// 零持续分配：网格字典/开放表/路径表全部静态复用（FindPath 单线程同步调用）。
/// </summary>
public static class EnemyPathfinder
{
    private const int Padding = 10;
    private const int MaxGridDim = 44;
    private const float CellWalkBox = 0.92f;     // 敌人≈1×1：格心 0.92 盒视为该格可站立
    private static readonly Vector2Int[] Dirs8 =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1),
    };
    private const int DiagCost = 14, StraightCost = 10;       // ×10 整数代价

    private static readonly Dictionary<Vector2Int, bool> walkable = new Dictionary<Vector2Int, bool>();
    private static readonly Dictionary<Vector2Int, int> gScore = new Dictionary<Vector2Int, int>();
    private static readonly Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
    private static readonly List<Vector2Int> open = new List<Vector2Int>();
    private static readonly HashSet<Vector2Int> closed = new HashSet<Vector2Int>();
    private static readonly List<Vector2> rawPath = new List<Vector2>();
    private static readonly Collider2D[] overlapBuffer = new Collider2D[8];
    private static readonly RaycastHit2D[] rayBuffer = new RaycastHit2D[8];
    private static int blockerMask = -1;
    private static ContactFilter2D blockerFilter;

    private static void EnsureBlockerFilter()
    {
        if (blockerMask >= 0) return;
        blockerMask = LayerMask.GetMask("Default", "Obstacle");
        blockerFilter = new ContactFilter2D { useTriggers = false };
        blockerFilter.SetLayerMask(blockerMask);
    }

    /// <summary>体宽直线通畅检查（供 EnemyAI 直走/跳段判定）：中心射线 + 两侧 ±0.32 平行射线。</summary>
    public static bool BodyLineClear(Vector2 from, Vector2 to, Transform self)
    {
        EnsureBlockerFilter();
        Vector2 delta = to - from;
        float dist = delta.magnitude;
        if (dist <= 0.01f) return true;
        Vector2 dir = delta / dist;
        Vector2 perp = new Vector2(-dir.y, dir.x) * 0.32f;
        return RayClear(from, dir, dist, self) && RayClear(from + perp, dir, dist, self) && RayClear(from - perp, dir, dist, self);
    }

    private static bool RayClear(Vector2 origin, Vector2 dir, float dist, Transform self)
    {
        int n = Physics2D.RaycastNonAlloc(origin, dir, rayBuffer, dist - 0.05f, blockerMask);
        for (int i = 0; i < n; i++)
        {
            var hit = rayBuffer[i];
            if (hit.collider == null || hit.collider.isTrigger) continue;
            Transform t = hit.collider.transform;
            if (t == self || t.IsChildOf(self)) continue;
            if (!IsStaticBlocker(hit.collider)) continue;   // 玩家/敌人等动态体不挡路
            return false;
        }
        return true;
    }

    /// <summary>求解路径（世界坐标航点，含起点后半段到终点）。失败返回 false（调用方回退直线）。</summary>
    public static bool FindPath(Vector2 from, Vector2 to, Transform self, List<Vector2> result)
    {
        result.Clear();
        EnsureBlockerFilter();

        Vector2Int start = ToCell(from), goal = ToCell(to);
        // 起终点格不可走时贴最近可走格（贴墙站立常见）
        if (!IsWalkable(start)) start = NearestWalkable(start, goal);
        if (!IsWalkable(goal)) goal = NearestWalkable(goal, start);
        if (start == goal || start.x < int.MinValue + 1 || goal.x < int.MinValue + 1)
        {
            // v1.1.35 契约修复：成功必须 ≥2 航点（调用方 pathIndex 从 1 起步）——补上起点，
            // 杜绝 count==1 时下游索引语义歧义（ArgumentOutOfRangeException 类）
            result.Add(from);
            result.Add(to);
            return true;
        }

        // 局部网格界限
        int x0 = Mathf.Min(start.x, goal.x) - Padding, x1 = Mathf.Max(start.x, goal.x) + Padding;
        int y0 = Mathf.Min(start.y, goal.y) - Padding, y1 = Mathf.Max(start.y, goal.y) + Padding;
        if (x1 - x0 > MaxGridDim) { int c = (x0 + x1) / 2; x0 = c - MaxGridDim / 2; x1 = c + MaxGridDim / 2; }
        if (y1 - y0 > MaxGridDim) { int c = (y0 + y1) / 2; y0 = c - MaxGridDim / 2; y1 = c + MaxGridDim / 2; }

        walkable.Clear(); gScore.Clear(); cameFrom.Clear(); closed.Clear(); open.Clear();
        gScore[start] = 0;
        open.Add(start);
        int safety = 900;

        while (open.Count > 0 && safety-- > 0)
        {
            // 取 f 最小（线性扫表；网格小、频次低，免堆实现）
            int best = 0, bestF = int.MaxValue;
            for (int i = 0; i < open.Count; i++)
            {
                int f = gScore[open[i]] + Heuristic(open[i], goal);
                if (f < bestF) { bestF = f; best = i; }
            }
            Vector2Int cur = open[best];
            open.RemoveAt(best);
            if (cur == goal) return Reconstruct(cur, to, self, result);
            closed.Add(cur);

            for (int d = 0; d < 8; d++)
            {
                var next = cur + Dirs8[d];
                if (next.x < x0 || next.x > x1 || next.y < y0 || next.y > y1) continue;
                if (closed.Contains(next)) continue;
                if (!IsWalkable(next)) continue;
                // 对角穿缝禁止：两正交邻格必须都可走
                if (d >= 4
                    && (!IsWalkable(new Vector2Int(cur.x + Dirs8[d].x, cur.y)) || !IsWalkable(new Vector2Int(cur.x, cur.y + Dirs8[d].y))))
                    continue;

                int cost = d >= 4 ? DiagCost : StraightCost;
                int ng = gScore[cur] + cost;
                if (!gScore.TryGetValue(next, out int g) || ng < g)
                {
                    gScore[next] = ng;
                    cameFrom[next] = cur;
                    if (!open.Contains(next)) open.Add(next);
                }
            }
        }
        return false;
    }

    private static bool Reconstruct(Vector2Int cur, Vector2 to, Transform self, List<Vector2> result)
    {
        rawPath.Clear();
        rawPath.Add(to);
        while (cameFrom.TryGetValue(cur, out var prev)) { rawPath.Add(CellCenter(cur)); cur = prev; }
        // 拉绳简化：从尾（起点端）向后贪心跳段
        int i = rawPath.Count - 1;
        result.Add(rawPath[i]);
        while (i > 0)
        {
            int j = i - 1;
            while (j > 0 && BodyLineClear(rawPath[i], rawPath[j - 1], self)) j--;
            result.Add(rawPath[j]);
            i = j;
        }
        // result 现为 起点→终点 倒序……反转
        result.Reverse();
        return result.Count >= 2;
    }

    private static int Heuristic(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
        return (dx > dy ? dx - dy : dy - dx) * StraightCost + Mathf.Min(dx, dy) * DiagCost;
    }

    private static Vector2Int ToCell(Vector2 p) => new Vector2Int(Mathf.RoundToInt(p.x - 0.5f), Mathf.RoundToInt(p.y - 0.5f));
    private static Vector2 CellCenter(Vector2Int c) => new Vector2(c.x + 0.5f, c.y + 0.5f);

    private static bool IsWalkable(Vector2Int cell)
    {
        if (walkable.TryGetValue(cell, out bool w)) return w;
        Vector2 center = CellCenter(cell);
        int n = Physics2D.OverlapBox(center, new Vector2(CellWalkBox, CellWalkBox), 0f,
            blockerFilter, overlapBuffer);
        bool ok = true;
        for (int i = 0; i < n; i++)
        {
            var c = overlapBuffer[i];
            if (c == null || c.isTrigger) continue;
            if (IsStaticBlocker(c)) { ok = false; break; }
        }
        walkable[cell] = ok;
        return ok;
    }

    private static bool IsStaticBlocker(Collider2D c)
        => c is TilemapCollider2D || c.gameObject.layer == LayerMask.NameToLayer("Obstacle");

    /// <summary>起点/终点格被挡时环形扩搜最近可走格（失败返回 x=int.MinValue 哨兵）。</summary>
    private static Vector2Int NearestWalkable(Vector2Int from, Vector2Int toward)
    {
        // 优先朝目标方向试一步（贴墙追击最常见姿态）
        Vector2Int bias = new Vector2Int(Mathf.RoundToInt(Mathf.Sign(toward.x - from.x)), Mathf.RoundToInt(Mathf.Sign(toward.y - from.y)));
        var preferred = new Vector2Int(from.x + bias.x, from.y + bias.y);
        if (IsWalkable(preferred)) return preferred;

        for (int r = 1; r <= 3; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                    var c = new Vector2Int(from.x + dx, from.y + dy);
                    if (IsWalkable(c)) return c;
                }
        return new Vector2Int(int.MinValue, int.MinValue);
    }
}
