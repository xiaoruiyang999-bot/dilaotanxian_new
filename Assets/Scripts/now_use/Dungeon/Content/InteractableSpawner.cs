using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 交互物生成器（静态，v0.5.3）：宝箱/祭坛/补给基座，实例化到 contentRoot 下。
/// 与三个内容 Spawner 同构，差异只有一点：交互物是 trigger，
/// SpawnPositionHelper 的 OverlapCircle（useTriggers=false）看不见它们，
/// 因此自行维护本房已放置列表，交互物之间保持 ≥2 格（商店 3 基座不重叠）。
/// 交互物一律不 RegisterEnemy——与清房条件无关。
/// </summary>
public static class InteractableSpawner
{
    private const float MinSeparation = 2f;

    public static void Spawn(Room room, SpawnTable table, System.Random rng)
    {
        if (room == null || table == null) return;

        // v0.5.3.1 Row 布局：条目即商品，房中心一列排放（商店陈列），不走随机散点
        if (table.layoutMode == SpawnLayout.Row)
        {
            SpawnRow(room, table);
            return;
        }

        int count = table.RollCount(rng);
        var placed = new List<Vector3>();
        for (int i = 0; i < count; i++)
        {
            SpawnTable.Entry e = table.PickEntry(rng);
            if (e == null) continue;

            // trigger 不参与 OverlapCircle，间距靠自行维护的已放置列表；
            // 间距冲突时整体重试（与 TryFind 的 20 次重试同语义，避免冲突直接丢件）
            const int maxAttempts = 10;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (!SpawnPositionHelper.TryFind(room, rng, out Vector3 pos)) break;
                if (TooClose(placed, pos)) continue;

                placed.Add(pos);
                GameObject go = Object.Instantiate(e.prefab, pos, Quaternion.identity, room.ContentRoot);
                go.name = $"{e.prefab.name}_{room.Id}_{i}";
                break;
            }
        }
    }

    private static bool TooClose(List<Vector3> placed, Vector3 pos)
    {
        foreach (Vector3 p in placed)
            if (Vector2.Distance(p, pos) < MinSeparation) return true;
        return false;
    }

    /// <summary>Row 布局（v0.5.3.1）：有效条目按列表顺序在房中心横轴一列排放，间距 rowSpacing。
    /// 位置在房中心，天然满足距门 ≥2.5（房宽 20，3 个商品总宽 5），无需重试。
    /// v0.7.5：商店展台上的商品由补给球（SupplyInteractable，即拾即用）换成正式消耗包（ItemPickup，
    /// E 拾取进背包按 C 使用）——展台/排布仍由 SpawnTable 资产驱动，生成时按补给类型映射消耗包并拆掉补给行为；
    /// 原第二排地面三件套（v0.7.3 SpawnShopConsumables）撤销，避免与展台商品重复。</summary>
    private static void SpawnRow(Room room, SpawnTable table)
    {
        var items = new List<SpawnTable.Entry>();
        foreach (SpawnTable.Entry e in table.entries)
            if (e != null && e.prefab != null) items.Add(e);

        for (int i = 0; i < items.Count; i++)
        {
            float x = room.Center.x + (i - (items.Count - 1) * 0.5f) * table.rowSpacing;
            var pos = new Vector3(x, room.Center.y, 0f);
            GameObject go = Object.Instantiate(items[i].prefab, pos, Quaternion.identity, room.ContentRoot);
            go.name = $"{items[i].prefab.name}_{room.Id}_{i}";

            // v0.7.5：补给基座 → 展台 + 测试药（仅商店 Row 陈列使用 Supply prefab，其余条目原样）
            if (go.TryGetComponent(out SupplyInteractable supply))
                ConvertSupplyToConsumable(go, supply, room);
        }
    }

    // 商品摆放高度：原补给球浮在展台上方 0.45（Supply prefab Orb 偏移），消耗包沿用
    private const float ShopItemOffsetY = 0.45f;

    /// <summary>商店展台改造（v0.7.5）：保留展台视觉，拆掉补给球与即拾即用行为，
    /// 原位摆上补给类型对应的正式消耗包（SupplyType 顺序与 PrepRoomManager.ConsumableAssetNames 对齐：血/甲/法力）。
    /// 资产缺失时留空展台，不阻断陈列。</summary>
    private static void ConvertSupplyToConsumable(GameObject pedestalGo, SupplyInteractable supply, Room room)
    {
        string[] names = PrepRoomManager.ConsumableAssetNames;
        int index = (int)supply.Type;
        ConsumableData data = index >= 0 && index < names.Length
            ? PrepRoomManager.LoadConsumable(names[index]) : null;

        // 拆补给球视觉与补给行为；基座触发碰撞体禁用（Destroy 受基类 RequireComponent 限制，
        // 且 ItemPickup 自带触发器，禁用后即退出 OverlapCircle 探测）
        Transform orb = pedestalGo.transform.Find("Orb");
        if (orb != null) Object.Destroy(orb.gameObject);
        Collider2D col = pedestalGo.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        Object.Destroy(supply);

        if (data == null) return;
        ItemPickup pickup = ItemPickup.Spawn(data,
            pedestalGo.transform.position + new Vector3(0f, ShopItemOffsetY, 0f));
        if (pickup != null) pickup.transform.SetParent(room.ContentRoot, true);
    }
}
