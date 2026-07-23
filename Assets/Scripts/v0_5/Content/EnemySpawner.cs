using UnityEngine;

/// <summary>
/// 敌人生成器（静态）：按权重表在房间内随机放敌人，实例化到 contentRoot 下并 RegisterEnemy。
/// RegisterEnemy 即接管休眠（可见但不动）与清房计数——本类不管休眠、不认识 Room 状态机。
/// </summary>
public static class EnemySpawner
{
    public static void Spawn(Room room, SpawnTable table, System.Random rng)
    {
        if (room == null || table == null) return;

        int count = table.RollCount(rng);
        for (int i = 0; i < count; i++)
        {
            SpawnTable.Entry e = table.PickEntry(rng);
            if (e == null) continue;
            if (!SpawnPositionHelper.TryFind(room, rng, out Vector3 pos)) continue;

            GameObject go = Object.Instantiate(e.prefab, pos, Quaternion.identity, room.ContentRoot);
            go.name = $"{e.prefab.name}_{room.Id}_{i}";
            room.RegisterEnemy(go.GetComponent<EnemyHealth>());
        }
    }
}
