using System.Collections;
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
    public SaveService.ActiveRunData ActiveRun { get; private set; }
    public Room CurrentRoom { get; private set; }
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
    public Room BossRoom => CurrentRoom != null && CurrentRoom.Type == RoomType.Boss ? CurrentRoom : null;
    private Coroutine pendingReward;
    private NodeRewardInteractable currentRewardInteractable;

    /// <summary>兼容旧调试入口：显式开始新 Run，不再创建一层实体布局。</summary>
    public void Generate() => Generate(0);

    public void Generate(int seedOverride)
    {
        int runSeed = seedOverride != 0 ? seedOverride : (seed != 0 ? seed : System.Environment.TickCount);
        PlayableCharacterId character = RunStateCarrier.Ensure().ChosenPlayableCharacterId;
        SaveService.ActiveRunData run = SaveService.CreateNewRun(runSeed, character);
        BeginOrResumeRun(run);
    }

    public bool BeginOrResumeRun(SaveService.ActiveRunData run)
    {
        if (config == null || builder == null || run == null)
        {
            Debug.LogError("[Dungeon] 路线入口缺少配置、Builder 或 ActiveRun");
            return false;
        }
        if (DungeonRouteRules.NeedsInitialization(run) && !DungeonRouteRules.TryInitialize(run))
        {
            Debug.LogError("[Dungeon] 无法初始化 DAG 路线");
            return false;
        }
        string error = "图为空";
        if (run.dungeonGraph == null || !run.dungeonGraph.Validate(out error)
            || run.dungeonGraph.Get(run.currentNodeId) == null)
        {
            Debug.LogError($"[Dungeon] ActiveRun 图或当前节点非法：{error}");
            return false;
        }
        ActiveRun = run;
        ActiveSeed = run.mainSeed;
        FloorNumber = run.floorNumber;
        DungeonMapUI.BindRun(run, TryChooseNext);
        SaveService.SaveRun(run);
        return LoadCurrentNode();
    }

    private bool LoadCurrentNode()
    {
        DungeonGraphNode node = DungeonRouteRules.Current(ActiveRun);
        if (node == null) return false;
        DetachCurrentRoom();
        RoomRewardHook.Detach();
        RewardChoiceUI.Close();
        DungeonMapUI.Close();
        try
        {
            Layout = builder.BuildSingle(node, config, FloorNumber,
                node.ObjectiveCompleted, out Vector3 spawnPosition);
            if (!builder.Rooms.TryGetValue(node.NodeId, out Room room))
                throw new System.InvalidOperationException("单节点构建未产出 Room");
            CurrentRoom = room;
            node.Generated = true;
            room.OnRoomCleared += OnCurrentRoomCleared;
            if (node.Type != NodeType.Boss)
                RouteExitInteractable.Create(room.ContentRoot,
                    new Vector3(room.Bounds.xMax - 2.5f, room.Bounds.center.y, 0f), this);
            if (player == null)
            {
                GameObject found = GameObject.FindGameObjectWithTag("Player");
                if (found != null) player = found.transform;
            }
            if (player == null) throw new System.InvalidOperationException("场景中没有 Player");
            player.position = spawnPosition;
            Physics2D.SyncTransforms();
            if (Camera.main != null && Camera.main.TryGetComponent(out CameraFollow cam))
                cam.SnapToTarget();
            if (node.Type == NodeType.Start) TutorialSigns.Spawn(room);
            room.Enter();
            if (node.ObjectiveCompleted && !node.RewardResolved
                && (node.Type == NodeType.Sage || node.Type == NodeType.Supply))
            {
                EnsureRewardInteractable(node);
                ScheduleTemporaryReward(node.NodeId);
            }
            SaveActiveRun();
            OnGenerated?.Invoke();
            Debug.Log($"[Dungeon] 当前节点 {node.NodeId}/{node.Type}，ActiveRoom=1，RunSeed={ActiveSeed}");
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[Dungeon] 节点 {node.NodeId} 加载失败：{exception}");
            Cleanup();
            return false;
        }
    }

    private void OnCurrentRoomCleared(Room room)
    {
        DungeonGraphNode node = DungeonRouteRules.Current(ActiveRun);
        if (node == null || room == null || room.Id != node.NodeId) return;
        if (DungeonRouteRules.TryCompleteObjective(ActiveRun, node.NodeId)) SaveActiveRun();
        if (node.Type == NodeType.Start || node.Type == NodeType.Shop)
        {
            if (DungeonRouteRules.TryResolveReward(ActiveRun, node.NodeId)) SaveActiveRun();
        }
        else if (!node.RewardResolved
            && (node.Type == NodeType.Sage || node.Type == NodeType.Supply))
        {
            EnsureRewardInteractable(node);
            ScheduleTemporaryReward(node.NodeId);
        }
    }

    private void EnsureRewardInteractable(DungeonGraphNode node)
    {
        if (currentRewardInteractable != null || CurrentRoom == null || node == null) return;
        currentRewardInteractable = NodeRewardInteractable.Create(CurrentRoom, node, this);
    }

    private void ScheduleTemporaryReward(int nodeId)
    {
        if (pendingReward != null) StopCoroutine(pendingReward);
        pendingReward = StartCoroutine(ShowTemporaryRewardAfterDelay(nodeId));
    }

    private IEnumerator ShowTemporaryRewardAfterDelay(int nodeId)
    {
        yield return new WaitForSeconds(2f);
        pendingReward = null;
        ShowTemporaryReward(nodeId);
    }

    public void OpenPendingReward(int nodeId)
    {
        DungeonGraphNode node = DungeonRouteRules.Current(ActiveRun);
        if (node == null || node.NodeId != nodeId || !node.ObjectiveCompleted
            || node.RewardResolved || RewardChoiceUI.IsOpen) return;
        List<NodeRewardService.RewardOption> rolled = NodeRewardService.Roll(node.RewardSeed, false);
        var options = new List<NodeRewardService.RewardOption>(3);
        foreach (NodeRewardService.RewardOption option in rolled)
            if (option.Kind == NodeRewardService.RewardKind.Coins
                || option.Kind == NodeRewardService.RewardKind.Heal
                || option.Kind == NodeRewardService.RewardKind.AttackUp)
                options.Add(option);
        RewardChoiceUI.Show(options, option => ApplyTemporaryReward(nodeId, option));
    }

    private void ShowTemporaryReward(int nodeId) => OpenPendingReward(nodeId);

    private void ApplyTemporaryReward(int nodeId, NodeRewardService.RewardOption option)
    {
        DungeonGraphNode node = DungeonRouteRules.Current(ActiveRun);
        if (node == null || node.NodeId != nodeId || node.RewardResolved) return;
        RunTransactionSource source = node.Type == NodeType.Sage
            ? RunTransactionSource.SageOffer : RunTransactionSource.SupplyReward;
        string transactionId = RunTransactionRules.BuildId(ActiveRun.runId, nodeId, source, 0);
        if (RunTransactionRules.Contains(ActiveRun, transactionId)) return;
        PlayerStats stats = player != null ? player.GetComponent<PlayerStats>() : null;
        Health health = player != null ? player.GetComponent<Health>() : null;
        if (stats == null || health == null) return;
        switch (option.Kind)
        {
            case NodeRewardService.RewardKind.Coins:
                stats.AddCoins(Mathf.RoundToInt(option.Value));
                break;
            case NodeRewardService.RewardKind.Heal:
                health.Heal(option.Value);
                break;
            case NodeRewardService.RewardKind.AttackUp:
                stats.PermDamageMult += option.Value;
                ActiveRun.temporaryRewardAttackBonus += option.Value;
                break;
            default:
                return;
        }
        ActiveRun.transactions.Add(new RunTransactionRecord
        {
            transactionId = transactionId, source = source, nodeId = nodeId,
            sequence = 0, selectedAbilityId = "temporary:" + option.Kind,
        });
        if (DungeonRouteRules.TryResolveReward(ActiveRun, nodeId))
        {
            currentRewardInteractable?.MarkResolved();
            SaveActiveRun();
        }
    }

    public void OpenRouteChoice()
    {
        DungeonGraphNode node = DungeonRouteRules.Current(ActiveRun);
        if (node == null || node.Type == NodeType.Boss) return;
        if (!node.Completed) return;
        DungeonMapUI.OpenRouteChoice();
    }

    public bool TryChooseNext(int nextNodeId)
    {
        if (ActiveRun == null || !DungeonRouteRules.CanChooseNext(ActiveRun, nextNodeId)) return false;
        SnapshotResources();
        if (!DungeonRouteRules.TryChooseNext(ActiveRun, nextNodeId)) return false;
        SaveService.SaveRun(ActiveRun);
        return LoadCurrentNode();
    }

    private void SnapshotResources()
    {
        if (ActiveRun == null || player == null) return;
        ActiveRun.resourcesInitialized = true;
        Health health = player.GetComponent<Health>();
        PlayerStats stats = player.GetComponent<PlayerStats>();
        WerewolfRage rage = player.GetComponent<WerewolfRage>();
        if (health != null) ActiveRun.currentHp = Mathf.RoundToInt(health.CurrentHealth);
        if (stats != null)
        {
            ActiveRun.currentArmor = Mathf.RoundToInt(stats.CurrentArmor);
            ActiveRun.currentMana = stats.CurrentMana;
            ActiveRun.runCoins = stats.Coins;
        }
        if (rage != null) ActiveRun.currentClassResource = rage.Current;
        ActiveRun.killsThisRun = RunTracker.Kills;
        ActiveRun.relicIds.Clear();
        foreach (RelicDefinition relic in RelicRuntime.Run.Owned)
            if (relic != null) ActiveRun.relicIds.Add(relic.relicId);
    }

    public void SaveActiveRun()
    {
        if (ActiveRun == null) return;
        SnapshotResources();
        SaveService.SaveRun(ActiveRun);
    }

    private void DetachCurrentRoom()
    {
        if (pendingReward != null) { StopCoroutine(pendingReward); pendingReward = null; }
        if (CurrentRoom != null) CurrentRoom.OnRoomCleared -= OnCurrentRoomCleared;
        currentRewardInteractable = null;
        CurrentRoom = null;
    }

    public void Cleanup()
    {
        DetachCurrentRoom();
        RoomRewardHook.Detach();
        RewardChoiceUI.Close();
        DungeonMapUI.UnbindRun();
        if (builder != null) builder.ClearAll();
        Layout = null;
        ActiveRun = null;
    }

    private void OnDestroy() => Cleanup();

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
