using UnityEngine;

/// <summary>
/// 生成位置合法性工具（三个 Spawner 共用，计划书五-C 规则 1~4）：
/// ① 距墙 ≥1 格；② 距门洞中心 ≥2.5 格（防堵门 + 别刷玩家脸上）；
/// ③ OverlapCircle(r=0.4) 无实体碰撞（useTriggers=false，自动跳过 RoomTrigger 与敌人探测圈）；
/// ④ 最多重试 20 次，失败放弃该个。
/// </summary>
public static class SpawnPositionHelper
{
    private const float WallMargin = 1f;
    private const float DoorMargin = 2.5f;
    private const float OverlapRadius = 0.4f;
    private const int MaxAttempts = 20;

    // Default(墙/门/装饰) + Enemy + Obstacle：墙、敌人、障碍物都算占位。
    // 注意：依赖 Obstacle 层已存在（任务0 先行创建），缺失时 GetMask 报错并返回 0。
    private static int SolidMask => LayerMask.GetMask("Default", "Enemy", "Obstacle");

    private static readonly Collider2D[] overlapBuffer = new Collider2D[4];

    /// <summary>在房间内尝试找一个合法生成点；失败返回 false。</summary>
    public static bool TryFind(Room room, System.Random rng, out Vector3 pos)
    {
        Rect b = room.Bounds;
        var filter = new ContactFilter2D { layerMask = SolidMask, useLayerMask = true, useTriggers = false };

        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            float x = Mathf.Lerp(b.xMin + WallMargin, b.xMax - WallMargin, (float)rng.NextDouble());
            float y = Mathf.Lerp(b.yMin + WallMargin, b.yMax - WallMargin, (float)rng.NextDouble());
            pos = new Vector3(x, y, 0f);

            if (!FarFromDoors(room, pos)) continue;
            if (Physics2D.OverlapCircle(pos, OverlapRadius, filter, overlapBuffer) > 0) continue;

            return true;
        }
        pos = default;
        return false;
    }

    private static bool FarFromDoors(Room room, Vector3 pos)
    {
        foreach (Door d in room.Doors)
        {
            if (d == null) continue;
            if (Vector2.Distance(pos, d.transform.position) < DoorMargin) return false;
        }
        return true;
    }
}
