using UnityEngine;

/// <summary>
/// 怪物战斗行为类型（v0.5.4.2）。
/// </summary>
public enum EnemyBehaviorType
{
    /// <summary>近战莽夫：直线冲脸，贴身攻击。当前默认行为。</summary>
    Melee,
    /// <summary>远程射手：保持 preferredDistance 距离，远程射击投射物。</summary>
    Ranged,
    /// <summary>游击型：突进攻击后自动后撤，循环。</summary>
    Skirmisher,
    /// <summary>冲锋型：蓄力后直线冲锋（Active阶段带位移），撞墙/撞人停止。</summary>
    Charger,
    /// <summary>召唤师：不直接攻击，定期召唤小怪。</summary>
    Summoner
}

/// <summary>
/// 怪物行为配置（v0.5.4.2）。
/// 一份资产定义一种怪物的完整战斗风格。
/// 改行为只改本资产 + 配 AttackDataSet，不改 AI/Combat 代码。
/// </summary>
[CreateAssetMenu(menuName = "Combat/Enemy Behavior Config", fileName = "EnemyBehaviorConfig")]
public class EnemyBehaviorConfig : ScriptableObject
{
    [Header("行为类型")]
    [Tooltip("选择一种战斗行为模式。Melee=近战（默认行为）。")]
    public EnemyBehaviorType behaviorType = EnemyBehaviorType.Melee;

    [Header("Ranged 远程配置")]
    [Tooltip("远程怪力图保持的距离区间（x=最小距离，y=最大距离）。玩家进入min距离时后退，退出max距离时靠近。")]
    public Vector2 preferredDistance = new Vector2(4f, 7f);

    [Tooltip("后退速度倍率（相对于 moveSpeed）")]
    [Range(0.5f, 2f)] public float retreatSpeedMultiplier = 1f;

    [Header("Skirmisher 游击配置")]
    [Tooltip("攻击后强制后退的时间（秒）")]
    [Range(0.1f, 2f)] public float retreatDuration = 0.5f;

    [Tooltip("攻击后强制后退的速度倍率")]
    [Range(0.5f, 3f)] public float skirmishRetreatSpeed = 1.5f;

    [Header("Charger 冲锋配置")]
    [Tooltip("冲锋时的速度倍率（相对于 moveSpeed）")]
    [Range(1f, 5f)] public float chargeSpeedMultiplier = 3f;

    [Tooltip("冲锋时是否忽略其他敌人碰撞（建议开启）")]
    public bool chargeIgnoreEnemies = true;

    [Header("Summoner 召唤配置")]
    [Tooltip("召唤的敌人 prefab（小兵）")]
    public GameObject minionPrefab;

    [Tooltip("每次召唤数量")]
    [Range(1, 5)] public int minionsPerSummon = 2;

    [Tooltip("召唤冷却（秒）")]
    [Range(3f, 20f)] public float summonCooldown = 8f;

    [Tooltip("本场同时存活的小兵上限")]
    [Range(1, 10)] public int maxMinionsAlive = 4;

    [Tooltip("召唤距离（小兵出生在以召唤师为中心的此半径内）")]
    [Range(1f, 4f)] public float summonRadius = 2f;

    [Header("通用配置（所有行为类型）")]
    [Tooltip("停止移动时是否面朝目标（Ranged/Skirmisher/Summoner 建议开启）")]
    public bool faceTargetWhileIdle = true;

    void OnValidate()
    {
        preferredDistance.x = Mathf.Max(0, preferredDistance.x);
        preferredDistance.y = Mathf.Max(preferredDistance.x + 1f, preferredDistance.y);
        retreatDuration = Mathf.Max(0, retreatDuration);
        chargeSpeedMultiplier = Mathf.Max(1f, chargeSpeedMultiplier);
        summonCooldown = Mathf.Max(1f, summonCooldown);
        maxMinionsAlive = Mathf.Max(1, maxMinionsAlive);
    }
}
