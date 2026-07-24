using UnityEngine;

/// <summary>
/// 障碍物生成器（静态）：放障碍物（色块 + BoxCollider2D，挡移动）。
/// 条目 destructible=true 时按条目 hp 初始化 ObstacleHealth（prefab 上已有则复用，没有则补挂）。
/// 障碍物一律不 RegisterEnemy——破坏障碍物不是清房条件，AllEnemiesDead 只数敌人。
/// </summary>
public static class ObstacleSpawner
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

            if (e.destructible)
            {
                ObstacleHealth hp = go.GetComponent<ObstacleHealth>();
                if (hp == null) hp = go.AddComponent<ObstacleHealth>();
                hp.Init(e.hp);
            }
        }
    }
}
