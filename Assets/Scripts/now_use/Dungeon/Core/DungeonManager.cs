using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 地牢门面：持有配置与种子，串起 Generator（纯 C#）→ Builder（实例化）→ 玩家出生。
/// 调试：Scene Gizmos 布局可视化 + 右键「Validate 1000 Seeds」离线自检（不依赖场景）。
/// </summary>
public class DungeonManager : MonoBehaviour
{
    [Header("配置")]
    [SerializeField] private DungeonConfig config;
    [Tooltip("0 = 每次随机；非 0 = 固定种子（同 seed 生成同一张图）")]
    [SerializeField] private int seed = 0;

    [Header("引用")]
    [SerializeField] private DungeonBuilder builder;
    [Tooltip("留空则按 Player tag 自动查找")]
    [SerializeField] private Transform player;

    [Header("调试")]
    [SerializeField] private bool drawGizmos = true;

    /// <summary>当前楼层布局（纯数据，小地图等系统直接消费）。</summary>
    public DungeonLayout Layout { get; private set; }
    /// <summary>本层实际使用的种子。</summary>
    public int ActiveSeed { get; private set; }
    /// <summary>当前楼层数（v0.5.4：由 RunManager 写入，仅作难度注入透传，默认 1）。</summary>
    public int FloorNumber { get; set; } = 1;

    /// <summary>一层生成完毕（含玩家传送与相机 Snap）时触发；小地图等消费方据此重建（v0.6.0）。
    /// 楼层切换 / 死亡重开同样经由 Generate 触发，无需额外监听。</summary>
    public event System.Action OnGenerated;

    /// <summary>M3·v0.8.1：楼层主题（3 层一换，1-3 废墟 / 4-6 墓穴 / 7-9 熔炉）。</summary>
    public static (string name, Color tint) GetFloorTheme(int floor)
    {
        switch (Mathf.Clamp((floor - 1) / 3, 0, 2))
        {
            case 0: return ("废墟", new Color(1f, 1f, 1f));
            case 1: return ("墓穴", new Color(0.72f, 0.8f, 1f));
            default: return ("熔炉", new Color(1f, 0.78f, 0.68f));
        }
    }

    private static readonly Dictionary<int, Room> emptyRooms = new Dictionary<int, Room>();

    /// <summary>当前楼层全部场景房间（id → Room，透传自 Builder；未生成时为空表）。</summary>
    public IReadOnlyDictionary<int, Room> Rooms => builder != null ? builder.Rooms : emptyRooms;

    /// <summary>当前楼层的 Boss 房（v0.5.4 RunManager 结算监听用）；未生成时返回 null。</summary>
    public Room BossRoom
    {
        get
        {
            if (builder == null) return null;
            foreach (KeyValuePair<int, Room> kv in builder.Rooms)
                if (kv.Value.Type == RoomType.Boss) return kv.Value;
            return null;
        }
    }

    private void Start()
    {
        Generate();
    }

    /// <summary>生成一层地牢：布局 → 实例化 → 玩家传送至起始房 + 相机瞬移。</summary>
    public void Generate() => Generate(0);

    /// <summary>v0.5.4：指定 seed 生成（RunManager 楼层 seed / 死亡重开用）；0 = 按 Inspector seed 规则。</summary>
    public void Generate(int seedOverride)
    {
        if (config == null || builder == null)
        {
            Debug.LogError("[Dungeon] Manager 引用未配置（config / builder）");
            return;
        }

        ActiveSeed = seedOverride != 0 ? seedOverride : (seed != 0 ? seed : System.Environment.TickCount);

        // v2.0.5 第二批：LinearHorizontal 单房节点模式——一次只建当前节点的房间（V2 §11.3
        // 单 Active 房间），完成→DAG 选择→TransitionToNode 过渡重建；LegacyGrid 保持全图直建。
        Graph = DungeonGraphGenerator.Generate(ActiveSeed ^ 0x0DA6);
        if (config.topology == DungeonConfig.DungeonTopology.LinearHorizontal)
        {
            CurrentNodeId = Graph.StartNodeId;
            BuildSingleNode(Graph.Get(Graph.StartNodeId), isInitialBuild: true);
            EnsureNodeRunner();
            return;
        }

        Layout = DungeonGenerator.Generate(config, ActiveSeed);
        Vector3 spawnPos = builder.Build(Layout, config, ActiveSeed, FloorNumber);

        // 第一批兼容：Legacy 模式下地图预览仍可用（全图直建的展示图）
        DungeonMapUI.CurrentGraph = Graph;
        DungeonMapUI.CurrentNodeId = -1;

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        // M5·v1.0.0：出生房教程牌（Start 房对应的场景 Room）
        if (builder != null && Layout != null && Layout.startRoom != null
            && builder.Rooms.TryGetValue(Layout.startRoom.id, out Room startRoom2))
            TutorialSigns.Spawn(startRoom2);
        if (player != null)
        {
            player.position = spawnPos;
            if (Camera.main != null && Camera.main.TryGetComponent(out CameraFollow cam))
                cam.SnapToTarget();
        }

        Debug.Log($"[Dungeon] 生成完成 seed={ActiveSeed} rooms={Layout.rooms.Count} connections={Layout.connections.Count} bossRoom=#{Layout.bossRoom.id} bossDist={Layout.bossRoom.distanceFromStart}");
        OnGenerated?.Invoke();
    }

