using UnityEngine;

/// <summary>
/// 房间运行时数据（v0.5.0 基础版：无状态机，仅持有布局数据与世界边界）。
/// v0.5.1 升级：状态机 Unvisited/Active/Cleared + 门管理 + 敌人注册与清房判定。
/// </summary>
public class Room : MonoBehaviour
{
    /// <summary>布局分配的稳定 ID（同 seed 重生成保持一致）。</summary>
    public int Id { get; private set; }
    public RoomType Type { get; private set; }
    /// <summary>世界边界（内部可行走区域）。唯一边界来源：布局算术得出，禁止从 Tilemap 反算。</summary>
    public Rect Bounds { get; private set; }
    public Vector2 Center => Bounds.center;

    public void Init(int id, RoomType type, Rect bounds)
    {
        Id = id;
        Type = type;
        Bounds = bounds;
    }
}
