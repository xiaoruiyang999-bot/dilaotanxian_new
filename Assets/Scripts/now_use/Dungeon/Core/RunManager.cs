using System.Collections;
using UnityEngine;

/// <summary>
/// 楼层循环总控（计划书五-E / v0.5.4）：持有 floorNumber 与主 seed；
/// 监听 Boss 房 OnRoomCleared → 房中心生成奖励宝箱 + 传送门；
/// 传送门 → NextFloor（单场景重建：Cleanup → floorSeed → Generate）；
/// 玩家死亡 → 2 秒后回第 1 层重开（HP/护甲回满、颜色恢复、主 seed 重roll）。
/// 对玩家只订阅事件、调用公开方法，不改其逻辑。
/// </summary>
public class RunManager : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private DungeonManager dungeonManager;
    [SerializeField] private GameObject rewardChestPrefab;
    [SerializeField] private GameObject portalPrefab;

    [Header("死亡重开")]
    [SerializeField] private float restartDelay = 2f;

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
        SubscribeBossRoom();
        Debug.Log($"[Run] 楼层循环启动：floor=1 mainSeed={MainSeed}");
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
        // 奖励宝箱与传送门在房中心左右错开，挂在 contentRoot 下（随 dungeonRoot 一并清理）
        Vector3 c = room.Center;
        if (rewardChestPrefab != null)
            Instantiate(rewardChestPrefab, c + new Vector3(-1.5f, 0f, 0f), Quaternion.identity, room.ContentRoot);
        if (portalPrefab != null)
        {
            GameObject portal = Instantiate(portalPrefab, c + new Vector3(1.5f, 0f, 0f), Quaternion.identity, room.ContentRoot);
            portal.GetComponent<PortalInteractable>().Init(this);
        }
        Debug.Log($"[Run] 第 {FloorNumber} 层 Boss 已清空：奖励宝箱与传送门已生成");
    }

    // ---------- 楼层切换 ----------

    public void NextFloor()
    {
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
    private IEnumerator RestartRun()
    {
        yield return new WaitForSeconds(restartDelay);
        UnsubscribeBossRoom();
        dungeonManager.Cleanup();
        FloorNumber = 1;
        dungeonManager.FloorNumber = 1;
        MainSeed = new System.Random().Next();   // 主 seed 重roll（运行时一次性事件，不进生成流）
        dungeonManager.Generate(MainSeed);
        playerHealth.ResetHealth();              // HP 回满，IsDead 解除
        playerStats.ModifyArmor(playerStats.MaxArmor - playerStats.CurrentArmor);   // 护甲回满
        player.Respawn();                        // 颜色恢复 + 速度清零
        SubscribeBossRoom();
        Debug.Log("[Run] 玩家死亡：回到第 1 层，状态已重置");
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
#endif
}
