using UnityEngine;

/// <summary>
/// 房间进入触发器：Trigger 覆盖房间内部（四边内缩 0.5 格，保证玩家完全进房后才触发关门）。
/// 只认 Player tag，敌人/道具等其他碰撞体直接忽略。
/// </summary>
public class RoomTrigger : MonoBehaviour
{
    private Room room;

    public void Init(Room room) { this.room = room; }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) room.Enter();
    }
}
