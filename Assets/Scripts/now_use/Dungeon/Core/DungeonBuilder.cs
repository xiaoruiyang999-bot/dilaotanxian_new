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

    [Header("v0.5.3 房间类型系统")]
    [Tooltip("7 种房间类型配置（着色/清房条件/内容 Profile），按 type 匹配")]
    [SerializeField] private RoomTypeConfig[] roomTypeConfigs;

    private Dictionary<RoomType, RoomTypeConfig> typeConfigMap;

    /// <summary>按类型取配置；缺失时返回 null（调用方走保底）。</summary>
    private RoomTypeConfig GetTypeConfig(RoomType type)
    {
        if (typeConfigMap == null)
        {
            typeConfigMap = new Dictionary<RoomType, RoomTypeConfig>();
            foreach (RoomTypeConfig c in roomTypeConfigs)
                if (c != null && !typeConfigMap.ContainsKey(c.type)) typeConfigMap.Add(c.type, c);
        }
        return typeConfigMap.TryGetValue(type, out RoomTypeConfig cfg) ? cfg : null;
    }

    private readonly Dictionary<int, Room> rooms = new Dictionary<int, Room>();
    /// <summary>当前楼层的房间（Room.id → Room），供 Gizmos / 后续系统查询。</summary>
    public IReadOnlyDictionary<int, Room> Rooms => rooms;

    private int roomW, roomH, doorW;   // 配置缓存（瓦片）

    /// <summary>按布局重建整层地牢，返回起始房中心（世界坐标）。
    /// layoutSeed 用于派生每个房间的内容子 seed（同 seed 下地图与内容完全一致）。</summary>
    private int floorTheme = 1;   // M3：当前层主题缓存

    public Vector3 Build(DungeonLayout layout, DungeonConfig config, int layoutSeed, int floorNumber = 1)
    {
        floorTheme = floorNumber;   // M3：主题缓存
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

        // v0.5.2：内容生成放在最后——位置规则要读门洞中心（门已建）、敌人登记要 Room 已 Init。
        foreach (RoomNode node in layout.rooms) SpawnContent(node, layoutSeed, config, floorNumber);

        return GetRoomCenterWorld(layout.startRoom);
    }

    /// <summary>按房间类型取 Profile 生成内容（子 seed 派生：seed*7919 + roomId）。Start 等无 Profile 房自然为空。
    /// v0.5.4：floorNumber 透传 EnemySpawner 做楼层难度注入。</summary>
    private void SpawnContent(RoomNode node, int layoutSeed, DungeonConfig config, int floorNumber)
    {
        RoomContentProfile profile = GetTypeConfig(node.type)?.contentProfile;
        if (profile == null) return;
        if (!rooms.TryGetValue(node.id, out Room room)) return;

        var rng = new System.Random(layoutSeed * 7919 + node.id);
        EnemySpawner.Spawn(room, profile.enemyTable, rng, floorNumber, config);
        ObstacleSpawner.Spawn(room, profile.obstacleTable, rng);
        DecorationSpawner.Spawn(room, profile.decorationTable, rng);
        InteractableSpawner.Spawn(room, profile.interactableTable, rng);
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
            if (Application.isPlaying)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            else DestroyImmediate(child.gameObject);
        }
    }

    // ---------- 坐标换算（格子 = 内部 + 四周各 1 格墙；跨格房占 spanX×spanY 格） ----------

    private int CellWidth => roomW + 2;
    private int CellHeight => roomH + 2;

    /// <summary>房间锚点格子原点的瓦片坐标（含墙外框左下角）。</summary>
    private Vector2Int CellOrigin(RoomNode node)
        => new Vector2Int(node.gridPos.x * CellWidth, node.gridPos.y * CellHeight);

    /// <summary>房间含墙瓦片矩形（v0.5.3.1：跨格房占 spanX×spanY 个粗格）。</summary>
    private RectInt TileRect(RoomNode node)
    {
        Vector2Int o = CellOrigin(node);
        return new RectInt(o.x, o.y, node.spanX * CellWidth, node.spanY * CellHeight);
    }

    /// <summary>房间内部矩形（去四周各 1 格墙）= Room.Bounds 唯一来源。</summary>
    private Rect InteriorRect(RoomNode node)
    {
        RectInt r = TileRect(node);
        return new Rect(r.xMin + 1, r.yMin + 1, r.width - 2, r.height - 2);
    }

    // ---------- 绘制 ----------

    /// <summary>画一个房间：内部地板 + 四周一圈墙（格子间距含墙，相邻房间的墙线互不重叠）。
    /// v0.5.3：内部地板按 RoomTypeConfig.floorTint 着色（alpha=0 不着色；需 FloorTile 解锁 LockColor）。</summary>
    private void PaintRoom(RoomNode node)
    {
        RectInt rect = TileRect(node);
        Color tint = GetTypeConfig(node.type)?.floorTint ?? Color.clear;
        // M3·v0.8.1：房型色 × 主题色叠乘（废墟白/墓穴冷蓝/熔炉暖红，3 层一换）
        tint *= DungeonManager.GetFloorTheme(floorTheme).tint;

        for (int x = 0; x < rect.width; x++)
        {
            for (int y = 0; y < rect.height; y++)
            {
                bool border = x == 0 || y == 0 || x == rect.width - 1 || y == rect.height - 1;
                var pos = new Vector3Int(rect.xMin + x, rect.yMin + y, 0);
                if (border)
                {
                    wallsTilemap.SetTile(pos, wallTile);
                }
                else
                {
                    floorTilemap.SetTile(pos, floorTile);
                    if (tint.a > 0f) floorTilemap.SetColor(pos, tint);
                }
            }
        }
    }

    /// <summary>在相邻房间的共用墙中段开门洞：两侧墙线一起打穿 + 铺地板。返回门洞世界矩形（供 Door 定位）。
    /// v0.5.3.1：方向按含墙矩形相邻判定（跨格房锚点差不再轴对齐），门洞在两房内部重叠段居中。</summary>
    private Rect? CarveDoor(RoomConnection conn)
    {
        RectInt ra = TileRect(conn.a), rb = TileRect(conn.b);

        if (rb.xMin >= ra.xMax || rb.xMax <= ra.xMin) // 东西向连接：打穿西房东墙线 + 东房西墙线
        {
            RectInt west = rb.xMin >= ra.xMax ? ra : rb;
            int wallX1 = west.xMax - 1;   // 西房东墙线；+1 = 东房西墙线
            int y0 = Mathf.Max(ra.yMin, rb.yMin) + 1;   // 两房内部重叠段（不含墙）
            int y1 = Mathf.Min(ra.yMax, rb.yMax) - 1;   // exclusive
            if (y1 - y0 < doorW) return null;           // 防御：重叠段不足（构造上不存在）
            int startY = Mathf.Clamp((y0 + y1 - doorW) / 2, y0, y1 - doorW);
            for (int dy = 0; dy < doorW; dy++)
            {
                OpenDoorTile(wallX1, startY + dy);
                OpenDoorTile(wallX1 + 1, startY + dy);
            }
            return new Rect(wallX1, startY, 2f, doorW);
        }
        if (rb.yMin >= ra.yMax || rb.yMax <= ra.yMin) // 南北向连接：打穿南房北墙线 + 北房南墙线
        {
            RectInt south = rb.yMin >= ra.yMax ? ra : rb;
            int wallY1 = south.yMax - 1;
            int x0 = Mathf.Max(ra.xMin, rb.xMin) + 1;
            int x1 = Mathf.Min(ra.xMax, rb.xMax) - 1;
            if (x1 - x0 < doorW) return null;
            int startX = Mathf.Clamp((x0 + x1 - doorW) / 2, x0, x1 - doorW);
            for (int dx = 0; dx < doorW; dx++)
            {
                OpenDoorTile(startX + dx, wallY1);
                OpenDoorTile(startX + dx, wallY1 + 1);
            }
            return new Rect(startX, wallY1, doorW, 2f);
        }
        // 非相邻属生成器数据错误（Validate 自检会拦截），此处静默忽略
        return null;
    }

    private void OpenDoorTile(int x, int y)
    {
        var pos = new Vector3Int(x, y, 0);
        wallsTilemap.SetTile(pos, null);
        floorTilemap.SetTile(pos, floorTile);
    }

    // ---------- Room / Door 实例化 ----------

    private void CreateRoomObject(RoomNode node)
    {
        Rect bounds = InteriorRect(node);   // v0.5.3.1：跨格房边界来自含墙矩形
        // v0.5.3：清房条件由 RoomTypeConfig 数据驱动；配置缺失时保底 = v0.5.1 旧映射
        RoomTypeConfig typeCfg = GetTypeConfig(node.type);
        RoomClearCondition condition = typeCfg != null
            ? typeCfg.clearCondition
            : (node.type == RoomType.Combat || node.type == RoomType.Boss
                ? RoomClearCondition.AllEnemiesDead : RoomClearCondition.None);

        var go = new GameObject($"Room_{node.id}_{node.type}");
        go.transform.SetParent(dungeonRoot, false);
        go.transform.position = bounds.center;

        // 内容挂载点（敌人/障碍物/装饰）：始终 active。
        // 休眠由 Room 按敌人逐个禁用 AI/Combat 实现（敌人可见但不动），不隐藏整个挂载点。
        var contentGo = new GameObject("ContentRoot");
        contentGo.transform.SetParent(go.transform, false);
        contentGo.transform.localPosition = Vector3.zero;

        Room room = go.AddComponent<Room>();
        room.Init(node.id, node.type, bounds, condition, contentGo.transform, node.distanceFromStart);
        rooms[node.id] = room;

        // 进入触发器：四边内缩 0.5 格，保证玩家完全进房后才触发（防关门夹人）
        var triggerGo = new GameObject("RoomTrigger");
        // v0.5.4.4.3：RoomTrigger 放到 Ignore Raycast layer，避免被 EnemyPerception 的
        // LOS 射线（Layer0+7）拦截成障碍物，导致远程/召唤敌人永远看不到玩家。
        triggerGo.layer = LayerMask.NameToLayer("Ignore Raycast");
        triggerGo.transform.SetParent(go.transform, false);
        triggerGo.transform.localPosition = Vector3.zero;
        var triggerCol = triggerGo.AddComponent<BoxCollider2D>();
        triggerCol.isTrigger = true;
        triggerCol.size = new Vector2(bounds.width - 1f, bounds.height - 1f);
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
        Rect r = InteriorRect(node);
        return new Vector3(r.center.x, r.center.y, 0f);
    }
}
