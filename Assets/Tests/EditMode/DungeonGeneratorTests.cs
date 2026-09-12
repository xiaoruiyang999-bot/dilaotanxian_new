using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 地牢固定 seed 生成与连通性（v1.0.4，审查报告 §六最小测试集第 2 项）：
/// 同 seed 重生成结果一致（楼层种子链路可复现）；全部房间从起始房 BFS 可达（不存在孤岛房）。
/// </summary>
public class DungeonGeneratorTests
{
    private static DungeonConfig MakeConfig()
    {
        // 本测试类的既有门禁描述 v1.x 四向迷宫体系——显式锁 LegacyGrid
        //（v2.0.4 起 config 默认 LinearHorizontal 横向链，旧语义测试不受默认值漂移影响）
        var config = ScriptableObject.CreateInstance<DungeonConfig>();
        config.topology = DungeonConfig.DungeonTopology.LegacyGrid;
        // 门禁锚定典型迷宫规模 8~12（v2.0.5 全局默认翻倍到 16~24 后网格更挤、
        // 扩容达成率自然下降——扩展机制的达标线不应随全局默认漂移）
        config.roomCountMin = 8;
        config.roomCountMax = 12;
        return config;
    }

    // ========== v2.0.4 LinearHorizontal 横向单向拓扑门禁（V2 文档 §4/§10） ==========

    private static DungeonConfig MakeLinearConfig() => ScriptableObject.CreateInstance<DungeonConfig>();

    [Test]
    public void LinearTopology_HorizontalChain_LeftInRightOut_BossRightmost()
    {
        var rngMeta = new System.Random(20260909);
        for (int i = 0; i < 60; i++)
        {
            int seed = rngMeta.Next();
            DungeonLayout layout = DungeonGenerator.Generate(MakeLinearConfig(), seed);

            Assert.NotNull(layout.startRoom);
            Assert.NotNull(layout.bossRoom);
            Assert.That(layout.rooms.Count, Is.InRange(MakeLinearConfig().roomCountMin, 100));

            // 全连接严格水平（左入右出：每条边两端行相同、后继列 = 前驱列 + 1）
            foreach (RoomConnection conn in layout.connections)
            {
                RoomNode a = conn.a, b = conn.b;
                RoomNode left = a.gridPos.x < b.gridPos.x ? a : b;
                RoomNode right = a.gridPos.x < b.gridPos.x ? b : a;
                Assert.AreEqual(left.gridPos.y, right.gridPos.y, $"seed={seed} 出现非水平连接（垂直门违反左入右出）");
                Assert.AreEqual(left.gridPos.x + 1, right.gridPos.x, $"seed={seed} 非相邻列连接");
            }

            // 房间数 = 连接数 + 1（链式无分叉）；Start 最左、Boss 最右
            Assert.AreEqual(layout.rooms.Count - 1, layout.connections.Count, $"seed={seed} 不是纯链结构");
            int maxX = int.MinValue; RoomNode rightmost = null;
            foreach (RoomNode r in layout.rooms)
                if (r.gridPos.x > maxX) { maxX = r.gridPos.x; rightmost = r; }
            Assert.AreSame(rightmost, layout.bossRoom, $"seed={seed} Boss 不在最右");
            Assert.AreEqual(RoomType.Boss, layout.bossRoom.type);
            Assert.AreEqual(RoomType.Start, layout.startRoom.type);
            Assert.AreEqual(0, layout.startRoom.gridPos.x, $"seed={seed} Start 不在最左");
        }
    }

    [Test]
    public void LinearTopology_SameSeed_Deterministic()
    {
        DungeonConfig config = MakeLinearConfig();
        DungeonLayout a = DungeonGenerator.Generate(config, 777);
        DungeonLayout b = DungeonGenerator.Generate(config, 777);
        Assert.AreEqual(a.rooms.Count, b.rooms.Count);
        for (int i = 0; i < a.rooms.Count; i++)
        {
            Assert.AreEqual(a.rooms[i].gridPos, b.rooms[i].gridPos);
            Assert.AreEqual(a.rooms[i].type, b.rooms[i].type);
        }
    }

    [Test]
    public void LinearTopology_AllRoomsReachable_LeftmostStart()
    {
        DungeonLayout layout = DungeonGenerator.Generate(MakeLinearConfig(), 4242);
        var visited = new System.Collections.Generic.HashSet<RoomNode> { layout.startRoom };
        var queue = new System.Collections.Generic.Queue<RoomNode>();
        queue.Enqueue(layout.startRoom);
        while (queue.Count > 0)
        {
            RoomNode cur = queue.Dequeue();
            foreach (RoomConnection conn in cur.connections)
            {
                RoomNode next = conn.Other(cur);
                if (next != null && visited.Add(next)) queue.Enqueue(next);
            }
        }
        Assert.AreEqual(layout.rooms.Count, visited.Count, "水平链必须全可达");
    }

    [Test]
    public void FixedSeed_GeneratesDeterministicLayout()
    {
        DungeonConfig config = MakeConfig();
        DungeonLayout a = DungeonGenerator.Generate(config, 12345);
        DungeonLayout b = DungeonGenerator.Generate(config, 12345);

        Assert.That(a.rooms, Is.Not.Empty);
        Assert.AreEqual(a.rooms.Count, b.rooms.Count, "同 seed 房间数应一致");
        Assert.AreEqual(a.connections.Count, b.connections.Count, "同 seed 连接数应一致");
        for (int i = 0; i < a.rooms.Count; i++)
        {
            Assert.AreEqual(a.rooms[i].gridPos, b.rooms[i].gridPos, $"房间 {i} 网格坐标应一致");
            Assert.AreEqual(a.rooms[i].type, b.rooms[i].type, $"房间 {i} 类型应一致");
            Assert.AreEqual(a.rooms[i].spanX, b.rooms[i].spanX, $"房间 {i} 横向尺寸应一致");
            Assert.AreEqual(a.rooms[i].spanY, b.rooms[i].spanY, $"房间 {i} 纵向尺寸应一致");
        }
    }