    /// <summary>v0.5.4 楼层切换清理：销毁 dungeonRoot 全部生成物 + 清两块 Tilemap（Builder.ClearAll 收口）。</summary>
    public void Cleanup()
    {
        if (builder == null) return;
        builder.ClearAll();
        Layout = null;
    }

    // ---------- v2.0.5 第二批：DAG 单房节点模式（V2 §11.1/§11.3） ----------

    /// <summary>当前层 DAG 图（两种拓扑都生成；Linear 模式驱动单房流程）。</summary>
    public DungeonGraphData Graph { get; private set; }
    /// <summary>Linear 模式当前节点 Id。</summary>
    public int CurrentNodeId { get; private set; } = -1;

    /// <summary>把 DAG 节点建成为场上唯一房间（复用整条 Builder 房间管线：塑形/内容/轮次/Bounds/相机）。</summary>
    private void BuildSingleNode(DungeonGraphNode node, bool isInitialBuild)
    {
        var mini = new DungeonLayout { seed = ActiveSeed };
        var roomNode = new RoomNode
        {
            id = 0,
            gridPos = Vector2Int.zero,
            spanX = 1,
            spanY = 1,
            type = ToRoomType(node.Type),
        };
        mini.rooms.Add(roomNode);
        mini.startRoom = roomNode;
        mini.bossRoom = node.Type == NodeType.Boss ? roomNode : null;

        builder.ClearAll();
        Layout = mini;
        Vector3 spawnPos = builder.Build(mini, config, ActiveSeed, FloorNumber);

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player != null)
        {
            // 出生/过渡都从房间左侧入口进（V2 §4.1 左入右出）
            Room room = null;
            if (builder.Rooms.TryGetValue(0, out Room built)) room = built;
            float entryX = room != null ? room.Bounds.xMin + 1.6f : spawnPos.x;
            float entryY = room != null ? room.Bounds.center.y : spawnPos.y;
            player.position = new Vector3(entryX, entryY, player.position.z);
            if (Camera.main != null && Camera.main.TryGetComponent(out CameraFollow cam))
                cam.SnapToTarget();
        }

