using UnityEngine;

/// <summary>
/// 房间内容配置（计划书五-C）：三张生成表的组合。
/// v0.5.2 所有战斗房共用一份默认 Profile；v0.5.3 起按房间类型由 RoomTypeConfig 选用不同 Profile。
/// 各表可空（空 = 该类内容不生成）。
/// </summary>
[CreateAssetMenu(menuName = "Dungeon/RoomContentProfile")]
public class RoomContentProfile : ScriptableObject
{
    public SpawnTable enemyTable;        // 可空（空 = 无战斗）
    public SpawnTable obstacleTable;     // 可空
    public SpawnTable decorationTable;   // 可空
    [Tooltip("v0.5.3：宝箱/祭坛/补给基座等 walk-over 交互物表（可空）")]
    public SpawnTable interactableTable; // 可空
}
