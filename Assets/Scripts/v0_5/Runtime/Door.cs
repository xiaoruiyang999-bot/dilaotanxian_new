using UnityEngine;

/// <summary>
/// 门：碰撞体 + 色块视觉（程序员美术）。
/// 状态由相邻两个 Room 推导——仅当两侧房间都不是 Active 时才开（计划书五-B：避免隔壁 Active 时门被误开）。
/// 外部只调 RefreshState()，本类是门状态的唯一真源。
/// </summary>
public class Door : MonoBehaviour
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private BoxCollider2D solidCollider;

    private Room roomA, roomB;

    /// <summary>初始化相邻房间与门洞尺寸（世界单位：E-W 门为 (2, doorWidth)，N-S 门为 (doorWidth, 2)）。</summary>
    public void Init(Room a, Room b, Vector2 size)
    {
        roomA = a;
        roomB = b;
        // 1×1 白块精灵随 scale 铺满门洞；碰撞体强制 size=1 随 scale 同步（防 prefab 上尺寸漂移）
        transform.localScale = new Vector3(size.x, size.y, 1f);
        if (solidCollider != null) solidCollider.size = Vector2.one;
        RefreshState();
    }

    /// <summary>重算开关：两侧房间都非 Active → 开；任一侧 Active → 关。</summary>
    public void RefreshState()
    {
        SetOpen(IsOpen(roomA) && IsOpen(roomB));
    }

    private static bool IsOpen(Room r) => r == null || r.State != RoomState.Active;

    private void SetOpen(bool open)
    {
        if (solidCollider != null) solidCollider.enabled = !open;
        if (visual != null) visual.enabled = !open;
    }
}
