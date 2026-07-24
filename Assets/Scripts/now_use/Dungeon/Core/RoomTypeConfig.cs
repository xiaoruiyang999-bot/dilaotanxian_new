using UnityEngine;

/// <summary>
/// 房间类型配置（计划书五-D）：一种房间类型一份——地板着色、清房条件、内容 Profile。
/// 全数据驱动：改类型表现/内容只改本资产与 Profile，不改代码。
/// </summary>
[CreateAssetMenu(menuName = "Dungeon/RoomTypeConfig", fileName = "RoomTypeConfig")]
public class RoomTypeConfig : ScriptableObject
{
    public RoomType type;

    [Tooltip("地板着色（alpha=0 = 不着色，保持默认米黄，Combat 用）")]
    public Color floorTint = Color.clear;

    public RoomClearCondition clearCondition = RoomClearCondition.None;

    [Tooltip("该类型房间的内容配置（可空 = 无内容，如 Start 安全房）")]
    public RoomContentProfile contentProfile;
}
