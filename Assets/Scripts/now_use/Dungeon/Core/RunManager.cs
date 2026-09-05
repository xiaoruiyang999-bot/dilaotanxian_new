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
        if (redirectToPrepWhenNoClass && RunStateCarrier.Ensure().LastChosenClass == null)
        {
            Debug.Log("[Run] 未选职业：重定向到准备场景（统一入口）");
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
    }

    /// <summary>
    /// 从跨场景载体应用职业/武器（v0.6.2 阶段 C：准备场景选定的配置应用到地牢玩家）。
    /// 未选择（旧场景直连测试）时保持现状。
    /// </summary>
    private void ApplyLoadoutFromCarrier()
    {
        RunStateCarrier carrier = RunStateCarrier.Ensure();

        if (carrier.LastChosenClass != null)
        {
            playerStats.ApplyClass(carrier.LastChosenClass);
            Debug.Log($"[Run] 应用职业：{carrier.LastChosenClass.DisplayName}");
        }

        if (carrier.LastWeapon != null)
        {
            PlayerWeaponHolder holder = player.GetComponent<PlayerWeaponHolder>();
            if (holder == null)
                holder = player.gameObject.AddComponent<PlayerWeaponHolder>();
            holder.Equip(carrier.LastWeapon);   // 新玩家 Current 为空，不会触发掉落
            Debug.Log($"[Run] 应用武器：{carrier.LastWeapon.DisplayName}");
        }

        // v1.0.6 角色外形（与职业独立）：狼人 = 视觉外形 + 变身能力（T=兽化，v1.0.9 还原）
        if (carrier.ChosenCharacter == CharacterSkin.Werewolf)
        {
            FrameAnimator animator = player.GetComponent<FrameAnimator>();
            if (animator != null) animator.SetWerewolfVisual(true);
            WerewolfTransformation.EnsureOn(player.gameObject);
            WerewolfDash.EnsureOn(player.gameObject);   // v1.1.42 狼人冲刺
            Debug.Log("[Run] 应用外形：狼人（T=兽化）");
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

    /// <summary>
    /// v0.6.2 阶段 C（R4）：死亡 → 2 秒后加载准备场景。
    /// 职业保留（RunStateCarrier.LastChosenClass，选择 UI 预置高亮可改选）；
    /// 武器不保留（清空载体，准备场景展台已刷新需重拿）；道具/宠物清空（随场景销毁）。
    /// HP/状态重置由准备场景的新玩家实例天然满足，旧场景对象随卸载销毁，无需手动清理。
    /// </summary>
    private IEnumerator RestartRun()
    {
        yield return new WaitForSeconds(restartDelay);
        RunStateCarrier.Ensure().ClearWeapon();
        ClassSelectUI.Close();   // 防御：静态 UI 状态不残留到新场景
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
        RunStateCarrier.Ensure().SetCharacter(CharacterSkin.Werewolf);
        fa.SetWerewolfVisual(true);
        WerewolfTransformation.EnsureOn(pc.gameObject);   // v1.0.9：含变身能力（T=兽化）
        WerewolfDash.EnsureOn(pc.gameObject);   // v1.1.42 狼人冲刺
    }

    [UnityEditor.MenuItem("Tools/Dungeon/Debug Toggle Beast")]
    private static void DebugToggleBeast()
    {
        var wt = FindAnyObjectByType<WerewolfTransformation>();
        if (wt == null) { Debug.LogWarning("[Run] Debug：未找到 WerewolfTransformation（先 Apply Werewolf Visual）"); return; }
        wt.Toggle();   // 等价按 T：狼↔兽化全链路（演出/帧组/血量/数值）
    }
#endif
}
