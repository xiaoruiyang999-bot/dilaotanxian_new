using UnityEngine;

/// <summary>
/// 障碍物生成器（静态）：放障碍物（色块 + BoxCollider2D，挡移动）。
/// 条目 destructible=true 时按条目 hp 初始化 ObstacleHealth（prefab 上已有则复用，没有则补挂）。
/// 障碍物一律不 RegisterEnemy——破坏障碍物不是清房条件，AllEnemiesDead 只数敌人。
/// </summary>
public static class ObstacleSpawner
{
    // v1.1.22 层次感：障碍密度保底——每 45 格房内面积至少 1 个（18×11≈198 → ≥5 个），
    // 表 roll 值低于保底时补齐；高于保底则尊重表值（SO 资产零改动，调密度仍以表为主）。
    private const float AreaPerObstacle = 45f;

    public static void Spawn(Room room, SpawnTable table, System.Random rng)
    {
        if (room == null || table == null) return;

        int target = Mathf.Max(
            table.RollCount(rng),
            Mathf.CeilToInt(room.Bounds.width * room.Bounds.height / AreaPerObstacle));

        for (int i = 0; i < target; i++)
        {
            SpawnTable.Entry e = table.PickEntry(rng);
            if (e == null) continue;
            if (!SpawnPositionHelper.TryFind(room, rng, out Vector3 pos)) continue;   // 落点尽失败则少生不硬塞

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
