using System.Collections.Generic;
using UnityEngine;

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

    private void Start()
    {
        Generate();
    }

    /// <summary>生成一层地牢：布局 → 实例化 → 玩家传送至起始房 + 相机瞬移。</summary>
    public void Generate()
    {
        if (config == null || builder == null)
        {
            Debug.LogError("[Dungeon] Manager 引用未配置（config / builder）");
            return;
        }

        ActiveSeed = seed != 0 ? seed : System.Environment.TickCount;
        Layout = DungeonGenerator.Generate(config, ActiveSeed);
        Vector3 spawnPos = builder.Build(Layout, config, ActiveSeed);

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player != null)
        {
            player.position = spawnPos;
            if (Camera.main != null && Camera.main.TryGetComponent(out CameraFollow cam))
                cam.SnapToTarget();
        }

        Debug.Log($"[Dungeon] 生成完成 seed={ActiveSeed} rooms={Layout.rooms.Count} connections={Layout.connections.Count} bossRoom=#{Layout.bossRoom.id} bossDist={Layout.bossRoom.distanceFromStart}");
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
