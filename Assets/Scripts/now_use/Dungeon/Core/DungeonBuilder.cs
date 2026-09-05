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

    // v1.1.4 地皮分层：terrainMask=逻辑层（草/土二值+距离场），groundTileset=视觉素材库。
    // 素材加载失败（Resources 缺失）时 terrainMask 为 null，全层自动回退旧的平色地板+房型着色路径。
    private TerrainMask terrainMask;
    private GroundTileset groundTileset;
    private int terrainDecoSeed;
    private readonly HashSet<Vector2Int> groundCells = new HashSet<Vector2Int>();

    private int layoutSeedCache;   // v1.1.31 RoomPlanner rng 派生用
    private WallDropAnimator wallDropAnimator;
    // v1.1.41 各房可达骨架合集（PaintRoom 收集 → 开洞后 TerrainMask.ApplySkeletonBias 地皮融入）
    private readonly HashSet<Vector2Int> skeletonCells = new HashSet<Vector2Int>();
    // v1.1.44 per-room 骨架（SpawnContent 用：大石块避让骨架格，防止内容阶段堵死路网入口）
    private readonly Dictionary<int, HashSet<Vector2Int>> roomSkeletons = new Dictionary<int, HashSet<Vector2Int>>();
    // v1.1.46 最终布局的内容生成白名单：防止敌人/奖励/装饰刷进挖除空洞或房内墙。
    private readonly Dictionary<int, List<Vector2Int>> roomSpawnCells = new Dictionary<int, List<Vector2Int>>();

    public Vector3 Build(DungeonLayout layout, DungeonConfig config, int layoutSeed, int floorNumber = 1)
    {
        floorTheme = floorNumber;   // M3：主题缓存
        roomW = config.roomWidth;
        roomH = config.roomHeight;
        doorW = config.doorWidth;
        layoutSeedCache = layoutSeed;   // v1.1.22：PaintRoom 塑形 rng 派生用

        ClearAll();

        // v1.1.4 地皮总纲 1~4 步：噪声逻辑层（世界绝对坐标采样，跨房间连续）→ 小簇修正 → 距离场。
        // 地形 seed 与装饰 seed 均派生自 layoutSeed：同 seed 复现门禁不变，两个随机场互不相同。
        terrainMask = null;
        groundTileset = GroundTileset.Load();
        if (groundTileset != null && layout.rooms.Count > 0)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (RoomNode node in layout.rooms)
            {
                RectInt r = TileRect(node);
                minX = Mathf.Min(minX, r.xMin); minY = Mathf.Min(minY, r.yMin);
                maxX = Mathf.Max(maxX, r.xMax); maxY = Mathf.Max(maxY, r.yMax);
            }
            terrainMask = TerrainMask.Generate(new RectInt(minX, minY, maxX - minX, maxY - minY), layoutSeed ^ 0x7EA9);
            terrainDecoSeed = layoutSeed ^ 0xC0FFEE;
        }

        // v1.1.22 塑形预计算：先算全部门洞矩形（纯几何）→ 建立各房门洞保护带格集，
        // PaintRoom 塑形时保证门→中心通路永不被挖除/砌墙；开洞仍延后到刷房之后（行为不变）
        var doorRects = new Dictionary<RoomConnection, Rect>();
        var doorCellsByRoom = new Dictionary<int, List<Vector2Int>>();
        foreach (RoomConnection conn in layout.connections)
        {
            Rect? r = ComputeDoorRect(conn);
            if (!r.HasValue) continue;
            doorRects[conn] = r.Value;
            AddDoorCells(doorCellsByRoom, conn.a.id, r.Value);
            AddDoorCells(doorCellsByRoom, conn.b.id, r.Value);
        }

        // v1.1.28 一块厚墙：占用集（各房覆盖的粗格）→ 邻接方向判定，墙线由一侧独画、两侧共享
        var occupied = new HashSet<Vector2Int>();
        foreach (RoomNode node in layout.rooms)
            for (int gy = node.gridPos.y; gy < node.gridPos.y + node.spanY; gy++)
                for (int gx = node.gridPos.x; gx < node.gridPos.x + node.spanX; gx++)
                    occupied.Add(new Vector2Int(gx, gy));

        // v1.1.29 渲染序自上而下：下方墙格后画——竖向墙列"下图扣上图"的堆叠遮挡（prop_01 顶伸 0.47）
        TilemapRenderer wallRenderer = wallsTilemap.GetComponent<TilemapRenderer>();
        if (wallRenderer != null) wallRenderer.sortOrder = TilemapRenderer.SortOrder.TopLeft;

        foreach (RoomNode node in layout.rooms) PaintRoom(node, doorCellsByRoom, occupied);
        foreach (KeyValuePair<RoomConnection, Rect> kv in doorRects) OpenDoor(kv.Value);

        // v1.1.46：必须在 PaintRoom 收集完 skeletonCells 后再偏置；旧顺序在集合仍为空时调用，
        // 实际从未生效。BuildRoadMaskSurface 尚未绘制，故此处改逻辑层仍是单次正确时机。
        if (terrainMask != null && skeletonCells.Count > 0)
            terrainMask.ApplySkeletonBias(skeletonCells, layoutSeed ^ 0x501);

        // v1.1.29 横竖定向：按最终拓扑（含门洞打断）逐格判向重铺——上下有墙邻=竖列（仅 prop_01 堆叠），
        // 否则横行（随机引用）。塑形墙段与门洞断口统一由此收敛，绘制期的临时铺装被覆盖。
        ReorientWalls();
        FlushWallColliderChanges();

        // v1.1.43 墙体入场只操作无碰撞视觉副本：真实 Walls Tilemap/Collider 已经在最终位置，
        // 避免逐格动画触发 TilemapCollider 重建或在落地过程中夹住玩家。
        if (Application.isPlaying && config.wallDropEnabled)
        {
            if (wallDropAnimator == null)
            {
                wallDropAnimator = GetComponent<WallDropAnimator>();
                if (wallDropAnimator == null) wallDropAnimator = gameObject.AddComponent<WallDropAnimator>();
            }
            wallDropAnimator.Play(wallsTilemap, layoutSeed, config.wallDropHeight,
                config.wallDropDuration, config.wallDropStagger);
        }

        BuildRoadMaskSurface();
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

        // v1.1.46 怪物轮次：掷中概率的普通战斗房追加第二波（第一波全灭 → 延迟 0.9s 增援，
        // 两波全灭才开门，增加战斗时长与节奏起伏）。波次 rng 由房 rng 派生子流（同 seed 复现不变）。
        if (node.type == RoomType.Combat && profile.enemyTable != null
            && config.combatWaveChance > 0f && rng.NextDouble() < config.combatWaveChance)
        {
            var waveGo = new GameObject("WaveController");
            waveGo.transform.SetParent(room.transform, false);
            var wave = waveGo.AddComponent<RoomWaveController>();
            wave.Setup(room, profile.enemyTable, floorNumber, config,
                new System.Random(rng.Next()), waves: 1);
            room.RegisterWaveProvider(wave);
        }
        // v1.1.27：障碍物职责整体移交废墟石块（StoneDecorSpawner 大石块）——未用石块的
        // 旧障碍表（木箱）不再生成；表路径休眠保留，未来需要混搭时恢复此行即可。
        // ObstacleSpawner.Spawn(room, profile.obstacleTable, rng);
        DecorationSpawner.Spawn(room, profile.decorationTable, rng);
        // v1.1.44：大石块避让骨架格——石块有碰撞且会砸在路网/入口上，把玩家口径验证
        // 留下的 ≥2 宽口重新堵死（可破坏所以不硬卡，但堵主路体验差）
        roomSkeletons.TryGetValue(node.id, out HashSet<Vector2Int> skeleton);
        StoneDecorSpawner.Spawn(room, rng, skeleton);   // v1.1.26 废墟石块：小=无碰撞点缀，大=障碍物替代（随机引用）
        BarrelDecorSpawner.Spawn(room, rng, skeleton);  // v1.1.45 可破坏木桶/木箱堆（小件纯装饰，同避让骨架）
        InteractableSpawner.Spawn(room, profile.interactableTable, rng);
    }

    /// <summary>清空 Tilemap 与全部生成物（重建 / 楼层切换共用）。</summary>
    public void ClearAll()
    {
        if (wallDropAnimator != null) wallDropAnimator.Cancel();
        floorTilemap.ClearAllTiles();
        wallsTilemap.ClearAllTiles();
        groundCells.Clear();
        skeletonCells.Clear();
        roomSkeletons.Clear();
        roomSpawnCells.Clear();
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

    // ---------- 坐标换算（v1.1.28 一块厚墙：格 = 西/南各 1 格墙 + 内部；东/北墙线与邻房共享） ----------

    private int CellWidth => roomW + 1;
    private int CellHeight => roomH + 1;

    /// <summary>房间锚点格子原点的瓦片坐标（含墙外框左下角）。</summary>
    private Vector2Int CellOrigin(RoomNode node)
        => new Vector2Int(node.gridPos.x * CellWidth, node.gridPos.y * CellHeight);

    /// <summary>房间含墙瓦片矩形（v0.5.3.1：跨格房占 spanX×spanY 个粗格）。</summary>
    private RectInt TileRect(RoomNode node)
    {
        Vector2Int o = CellOrigin(node);
        return new RectInt(o.x, o.y, node.spanX * CellWidth, node.spanY * CellHeight);
    }

    /// <summary>房间内部矩形（v1.1.28：内缩西/南各 1 格墙；东/上边界即共享墙线）= Room.Bounds 唯一来源。</summary>
    private Rect InteriorRect(RoomNode node)
    {
        RectInt r = TileRect(node);
        return new Rect(r.xMin + 1, r.yMin + 1, r.width - 1, r.height - 1);
    }

    // ---------- 绘制 ----------

    /// <summary>画一个房间（v1.1.28 一块厚墙结构）：
    /// 内部地板 = [xMin+1, xMax) × [yMin+1, yMax)；墙线 = 西列与南行本房必画（邻房共享同一列/行），
    /// 东列/北行仅当该方向无邻房时画在自身外沿（xMax 列 / yMax 行）——任意两房间隔恰好一块厚。
    /// v0.5.3：内部地板按 RoomTypeConfig.floorTint 着色（alpha=0 不着色）。
    /// v1.1.31：非 Start/Boss 房走五职责塑形管线（房形/轮廓墙/障碍/验证/重试保底）。</summary>
    private void PaintRoom(RoomNode node, Dictionary<int, List<Vector2Int>> doorCellsByRoom,
        HashSet<Vector2Int> occupied)
    {
        RectInt rect = TileRect(node);
        Color tint = GetTypeConfig(node.type)?.floorTint ?? Color.clear;
        // M3·v0.8.1：房型色 × 主题色叠乘（废墟白/墓穴冷蓝/熔炉暖红，3 层一换）
        tint *= DungeonManager.GetFloorTheme(floorTheme).tint;

        // 一块厚墙线：西列/南行必画；东列/北行无邻才画（画在自身外沿，列/行与邻房原点重合处由邻画）
        for (int y = rect.yMin; y < rect.yMax; y++) SetWallTile(new Vector3Int(rect.xMin, y, 0));
        for (int x = rect.xMin + 1; x < rect.xMax; x++) SetWallTile(new Vector3Int(x, rect.yMin, 0));
        if (!HasNeighborEast(occupied, node))
            for (int y = rect.yMin; y < rect.yMax; y++) SetWallTile(new Vector3Int(rect.xMax, y, 0));
        if (!HasNeighborNorth(occupied, node))
            for (int x = rect.xMin; x <= rect.xMax; x++) SetWallTile(new Vector3Int(x, rect.yMax, 0));

        // ---------- v1.1.31 五职责塑形管线：房形→轮廓墙→障碍→验证（失败同 RNG 重试，保底整房） ----------
        // Start（出生/传送落点安全）与 Boss（竞技场可读性）保持完整矩形
        RectInt interiorRect = new RectInt(rect.xMin + 1, rect.yMin + 1, rect.width - 1, rect.height - 1);
        RoomPlan plan = RoomPlan.Plain(interiorRect);
        if (node.type != RoomType.Start && node.type != RoomType.Boss)
        {
            var rng = new System.Random(layoutSeedCache * 31 + node.id * 911);
            doorCellsByRoom.TryGetValue(node.id, out List<Vector2Int> doorCells);
            plan = RoomPlanner.CreatePlan(interiorRect, doorCells, rng);
        }
        foreach (var sk in plan.Skeleton) skeletonCells.Add(sk);   // v1.1.41 地皮融入：骨架合集
        roomSkeletons[node.id] = plan.Skeleton;   // v1.1.44 大石块避让用
        roomSpawnCells[node.id] = new List<Vector2Int>(plan.SpawnCells);

        int xEnd = interiorRect.xMax, yEnd = interiorRect.yMax;
        for (int x = interiorRect.xMin; x < xEnd; x++)
        {
            for (int y = interiorRect.yMin; y < yEnd; y++)
            {
                var cell = new Vector2Int(x, y);
                var pos = new Vector3Int(x, y, 0);
                if (plan.IsWall(cell))
                {
                    // v1.1.46 单次绘制：墙格绝不同时保留地板，避免轮廓看成贴外墙的内部墙。
                    groundCells.Remove(cell);
                    floorTilemap.SetTile(pos, null);
                    SetWallTile(pos);   // 轮廓墙（交界一格）+ 障碍（分散小掩体岛）
                }
                else if (plan.IsWalkable(cell))
                {
                    if (terrainMask != null) groundCells.Add(cell);
                    else
                    {
                        floorTilemap.SetTile(pos, floorTile);
                        if (tint.a > 0f) floorTilemap.SetColor(pos, tint);
                    }
                }
                else
                {
                    // 深层挖除格必须真正留空。旧流程先铺满内部地板、此处只“不做事”，
                    // 造成挖除区仍有地面，轮廓 L 墙因此被误看成粘在外墙上的房内障碍。
                    groundCells.Remove(cell);
                    floorTilemap.SetTile(pos, null);
                    wallsTilemap.SetTile(pos, null);
                }
            }
        }
    }

    /// <summary>东向是否有邻房占格（占用集按各房 span 覆盖的粗格判定）。</summary>
    private static bool HasNeighborEast(HashSet<Vector2Int> occupied, RoomNode node)
    {
        int nx = node.gridPos.x + node.spanX;
        for (int gy = node.gridPos.y; gy < node.gridPos.y + node.spanY; gy++)
            if (occupied.Contains(new Vector2Int(nx, gy))) return true;
        return false;
    }

    /// <summary>北向是否有邻房占格。</summary>
    private static bool HasNeighborNorth(HashSet<Vector2Int> occupied, RoomNode node)
    {
        int ny = node.gridPos.y + node.spanY;
        for (int gx = node.gridPos.x; gx < node.gridPos.x + node.spanX; gx++)
            if (occupied.Contains(new Vector2Int(gx, ny))) return true;
        return false;
    }

    /// <summary>铺一块墙（v1.1.28 铺装期临时图；最终横竖定向由 ReorientWalls 收敛）：
    /// 石墙素材（宽归一+Grid 整格碰撞）缺素材回退白方块墙瓦。</summary>
    private void SetWallTile(Vector3Int pos)
    {
        TileBase t = WallPropTileset.GetHorizontal(
            Mathf.FloorToInt(TerrainMask.Hash01(pos.x, pos.y, layoutSeedCache ^ 0xA11) * 1024f));
        wallsTilemap.SetTile(pos, t != null ? t : wallTile);
    }

    /// <summary>
    /// TilemapCollider 默认批量到物理步刷新；本层却会在同一 Build 调用末尾生成内容。
    /// 先提交墙体变更，物理重叠检测作为布局白名单之后的第二道保险。
    /// </summary>
    private void FlushWallColliderChanges()
    {
        TilemapCollider2D wallCollider = wallsTilemap.GetComponent<TilemapCollider2D>();
        if (wallCollider != null && wallCollider.hasTilemapChanges)
            wallCollider.ProcessTilemapChanges();
        Physics2D.SyncTransforms();
    }

    /// <summary>
    /// 墙体横竖定向终遍（v1.1.29，开洞后执行）：格上下有墙邻 = 竖列（**仅 prop_01**，
    /// 底对齐向上伸 0.47 + TopLeft 渲染序 → 下方块扣在上一块底部，连续墙柱堆叠）；
    /// 其余 = 横行（非 prop_01 池随机引用）。角格（上下左右皆有邻）归竖列。
    /// </summary>
    private void ReorientWalls()
    {
        BoundsInt bounds = wallsTilemap.cellBounds;
        for (int y = bounds.yMin; y <= bounds.yMax; y++)
            for (int x = bounds.xMin; x <= bounds.xMax; x++)
            {
                var pos = new Vector3Int(x, y, 0);
                if (wallsTilemap.GetTile(pos) == null) continue;

                bool vertical = wallsTilemap.GetTile(new Vector3Int(x, y + 1, 0)) != null
                             || wallsTilemap.GetTile(new Vector3Int(x, y - 1, 0)) != null;
                Tile t = vertical
                    ? WallPropTileset.GetVertical()
                    : WallPropTileset.GetHorizontal(
                        Mathf.FloorToInt(TerrainMask.Hash01(x, y, layoutSeedCache ^ 0xA11) * 1024f));
                wallsTilemap.SetTile(pos, t != null ? (TileBase)t : wallTile);
            }
    }

    /// <summary>门洞矩形纯计算（v1.1.28 一块厚墙）：共享墙线 = 西房 xMax 列（即东房 xMin 列），
    /// 单列打穿；门洞在两房内部重叠段居中。返回 null = 非相邻（生成器数据错误，Validate 自检拦截）。</summary>
    private Rect? ComputeDoorRect(RoomConnection conn)
    {
        RectInt ra = TileRect(conn.a), rb = TileRect(conn.b);

        if (rb.xMin >= ra.xMax || rb.xMax <= ra.xMin) // 东西向：打穿共享墙列（西侧房的外沿列）
        {
            int wallX = rb.xMin >= ra.xMax ? ra.xMax : rb.xMax;
            int y0 = Mathf.Max(ra.yMin, rb.yMin) + 1;   // 两房内部重叠段（内部 = [yMin+1, yMax)）
            int y1 = Mathf.Min(ra.yMax, rb.yMax);       // exclusive
            if (y1 - y0 < doorW) return null;           // 防御：重叠段不足（构造上不存在）
            int startY = Mathf.Clamp((y0 + y1 - doorW) / 2, y0, y1 - doorW);
            return new Rect(wallX, startY, 1f, doorW);
        }
        if (rb.yMin >= ra.yMax || rb.yMax <= ra.yMin) // 南北向：打穿共享墙行
        {
            int wallY = rb.yMin >= ra.yMax ? ra.yMax : rb.yMax;
            int x0 = Mathf.Max(ra.xMin, rb.xMin) + 1;
            int x1 = Mathf.Min(ra.xMax, rb.xMax);      // exclusive
            if (x1 - x0 < doorW) return null;
            int startX = Mathf.Clamp((x0 + x1 - doorW) / 2, x0, x1 - doorW);
            return new Rect(startX, wallY, doorW, 1f);
        }
        return null;
    }

    /// <summary>按门洞矩形开洞（v1.1.22 自 CarveDoor 拆出）：两侧墙线打穿 + 铺地板/登记地面格。</summary>
    private void OpenDoor(Rect doorRect)
    {
        int x0 = Mathf.RoundToInt(doorRect.xMin), y0 = Mathf.RoundToInt(doorRect.yMin);
        int w = Mathf.RoundToInt(doorRect.width), h = Mathf.RoundToInt(doorRect.height);
        for (int x = x0; x < x0 + w; x++)
            for (int y = y0; y < y0 + h; y++)
                OpenDoorTile(x, y);
    }

    /// <summary>门洞保护带格集收集（v1.1.22）：门洞矩形覆盖的整格写入对应房间的列表（两侧房都收）。</summary>
    private static void AddDoorCells(Dictionary<int, List<Vector2Int>> byRoom, int roomId, Rect doorRect)
    {
        if (!byRoom.TryGetValue(roomId, out var list)) byRoom[roomId] = list = new List<Vector2Int>(8);
        int x0 = Mathf.FloorToInt(doorRect.xMin), y0 = Mathf.FloorToInt(doorRect.yMin);
        int x1 = Mathf.CeilToInt(doorRect.xMax), y1 = Mathf.CeilToInt(doorRect.yMax);
        for (int x = x0; x < x1; x++)
            for (int y = y0; y < y1; y++)
                list.Add(new Vector2Int(x, y));
    }

    private void OpenDoorTile(int x, int y)
    {
        var pos = new Vector3Int(x, y, 0);
        wallsTilemap.SetTile(pos, null);
        if (terrainMask != null)
            groundCells.Add(new Vector2Int(x, y));
        else
            floorTilemap.SetTile(pos, floorTile);
    }

    private void BuildRoadMaskSurface()
    {
        if (terrainMask == null || groundCells.Count == 0) return;

        // v1.1.25：恢复原套件逐格铺装（等比例 1 图=1 格，85px@PPU85 契约自适应）——
        // RoadMask 整层 mesh 单底图平铺观感单一（用户反馈），mesh+shader 路径休眠保留，
        // 待底图升级为多变体图集后可重启（恢复下方 surface.Build 调用即可）。
        foreach (Vector2Int cell in groundCells)
        {
            var pos = new Vector3Int(cell.x, cell.y, 0);
            TerrainPainter.PaintCell(floorTilemap, pos, terrainMask, groundTileset, terrainDecoSeed);
        }
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
        roomSpawnCells.TryGetValue(node.id, out List<Vector2Int> spawnCells);
        room.Init(node.id, node.type, bounds, condition, contentGo.transform,
            node.distanceFromStart, spawnCells);
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

        // v1.0.12 回滚 v1.0.11 的 GetCellCenterWorld"修复"：Grid 位于原点、cellSize=1（见场景
        // Grid/Walls/Floor 三者 transform 全零），瓦片坐标即世界坐标、格子边界在整数上；
        // doorRect.center 恰为门洞（跨两格）的几何中心，直接用即精确对齐。
        // CellCenterWorld 会多加半格中心偏移，把门推进墙体半格（用户截图实锤），故回滚。
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

#if UNITY_EDITOR
    /// <summary>门对齐验收工具（v1.0.12）：逐门打印位置/缩放/开关态，配合画面核对门体与洞口是否重合。</summary>
    [UnityEditor.MenuItem("Tools/Dungeon/Debug Dump Door Alignment")]
    private static void DebugDumpDoors()
    {
        Door[] doors = FindObjectsByType<Door>(FindObjectsInactive.Exclude);   // 去掉过时的 FindObjectsSortMode（仅调试工具，顺序无意义）
        if (doors.Length == 0) { Debug.LogWarning("[Door] 当前场景无门（地牢未生成？）"); return; }
        foreach (Door d in doors)
        {
            Vector3 p = d.transform.position;
            Vector3 s = d.transform.localScale;
            Debug.Log($"[Door] {d.name} @({p.x:F2},{p.y:F2}) 尺寸({s.x:F1}x{s.y:F1}) —— 洞口世界区域 [{p.x - s.x / 2:F1},{p.x + s.x / 2:F1}]x[{p.y - s.y / 2:F1},{p.y + s.y / 2:F1}]（门可见=相邻房 Active 锁门中）");
        }
    }
#endif
}
