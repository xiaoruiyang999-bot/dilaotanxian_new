using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 地牢实例化器：读 DungeonLayout → 画 Floor/Walls Tilemap、开门洞、建 Room 空壳。
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
        foreach (RoomConnection conn in layout.connections) CarveDoor(conn);
        foreach (RoomNode node in layout.rooms) CreateRoomObject(node);

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

    /// <summary>在相邻房间的共用墙中段开门洞：两侧墙线一起打穿 + 铺地板。</summary>
    private void CarveDoor(RoomConnection conn)
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
        }
        else if (delta.y != 0) // 南北向连接：打穿南房间的北墙线 + 北房间的南墙线
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
        }
        // delta 为 (0,0) 或非相邻属生成器数据错误（Validate 自检会拦截），此处静默忽略
    }

    private void OpenDoorTile(int x, int y)
    {
        var pos = new Vector3Int(x, y, 0);
        wallsTilemap.SetTile(pos, null);
        floorTilemap.SetTile(pos, floorTile);
    }

    // ---------- Room 空壳 ----------

    private void CreateRoomObject(RoomNode node)
    {
        Vector2Int inner = InteriorOrigin(node);
        var bounds = new Rect(inner.x, inner.y, roomW, roomH);

        var go = new GameObject($"Room_{node.id}_{node.type}");
        go.transform.SetParent(dungeonRoot, false);
        go.transform.position = bounds.center;
        Room room = go.AddComponent<Room>();
        room.Init(node.id, node.type, bounds);
        rooms[node.id] = room;
    }

    /// <summary>房间内部中心（世界坐标）。</summary>
    public Vector3 GetRoomCenterWorld(RoomNode node)
    {
        Vector2Int inner = InteriorOrigin(node);
        return new Vector3(inner.x + roomW * 0.5f, inner.y + roomH * 0.5f, 0f);
    }
}
