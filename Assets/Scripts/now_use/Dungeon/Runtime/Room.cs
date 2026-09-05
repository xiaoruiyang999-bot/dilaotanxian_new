using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物轮次提供者（v1.1.46）：Room 清房前询问——还有未到来的波次则请求刷出，
/// 不进入 Cleared。实现方（RoomWaveController）须保证 RequestNextWave 幂等
///（延迟刷出途中重复调用无副作用）。
/// </summary>
public interface IWaveProvider
{
    /// <summary>是否还有未到来的波次（含延迟刷出途中的波）。</summary>
    bool HasPendingWave { get; }
    /// <summary>请求刷出下一波（幂等：已在途则忽略）。</summary>
    void RequestNextWave();
}

/// <summary>
/// 房间（v0.5.1 完整版）：状态机 Unvisited/Active/Cleared + 门管理 + 敌人注册与清房判定。
/// 低耦合：不认识 EnemyAI，只认「注册了若干个会死的东西」（EnemyHealth.OnDeath 计数）。
/// 休眠制：战斗房敌人未进房时**可见但不动**（禁用 EnemyAI/EnemyCombat + 刚体冻结），
/// 玩家能透过门洞看到房内敌人；进房瞬间唤醒。
/// v1.1.46：波次扩展点——清房判定前询问 IWaveProvider（怪物轮次，见 RoomWaveController）。
/// </summary>
public class Room : MonoBehaviour
{
    /// <summary>布局分配的稳定 ID（同 seed 重生成保持一致）。</summary>
    public int Id { get; private set; }
    public RoomType Type { get; private set; }
    public int DistanceFromStart { get; private set; }
    /// <summary>世界边界（内部可行走区域）。唯一边界来源：布局算术得出，禁止从 Tilemap 反算。</summary>
    public Rect Bounds { get; private set; }
    public Vector2 Center => Bounds.center;

    public RoomState State { get; private set; } = RoomState.Unvisited;
    public RoomClearCondition ClearCondition { get; private set; }
    /// <summary>内容挂载点：敌人/障碍物/装饰挂在其下；战斗房初始 SetActive(false) 休眠。</summary>
    public Transform ContentRoot { get; private set; }

    /// <summary>首次进入（Unvisited → Active）时触发（风格同 WeaponHitbox.OnHit / EnemyHealth.OnDeath）。</summary>
    public System.Action<Room> OnRoomEntered;
    /// <summary>清房（→ Cleared）时触发。v0.5.4 Boss 结算监听此事件。</summary>
    public System.Action<Room> OnRoomCleared;

    private readonly List<EnemyHealth> enemies = new List<EnemyHealth>();
    private readonly List<Door> doors = new List<Door>();
    private readonly Dictionary<EnemyHealth, RigidbodyConstraints2D> enemyConstraints =
        new Dictionary<EnemyHealth, RigidbodyConstraints2D>();
    private readonly Dictionary<EnemyHealth, System.Action> enemyDeathHandlers =
        new Dictionary<EnemyHealth, System.Action>();
    /// <summary>本房间的门（只读）。生成位置规则（距门 ≥2.5 格）使用。</summary>
    public IReadOnlyList<Door> Doors => doors;

    // v1.1.46：由最终 RoomPlan 提供的内容生成白名单。null 仅用于旧场景/手工测试房，
    // 空列表则表示本房确实没有安全落点，不能退回矩形随机而刷进空洞。
    private IReadOnlyList<Vector2Int> spawnCells;
    public bool HasSpawnGrid => spawnCells != null;

    /// <summary>波次提供者（v1.1.46 怪物轮次；空 = 单波清房，行为与旧版一致）。</summary>
    private IWaveProvider waveProvider;

    public void Init(int id, RoomType type, Rect bounds, RoomClearCondition clearCondition,
        Transform contentRoot, int distanceFromStart = 0,
        IReadOnlyList<Vector2Int> validSpawnCells = null)
    {
        Id = id;
        Type = type;
        Bounds = bounds;
        ClearCondition = clearCondition;
        ContentRoot = contentRoot;
        DistanceFromStart = Mathf.Max(0, distanceFromStart);
        spawnCells = validSpawnCells;
    }

    /// <summary>从最终布局白名单均匀抽一个格；世界位置换算由 SpawnPositionHelper 统一完成。</summary>
    public bool TryGetSpawnCell(System.Random rng, out Vector2Int cell)
    {
        if (rng != null && spawnCells != null && spawnCells.Count > 0)
        {
            cell = spawnCells[rng.Next(spawnCells.Count)];
            return true;
        }
        cell = default;
        return false;
    }

    /// <summary>登记门（Builder 建门时调用，相邻两个房间各登记一次）。</summary>
    public void RegisterDoor(Door door)
    {
        if (door != null && !doors.Contains(door)) doors.Add(door);
    }

    /// <summary>登记波次提供者（v1.1.46 怪物轮次；DungeonBuilder 对掷中轮次的战斗房调用）。</summary>
    public void RegisterWaveProvider(IWaveProvider provider)
    {
        waveProvider = provider;
    }

