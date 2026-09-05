using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 木桶酒桶装饰生成器（v1.1.45 接入）
/// - 普通装饰木桶(长边 &lt;0.85单位)：Trigger碰撞体（可穿过，可被攻击打碎），sortingOrder3，挂载Room.ContentRoot
/// - 大型木箱堆(≥0.85单位,barrel_18~23 实测 0.93~1.05)：实体障碍物，实体碰撞盒、Obstacle层、阻挡通行 + ObstacleHealth可破坏
///   （阈值从石块的 1.2 收至 0.85：木桶素材池实测最大 ≈1.05，1.2 会让实体桶分支永不触发）
/// - 点位复用SpawnPositionHelper，自动避开墙体、门、实体重叠；实体件另避让房内骨架格
///   （v1.1.44 R22：不回堵玩家口径验证留出的路网/入口，重试 ≤6；Trigger 装饰桶可穿过，无需避让）
/// 素材存放路径：Resources/Art/Decor/Barrels ，全部透明PNG精灵（barrel_NN 自动入池，85px@PPU85）
/// </summary>
public static class BarrelDecorSpawner
{
    private const string ResourceDir = "Art/Decor/Barrels";

    private const float LargeThresholdUnits = 0.85f;
    private const int BarrelHp = 3;
    private const float ColliderShrink = 0.8f;
    private const float Brightness = 1.75f;
    private const int AvoidRetryMax = 6;   // 实体件落点撞骨架格的换点重试上限
    // v1.1.46 纯代码整体缩放：视觉与碰撞体同比（BoxCollider2D.size 是局部量，随 localScale 放大）。
    // 1=素材原尺寸（58~89px@PPU85 → 0.68~1.05 单位）；2=放大一倍（1.4~2.1 单位，与角色/房间尺度更配）
    private const float SizeScale = 2f;

    private static Sprite[] smallBarrels, largeBarrels;
    private static bool loadFailed;

    public static void Spawn(Room room, System.Random rng, HashSet<Vector2Int> avoidCells = null)
    {
        if (room == null || rng == null || !Load()) return;

        // 普通装饰木桶：1~4个每房间
        int smallCount = rng.Next(1, 5);
        for (int i = 0; i < smallCount; i++)
        {
            if (!SpawnPositionHelper.TryFind(room, rng, out var pos)) continue;
            CreateBarrel(room, smallBarrels[rng.Next(smallBarrels.Length)], pos, rng, large: false);
        }

        // 障碍物大型木箱堆：0~2个每房间（避让骨架格，防回堵路网）
        int largeCount = rng.Next(0, 3);
        for (int i = 0; i < largeCount; i++)
        {
            for (int t = 0; t < AvoidRetryMax; t++)
            {
                if (!SpawnPositionHelper.TryFind(room, rng, out var pos)) break;
                if (avoidCells != null
                    && avoidCells.Contains(new Vector2Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y)))) continue;
                CreateBarrel(room, largeBarrels[rng.Next(largeBarrels.Length)], pos, rng, large: true);
                break;
            }
        }
    }

    private static void CreateBarrel(Room room, Sprite sprite, Vector3 pos, System.Random rng, bool large)
    {
        var go = new GameObject(large ? $"Barrel_Large_{sprite.name}" : $"Barrel_Small_{sprite.name}");
        go.transform.SetParent(room.ContentRoot, false);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * SizeScale;   // 视觉与碰撞体同比放大（col.size 为局部量自动跟随）

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 3;
        sr.flipX = rng.NextDouble() < 0.5;
        sr.color = new Color(Brightness, Brightness, Brightness, 1f);

        Vector2 spriteSize = sprite.bounds.size * ColliderShrink;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = spriteSize;

        if (large)
        {
            // 大木桶：实体障碍物，阻挡角色怪物通行
            go.layer = LayerMask.NameToLayer("Obstacle");
            col.isTrigger = false;
        }
        else
        {
            // 装饰小木桶：Trigger碰撞，可穿过，但接收攻击判定
            go.layer = LayerMask.NameToLayer("Obstacle");
            col.isTrigger = true;
        }

        // ========= 全部木桶：无论大小，都挂载可破坏组件 =========
        ObstacleHealth hp = go.AddComponent<ObstacleHealth>();
        hp.Init(BarrelHp);
    }

    private static bool Load()
    {
        if (smallBarrels != null && largeBarrels != null) return true;
        if (loadFailed) return false;

        var smallList = new System.Collections.Generic.List<Sprite>(24);
        var largeList = new System.Collections.Generic.List<Sprite>(8);

        foreach (Sprite s in Resources.LoadAll<Sprite>(ResourceDir))
        {
            float longSide = Mathf.Max(s.bounds.size.x, s.bounds.size.y);
            if (longSide >= LargeThresholdUnits)
                largeList.Add(s);
            else
                smallList.Add(s);
        }

        if (smallList.Count == 0 && largeList.Count == 0)
        {
            loadFailed = true;
            Debug.Log($"[BarrelDecor] 无木桶素材 Resources/{ResourceDir}，木桶装饰停用。");
            return false;
        }
        if (smallList.Count == 0)
            smallList.Add(largeList[largeList.Count - 1]);

        smallBarrels = smallList.ToArray();
        largeBarrels = largeList.Count > 0 ? largeList.ToArray() : smallList.ToArray();
        return true;
    }
}