    [Test]
    public void Layout_AllRoomsReachableFromStart()
    {
        foreach (int seed in new[] { 1, 42, 20260831 })
        {
            DungeonLayout layout = DungeonGenerator.Generate(MakeConfig(), seed);

            Assert.NotNull(layout.startRoom, $"seed={seed} 无起始房");
            Assert.NotNull(layout.bossRoom, $"seed={seed} 无 Boss 房");
            Assert.That(layout.rooms, Has.Count.GreaterThan(2), $"seed={seed} 房间数异常");

            var visited = new HashSet<RoomNode> { layout.startRoom };
            var queue = new Queue<RoomNode>();
            queue.Enqueue(layout.startRoom);
            while (queue.Count > 0)
            {
                RoomNode cur = queue.Dequeue();
                foreach (RoomConnection conn in cur.connections)
                {
                    RoomNode next = conn.Other(cur);
                    if (next != null && visited.Add(next))
                        queue.Enqueue(next);
                }
            }

            Assert.AreEqual(layout.rooms.Count, visited.Count,
                $"seed={seed} 存在 {layout.rooms.Count - visited.Count} 个从起始房不可达的房间");
        }
    }

    /// <summary>
    /// v1.1.46 战斗房整格扩展门禁：普通 Combat 房至少增大一倍（2×2 ≈4×面积 或 2×1/1×2 ≈2×面积），
    /// 扩展失败才保 1×1（尽力满足策略）。统计 100 seed 的达成率并保证大房占比 ≥80%
    ///（邻接生长布局有天然空闲角，低于该值说明扩展器退化）。
    /// </summary>
    [Test]
    public void CombatRooms_AtLeastDoubleSize_MajorityOfSeeds()
    {
        var rngMeta = new System.Random(20260906);
        int combatTotal = 0, expanded = 0, square = 0;

        for (int i = 0; i < 100; i++)
        {
            DungeonLayout layout = DungeonGenerator.Generate(MakeConfig(), rngMeta.Next());
            foreach (RoomNode r in layout.rooms)
            {
                if (r.type != RoomType.Combat) continue;
                combatTotal++;
                if (r.spanX >= 2 || r.spanY >= 2)
                {
                    expanded++;
                    if (r.spanX >= 2 && r.spanY >= 2) square++;
                }
            }
        }

        Assert.That(combatTotal, Is.GreaterThan(200), "100 层 Combat 房样本量异常");
        float expandRate = expanded / (float)combatTotal;
        Debug.Log($"[CombatSpan] Combat 房 {combatTotal} 个：扩成 ≥2×1 = {expanded}（{expandRate:P0}，其中 2×2 = {square}）");
        Assert.That(expandRate, Is.GreaterThanOrEqualTo(0.8f),
            $"战斗房 ≥2 倍面积达成率仅 {expandRate:P0}（目标 ≥80%）");
    }

    /// <summary>v1.1.52：固定仪式厅始终为 2×2、唯一南入口；地图 seed 只能改变房间位置。</summary>
    [Test]
    public void BossRoom_IsAlwaysLeafFixedTwoByTwoAndSouthFacing()
    {
        DungeonConfig config = MakeConfig();
        config.bossCellSpan = 4; // 旧序列化字段即使被运行时误改，也不得改变手工房合同。
        var rng = new System.Random(20260907);
        for (int i = 0; i < 200; i++)
        {
            int seed = rng.Next();
            DungeonLayout layout = DungeonGenerator.Generate(config, seed);
            Assert.That(layout.bossRoom, Is.Not.Null, $"seed={seed} 缺 Boss 房");
            Assert.That(layout.bossRoom.IsLeaf, Is.True, $"seed={seed} Boss 房不是单入口叶子");
            Assert.That(layout.bossRoom.spanX, Is.EqualTo(BossRitualRoomTemplate.CoarseSpan),
                $"seed={seed} Boss 房横向尺寸回退");
            Assert.That(layout.bossRoom.spanY, Is.EqualTo(BossRitualRoomTemplate.CoarseSpan),
                $"seed={seed} Boss 房纵向尺寸回退");
            Assert.That(layout.bossRoom.distanceFromStart,
                Is.GreaterThanOrEqualTo(Mathf.Min(config.bossMinDistance, layout.rooms.Count - 1)),
                $"seed={seed} Boss 房距离不足");

            RoomConnection entrance = layout.bossRoom.connections[0];
            Vector2Int bossOriginal = entrance.OriginalGridPos(layout.bossRoom);
            Vector2Int neighborOriginal = entrance.OriginalGridPos(entrance.Other(layout.bossRoom));
            Assert.That(neighborOriginal - bossOriginal, Is.EqualTo(Vector2Int.down),
                $"seed={seed} 固定仪式厅入口不是南入口");
            Assert.That(layout.bossRoom.gridPos, Is.EqualTo(bossOriginal),
                $"seed={seed} Boss 扩格锚点不固定");
        }
    }
}
