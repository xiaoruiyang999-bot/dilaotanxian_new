using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 房间类型。v0.5.0 只用 Start/Combat/Boss，其余由 v0.5.3 的 RoomTypeAssigner 填全。
/// </summary>
public enum RoomType { Start, Combat, Elite, Treasure, Shop, Event, Boss }

/// <summary>
/// 布局房间节点（纯数据，不依赖场景）。
/// </summary>
public class RoomNode
{
    public int id;                      // 生成器分配的自增 ID，同 seed 重生成保持稳定（存档/回放引房依据）
    public Vector2Int gridPos;          // 粗网格坐标
    public RoomType type;               // v0.5.0 填 Start/Combat/Boss，v0.5.3 由 Assigner 填全
    public int distanceFromStart;       // BFS 距离（Boss/特殊房选址依据）
    public List<RoomConnection> connections = new List<RoomConnection>();
    public bool IsLeaf => connections.Count == 1;   // 叶子房（宝箱/商店选址依据）
}

/// <summary>
/// 房间连接（无向边）。未来扩展钩子：corridor 数据、门类型（普通/锁/秘密）都加在这里。
/// </summary>
public class RoomConnection
{
    public RoomNode a, b;

    public RoomConnection(RoomNode a, RoomNode b) { this.a = a; this.b = b; }

    public RoomNode Other(RoomNode self) => self == a ? b : a;
}

/// <summary>
/// 地牢布局（纯 C# 数据，不依赖场景/MonoBehaviour）。
/// DungeonGenerator 产出，DungeonBuilder 消费——数据与表现分离的唯一数据流。
/// </summary>
public class DungeonLayout
{
    public int seed;
    public List<RoomNode> rooms = new List<RoomNode>();
    public List<RoomConnection> connections = new List<RoomConnection>();
    public RoomNode startRoom, bossRoom;
}
