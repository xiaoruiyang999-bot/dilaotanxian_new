using System.Collections;
using UnityEngine;

/// <summary>
/// 怪物轮次控制器（v1.1.46）：挂在掷中轮次的战斗房上，实现 Room.IWaveProvider。
/// 流程：第一波全灭 → Room.TryClearRoom 询问 HasPendingWave → RequestNextWave
/// → 延迟 WaveDelay 秒（清场喘息 + 玩家换位窗口）→ EnemySpawner.Spawn 复用整套
/// 按表生成（含楼层缩放/词缀/AI 独立随机源，Active 态注册即活跃）→ 新一波登记进
/// Room 计数，门保持关闭；末波全灭后 HasPendingWave=false，Room 正常 Cleared。
/// 复用即正确：不重写生成逻辑，波次只是"再一次调用 EnemySpawner.Spawn"。
/// 幂等：RequestNextWave 在延迟刷出途中重复调用被 inFlight 拦截（Room 的
/// NotifyEnemyDied 与 LateUpdate 兜底两条路径都会问）。
/// 楼层重建：挂 Room 同 GO（dungeonRoot 下），ClearAll 销毁即协程终止，无残留。
/// </summary>
public class RoomWaveController : MonoBehaviour, IWaveProvider
{
    private const float WaveDelay = 0.9f;   // 波间喘息：最后一只倒下到增援登场的间隔

    private Room room;
    private SpawnTable table;
    private int floorNumber;
    private DungeonConfig config;
    private System.Random rng;
    private int remainingWaves;
    private bool inFlight;   // 延迟刷出途中（已消费本轮请求，等待登场）

    public bool HasPendingWave => remainingWaves > 0;

    /// <summary>初始化（DungeonBuilder 挂载后调用一次）。waves=追加波数（两轮制传 1）。</summary>
    public void Setup(Room room, SpawnTable table, int floorNumber, DungeonConfig config,
        System.Random rng, int waves)
    {
        this.room = room;
        this.table = table;
        this.floorNumber = floorNumber;
        this.config = config;
        this.rng = rng;
        this.remainingWaves = Mathf.Max(0, waves);
    }

    public void RequestNextWave()
    {
        if (remainingWaves <= 0 || inFlight) return;
        remainingWaves--;
        inFlight = true;
        StartCoroutine(SpawnWaveRoutine());
    }

    private IEnumerator SpawnWaveRoutine()
    {
        yield return new WaitForSeconds(WaveDelay);

        inFlight = false;
        if (room == null || table == null) yield break;   // 楼层已重建/表被毁：静默放弃

        EnemySpawner.Spawn(room, table, rng, floorNumber, config);
        Debug.Log($"[Wave] 房间 {room.Id} 增援登场（第 2 波，位置规则复用 SpawnPositionHelper）");
    }
}
