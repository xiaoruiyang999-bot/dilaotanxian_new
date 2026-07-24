using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人生成器（静态）：按权重表在房间内随机放敌人，实例化到 contentRoot 下并 RegisterEnemy。
/// v0.5.3.1：条目 minCount 保底先行（精英房至少 1 精英），再按权重抽满数量，洗牌后生成。
/// RegisterEnemy 即接管休眠（可见但不动）与清房计数——本类不管休眠、不认识 Room 状态机。
/// </summary>
public static class EnemySpawner
{
    public static void Spawn(Room room, SpawnTable table, System.Random rng)
    {
        if (room == null || table == null) return;

        // v0.5.3.1 保底：minCount>0 的条目无视权重先行（如精英房至少 1 精英）
        var picks = new List<SpawnTable.Entry>();
        foreach (SpawnTable.Entry e in table.entries)
            if (e != null && e.prefab != null)
                for (int k = 0; k < e.minCount; k++) picks.Add(e);

        int count = Mathf.Max(table.RollCount(rng), picks.Count);
        while (picks.Count < count)
        {
            SpawnTable.Entry e = table.PickEntry(rng);
            if (e == null) break;
            picks.Add(e);
        }

        // 洗牌（房间子 seed），避免保底条目固定占据生成顺序前部
        for (int i = picks.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (picks[i], picks[j]) = (picks[j], picks[i]);
        }

        for (int i = 0; i < picks.Count; i++)
        {
            if (!SpawnPositionHelper.TryFind(room, rng, out Vector3 pos)) continue;

            GameObject go = Object.Instantiate(picks[i].prefab, pos, Quaternion.identity, room.ContentRoot);
            go.name = $"{picks[i].prefab.name}_{room.Id}_{i}";
            room.RegisterEnemy(go.GetComponent<EnemyHealth>());
        }
    }
}
