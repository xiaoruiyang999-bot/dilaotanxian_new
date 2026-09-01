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
}
