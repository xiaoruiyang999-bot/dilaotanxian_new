using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 废墟石块装饰生成器（v1.1.26）：Resources/Art/Decor/Stones/prop_NN 随机引用，每格独立抽取。
/// - 小石块（长边 &lt; 1.06 单位，即素材长边 &lt;90px）：**无碰撞纯装饰**，sortingOrder 3（法力球之上金币之下），
///   随机水平翻转增加变化，挂 Room.ContentRoot 随房销毁/休眠；
/// - 大石块（≥1.06 单位）：**障碍物替代**——BoxCollider2D（sprite bounds ×0.8，圆石留角部余量防卡人）
///   + Obstacle 层 + ObstacleHealth(3) 可破坏，语义与 Obstacle prefab 一致（挡移动、不进清房计数）；
/// - 防卡墙：落点全走 SpawnPositionHelper.TryFind（距墙 ≥1 格、距门 ≥2.5、实体重叠检测——
///   v1.1.23 房内模板墙/挖除角自动避开）；
/// - 数量克制：小 1~4、大 0~2 每房（rng 决定，含 0 概率留白）；确定性与 SpawnContent 同 rng。
/// 素材契约：任意分辨率透明 PNG，85px@PPU85（与地皮同尺度），命名 prop_NN 自动入池。
/// </summary>
public static class StoneDecorSpawner
{
    private const string ResourceDir = "Art/Decor/Stones";
    // 障碍物尺寸门槛（v1.1.27 收紧）：长边 ≥1.2 单位（≈102px）才可作为障碍物生成——
    // 中等石块并入无碰撞装饰池，"当障碍物的必须是真的巨石"（当前合格 ≈7 张：105~146px）。
    private const float LargeThresholdUnits = 1.2f;
    private const int LargeStoneHp = 3;                // 与障碍物表默认 hp 一致
    private const float ColliderShrink = 0.8f;         // 圆石碰撞盒内缩：留角部余量防卡人卡墙

    // 亮度调节（v1.1.26）：1=原图亮度，<1 变暗、>1 提亮（乘色，全石块统一生效）
    private const float Brightness = 1.75f;

    private static Sprite[] smallStones, largeStones;
    private static bool loadFailed;

    public static void Spawn(Room room, System.Random rng, HashSet<Vector2Int> avoidCells = null)
    {
        if (room == null || rng == null || !Load()) return;

        // 小石块：1~4 个纯点缀（无碰撞，不避让骨架）
        int smallCount = rng.Next(1, 5);
        for (int i = 0; i < smallCount; i++)
        {
            if (!SpawnPositionHelper.TryFind(room, rng, out var pos)) continue;
            CreateStone(room, smallStones[rng.Next(smallStones.Length)], pos, rng, large: false);
        }

        // 大石块：1~3 个障碍物替代（v1.1.27 承接全部障碍物职责——木箱表已停用，密度由石块独扛）
        // v1.1.44：落点避让骨架格（细路网/入口上不放有碰撞的石块——玩家口径验证留出的
        // ≥2 宽口不回堵；重试最多 6 次，全撞骨架则放弃该块，密度让位于可达性）
        int largeCount = rng.Next(1, 4);
        for (int i = 0; i < largeCount; i++)
        {
            bool placed = false;
            for (int t = 0; t < 6 && !placed; t++)
            {
                if (!SpawnPositionHelper.TryFind(room, rng, out var pos)) break;
                if (avoidCells != null
                    && avoidCells.Contains(new Vector2Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y)))) continue;
                CreateStone(room, largeStones[rng.Next(largeStones.Length)], pos, rng, large: true);
                placed = true;
            }
        }
    }

    private static void CreateStone(Room room, Sprite sprite, Vector3 pos, System.Random rng, bool large)
    {
        var go = new GameObject(large ? $"StoneLarge_{sprite.name}" : $"StoneSmall_{sprite.name}");
        go.transform.SetParent(room.ContentRoot, false);
        go.transform.position = pos;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 3;   // 地面/法力球(1~2)之上，金币(4)与角色(10)之下
        sr.flipX = rng.NextDouble() < 0.5;   // 水平翻转廉价变体
        sr.color = new Color(Brightness, Brightness, Brightness, 1f);   // 亮度旋钮（1=原图）

        if (!large) return;   // 小石块：无碰撞，纯装饰

        // 大石块：障碍物语义（Obstacle 层 + 满足阻挡的碰撞盒 + 可破坏）
        go.layer = LayerMask.NameToLayer("Obstacle");
        Vector2 s = sprite.bounds.size * ColliderShrink;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = s;   // 精灵 bounds 居中，offset 保持 0
        ObstacleHealth hp = go.AddComponent<ObstacleHealth>();
        hp.Init(LargeStoneHp);
    }

    /// <summary>加载并按世界尺寸分类（长边 ≥1.06 单位 = 大石块）；素材缺失/为空静默停用。</summary>
    private static bool Load()
    {
        if (smallStones != null && largeStones != null) return true;
        if (loadFailed) return false;

        var small = new System.Collections.Generic.List<Sprite>(24);
        var large = new System.Collections.Generic.List<Sprite>(8);
        foreach (Sprite s in Resources.LoadAll<Sprite>(ResourceDir))
        {
            float longSide = Mathf.Max(s.bounds.size.x, s.bounds.size.y);
            if (longSide >= LargeThresholdUnits) large.Add(s);
            else small.Add(s);
        }
        if (small.Count == 0 && large.Count == 0)
        {
            loadFailed = true;
            Debug.Log($"[StoneDecor] 无可用石块素材（Resources/{ResourceDir}），装饰停用。");
            return false;
        }
        if (small.Count == 0) small.Add(large[large.Count - 1]);   // 极端兜底：只有大图时小类借用最小的大图
        smallStones = small.ToArray();
        largeStones = large.Count > 0 ? large.ToArray() : small.ToArray();
        return true;
    }
}
