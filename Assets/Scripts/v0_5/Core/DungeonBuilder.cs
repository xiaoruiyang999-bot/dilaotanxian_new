using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 地牢实例化器：读 DungeonLayout → 画 Floor/Walls Tilemap、开门洞、建 Room + Door + RoomTrigger。
/// 与 DungeonGenerator 数据/表现分离：本类只负责「表现」，不含任何生成逻辑。
/// 世界坐标约定（计划书 4.4）：瓦片 1×1 单位，Grid 位于原点，瓦片 (x,y) 占据世界 [x,x+1]×[y,y+1]。
/// </summary>
public class DungeonBuilder : MonoBehaviour
{
    [Header("Tilemap 引用")]
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Tilemap wallsTilemap;

    [Header("瓦片资产")]
    [SerializeField] private TileBase floorTile;
    [SerializeField] private TileBase wallTile;

    [Header("挂载点（所有生成物挂在其下，v0.5.4 Cleanup 销毁根）")]
    [SerializeField] private Transform dungeonRoot;

    [Header("v0.5.1 房间流程")]
    [SerializeField] private Door doorPrefab;

    private readonly Dictionary<int, Room> rooms = new Dictionary<int, Room>();
    /// <summary>当前楼层的房间（Room.id → Room），供 Gizmos / 后续系统查询。</summary>
    public IReadOnlyDictionary<int, Room> Rooms => rooms;

    private int roomW, roomH, doorW;   // 配置缓存（瓦片）

    /// <summary>按布局重建整层地牢，返回起始房中心（世界坐标）。</summary>
    public Vector3 Build(DungeonLayout layout, DungeonConfig config)
    {
        roomW = config.roomWidth;
        roomH = config.roomHeight;
        doorW = config.doorWidth;

        ClearAll();
        foreach (RoomNode node in layout.rooms) PaintRoom(node);

        // 先开门洞（记录门洞矩形），后建 Room，最后建 Door（Door 需要两侧 Room 已存在）
        var doorRects = new Dictionary<RoomConnection, Rect>();
        foreach (RoomConnection conn in layout.connections)
        {
            Rect? r = CarveDoor(conn);
            if (r.HasValue) doorRects[conn] = r.Value;
        }
        foreach (RoomNode node in layout.rooms) CreateRoomObject(node);
        foreach (KeyValuePair<RoomConnection, Rect> kv in doorRects) CreateDoor(kv.Key, kv.Value);

        return GetRoomCenterWorld(layout.startRoom);
    }

