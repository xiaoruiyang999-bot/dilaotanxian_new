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
        // CreateInstance 走字段默认值（8~12 房 + 宝箱/商店特殊房），无需加载资产
        return ScriptableObject.CreateInstance<DungeonConfig>();
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
}
