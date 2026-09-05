using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 房内布局回归门禁（v1.1.46）：既验证“能走”，也验证“像战斗房”。
/// 覆盖默认 30×18、横向 Elite 61×18、纵向 Elite 30×37，以及旧小房 18×11。
/// </summary>
public class RoomLayoutTests
{
    private static RoomPlan Create(int width, int height, List<Vector2Int> doors, int seed)
        => RoomPlanner.CreatePlan(new RectInt(0, 0, width, height), doors, new System.Random(seed));

    private static List<Vector2Int> MakeDoors(int width, int height, System.Random rng)
    {
        int doorCount = 1 + rng.Next(4);
        var doors = new List<Vector2Int>(doorCount);
        for (int i = 0; i < doorCount; i++)
        {
            int side = rng.Next(4);
            if (side == 0) doors.Add(new Vector2Int(width / 2, -1));
            else if (side == 1) doors.Add(new Vector2Int(width / 2, height));
            else if (side == 2) doors.Add(new Vector2Int(-1, height / 2));
            else doors.Add(new Vector2Int(width, height / 2));
        }
        return doors;
    }

    private static List<Vector2Int> FourDoors(int width, int height) => new List<Vector2Int>
    {
        new Vector2Int(width / 2, -1),
        new Vector2Int(width / 2, height),
        new Vector2Int(-1, height / 2),
        new Vector2Int(width, height / 2),
    };

    [Test]
    public void PlayerGauge_AllFreeCellsAndDoorsReachable_AcrossSeedsAndActualSizes()
    {
        var rngMeta = new System.Random(20260905);
        var sizes = new[] { (18, 11), (30, 18), (61, 18), (30, 37) };
        int rooms = 0;

        foreach (var size in sizes)
            for (int i = 0; i < 160; i++)
            {
                int seed = rngMeta.Next();
                List<Vector2Int> doors = MakeDoors(size.Item1, size.Item2, rngMeta);
                RoomPlan plan = Create(size.Item1, size.Item2, doors, seed);
                Vector2Int center = new Vector2Int(size.Item1 / 2, size.Item2 / 2);
                HashSet<Vector2Int> reach = RoomLayoutValidator.Reachable2Wide(plan, center);

                foreach (var door in doors)
                {
                    var anchor = new Vector2Int(
                        Mathf.Clamp(door.x, 0, size.Item1 - 1),
                        Mathf.Clamp(door.y, 0, size.Item2 - 1));
                    if (!reach.Contains(anchor))
                        Assert.Fail($"seed={seed} 尺寸{size} 门{door} 玩家口径不可达");
                }

                foreach (var cell in plan.Walkable)
                {
                    if (plan.Obstacles.Contains(cell)) continue;
                    if (!reach.Contains(cell))
                        Assert.Fail($"seed={seed} 尺寸{size} 格{cell} 被围成玩家不可进入区域");
                }
                rooms++;
            }

        Assert.That(rooms, Is.EqualTo(640));
    }

    [Test]
    public void CreatePlan_SameSeed_ProducesIdenticalPlanIncludingSkeleton()
    {
        var rngMeta = new System.Random(7);
        for (int i = 0; i < 60; i++)
        {
            int seed = rngMeta.Next();
            List<Vector2Int> doors = MakeDoors(30, 18, rngMeta);
            RoomPlan a = Create(30, 18, doors, seed);
            RoomPlan b = Create(30, 18, doors, seed);

            Assert.That(a.Obstacles.SetEquals(b.Obstacles), $"seed={seed} 障碍应一致");
            Assert.That(a.Outline.SetEquals(b.Outline), $"seed={seed} 轮廓应一致");
            Assert.That(a.Walkable.SetEquals(b.Walkable), $"seed={seed} 可行走区应一致");
            Assert.That(a.Skeleton.SetEquals(b.Skeleton), $"seed={seed} 骨架应一致");
            CollectionAssert.AreEqual(a.SpawnCells, b.SpawnCells, $"seed={seed} 内容白名单应一致");
        }
    }

    [Test]
    public void CoverIslands_AreSmallSeparatedDistributedAndStillRandom()
    {
        var sizes = new[] { (30, 18), (61, 18), (30, 37) };
        const int samples = 180;

        foreach (var size in sizes)
        {
            int nonEmpty = 0;
            int totalObstacleCells = 0;
            var signatures = new HashSet<string>();
            List<Vector2Int> doors = FourDoors(size.Item1, size.Item2);

            for (int seed = 0; seed < samples; seed++)
            {
                RoomPlan plan = Create(size.Item1, size.Item2, doors,
                    unchecked(seed * 104729 + size.Item1 * 1009 + size.Item2));
                AssertQualityRules(plan, doors, seed);
                if (plan.Obstacles.Count > 0) nonEmpty++;
                totalObstacleCells += plan.Obstacles.Count;
                signatures.Add(BuildObstacleSignature(plan));
            }

            float nonEmptyRatio = nonEmpty / (float)samples;
            float averageCells = totalObstacleCells / (float)samples;
            Debug.Log($"[RoomLayout v1.1.46] {size}: 非空 {nonEmptyRatio:P0}, " +
                      $"障碍均值 {averageCells:F1}, 唯一布局 {signatures.Count}/{samples}");

            Assert.That(nonEmptyRatio, Is.GreaterThanOrEqualTo(0.70f),
                $"尺寸{size} 空房过多，随机性退化");
            Assert.That(averageCells, Is.GreaterThanOrEqualTo(size.Item1 * size.Item2 > 800 ? 12f : 7f),
                $"尺寸{size} 掩体密度过低");
            Assert.That(signatures.Count, Is.GreaterThanOrEqualTo(120),
                $"尺寸{size} 布局重复过多，随机性不足");
        }
    }

