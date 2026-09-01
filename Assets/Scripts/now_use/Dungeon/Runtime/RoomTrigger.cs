using UnityEngine;

/// <summary>
/// 房间进入触发器：Trigger 覆盖房间内部。
/// 只有玩家碰撞体完整进入房间边界后才激活房间，避免门在玩家仍横跨门洞时关闭并把角色夹住。
/// 只认 Player tag，敌人/道具等其他碰撞体直接忽略。
/// </summary>
public class RoomTrigger : MonoBehaviour
{
    private const float BoundsEpsilon = 0.001f;
    private Room room;

    public void Init(Room room) { this.room = room; }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryEnter(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryEnter(other);
    }

    private void TryEnter(Collider2D other)
    {
        if (room == null || room.State != RoomState.Unvisited || !other.CompareTag("Player")) return;
        if (IsFullyInside(room.Bounds, other.bounds)) room.Enter();
    }

    public static bool IsFullyInside(Rect roomBounds, Bounds colliderBounds)
    {
        return colliderBounds.min.x >= roomBounds.xMin - BoundsEpsilon
            && colliderBounds.max.x <= roomBounds.xMax + BoundsEpsilon
            && colliderBounds.min.y >= roomBounds.yMin - BoundsEpsilon
            && colliderBounds.max.y <= roomBounds.yMax + BoundsEpsilon;
    }
}
