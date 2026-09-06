using UnityEngine;

/// <summary>
/// 具体事件类型定义：覆盖项目核心事件通道。
/// 在 Assets/Resources/Events/ 下创建实例（如 EnemyDeath、RoomCleared），
/// 代码中通过 GameEvents静态类获取引用，避免硬编码路径。
/// 注：命名空间用 GameEventTypes（编译修复）——不能与同文件的全局静态类 GameEvents 同名（CS0101）。
/// </summary>
namespace GameEventTypes
{
    // ========== 无参数事件 ==========

    [CreateAssetMenu(fileName = "OnEnemyDied", menuName = "Events/Enemy/Enemy Died")]
    public class EnemyDiedEvent : GameEvent { }

    [CreateAssetMenu(fileName = "OnRoomCleared", menuName = "Events/Room/Room Cleared")]
    public class RoomClearedEvent : GameEvent { }

    [CreateAssetMenu(fileName = "OnPlayerDied", menuName = "Events/Player/Player Died")]
    public class PlayerDiedEvent : GameEvent { }

    [CreateAssetMenu(fileName = "OnBossDefeated", menuName = "Events/Boss/Boss Defeated")]
    public class BossDefeatedEvent : GameEvent { }

    [CreateAssetMenu(fileName = "OnFloorCompleted", menuName = "Events/Dungeon/Floor Completed")]
    public class FloorCompletedEvent : GameEvent { }

    // ========== 带参数事件 ==========

    [CreateAssetMenu(fileName = "OnHealthChanged", menuName = "Events/Health/Health Changed")]
    public class HealthChangedEvent : GameEvent<FloatPair> { }

    [CreateAssetMenu(fileName = "OnDamageDealt", menuName = "Events/Combat/Damage Dealt")]
    public class DamageDealtEvent : GameEvent<DamageInfo> { }

    [CreateAssetMenu(fileName = "OnCoinCollected", menuName = "Events/Player/Coin Collected")]
    public class CoinCollectedEvent : GameEvent<int> { }

    [CreateAssetMenu(fileName = "OnRoomEntered", menuName = "Events/Room/Room Entered")]
    public class RoomEnteredEvent : GameEvent<int> { } // room id
}

/// <summary>浮点数对：用于 (current, max) 类事件。</summary>
[System.Serializable]
public struct FloatPair
{
    public float current;
    public float max;

    public FloatPair(float current, float max)
    {
        this.current = current;
        this.max = max;
    }
}

/// <summary>伤害信息：用于伤害结算事件。</summary>
[System.Serializable]
public struct DamageInfo
{
    public float amount;
    public bool isCrit;
    public bool isTrueDamage;
    public GameObject target;
    public GameObject attacker;

    public DamageInfo(float amount, bool isCrit, bool isTrueDamage, GameObject target, GameObject attacker)
    {
        this.amount = amount;
        this.isCrit = isCrit;
        this.isTrueDamage = isTrueDamage;
        this.target = target;
        this.attacker = attacker;
    }
}

/// <summary>
/// 事件通道静态访问器：避免 Resources.Load 硬编码。
/// 使用方式：GameEvents.Instance.EnemyDied.Raise();
/// 或在 ScriptableObject 引用中直接拖拽。
/// </summary>
public static class GameEvents
{
    private static GameEventsInstance instance;

    public static GameEventsInstance Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<GameEventsInstance>("Events/GameEventsInstance");
                if (instance == null)
                    Debug.LogWarning("[GameEvents] GameEventsInstance not found in Resources/Events/");
            }
            return instance;
        }
    }
}