    private static void AssertQualityRules(RoomPlan plan, List<Vector2Int> doors, int seed)
    {
        RectInt area = plan.Interior;
        Vector2Int center = new Vector2Int(area.xMin + area.width / 2, area.yMin + area.height / 2);
        int outer = RoomObstaclePlanner.OuterWallClearance(area);
        int carved = area.width * area.height - plan.Walkable.Count;
        int carveLimit = Mathf.FloorToInt(area.width * area.height * 0.12f);

        Assert.That(carved, Is.LessThanOrEqualTo(carveLimit), $"seed={seed} 房形挖除过量");
        Assert.That(plan.Outline.Count, Is.LessThanOrEqualTo(16), $"seed={seed} 轮廓墙过长");
        Assert.That(plan.Obstacles.Count,
            Is.LessThanOrEqualTo(RoomObstaclePlanner.TargetObstacleCells(plan.Walkable.Count)
                                 + RoomObstaclePlanner.MaxIslandCells - 1),
            $"seed={seed} 障碍密度失控");

        foreach (var cell in plan.Obstacles)
        {
            Assert.That(cell.x, Is.GreaterThanOrEqualTo(area.xMin + outer), $"seed={seed} 障碍贴西墙 {cell}");
            Assert.That(cell.x, Is.LessThan(area.xMax - outer), $"seed={seed} 障碍贴东墙 {cell}");
            Assert.That(cell.y, Is.GreaterThanOrEqualTo(area.yMin + outer), $"seed={seed} 障碍贴南墙 {cell}");
            Assert.That(cell.y, Is.LessThan(area.yMax - outer), $"seed={seed} 障碍贴北墙 {cell}");
            Assert.That(Mathf.Abs(cell.x - center.x) > 3 || Mathf.Abs(cell.y - center.y) > 2,
                $"seed={seed} 障碍侵入中心战斗区 {cell}");

            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    if (plan.Outline.Contains(new Vector2Int(cell.x + dx, cell.y + dy)))
                        Assert.Fail($"seed={seed} 障碍 {cell} 粘连轮廓墙");
        }

        List<HashSet<Vector2Int>> components = FindObstacleComponents(plan.Obstacles);
        for (int i = 0; i < components.Count; i++)
        {
            Assert.That(components[i].Count, Is.InRange(2, RoomObstaclePlanner.MaxIslandCells),
                $"seed={seed} 第{i}个掩体岛尺寸异常");
            for (int j = i + 1; j < components.Count; j++)
                foreach (var a in components[i])
                    foreach (var b in components[j])
                        if (Chebyshev(a, b) < RoomObstaclePlanner.MinimumIslandCellDistance)
                            Assert.Fail($"seed={seed} 掩体岛 {i}/{j} 过度粘连");
        }

        var anchors = new List<Vector2Int>(doors.Count);
        foreach (var door in doors)
            anchors.Add(new Vector2Int(
                Mathf.Clamp(door.x, area.xMin, area.xMax - 1),
                Mathf.Clamp(door.y, area.yMin, area.yMax - 1)));
        Assert.That(RoomLayoutValidator.Validate(plan, anchors, center), Is.True, $"seed={seed} 基础验证失败");
        Assert.That(RoomLayoutValidator.ValidatePlayerGauge(plan, anchors, center), Is.True,
            $"seed={seed} 玩家口径验证失败");

        Assert.That(plan.SpawnCells, Is.Not.Empty, $"seed={seed} 没有内容安全落点");
        foreach (var spawnCell in plan.SpawnCells)
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var halo = new Vector2Int(spawnCell.x + dx, spawnCell.y + dy);
                    // 热循环只在违规时构造 NUnit 失败对象；逐格 Assert 会制造数百万次
                    // 断言开销，却不会提高覆盖率。
                    if (!plan.Walkable.Contains(halo))
                        Assert.Fail($"seed={seed} 内容格 {spawnCell} 的邻域 {halo} 落入空洞/外墙");
                    if (plan.IsWall(halo))
                        Assert.Fail($"seed={seed} 内容格 {spawnCell} 的邻域 {halo} 碰到房内墙");
                }
    }

    private static List<HashSet<Vector2Int>> FindObstacleComponents(HashSet<Vector2Int> obstacles)
    {
        var remaining = new HashSet<Vector2Int>(obstacles);
        var components = new List<HashSet<Vector2Int>>();
        while (remaining.Count > 0)
        {
            Vector2Int seed = default;
            foreach (var cell in remaining) { seed = cell; break; }
            var component = new HashSet<Vector2Int> { seed };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(seed);
            remaining.Remove(seed);
            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                Visit(cell + Vector2Int.right);
                Visit(cell + Vector2Int.left);
                Visit(cell + Vector2Int.up);
                Visit(cell + Vector2Int.down);
            }
            components.Add(component);

            void Visit(Vector2Int next)
            {
                if (!remaining.Remove(next)) return;
                component.Add(next);
                queue.Enqueue(next);
            }
        }
        return components;
    }

    private static string BuildObstacleSignature(RoomPlan plan)
    {
        var signature = new StringBuilder(plan.Interior.width * plan.Interior.height);
        for (int y = plan.Interior.yMin; y < plan.Interior.yMax; y++)
            for (int x = plan.Interior.xMin; x < plan.Interior.xMax; x++)
                signature.Append(plan.Obstacles.Contains(new Vector2Int(x, y)) ? '#' : '.');
        return signature.ToString();
    }

    private static int Chebyshev(Vector2Int a, Vector2Int b)
        => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
}
