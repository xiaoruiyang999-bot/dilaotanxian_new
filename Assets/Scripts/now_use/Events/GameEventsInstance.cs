using UnityEngine;

/// <summary>
/// 事件通道集中管理器（ScriptableObject 单例）：
/// 存放于 Assets/Resources/Events/GameEventsInstance.asset，
/// 通过 GameEvents.Instance 访问。
/// 
/// 优势：
///   - Inspector 可视化配置所有事件通道
///   - 运行时零查找开销
///   - 跨场景持久（Resources 加载）
/// </summary>
[CreateAssetMenu(fileName = "GameEventsInstance", menuName = "Events/Game Events Instance")]
public class GameEventsInstance : ScriptableObject
{
    [Header("敌人事件")]
    public GameEventTypes.EnemyDiedEvent EnemyDied;

    [Header("房间事件")]
    public GameEventTypes.RoomClearedEvent RoomCleared;
    public GameEventTypes.RoomEnteredEvent RoomEntered;

    [Header("玩家事件")]
    public GameEventTypes.PlayerDiedEvent PlayerDied;
    public GameEventTypes.CoinCollectedEvent CoinCollected;

    [Header("Boss 事件")]
    public GameEventTypes.BossDefeatedEvent BossDefeated;

    [Header("楼层事件")]
    public GameEventTypes.FloorCompletedEvent FloorCompleted;

    [Header("战斗事件")]
    public GameEventTypes.DamageDealtEvent DamageDealt;
    public GameEventTypes.HealthChangedEvent HealthChanged;

    /// <summary>运行时初始化（Resources.Load 时自动调用）。</summary>
    private void OnEnable()
    {
        // 确保所有事件通道非空
        if (EnemyDied == null) Debug.LogWarning("[GameEvents] EnemyDied event not assigned");
        if (RoomCleared == null) Debug.LogWarning("[GameEvents] RoomCleared event not assigned");
        if (PlayerDied == null) Debug.LogWarning("[GameEvents] PlayerDied event not assigned");
    }
}