    /// <summary>登记敌人并订阅死亡（v0.5.1 手工摆放 / v0.5.2 Spawner 共用此入口）。
    /// 战斗房未进入时登记的敌人立即休眠（可见但不动）。</summary>
    public void RegisterEnemy(EnemyHealth enemy)
    {
        if (enemy == null || enemies.Contains(enemy)) return;
        enemies.Add(enemy);
        System.Action deathHandler = () => NotifyEnemyDied(enemy);
        enemyDeathHandlers[enemy] = deathHandler;
        enemy.OnDeath += deathHandler;
        var rb = enemy.GetComponent<Rigidbody2D>();
        if (rb != null) enemyConstraints[enemy] = rb.constraints;
        if (ClearCondition == RoomClearCondition.AllEnemiesDead && State == RoomState.Unvisited)
            SetEnemyDormant(enemy, true);
    }

    private void LateUpdate()
    {
        if (State != RoomState.Active || ClearCondition != RoomClearCondition.AllEnemiesDead)
            return;

        // OnDeath 是最快路径；这里负责清除已销毁或已死亡但漏发事件的残留引用，
        // 避免 enemies.Count 永远无法归零而导致房门永久关闭。
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyHealth enemy = enemies[i];
            if (enemy == null)
            {
                enemies.RemoveAt(i);
                continue;
            }

            if (enemy.IsDead)
                RemoveEnemy(enemy);
        }

        TryClearRoom();
    }

    /// <summary>休眠开关：禁用 AI/Combat + 冻结刚体；精灵与碰撞保持原样（可见、可被武器打到）。</summary>
    private void SetEnemyDormant(EnemyHealth enemy, bool dormant)
    {
        if (enemy == null) return;
        var ai = enemy.GetComponent<EnemyAI>();
        if (ai != null) ai.enabled = !dormant;
        var combat = enemy.GetComponent<EnemyCombat>();
        if (combat != null) combat.enabled = !dormant;
        var rb = enemy.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            if (dormant)
                rb.constraints = RigidbodyConstraints2D.FreezeAll;
            else if (enemyConstraints.TryGetValue(enemy, out RigidbodyConstraints2D originalConstraints))
                rb.constraints = originalConstraints;
        }
    }

    private void SetAllEnemiesDormant(bool dormant)
    {
        foreach (EnemyHealth e in enemies) SetEnemyDormant(e, dormant);
    }

    /// <summary>玩家首次进入（RoomTrigger 调用）。</summary>
    public void Enter()
    {
        if (State != RoomState.Unvisited) return;
        State = RoomState.Active;
        OnRoomEntered?.Invoke(this);

        if (ClearCondition == RoomClearCondition.None)
        {
            SetCleared();   // 无战斗房：直接完成，门常开
            return;
        }

        // AllEnemiesDead：唤醒敌人 + 关门
        SetAllEnemiesDormant(false);
        RefreshDoors();
        if (enemies.Count == 0) SetCleared(); // 防御：空战斗房直接完成
    }

    /// <summary>敌人死亡回调（注册时以闭包订阅）。</summary>
    public void NotifyEnemyDied(EnemyHealth enemy)
    {
        RemoveEnemy(enemy);
        TryClearRoom();
    }

    private void RemoveEnemy(EnemyHealth enemy)
    {
        if (enemy != null && enemyDeathHandlers.TryGetValue(enemy, out System.Action deathHandler))
            enemy.OnDeath -= deathHandler;

        enemies.Remove(enemy);
        enemyConstraints.Remove(enemy);
        enemyDeathHandlers.Remove(enemy);
    }

    private void TryClearRoom()
    {
        if (State != RoomState.Active
            || ClearCondition != RoomClearCondition.AllEnemiesDead
            || enemies.Count != 0) return;

        // v1.1.46 波次扩展点：本波全灭但还有未到来的波 → 请求刷出，房门继续关着
        if (waveProvider != null && waveProvider.HasPendingWave)
        {
            waveProvider.RequestNextWave();   // 幂等：延迟刷出途中重复调用无副作用
            return;
        }
        SetCleared();
    }

    private void OnDestroy()
    {
        foreach (KeyValuePair<EnemyHealth, System.Action> pair in enemyDeathHandlers)
            if (pair.Key != null) pair.Key.OnDeath -= pair.Value;

        enemyDeathHandlers.Clear();
    }

    private void SetCleared()
    {
        State = RoomState.Cleared;
        RefreshDoors();
        OnRoomCleared?.Invoke(this);
    }

    private void RefreshDoors()
    {
        doors.RemoveAll(d => d == null);   // v1.1.37：楼层清理后的死门引用直接摘除（MissingSource 族根治）
        foreach (Door d in doors) d.RefreshState();
    }

    /// <summary>调试：杀光房内所有登记敌人（验收「杀光开门」用）。</summary>
    [ContextMenu("Debug Kill All Enemies")]
    private void DebugKillAllEnemies()
    {
        for (int i = enemies.Count - 1; i >= 0; i--)
            if (enemies[i] != null) enemies[i].TakeDamage(float.MaxValue);
    }
}
