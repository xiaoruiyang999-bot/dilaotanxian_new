using UnityEngine;

/// <summary>
/// v2.0.10 批2 Boss 房组合根（修缮计划 §3.3）：
/// 持有 Room、RoomBounds、BossArenaState 与 WorldHazardRoot 的显式上下文。
/// BossRitualRoomDecorator 建完房间后创建；GrandBossBrain.EnsureOn 消费。
/// 消除隐式 Find/运行时补建——缺上下文时 fail-fast。
/// </summary>
public class GrandBossEncounterContext : MonoBehaviour
{
    public Room Room { get; private set; }
    public Rect RoomBounds { get; private set; }
    public BossArenaState Arena { get; private set; }
    public Transform WorldHazardRoot { get; private set; }

    /// <summary>创建组合根（Decorator 建房后调用一次）。</summary>
    public static GrandBossEncounterContext Create(Room room, Transform parent)
    {
        var go = new GameObject("GrandBossEncounterContext");
        go.transform.SetParent(parent, false);
        var ctx = go.AddComponent<GrandBossEncounterContext>();
        ctx.Room = room;
        ctx.RoomBounds = room != null ? room.Bounds : Rect.zero;

        // WorldHazardRoot:预警/月痕/碎石的世界空间挂点(不挂 Boss 下——§4.3)
        var hazard = new GameObject("WorldHazardRoot");
        hazard.transform.SetParent(go.transform, false);
        ctx.WorldHazardRoot = hazard.transform;
        return ctx;
    }

    /// <summary>绑定既有 Arena（RegisterArenaPillars 已创建的场景共享）。</summary>
    public void BindArena(BossArenaState arena)
    {
        Arena = arena;
    }
}
