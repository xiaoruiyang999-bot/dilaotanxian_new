using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 楼层循环总控（计划书五-E / v0.5.4）：持有 floorNumber 与主 seed；
/// 监听 Boss 房 OnRoomCleared → 房中心生成奖励宝箱 + 传送门；
/// 传送门 → NextFloor（单场景重建：Cleanup → floorSeed → Generate）；
/// v0.6.2 阶段 C（R4）：职业/武器选择经 RunStateCarrier 从准备场景带入（Start 时应用）；
/// 玩家死亡 → 2 秒后加载准备场景（职业默认上次，武器不保留需重拿）。
/// 对玩家只订阅事件、调用公开方法，不改其逻辑。
/// </summary>
public class RunManager : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private DungeonManager dungeonManager;
    [SerializeField] private GameObject rewardChestPrefab;
    [SerializeField] private GameObject portalPrefab;

    [Header("死亡重开")]
    [Tooltip("死亡后自动返回准备场景的延迟（秒）。v1.0.5 起为结算面板留阅读时间；面板上 Esc/点击可提前返回")]
    [SerializeField] private float restartDelay = 6f;
    [Tooltip("重开本局加载的准备场景名（需在 Build Settings 中）")]
    [SerializeField] private string prepSceneName = "v0_7_PrepRoom";

    [Header("统一入口（v1.0.6）")]
    [Tooltip("场景内无职业时重定向回准备场景（防止绕过职业选择直连地牢；两个地牢场景均开启）")]
    [SerializeField] private bool redirectToPrepWhenNoClass = false;

    public int FloorNumber { get; private set; } = 1;
    public int MainSeed { get; private set; }

    private PlayerController player;
    private Health playerHealth;
    private PlayerStats playerStats;
    private Room bossRoomSubscribed;

    void Start()
    {
        // DungeonManager.Start 先生成第 1 层；延迟一帧初始化，保证脚本执行顺序无关
        StartCoroutine(InitDelayed());
    }

    private IEnumerator InitDelayed()
    {
        yield return null;
        // v1.0.6 统一入口：未选职业直连本场景（编辑器里 Play 了地牢场景/旧 v0_5 场景）时，
        // 重定向回准备场景走正式流程，而不是以无职业状态裸进地牢
        if (redirectToPrepWhenNoClass && !RunStateCarrier.Ensure().HasPlayableCharacter)
        {
            Debug.Log("[Run] 未选职业角色：重定向到准备场景（统一入口）");
            SceneManager.LoadScene(prepSceneName);
            yield break;
        }
        MainSeed = dungeonManager.ActiveSeed;
        player = FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            Debug.LogError("[Run] 场景中未找到 PlayerController");
            yield break;
        }
        playerHealth = player.GetHealth();
        playerStats = player.GetStats();
        playerHealth.OnDeath += OnPlayerDeath;
        RunTracker.BeginRun();          // v1.0.5 死亡结算统计：每次地牢场景加载重置，NextFloor 不重置（整局累计）
        ApplyLoadoutFromCarrier();
        SubscribeBossRoom();
        Debug.Log($"[Run] 楼层循环启动：floor=1 mainSeed={MainSeed}");
        StartCoroutine(PlayRequiredNarrative());   // v2.0.7 每 Run 必得碎片（V2 §2.3 保底）
    }

    /// <summary>
    /// 从跨场景载体应用职业/武器（v0.6.2 阶段 C：准备场景选定的配置应用到地牢玩家）。
    /// 未选择（旧场景直连测试）时保持现状。
    /// </summary>
    private void ApplyLoadoutFromCarrier()
    {
        RunStateCarrier carrier = RunStateCarrier.Ensure();

        PlayableCharacterDefinition definition = carrier.ChosenPlayableCharacter;
        if (definition != null)
        {
            playerStats.ApplyPlayableCharacter(definition);
            Debug.Log($"[Run] 应用职业角色：{definition.DisplayName}");
        }

        if (carrier.LastWeapon != null)
        {
            PlayerWeaponHolder holder = player.GetComponent<PlayerWeaponHolder>();
            if (holder == null)
                holder = player.gameObject.AddComponent<PlayerWeaponHolder>();
            holder.Equip(carrier.LastWeapon);   // 新玩家 Current 为空，不会触发掉落
            Debug.Log($"[Run] 应用武器：{carrier.LastWeapon.DisplayName}");
        }

        // V2 迁移期：狼人外形挂载怒痕/兽化链；正式局内才启用时间恢复。
        if (definition != null && definition.Id == PlayableCharacterId.Werewolf)
        {
            CharacterSelectUI.ApplyCharacterRuntime(player.gameObject, definition.Id);
            WerewolfTransformation transformation = WerewolfTransformation.EnsureOn(player.gameObject);
            transformation.Rage?.SetPassiveRecoveryEnabled(true);
            WerewolfDash.EnsureOn(player.gameObject);   // v1.1.42 狼人冲刺
            Debug.Log("[Run] 应用职业角色：狼人（怒痕满后 Q 兽化）");
        }
    }

    void OnDestroy()
    {
        if (playerHealth != null) playerHealth.OnDeath -= OnPlayerDeath;
        UnsubscribeBossRoom();
    }

    // ---------- Boss 结算 ----------

    private void SubscribeBossRoom()
    {
        bossRoomSubscribed = dungeonManager.BossRoom;
        if (bossRoomSubscribed != null) bossRoomSubscribed.OnRoomCleared += OnBossCleared;
    }

    private void UnsubscribeBossRoom()
    {
        if (bossRoomSubscribed != null) bossRoomSubscribed.OnRoomCleared -= OnBossCleared;
        bossRoomSubscribed = null;
    }

    private void OnBossCleared(Room room)
    {
        // v1.1.52：消费固定仪式厅的最终 SpawnCells 插槽，并做 NonAlloc 物理复核；
        // 不再使用 world-X 的 Center±1.5，也不让结算位置受地图 seed 影响。
        Vector3? reservedPosition = null;
        bool chestSpawned = false;
        bool portalSpawned = false;
        if (rewardChestPrefab != null)
        {
            if (TryResolveBossRewardPosition(room, portal: false, reservedPosition,
                out Vector3 chestPosition))
            {
                Instantiate(rewardChestPrefab, chestPosition, Quaternion.identity, room.ContentRoot);
                reservedPosition = chestPosition;
                chestSpawned = true;
            }
        }
        if (portalPrefab != null)
        {
            // 宝箱/传送门的交互碰撞是 Trigger，不参与 NonAlloc 实体检测；显式保留最小间距，
            // 避免两个独立 fallback 抽到同一 SpawnCell 后完全重叠。
            if (TryResolveBossRewardPosition(room, portal: true, reservedPosition,
                out Vector3 portalPosition))
            {
                GameObject portal = Instantiate(portalPrefab, portalPosition, Quaternion.identity, room.ContentRoot);
                portal.GetComponent<PortalInteractable>().Init(this);
                portalSpawned = true;
            }
        }
        Debug.Log($"[Run] 第 {FloorNumber} 层 Boss 已清空：宝箱={chestSpawned}，传送门={portalSpawned}");
    }

    private static bool TryResolveBossRewardPosition(Room room, bool portal,
        Vector3? reservedPosition, out Vector3 position)
    {
        if (BossRitualRoomDecorator.TryGetRewardSocket(
            room, portal, out Vector3 fixedPosition, reservedPosition))
        {
            position = fixedPosition;
            return true;
        }

        // 极端情况（玩家/残留实体同时占满三组插槽）：仍只从最终 RoomPlan 白名单取点，
        // 常量随机流保证不受地图 seed 影响；TryFind 内含距门与 NonAlloc 检查。
        int salt = portal ? 0x50A7A1 : 0x0C4E57;
        var rng = new System.Random(BossRitualRoomTemplate.ContentSeed ^ salt);
        for (int attempt = 0; attempt < 4; attempt++)
            if (SpawnPositionHelper.TryFind(room, rng, out Vector3 safePosition)
                && (!reservedPosition.HasValue
                    || Vector3.Distance(safePosition, reservedPosition.Value)
                        >= BossRitualRoomDecorator.RewardMinSeparation))
            {
                position = safePosition;
                return true;
            }

        // 进度关键物不能静默漏刷。若动态实体暂时占满候选，最后仍使用模板产生的 SpawnCell，
        // 但继续遵守宝箱/传送门互斥；下一物理帧实体移动后即可正常交互。
        if (BossRitualRoomDecorator.TryGetRewardSocket(room, portal, out Vector3 planFallback,
            reservedPosition, requirePhysicalClear: false))
        {
            Debug.LogWarning($"[Run] Boss {(portal ? "传送门" : "宝箱")}安全插槽暂被占用，使用固定 Plan 插槽保底。");
            position = planFallback;
            return true;
        }

        Debug.LogError($"[Run] Boss {(portal ? "传送门" : "宝箱")}无任何 RoomPlan 安全插槽；固定房合同已损坏。");
        position = default;
        return false;
    }

    // ---------- 楼层切换 ----------

    /// <summary>v2.0.7：延迟 1.2s 投放本 Run 必得碎片（玩家先落地看清房间，再读文本）。</summary>
    private System.Collections.IEnumerator PlayRequiredNarrative()
    {
        yield return new WaitForSeconds(1.2f);
        var fragment = NarrativeService.NextRequiredProgress();
        if (fragment != null) NarrativePanelUI.Show(new[] { fragment });
    }

    public void NextFloor()
    {
        // v2.0.6 星蓝币过层结算（V2 §13.1）：击败 Boss 过层 = 随身 100% 封存守灯厅
        if (playerStats != null && playerStats.Coins > 0)
        {
            int banked = StarCoinBank.BankOnBossClear(playerStats.Coins);
            playerStats.ResetCoins();
            Debug.Log($"[Run] 星蓝币过层封存 +" + banked + "（守灯厅累计 " + StarCoinBank.Banked + "）");
        }
        UnsubscribeBossRoom();
        dungeonManager.Cleanup();
        FloorNumber++;
        dungeonManager.FloorNumber = FloorNumber;
        dungeonManager.Generate(FloorSeed());   // 生成 + 传送玩家 + 相机 Snap（v0.5.0 链路）
        SubscribeBossRoom();
        Debug.Log($"[Run] 进入第 {FloorNumber} 层（玩家 HP/护甲保留）");
    }

    private int FloorSeed() => MainSeed + FloorNumber * 104729;   // 质数步长，每层可复现且互不雷同

    // ---------- 死亡重开 ----------

    private void OnPlayerDeath() => StartCoroutine(RestartRun());

    /// <summary>
    /// v0.6.2 阶段 C（R4）：死亡 → 2 秒后加载准备场景。
    /// 职业角色保留；武器恢复为该角色基础武器；道具/宠物清空（随场景销毁）。
    /// HP/状态重置由准备场景的新玩家实例天然满足，旧场景对象随卸载销毁，无需手动清理。
    /// </summary>
    private IEnumerator RestartRun()
    {
        yield return new WaitForSeconds(restartDelay);
        RunStateCarrier.Ensure().ResetWeaponToCharacterDefault();
        CharacterSelectUI.Close();   // 防御：静态 UI 状态不残留到新场景
        Debug.Log("[Run] 玩家死亡：返回准备场景");
        SceneManager.LoadScene(prepSceneName);
    }

    // ---------- 编辑器调试（验收辅助，仅 Editor） ----------

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Dungeon/Debug Clear Boss Room")]
    private static void DebugClearBossRoom()
    {
        var mgr = FindAnyObjectByType<DungeonManager>();
        Room boss = mgr != null ? mgr.BossRoom : null;
        if (boss == null) { Debug.LogWarning("[Run] Debug：当前无 Boss 房"); return; }
        boss.Enter();   // 模拟玩家进房（Active），否则清房条件不触发
        foreach (EnemyHealth eh in boss.GetComponentsInChildren<EnemyHealth>())
            eh.TakeDamage(float.MaxValue);
        Debug.Log("[Run] Debug：Boss 房敌人已清空");
    }

    [UnityEditor.MenuItem("Tools/Dungeon/Debug Kill Player")]
    private static void DebugKillPlayer()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc == null) { Debug.LogWarning("[Run] Debug：未找到玩家"); return; }
        pc.GetHealth().TakeDamage(float.MaxValue);
    }

    [UnityEditor.MenuItem("Tools/Dungeon/Debug Apply Werewolf Visual")]
    private static void DebugWerewolfVisual()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        var fa = pc != null ? pc.GetComponent<FrameAnimator>() : null;
        if (fa == null) { Debug.LogWarning("[Run] Debug：未找到玩家 FrameAnimator"); return; }
        RunStateCarrier.Ensure().SetPlayableCharacter(PlayableCharacterId.Werewolf);
        PlayableCharacterDefinition definition = PlayableCharacterCatalog.Get(PlayableCharacterId.Werewolf);
        pc.GetStats().ApplyPlayableCharacter(definition);
        CharacterSelectUI.ApplyCharacterRuntime(pc.gameObject, PlayableCharacterId.Werewolf);
        WerewolfTransformation transformation = WerewolfTransformation.EnsureOn(pc.gameObject);
        transformation.Rage?.SetPassiveRecoveryEnabled(true);
        WerewolfDash.EnsureOn(pc.gameObject);   // v1.1.42 狼人冲刺
    }

    [UnityEditor.MenuItem("Tools/Dungeon/Debug Toggle Beast")]
    private static void DebugToggleBeast()
    {
        var wt = FindAnyObjectByType<WerewolfTransformation>();
        if (wt == null) { Debug.LogWarning("[Run] Debug：未找到 WerewolfTransformation（先 Apply Werewolf Visual）"); return; }
        if (!wt.IsBeast)
            wt.Rage?.DebugFill();
        wt.Toggle();
    }
#endif
}
