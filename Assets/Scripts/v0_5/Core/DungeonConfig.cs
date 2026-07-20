using UnityEngine;

/// <summary>
/// 楼层地牢配置（数据驱动唯一来源）。
/// v0.5.0 只用「地图」字段；「特殊房间」v0.5.3 启用；「楼层缩放」v0.5.4 启用。
/// </summary>
[CreateAssetMenu(menuName = "Dungeon/DungeonConfig", fileName = "DungeonConfig")]
public class DungeonConfig : ScriptableObject
{
    [Header("地图")]
    [Tooltip("每层房间数量区间（含起始房与 Boss 房）")]
    public int roomCountMin = 8, roomCountMax = 12;
    [Tooltip("房间内部尺寸（瓦片数），不含四周各 1 格墙")]
    public int roomWidth = 18, roomHeight = 11;
    [Tooltip("门洞宽（瓦片数）")]
    public int doorWidth = 2;

    [Header("特殊房间（v0.5.3 启用）")]
    public int treasureCount = 1;
    public int shopCount = 1;
    public int eventCount = 0;
    [Range(0f, 1f)] public float eliteChance = 0.15f;
    [Tooltip("Boss 房距出生房最小 BFS 距离（尽力满足）")]
    public int bossMinDistance = 3;

    [Header("楼层缩放（v0.5.4 启用）")]
    [Tooltip("每层敌人数量 +N（封顶另定）")]
    public int enemyCountBonusPerFloor = 1;
    [Tooltip("每层敌人 HP ×(1 + N×(floor-1))")]
    public float hpMultiplierPerFloor = 0.15f;
}