        node.Visited = true;
        node.Generated = true;
        foreach (int nextId in node.NextNodeIds)
        {
            DungeonGraphNode t = Graph.Get(nextId);
            if (t != null) t.Discovered = true;
        }
        DungeonMapUI.CurrentGraph = Graph;
        DungeonMapUI.CurrentNodeId = node.NodeId;
        Debug.Log($"[Dungeon] 节点房就位：#{node.NodeId} {node.Type}（后继 {node.NextNodeIds.Count} 个）");
        OnGenerated?.Invoke();
    }

    /// <summary>过渡到下一节点（DungeonNodeRunner 消费）：黑屏淡入 → 清场重建 → 淡出。</summary>
    public void TransitionToNode(int nodeId, System.Action onArrived = null)
    {
        DungeonGraphNode node = Graph != null ? Graph.Get(nodeId) : null;
        if (node == null) return;
        CurrentNodeId = nodeId;
        StartCoroutine(TransitionRoutine(node, onArrived));
    }

    private System.Collections.IEnumerator TransitionRoutine(DungeonGraphNode node, System.Action onArrived)
    {
        CanvasGroup fader = GetOrCreateTransitionFader();
        float t = 0f;
        while (t < 0.25f) { t += Time.unscaledDeltaTime; fader.alpha = t / 0.25f; yield return null; }
        fader.alpha = 1f;

        BuildSingleNode(node, isInitialBuild: false);
        onArrived?.Invoke();

        t = 0f;
        while (t < 0.35f) { t += Time.unscaledDeltaTime; fader.alpha = 1f - t / 0.35f; yield return null; }
        fader.alpha = 0f;
        if (fader.gameObject.activeSelf) fader.gameObject.SetActive(false);
    }

    private CanvasGroup transitionFader;
    private CanvasGroup GetOrCreateTransitionFader()
    {
        if (transitionFader != null) return transitionFader;
        var go = new GameObject("NodeTransitionFader", typeof(Canvas), typeof(CanvasGroup), typeof(Image));
        go.transform.SetSiblingIndex(0);
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;   // 全 UI 之上
        var img = go.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        transitionFader = go.GetComponent<CanvasGroup>();
        transitionFader.alpha = 0f;
        return transitionFader;
    }

    private static RoomType ToRoomType(NodeType type)
    {
        switch (type)
        {
            case NodeType.Start: return RoomType.Start;
            case NodeType.Elite: return RoomType.Elite;
            case NodeType.Treasure: return RoomType.Treasure;
            case NodeType.Shop: return RoomType.Shop;
            case NodeType.Event: return RoomType.Event;
            case NodeType.Boss: return RoomType.Boss;
            case NodeType.Recovery: return RoomType.Event;   // 占位映射：恢复节点类型 UI 区分，房间用事件房
            default: return RoomType.Combat;
        }
    }

    private void EnsureNodeRunner()
    {
        if (GetComponent<DungeonNodeRunner>() == null)
            gameObject.AddComponent<DungeonNodeRunner>();
    }

    // ---------- 离线自检 ----------

    [ContextMenu("Validate 1000 Seeds")]
    private void ValidateFromContextMenu() => Validate1000Seeds(config);

    /// <summary>批量自检：1000 个种子逐个断言连通性 / 房间数 / Boss 有效性，输出统计。不依赖场景。</summary>
    public static void Validate1000Seeds(DungeonConfig config)
    {
        if (config == null) { Debug.LogError("[Dungeon] Validate: config 为空"); return; }

        int failures = 0, totalRooms = 0, totalBossDist = 0, minBossDist = int.MaxValue, maxBossDist = 0;
        int bossFull = 0, eliteTotal = 0, eliteFull = 0;   // v0.5.3.1 扩展达成率统计
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            int testSeed = 100000 + i;
            DungeonLayout layout = DungeonGenerator.Generate(config, testSeed);
            string error = ValidateLayout(layout, config);
            if (error != null)
            {
                failures++;
                Debug.LogWarning($"[Dungeon] Validate seed={testSeed}: {error}");
            }
            totalRooms += layout.rooms.Count;
            int d = layout.bossRoom.distanceFromStart;
            totalBossDist += d;
            if (d < minBossDist) minBossDist = d;
            if (d > maxBossDist) maxBossDist = d;
            if (layout.bossRoom.spanX >= 2 && layout.bossRoom.spanY >= 2) bossFull++;
            foreach (RoomNode r in layout.rooms)
                if (r.type == RoomType.Elite)
                {
                    eliteTotal++;
                    if (r.spanX * r.spanY >= 2) eliteFull++;
                }
        }
        sw.Stop();
        Debug.Log($"[Dungeon] Validate 1000 Seeds 完成：失败 {failures}/1000，平均房间数 {totalRooms / 1000f:F1}，Boss距离 min={minBossDist} avg={totalBossDist / 1000f:F1} max={maxBossDist}，Boss 2×2 达成 {bossFull / 10f:F0}%，Elite 扩展达成 {(eliteTotal > 0 ? eliteFull * 100f / eliteTotal : 100f):F0}%（{eliteFull}/{eliteTotal}），耗时 {sw.ElapsedMilliseconds}ms");
    }

    /// <summary>布局不变量检查（返回 null = 通过）。</summary>
    private static string ValidateLayout(DungeonLayout layout, DungeonConfig config)
    {
        if (layout.rooms.Count < config.roomCountMin || layout.rooms.Count > config.roomCountMax)
            return $"房间数 {layout.rooms.Count} 超出 [{config.roomCountMin},{config.roomCountMax}]";
        if (layout.startRoom == null) return "缺少起始房";
        if (layout.bossRoom == null || layout.bossRoom == layout.startRoom) return "Boss 房无效";
        if (!layout.bossRoom.IsLeaf) return $"Boss 房 #{layout.bossRoom.id} 不是单入口叶子";
        int bossSpan = BossRitualRoomTemplate.CoarseSpan;
        if (layout.bossRoom.spanX != bossSpan || layout.bossRoom.spanY != bossSpan)
            return $"Boss 房 #{layout.bossRoom.id} 尺寸 {layout.bossRoom.spanX}×{layout.bossRoom.spanY}，应为 {bossSpan}×{bossSpan}";
        RoomConnection bossEntrance = layout.bossRoom.connections[0];
        Vector2Int bossOriginal = bossEntrance.OriginalGridPos(layout.bossRoom);
        Vector2Int neighborOriginal = bossEntrance.OriginalGridPos(bossEntrance.Other(layout.bossRoom));
        if (neighborOriginal - bossOriginal != Vector2Int.down)
            return $"Boss 房 #{layout.bossRoom.id} 不是固定南入口";

        // 连通性：从起始房 BFS 可达房间数必须等于总数
        var visited = new HashSet<RoomNode>();
        var queue = new Queue<RoomNode>();
        visited.Add(layout.startRoom);
        queue.Enqueue(layout.startRoom);
        while (queue.Count > 0)
        {
            RoomNode cur = queue.Dequeue();
            foreach (RoomConnection conn in cur.connections)
            {
                RoomNode next = conn.Other(cur);
                if (visited.Add(next)) queue.Enqueue(next);
            }
        }
        if (visited.Count != layout.rooms.Count)
            return $"地图不连通：{visited.Count}/{layout.rooms.Count}";
        foreach (RoomNode r in layout.rooms)
            if (r.distanceFromStart < 0) return $"房间 #{r.id} BFS 距离未填写";

        // v0.5.3：特殊房选址规则与数量
        int treasure = 0, shop = 0, ev = 0;
        foreach (RoomNode r in layout.rooms)
        {
            if (r.type == RoomType.Treasure) treasure++;
            else if (r.type == RoomType.Shop) shop++;
            else if (r.type == RoomType.Event) ev++;

            bool isSpecial = r.type == RoomType.Treasure || r.type == RoomType.Shop || r.type == RoomType.Event;
            if (isSpecial && (r == layout.startRoom || r == layout.bossRoom))
                return $"特殊房 {r.type} 落在 Start/Boss 上 (#{r.id})";
            if (r.type == RoomType.Elite && r.distanceFromStart < 2)
                return $"Elite 房 #{r.id} 距离 {r.distanceFromStart} < 2";
            if (r.type == RoomType.Start && r != layout.startRoom)
                return $"出现第二个 Start 房 (#{r.id})";
            if (r.type == RoomType.Boss && r != layout.bossRoom)
                return $"出现第二个 Boss 房 (#{r.id})";
        }
        int capacity = layout.rooms.Count - 2;   // 扣除 Start/Boss
        int wantTotal = config.treasureCount + config.shopCount + config.eventCount;
        if (treasure + shop + ev != System.Math.Min(wantTotal, capacity))
            return $"特殊房总数 {treasure + shop + ev} 未达标（配置合计 {wantTotal}，容量 {capacity}）";
        if (wantTotal <= capacity
            && (treasure != config.treasureCount || shop != config.shopCount || ev != config.eventCount))
            return $"特殊房分项不达标：Treasure {treasure}/{config.treasureCount} Shop {shop}/{config.shopCount} Event {ev}/{config.eventCount}";

        // v0.5.3.1：跨格房间占用格不相交
        var cellOwner = new Dictionary<Vector2Int, int>();
        foreach (RoomNode r in layout.rooms)
        {
            for (int x = 0; x < r.spanX; x++)
                for (int y = 0; y < r.spanY; y++)
                {
                    Vector2Int c = r.gridPos + new Vector2Int(x, y);
                    if (cellOwner.TryGetValue(c, out int owner))
                        return $"房间 #{r.id} 与 #{owner} 占用格重叠 {c}";
                    cellOwner[c] = r.id;
                }
        }
        return null;
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Dungeon/Validate 1000 Seeds")]
    private static void ValidateFromEditorMenu()
    {
        var mgr = FindAnyObjectByType<DungeonManager>();
        if (mgr == null || mgr.config == null) { Debug.LogError("[Dungeon] 场景中未找到配置好的 DungeonManager"); return; }
        Validate1000Seeds(mgr.config);
    }
#endif

    // ---------- Gizmos ----------

    private void OnDrawGizmos()
    {
        if (!drawGizmos || Layout == null || builder == null) return;

        foreach (KeyValuePair<int, Room> kv in builder.Rooms)
        {
            Room room = kv.Value;
            Gizmos.color = room.Type switch
            {
                RoomType.Start    => Color.green,
                RoomType.Elite    => new Color(1f, 0.45f, 0f),
                RoomType.Treasure => Color.yellow,
                RoomType.Shop     => Color.blue,
                RoomType.Event    => new Color(0.7f, 0.3f, 1f),
                RoomType.Boss     => Color.red,
                _                 => new Color(1f, 1f, 1f, 0.5f),   // Combat
            };
            Gizmos.DrawWireCube(room.Bounds.center, room.Bounds.size);
        }

        Gizmos.color = Color.cyan;
        foreach (RoomConnection conn in Layout.connections)
        {
            if (builder.Rooms.TryGetValue(conn.a.id, out Room ra) && builder.Rooms.TryGetValue(conn.b.id, out Room rb))
                Gizmos.DrawLine(ra.Center, rb.Center);
        }
    }
}
