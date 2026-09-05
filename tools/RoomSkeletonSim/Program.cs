// ============================================================================
// RoomSkeletonSim — 房内骨架/障碍管线离线仿真(v1.1.44 历史开发工具,不参与 Unity 编译)
// 注意：v1.1.46 已把运行时障碍改为“小掩体岛多候选评分”，本文件只保留旧版对照，
// 不再代表当前生成结果；现行统计与门禁以 Assets/Tests/EditMode/RoomLayoutTests.cs 为准。
// 目的:复刻 Assets/Scripts/now_use/Dungeon/Generation/ 五职责管线(逐行保真,rng 消费
// 顺序一致),在其上叠加"玩家口径可达性"分析:
//   玩家胶囊净宽 1.32(Player.prefab 3.65×scale0.3618)→ 1 格净宽(1.0)不可过、2 格(2.0)可过。
//   口径:通行边 = 4 邻边两侧至少一侧的 2 格带皆 free(该处通道净宽 ≥2)。
// 运行:Unity 自带 dotnet run -- (可加 fix / print 参数)
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RoomSkeletonSim
{
    // ---------- mini Unity 类型(语义与 UnityEngine 一致) ----------
    public struct V2 : IEquatable<V2>
    {
        public int x, y;
        public V2(int x, int y) { this.x = x; this.y = y; }
        public bool Equals(V2 o) => x == o.x && y == o.y;
        public override bool Equals(object o) => o is V2 v && Equals(v);
        public override int GetHashCode() => (x * 397) ^ y;
        public static bool operator ==(V2 a, V2 b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(V2 a, V2 b) => !(a == b);
        public override string ToString() => $"({x},{y})";
    }

    public struct R2
    {
        public int x, y, w, h;
        public R2(int x, int y, int w, int h) { this.x = x; this.y = y; this.w = w; this.h = h; }
        public int xMin => x; public int yMin => y;
        public int xMax => x + w; public int yMax => y + h;
        public bool Contains(V2 c) => c.x >= x && c.x < x + w && c.y >= y && c.y < y + h;
    }

    public static class M
    {
        public static int Clamp(int v, int a, int b) => v < a ? a : (v > b ? b : v);
        public static int Abs(int v) => v < 0 ? -v : v;
        public static int Max(int a, int b) => a > b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static int Sign(int v) => v < 0 ? -1 : (v > 0 ? 1 : 0);
    }

    // ---------- RoomPlan(复刻) ----------
    public class Plan
    {
        public R2 Interior;
        public readonly HashSet<V2> Walkable = new HashSet<V2>();
        public readonly HashSet<V2> Outline = new HashSet<V2>();
        public readonly HashSet<V2> Obstacles = new HashSet<V2>();
        public readonly HashSet<V2> Skeleton = new HashSet<V2>();
        public static Plan Plain(R2 interior)
        {
            var p = new Plan { Interior = interior };
            for (int y = interior.yMin; y < interior.yMax; y++)
                for (int x = interior.xMin; x < interior.xMax; x++)
                    p.Walkable.Add(new V2(x, y));
            return p;
        }
    }

    // ---------- RoomShapeGenerator(复刻) ----------
    public static class ShapeGen
    {
        private const float MaxCarveRatio = 0.30f;
        public static HashSet<V2> Generate(R2 interior, Random rng, HashSet<V2> protect)
        {
            var carved = new HashSet<V2>();
            double roll = rng.NextDouble();
            if (roll < 0.30) Notch(interior, rng, protect, carved);
            else if (roll < 0.50)
            {
                Notch(interior, rng, protect, carved);
                if (carved.Count < interior.w * interior.h * MaxCarveRatio * 0.6)
                    Notch(interior, rng, protect, carved);
            }
            else if (roll < 0.62) Chamfer(interior, rng, protect, carved);

            int limit = (int)(interior.w * interior.h * MaxCarveRatio);
            if (carved.Count > limit)
            {
                var keep = new HashSet<V2>();
                int n = 0;
                for (int y = interior.yMin; y < interior.yMax && n < limit; y++)
                    for (int x = interior.xMin; x < interior.xMax && n < limit; x++)
                    {
                        var c = new V2(x, y);
                        if (carved.Contains(c)) { keep.Add(c); n++; }
                    }
                carved = keep;
            }
            return carved;
        }

        private static void Notch(R2 interior, Random rng, HashSet<V2> protect, HashSet<V2> carved)
        {
            int w = rng.Next(3, interior.w / 2 + 1);
            int h = rng.Next(2, interior.h / 2 + 1);
            if (w * h > interior.w * interior.h / 4) w = M.Max(3, w / 2);

            int corner = rng.Next(4);
            int x0 = corner % 2 == 0 ? interior.xMin : interior.xMax - w;
            int y0 = corner < 2 ? interior.yMin : interior.yMax - h;
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                {
                    var c = new V2(x, y);
                    if (protect.Contains(c)) continue;
                    carved.Add(c);
                }
        }

        private static void Chamfer(R2 interior, Random rng, HashSet<V2> protect, HashSet<V2> carved)
        {
            int k = rng.Next(2, 4);
            for (int dy = 0; dy < k; dy++)
                for (int dx = 0; dx < k; dx++)
                {
                    if (dx + dy >= k) continue;
                    TryAdd(carved, protect, new V2(interior.xMin + dx, interior.yMin + dy));
                    TryAdd(carved, protect, new V2(interior.xMax - 1 - dx, interior.yMin + dy));
                    TryAdd(carved, protect, new V2(interior.xMin + dx, interior.yMax - 1 - dy));
                    TryAdd(carved, protect, new V2(interior.xMax - 1 - dx, interior.yMax - 1 - dy));
                }
        }

        private static void TryAdd(HashSet<V2> carved, HashSet<V2> protect, V2 c)
        {
            if (!protect.Contains(c)) carved.Add(c);
        }
    }

    // ---------- RoomBoundaryBuilder(复刻) ----------
    public static class Boundary
    {
        public static List<V2> Build(HashSet<V2> carved, HashSet<V2> walkable)
        {
            var list = new List<V2>();
            foreach (var c in carved)
            {
                if (walkable.Contains(new V2(c.x + 1, c.y))
                    || walkable.Contains(new V2(c.x - 1, c.y))
                    || walkable.Contains(new V2(c.x, c.y + 1))
                    || walkable.Contains(new V2(c.x, c.y - 1)))
                    list.Add(c);
            }
            return list;
        }
    }

    // ---------- RoomObstaclePlanner(复刻 + v2 段列表化) ----------
    public static class ObstaclePlanner
    {
        private const float StubOmitChance = 0.25f;
        private const float EmptyRoomChance = 0.20f;
        private const int OuterWallMargin = 2;
        private const int CenterProtectRadius = 2;

        private static readonly (V2 a, V2 b)[][] Templates =
        {
            new[] { (new V2(-5, 3), new V2(-3, 3)), (new V2(3, 3), new V2(5, 3)),
                    (new V2(-5, -3), new V2(-3, -3)), (new V2(3, -3), new V2(5, -3)) },
            new[] { (new V2(-6, 3), new V2(-6, 1)), (new V2(-6, -1), new V2(-6, -3)),
                    (new V2(6, 3), new V2(6, 1)), (new V2(6, -1), new V2(6, -3)) },
            new[] { (new V2(-6, 3), new V2(-4, 3)), (new V2(-6, 3), new V2(-6, 5)),
                    (new V2(4, -3), new V2(6, -3)), (new V2(6, -3), new V2(6, -5)) },
            new[] { (new V2(-4, 3), new V2(-3, 3)), (new V2(3, 3), new V2(4, 3)),
                    (new V2(-4, -3), new V2(-3, -3)), (new V2(3, -3), new V2(4, -3)) },
            new[] { (new V2(-5, 1), new V2(-5, -1)), (new V2(5, -1), new V2(5, 1)) },
            new[] { (new V2(-7, 2), new V2(-5, 2)), (new V2(5, 2), new V2(7, 2)),
                    (new V2(-7, -2), new V2(-5, -2)), (new V2(5, -2), new V2(7, -2)) },
            new[] { (new V2(-5, 4), new V2(-4, 4)), (new V2(4, 4), new V2(5, 4)),
                    (new V2(-5, -4), new V2(-4, -4)), (new V2(4, -4), new V2(5, -4)) },
        };

        /// <summary>v2:只做模板选择与变体变换,返回段端点列表(绝对坐标),落位交 RoomPlanner 增量验证。</summary>
        public static List<(V2 a, V2 b)> PlanSegments(R2 interior, Random rng, int offsetRange = 1)
        {
            var segments = new List<(V2 a, V2 b)>();
            if (rng.NextDouble() < EmptyRoomChance) return segments;

            var tpl = Templates[rng.Next(Templates.Length)];
            int rotation = rng.Next(4);
            bool mirrorH = rng.NextDouble() < 0.5;
            bool mirrorV = rng.NextDouble() < 0.5;
            var offset = new V2(rng.Next(-offsetRange, offsetRange + 1), rng.Next(-offsetRange, offsetRange + 1));
            V2 center = new V2(interior.xMin + interior.w / 2, interior.yMin + interior.h / 2);

            foreach (var stub in tpl)
            {
                if (rng.NextDouble() < StubOmitChance) continue;

                V2 a = Transform(stub.a, rotation, mirrorH, mirrorV);
                V2 b = Transform(stub.b, rotation, mirrorH, mirrorV);

                int jitter = rng.Next(-1, 2);
                if (a.x == b.x) b.y += jitter; else b.x += jitter;

                segments.Add((new V2(a.x + center.x + offset.x, a.y + center.y + offset.y),
                              new V2(b.x + center.x + offset.x, b.y + center.y + offset.y)));
            }
            return segments;
        }

        // ---- 旧版整体落位(复刻,用于 OLD 模式) ----
        public static HashSet<V2> PlanObstacles(R2 interior, HashSet<V2> walkable,
            HashSet<V2> outline, HashSet<V2> protect, Random rng)
        {
            var result = new HashSet<V2>();
            var segments = PlanSegmentsRaw(interior, rng, walkable, outline, protect);
            foreach (var s in segments) result.UnionWith(s);
            return result;
        }

        private static List<List<V2>> PlanSegmentsRaw(R2 interior, Random rng,
            HashSet<V2> walkable, HashSet<V2> outline, HashSet<V2> protect)
        {
            var result = new List<List<V2>>();
            var segs = PlanSegments(interior, rng, offsetRange: 2);   // OLD 复刻:偏移 ±2
            V2 center = new V2(interior.xMin + interior.w / 2, interior.yMin + interior.h / 2);
            foreach (var (a, b) in segs)
            {
                var fragment = new List<V2>(8);
                foreach (var c in Rasterize(a, b))
                {
                    if (!HardConstraint(interior, walkable, outline, protect, c, center, keepCenterGuard: true))
                    {
                        if (fragment.Count >= 2) result.Add(fragment);
                        fragment = new List<V2>(8);
                        continue;
                    }
                    fragment.Add(c);
                }
                if (fragment.Count >= 2) result.Add(fragment);
            }
            return result;
        }

        public static bool HardConstraint(R2 interior, HashSet<V2> walkable,
            HashSet<V2> outline, HashSet<V2> protect, V2 cell, V2 center, bool keepCenterGuard = false)
        {
            if (cell.x < interior.xMin + OuterWallMargin || cell.x >= interior.xMax - OuterWallMargin) return false;
            if (cell.y < interior.yMin + OuterWallMargin || cell.y >= interior.yMax - OuterWallMargin) return false;
            if (!walkable.Contains(cell) || outline.Contains(cell)) return false;
            if (protect.Contains(cell)) return false;
            if (keepCenterGuard)
            {
                int cdx = M.Abs(cell.x - center.x), cdy = M.Abs(cell.y - center.y);
                if (M.Max(cdx, cdy) <= CenterProtectRadius) return false;
            }
            return true;
        }

        private static V2 Transform(V2 p, int rotation, bool mirrorH, bool mirrorV)
        {
            var c = p;
            if (mirrorH) c.x = -c.x;
            if (mirrorV) c.y = -c.y;
            for (int i = 0; i < rotation; i++)
                c = new V2(-c.y, c.x);
            return c;
        }

        public static IEnumerable<V2> Rasterize(V2 a, V2 b)
        {
            int dx = M.Sign(b.x - a.x), dy = M.Sign(b.y - a.y);
            if (dx != 0 && dy != 0) dy = 0;
            var cur = a;
            yield return cur;
            while (cur != b)
            {
                cur = new V2(cur.x + dx, cur.y + dy);
                yield return cur;
            }
        }
    }

    // ---------- RoomLayoutValidator(复刻,现状) ----------
    public static class ValidatorOld
    {
        private const float MinFreeRatio = 0.65f;
        private const int MaxObstacleComponent = 6;

        public static bool Validate(Plan plan, List<V2> doorAnchors, V2 center)
        {
            if (!plan.Walkable.Contains(center) || plan.Obstacles.Contains(center)) return false;
            if (!CheckObstacles(plan)) return false;
            var reach = FloodFromCenter(plan, center);
            if (reach.Count < plan.Interior.w * plan.Interior.h * MinFreeRatio) return false;
            foreach (var anchor in doorAnchors)
                if (!reach.Contains(anchor)) return false;
            int freeTotal = 0;
            foreach (var c in plan.Walkable)
                if (!plan.Obstacles.Contains(c)) freeTotal++;
            return reach.Count == freeTotal;
        }

        private static bool CheckObstacles(Plan plan)
        {
            var visited = new HashSet<V2>();
            foreach (var seed in plan.Obstacles)
            {
                if (visited.Contains(seed)) continue;
                int size = 0;
                var stack = new Stack<V2>();
                stack.Push(seed);
                visited.Add(seed);
                while (stack.Count > 0)
                {
                    var c = stack.Pop();
                    size++;
                    if (size > MaxObstacleComponent) return false;
                    if (plan.Outline.Contains(c)) return false;
                    if (c.x <= plan.Interior.xMin + 1 || c.x >= plan.Interior.xMax - 2) return false;
                    if (c.y <= plan.Interior.yMin + 1 || c.y >= plan.Interior.yMax - 2) return false;
                    PushNeighbor(stack, visited, plan, new V2(c.x + 1, c.y));
                    PushNeighbor(stack, visited, plan, new V2(c.x - 1, c.y));
                    PushNeighbor(stack, visited, plan, new V2(c.x, c.y + 1));
                    PushNeighbor(stack, visited, plan, new V2(c.x, c.y - 1));
                }
            }
            return true;
        }

        private static void PushNeighbor(Stack<V2> stack, HashSet<V2> visited, Plan plan, V2 c)
        {
            if (plan.Obstacles.Contains(c) && !visited.Contains(c)) { visited.Add(c); stack.Push(c); }
        }

        public static HashSet<V2> FloodFromCenter(Plan plan, V2 center)
        {
            var reach = new HashSet<V2> { center };
            var queue = new Queue<V2>();
            queue.Enqueue(center);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                TryStep(plan, reach, queue, new V2(c.x + 1, c.y));
                TryStep(plan, reach, queue, new V2(c.x - 1, c.y));
                TryStep(plan, reach, queue, new V2(c.x, c.y + 1));
                TryStep(plan, reach, queue, new V2(c.x, c.y - 1));
            }
            return reach;
        }

        private static void TryStep(Plan plan, HashSet<V2> reach, Queue<V2> queue, V2 c)
        {
            if (reach.Contains(c) || !plan.Walkable.Contains(c) || plan.Obstacles.Contains(c)) return;
            reach.Add(c);
            queue.Enqueue(c);
        }
    }

    // ---------- RoomPlanner(OLD 复刻 + v2 细骨架/增量落位) ----------
    public static class Planner
    {
        private const int MaxAttempts = 10;

        public static Plan CreatePlan(R2 interior, List<V2> doorCells, Random rng, bool v2)
        {
            if (v2) return CreatePlanV2(interior, doorCells, rng);
            return CreatePlanOld(interior, doorCells, rng, fixPassages: false);
        }

        // ================= v2:细骨架路网 + 障碍增量落位(玩家口径验证) =================
        private static Plan CreatePlanV2(R2 interior, List<V2> doorCells, Random rng)
        {
            if (interior.w < 10 || interior.h < 8) return Plan.Plain(interior);

            V2 center = new V2(interior.xMin + interior.w / 2, interior.yMin + interior.h / 2);

            // 细骨架 = 路网:中心 3×3 + 门→中心宽 3 走廊(不再铺十字带/中带/内环大片禁放区)
            var anchors = new List<V2>();
            var protect = new HashSet<V2>();
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    AddIfInside(protect, interior, new V2(center.x + dx, center.y + dy));
            foreach (var door in doorCells)
            {
                var anchor = ClampIn(interior, door);
                anchors.Add(anchor);
                foreach (var st in LineSteps(anchor, center))
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            AddIfInside(protect, interior, new V2(st.x + dx, st.y + dy));
            }

            Plan fallback = null;
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var carved = ShapeGen.Generate(interior, rng, protect);
                var plan = Assemble(interior, carved, protect, rng);
                if (!ValidatorOld.Validate(plan, anchors, center)          // 面积比/门/格级全连通
                    || !ValidatorV2.ValidateFull(plan, anchors, center))   // 玩家口径(拦挖除残带窄区)
                {
                    if (fallback == null) fallback = plan;
                    continue;
                }

                // 障碍增量落位:每段放入后立即全量复验,失败尾缩,再失败弃段——最终态天然合法
                foreach (var (a, b) in ObstaclePlanner.PlanSegments(interior, rng))
                {
                    // 硬约束过滤 → 连续 fragment(违规格切断,与 v1.1.42 语义一致)
                    var fragments = new List<List<V2>>();
                    var frag = new List<V2>(8);
                    foreach (var c in ObstaclePlanner.Rasterize(a, b))
                    {
                        if (!ObstaclePlanner.HardConstraint(interior, plan.Walkable, plan.Outline, protect, c, center))
                        {
                            if (frag.Count >= 2) fragments.Add(frag);
                            frag = new List<V2>(8);
                            continue;
                        }
                        frag.Add(c);
                    }
                    if (frag.Count >= 2) fragments.Add(frag);

                    foreach (var cells in fragments)
                    {
                        ValidatorV2.CellsCandidate += cells.Count;
                        bool keptAny = false;
                        for (int keep = cells.Count; keep >= 2; keep--)   // 尾缩重试
                        {
                            for (int i = 0; i < keep; i++) plan.Obstacles.Add(cells[i]);
                            if (ValidatorV2.ValidateFull(plan, anchors, center))
                            {
                                keptAny = true;
                                ValidatorV2.CellsPlaced += keep;
                                ValidatorV2.CellsShrunk += cells.Count - keep;
                                break;
                            }
                            for (int i = 0; i < keep; i++) plan.Obstacles.Remove(cells[i]);
                        }
                        if (keptAny) ValidatorV2.SegKept++; else ValidatorV2.SegRejected++;
                    }
                }
                return plan;   // 增量验证保证合法
            }

            if (fallback != null)
            {
                var noObs = new Plan { Interior = interior };
                foreach (var c in fallback.Walkable) noObs.Walkable.Add(c);
                foreach (var c in fallback.Outline) noObs.Outline.Add(c);
                if (ValidatorOld.Validate(noObs, anchors, center)) return noObs;
            }
            return Plan.Plain(interior);
        }

        // ================= OLD(复刻现状) =================
        private static Plan CreatePlanOld(R2 interior, List<V2> doorCells, Random rng, bool fixPassages)
        {
            if (interior.w < 10 || interior.h < 8) return Plan.Plain(interior);

            V2 center = new V2(interior.xMin + interior.w / 2, interior.yMin + interior.h / 2);

            var anchors = new List<V2>();
            var protect = new HashSet<V2> { center };
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    AddIfInside(protect, interior, new V2(center.x + dx, center.y + dy));
            foreach (var door in doorCells)
            {
                var anchor = ClampIn(interior, door);
                anchors.Add(anchor);
                AddIfInside(protect, interior, anchor);
                var steps = LineSteps(anchor, center);
                foreach (var st in steps)
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            AddIfInside(protect, interior, new V2(st.x + dx, st.y + dy));
            }

            for (int x = interior.xMin; x < interior.xMax; x++)
                for (int dy = -1; dy <= 1; dy++)
                    AddIfInside(protect, interior, new V2(x, center.y + dy));
            for (int y = interior.yMin; y < interior.yMax; y++)
                for (int dx = -1; dx <= 1; dx++)
                    AddIfInside(protect, interior, new V2(center.x + dx, y));

            int laneY = rng.Next(2) == 0
                ? interior.yMin + interior.h / 3
                : interior.yMin + interior.h * 2 / 3;
            bool laneHFromLeft = rng.Next(2) == 0;
            int hxStart = laneHFromLeft ? interior.xMin : center.x + 1;
            int hxEnd = laneHFromLeft ? center.x : interior.xMax;
            for (int x = hxStart; x < hxEnd; x++)
                for (int dy = 0; dy <= 1; dy++)
                    AddIfInside(protect, interior, new V2(x, laneY + dy));
            int laneX = rng.Next(2) == 0
                ? interior.xMin + interior.w / 3
                : interior.xMin + interior.w * 2 / 3;
            bool laneVFromBottom = rng.Next(2) == 0;
            int vyStart = laneVFromBottom ? interior.yMin : center.y + 1;
            int vyEnd = laneVFromBottom ? center.y : interior.yMax;
            for (int y = vyStart; y < vyEnd; y++)
                for (int dx = 0; dx <= 1; dx++)
                    AddIfInside(protect, interior, new V2(laneX + dx, y));

            for (int x = interior.xMin; x < interior.xMax; x++)
                for (int dy = 0; dy < 2; dy++)
                {
                    AddIfInside(protect, interior, new V2(x, interior.yMin + dy));
                    AddIfInside(protect, interior, new V2(x, interior.yMax - 1 - dy));
                }
            for (int y = interior.yMin; y < interior.yMax; y++)
                for (int dx = 0; dx < 2; dx++)
                {
                    AddIfInside(protect, interior, new V2(interior.xMin + dx, y));
                    AddIfInside(protect, interior, new V2(interior.xMax - 1 - dx, y));
                }

            Plan fallback = null;
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var carved = ShapeGen.Generate(interior, rng, protect);
                var plan = Assemble(interior, carved, protect, rng);
                foreach (var c in ObstaclePlanner.PlanObstacles(interior, plan.Walkable, plan.Outline, protect, rng))
                    if (plan.Walkable.Contains(c)) plan.Obstacles.Add(c);
                if (!ValidatorOld.Validate(plan, anchors, center))
                {
                    if (fallback == null) fallback = plan;
                    continue;
                }
                return plan;
            }

            if (fallback != null)
            {
                var noObs = new Plan { Interior = interior };
                foreach (var c in fallback.Walkable) noObs.Walkable.Add(c);
                foreach (var c in fallback.Outline) noObs.Outline.Add(c);
                if (ValidatorOld.Validate(noObs, anchors, center)) return noObs;
            }
            return Plan.Plain(interior);
        }

        private static Plan Assemble(R2 interior, HashSet<V2> carved, HashSet<V2> protect, Random rng)
        {
            var plan = new Plan { Interior = interior };
            for (int y = interior.yMin; y < interior.yMax; y++)
                for (int x = interior.xMin; x < interior.xMax; x++)
                {
                    var c = new V2(x, y);
                    if (!carved.Contains(c)) plan.Walkable.Add(c);
                }
            foreach (var c in Boundary.Build(carved, plan.Walkable)) plan.Outline.Add(c);
            foreach (var c in protect) if (plan.Walkable.Contains(c)) plan.Skeleton.Add(c);

            foreach (var o in plan.Outline)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var n = new V2(o.x + dx, o.y + dy);
                        if (plan.Walkable.Contains(n)) protect.Add(n);
                    }
            return plan;
        }

        private static void AddIfInside(HashSet<V2> set, R2 r, V2 c)
        {
            if (r.Contains(c)) set.Add(c);
        }

        private static V2 ClampIn(R2 r, V2 c)
            => new V2(M.Clamp(c.x, r.xMin, r.xMax - 1), M.Clamp(c.y, r.yMin, r.yMax - 1));

        private static List<V2> LineSteps(V2 a, V2 b)
        {
            var list = new List<V2> { a };
            var cur = a;
            while (cur != b)
            {
                int dx = M.Sign(b.x - cur.x), dy = M.Sign(b.y - cur.y);
                cur = new V2(cur.x + (cur.x != b.x ? dx : 0), cur.y + (cur.y != b.y ? dy : 0));
                list.Add(cur);
            }
            return list;
        }
    }

    // ---------- v2 验证器:格级全连通 + 玩家口径(2 宽通行边)全格可达 ----------
    public static class ValidatorV2
    {
        // 仿真统计:段/格损失分解
        public static long SegKept, SegRejected, CellsCandidate, CellsPlaced, CellsShrunk;

        /// <summary>全量验证:①门锚点/②全 free 格 玩家口径(2 宽)可达。</summary>
        public static bool ValidateFull(Plan plan, List<V2> anchors, V2 center)
        {
            if (!PassageFixer.Free(plan, center)) return false;
            var reach = PassageFixer.Reach2Wide(plan, center);

            foreach (var a in anchors)
                if (!reach.Contains(a)) return false;

            foreach (var c in plan.Walkable)
                if (!plan.Obstacles.Contains(c) && !reach.Contains(c)) return false;
            return true;
        }
    }

    // ============================================================================
    // 玩家口径可达分析(新):净宽 ≥2 通行边 BFS + 实质空间(2×2 块)覆盖 + 拆口修复
    // ============================================================================
    public static class PassageFixer
    {
        public static bool Free(Plan p, V2 c) => p.Walkable.Contains(c) && !p.Obstacles.Contains(c);

        /// <summary>通行边判定:4 邻边 (u,v) 两侧至少一侧的 2 格带皆 free(净宽 ≥2 才能过 1.32 胶囊)。</summary>
        public static bool EdgePassable(Plan p, V2 u, V2 v)
        {
            if (!Free(p, u) || !Free(p, v)) return false;
            if (u.y == v.y)   // 水平边:上/下两侧
            {
                int x0 = M.Min(u.x, v.x);
                if (Free(p, new V2(x0, u.y + 1)) && Free(p, new V2(x0 + 1, u.y + 1))) return true;
                if (Free(p, new V2(x0, u.y - 1)) && Free(p, new V2(x0 + 1, u.y - 1))) return true;
                return false;
            }
            else              // 垂直边:左/右两侧
            {
                int y0 = M.Min(u.y, v.y);
                if (Free(p, new V2(u.x + 1, y0)) && Free(p, new V2(u.x + 1, y0 + 1))) return true;
                if (Free(p, new V2(u.x - 1, y0)) && Free(p, new V2(u.x - 1, y0 + 1))) return true;
                return false;
            }
        }

        /// <summary>玩家口径可达集(从中心沿通行边 BFS)。</summary>
        public static HashSet<V2> Reach2Wide(Plan p, V2 center)
        {
            var reach = new HashSet<V2>();
            if (!Free(p, center)) return reach;
            reach.Add(center);
            var queue = new Queue<V2>();
            queue.Enqueue(center);
            var dirs = new[] { new V2(1, 0), new V2(-1, 0), new V2(0, 1), new V2(0, -1) };
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var d in dirs)
                {
                    var n = new V2(c.x + d.x, c.y + d.y);
                    if (reach.Contains(n) || !EdgePassable(p, c, n)) continue;
                    reach.Add(n);
                    queue.Enqueue(n);
                }
            }
            return reach;
        }

        /// <summary>实质空间格集 S:所有 2×2 free 块覆盖的格(玩家能驻留转身的空间)。</summary>
        public static HashSet<V2> SevereCells(Plan p)
        {
            var s = new HashSet<V2>();
            for (int y = p.Interior.yMin; y < p.Interior.yMax - 1; y++)
                for (int x = p.Interior.xMin; x < p.Interior.xMax - 1; x++)
                {
                    if (Free(p, new V2(x, y)) && Free(p, new V2(x + 1, y))
                        && Free(p, new V2(x, y + 1)) && Free(p, new V2(x + 1, y + 1)))
                    {
                        s.Add(new V2(x, y)); s.Add(new V2(x + 1, y));
                        s.Add(new V2(x, y + 1)); s.Add(new V2(x + 1, y + 1));
                    }
                }
            return s;
        }

        /// <summary>格级 BFS 父字典(从 center,穿 free 格,不限宽)——供拆口路径回溯。</summary>
        public static Dictionary<V2, V2> CellBfsParents(Plan p, V2 center)
        {
            var parents = new Dictionary<V2, V2> { { center, center } };
            var queue = new Queue<V2>();
            queue.Enqueue(center);
            var dirs = new[] { new V2(1, 0), new V2(-1, 0), new V2(0, 1), new V2(0, -1) };
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var d in dirs)
                {
                    var n = new V2(c.x + d.x, c.y + d.y);
                    if (parents.ContainsKey(n) || !Free(p, n)) continue;
                    parents[n] = c;
                    queue.Enqueue(n);
                }
            }
            return parents;
        }

        /// <summary>统计拆掉的障碍格数(仿真观测用)。</summary>
        public static int WidenedCount;

        /// <summary>
        /// 拆口修复:任何"实质空间格(2×2 块)不在 2 宽可达集"→ 沿格级路径找第一条窄边,
        /// 拆其侧格中的障碍(拓成 2 宽口);拆不了(侧格皆墙/轮廓)返回 false(整案重试)。
        /// </summary>
        public static bool Fix(Plan p, V2 center)
        {
            for (int round = 0; round < 12; round++)
            {
                var reach = Reach2Wide(p, center);
                var severe = SevereCells(p);
                V2 bad = default; bool hasBad = false;
                foreach (var c in severe)
                    if (!reach.Contains(c))
                    {
                        if (!hasBad || Dist(c, center) < Dist(bad, center)) { bad = c; hasBad = true; }
                    }
                if (!hasBad) return true;

                var parents = CellBfsParents(p, center);
                if (!parents.ContainsKey(bad)) return false;   // 格级都不可达(⑤已拦截,防御)

                var path = new List<V2>();
                var cur = bad;
                while (cur != center) { path.Add(cur); cur = parents[cur]; }
                path.Add(center);
                path.Reverse();

                bool widened = false;
                for (int i = 0; i < path.Count - 1 && !widened; i++)
                {
                    var u = path[i]; var v = path[i + 1];
                    if (EdgePassable(p, u, v)) continue;
                    widened = TryWidenEdge(p, u, v);
                    if (widened) WidenedCount++;
                }
                if (!widened) return false;   // 窄边上无障碍可拆 → 整案失败
            }
            return false;
        }

        private static int Dist(V2 a, V2 b) => M.Abs(a.x - b.x) + M.Abs(a.y - b.y);

        /// <summary>把窄边拓宽:侧格(两侧 4 格)中的障碍格拆除一个;侧格皆非障碍返回 false。</summary>
        private static bool TryWidenEdge(Plan p, V2 u, V2 v)
        {
            var sides = new List<V2>(4);
            if (u.y == v.y)
            {
                int x0 = M.Min(u.x, v.x);
                sides.Add(new V2(x0, u.y + 1)); sides.Add(new V2(x0 + 1, u.y + 1));
                sides.Add(new V2(x0, u.y - 1)); sides.Add(new V2(x0 + 1, u.y - 1));
            }
            else
            {
                int y0 = M.Min(u.y, v.y);
                sides.Add(new V2(u.x + 1, y0)); sides.Add(new V2(u.x + 1, y0 + 1));
                sides.Add(new V2(u.x - 1, y0)); sides.Add(new V2(u.x - 1, y0 + 1));
            }
            foreach (var s in sides)
                if (p.Obstacles.Remove(s)) return true;
            return false;
        }
    }

    // ---------- 主程序:复现统计 + 修复效果对比 ----------
    internal static class Program
    {
        private static void Main(string[] args)
        {
            bool v2Mode = args.Contains("v2");
            int printBad = args.Contains("print") ? (v2Mode ? 2 : 3) : 0;
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine($"=== RoomSkeletonSim mode={(v2Mode ? "V2(细骨架+增量落位+玩家口径)" : "OLD(现状)")} ===");

            var sizes = new[] { new[] { 18, 11 }, new[] { 28, 15 }, new[] { 36, 21 } };
            var rngMeta = new Random(20260905);

            foreach (var sz in sizes)
            {
                int rooms = 1000;
                int badRooms = 0, plainRooms = 0, tightRooms = 0, carvedRooms = 0;
                long totalObstacles = 0, totalWidened = 0;
                ValidatorV2.SegKept = ValidatorV2.SegRejected = 0;
                ValidatorV2.CellsCandidate = ValidatorV2.CellsPlaced = ValidatorV2.CellsShrunk = 0;
                var examples = new List<string>();

                for (int i = 0; i < rooms; i++)
                {
                    int layoutSeed = rngMeta.Next();
                    int roomId = rngMeta.Next(1, 100);
                    var rng = new Random(layoutSeed * 31 + roomId * 911);
                    var interior = new R2(0, 0, sz[0], sz[1]);

                    int doorCount = 1 + rngMeta.Next(3);
                    var doors = new List<V2>();
                    for (int d = 0; d < doorCount; d++)
                    {
                        int side = rngMeta.Next(4);
                        if (side == 0) doors.Add(new V2(sz[0] / 2, -1));        // 南
                        else if (side == 1) doors.Add(new V2(sz[0] / 2, sz[1])); // 北
                        else if (side == 2) doors.Add(new V2(-1, sz[1] / 2));    // 西
                        else doors.Add(new V2(sz[0], sz[1] / 2));                // 东
                    }

                    PassageFixer.WidenedCount = 0;
                    var plan = Planner.CreatePlan(interior, doors, rng, v2Mode);
                    totalObstacles += plan.Obstacles.Count;
                    totalWidened += PassageFixer.WidenedCount;

                    var center = new V2(interior.w / 2, interior.h / 2);
                    var reach = PassageFixer.Reach2Wide(plan, center);
                    var severe = PassageFixer.SevereCells(plan);
                    bool bad = severe.Any(c => !reach.Contains(c));
                    // 严格口径:任何 free 格(含 1 宽缝)不在 2 宽可达集——内容生成器会往里刷敌人/宝箱
                    int tightBad = 0;
                    foreach (var c in plan.Walkable)
                        if (!plan.Obstacles.Contains(c) && !reach.Contains(c)) tightBad++;
                    if (bad)
                    {
                        badRooms++;
                        if (examples.Count < printBad) examples.Add(Render(plan, reach, severe, center));
                    }
                    else if (v2Mode && examples.Count < printBad && plan.Obstacles.Count >= 3)
                        examples.Add(Render(plan, reach, severe, center));
                    if (tightBad > 0) tightRooms++;
                    if (plan.Walkable.Count == interior.w * interior.h) plainRooms++;
                    if (plan.Outline.Count > 0) carvedRooms++;
                }

                Console.WriteLine($"尺寸 {sz[0]}x{sz[1]}: {rooms} 房 | 2×2空间不可进 = {badRooms} ({badRooms * 100.0 / rooms:F1}%) | " +
                    $"含窄区(全格口径) = {tightRooms} ({tightRooms * 100.0 / rooms:F1}%) | 障碍均值 {totalObstacles / (double)rooms:F1} | " +
                    $"有挖除房 {carvedRooms} | Plain保底房 {plainRooms} ({plainRooms * 100.0 / rooms:F0}%)");
                if (v2Mode)
                    Console.WriteLine($"    段: 留 {ValidatorV2.SegKept} / 弃 {ValidatorV2.SegRejected} | " +
                        $"候选格 {ValidatorV2.CellsCandidate} 放入 {ValidatorV2.CellsPlaced} 尾缩损失 {ValidatorV2.CellsShrunk}");
                foreach (var ex in examples)
                {
                    Console.WriteLine("--- 例图(!=不可进空间 o=2宽可达 .=free但非实质 #=障碍 ==轮廓 ▒=虚空) ---");
                    Console.WriteLine(ex);
                }
            }
        }

        private static string Render(Plan p, HashSet<V2> reach, HashSet<V2> severe, V2 center)
        {
            var sb = new StringBuilder();
            for (int y = p.Interior.yMax - 1; y >= p.Interior.yMin; y--)
            {
                for (int x = p.Interior.xMin; x < p.Interior.xMax; x++)
                {
                    var c = new V2(x, y);
                    char ch;
                    if (p.Obstacles.Contains(c)) ch = '#';
                    else if (!p.Walkable.Contains(c)) ch = p.Outline.Contains(c) ? '=' : '▒';
                    else if (severe.Contains(c) && !reach.Contains(c)) ch = '!';
                    else if (reach.Contains(c)) ch = 'o';
                    else ch = '.';
                    sb.Append(c == center ? 'C' : ch);
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