    /// <summary>清空 Tilemap 与全部生成物（重建 / 楼层切换共用）。</summary>
    public void ClearAll()
    {
        floorTilemap.ClearAllTiles();
        wallsTilemap.ClearAllTiles();
        rooms.Clear();
        for (int i = dungeonRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = dungeonRoot.GetChild(i);
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
    }

    // ---------- 坐标换算（格子 = 内部 + 四周各 1 格墙） ----------

    private int CellWidth => roomW + 2;
    private int CellHeight => roomH + 2;

    /// <summary>房间格子原点的瓦片坐标（含墙外框左下角）。</summary>
    private Vector2Int CellOrigin(RoomNode node)
        => new Vector2Int(node.gridPos.x * CellWidth, node.gridPos.y * CellHeight);

    /// <summary>房间内部左下角瓦片坐标。</summary>
    private Vector2Int InteriorOrigin(RoomNode node)
    {
        Vector2Int o = CellOrigin(node);
        return new Vector2Int(o.x + 1, o.y + 1);
    }

    // ---------- 绘制 ----------

    /// <summary>画一个房间：内部地板 + 四周一圈墙（格子间距含墙，相邻房间的墙线互不重叠）。</summary>
    private void PaintRoom(RoomNode node)
    {
        Vector2Int o = CellOrigin(node);
        for (int x = 0; x < CellWidth; x++)
        {
            for (int y = 0; y < CellHeight; y++)
            {
                bool border = x == 0 || y == 0 || x == CellWidth - 1 || y == CellHeight - 1;
                var pos = new Vector3Int(o.x + x, o.y + y, 0);
                if (border) wallsTilemap.SetTile(pos, wallTile);
                else floorTilemap.SetTile(pos, floorTile);
            }
        }
    }

    /// <summary>在相邻房间的共用墙中段开门洞：两侧墙线一起打穿 + 铺地板。返回门洞世界矩形（供 Door 定位）。</summary>
    private Rect? CarveDoor(RoomConnection conn)
    {
        Vector2Int delta = conn.b.gridPos - conn.a.gridPos;
        if (delta.x != 0) // 东西向连接：打穿西房间的东墙线 + 东房间的西墙线
        {
            RoomNode west = delta.x > 0 ? conn.a : conn.b;
            Vector2Int westInner = InteriorOrigin(west);
            int wallX1 = westInner.x + roomW;
            int startY = westInner.y + (roomH - doorW) / 2;
            for (int dy = 0; dy < doorW; dy++)
            {
                OpenDoorTile(wallX1, startY + dy);
                OpenDoorTile(wallX1 + 1, startY + dy);
            }
            return new Rect(wallX1, startY, 2f, doorW);
        }
        if (delta.y != 0) // 南北向连接：打穿南房间的北墙线 + 北房间的南墙线
        {
            RoomNode south = delta.y > 0 ? conn.a : conn.b;
            Vector2Int southInner = InteriorOrigin(south);
            int wallY1 = southInner.y + roomH;
            int startX = southInner.x + (roomW - doorW) / 2;
            for (int dx = 0; dx < doorW; dx++)
            {
                OpenDoorTile(startX + dx, wallY1);
                OpenDoorTile(startX + dx, wallY1 + 1);
            }
            return new Rect(startX, wallY1, doorW, 2f);
        }
        // delta 为 (0,0) 或非相邻属生成器数据错误（Validate 自检会拦截），此处静默忽略
        return null;
    }

    private void OpenDoorTile(int x, int y)
    {
        var pos = new Vector3Int(x, y, 0);
        wallsTilemap.SetTile(pos, null);
        floorTilemap.SetTile(pos, floorTile);
    }

    // ---------- Room / Door 实例化 ----------

    /// <summary>清房条件映射（v0.5.3 起改由 RoomTypeConfig 数据驱动）。</summary>
    private static RoomClearCondition ConditionFor(RoomType type)
    {
        return type == RoomType.Combat || type == RoomType.Boss
            ? RoomClearCondition.AllEnemiesDead
            : RoomClearCondition.None;
    }

    private void CreateRoomObject(RoomNode node)
    {
        Vector2Int inner = InteriorOrigin(node);
        var bounds = new Rect(inner.x, inner.y, roomW, roomH);
        RoomClearCondition condition = ConditionFor(node.type);

        var go = new GameObject($"Room_{node.id}_{node.type}");
        go.transform.SetParent(dungeonRoot, false);
        go.transform.position = bounds.center;

        // 内容挂载点（敌人/障碍物/装饰）：始终 active。
        // 休眠由 Room 按敌人逐个禁用 AI/Combat 实现（敌人可见但不动），不隐藏整个挂载点。
        var contentGo = new GameObject("ContentRoot");
        contentGo.transform.SetParent(go.transform, false);
        contentGo.transform.localPosition = Vector3.zero;

        Room room = go.AddComponent<Room>();
        room.Init(node.id, node.type, bounds, condition, contentGo.transform);
        rooms[node.id] = room;

        // 进入触发器：四边内缩 0.5 格，保证玩家完全进房后才触发（防关门夹人）
        var triggerGo = new GameObject("RoomTrigger");
        triggerGo.transform.SetParent(go.transform, false);
        triggerGo.transform.localPosition = Vector3.zero;
        var triggerCol = triggerGo.AddComponent<BoxCollider2D>();
        triggerCol.isTrigger = true;
        triggerCol.size = new Vector2(roomW - 1f, roomH - 1f);
        triggerGo.AddComponent<RoomTrigger>().Init(room);
    }

    private void CreateDoor(RoomConnection conn, Rect doorRect)
    {
        if (doorPrefab == null) return;
        if (!rooms.TryGetValue(conn.a.id, out Room ra) || !rooms.TryGetValue(conn.b.id, out Room rb)) return;

        Door door = Instantiate(doorPrefab, doorRect.center, Quaternion.identity, dungeonRoot);
        door.name = $"Door_{conn.a.id}_{conn.b.id}";
        door.Init(ra, rb, doorRect.size);
        ra.RegisterDoor(door);
        rb.RegisterDoor(door);
    }

    /// <summary>房间内部中心（世界坐标）。</summary>
    public Vector3 GetRoomCenterWorld(RoomNode node)
    {
        Vector2Int inner = InteriorOrigin(node);
        return new Vector3(inner.x + roomW * 0.5f, inner.y + roomH * 0.5f, 0f);
    }
}
