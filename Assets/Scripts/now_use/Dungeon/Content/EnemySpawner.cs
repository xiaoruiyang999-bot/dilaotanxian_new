using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人生成器（静态）：按权重表在房间内随机放敌人，实例化到 contentRoot 下并 RegisterEnemy。
/// v0.5.3.1：条目 minCount 保底先行（精英房至少 1 精英），再按权重抽满数量，洗牌后生成。
/// RegisterEnemy 即接管休眠（可见但不动）与清房计数——本类不管休眠、不认识 Room 状态机。
/// </summary>
public static class EnemySpawner
{
    /// <summary>按权重表在房间内随机放敌人。v0.5.4：floorNumber&gt;1 时注入楼层难度
    ///（数量 +enemyCountBonusPerFloor×(floor-1) 封顶 8，HP ×(1+hpMultiplierPerFloor×(floor-1))）。</summary>
    public static void Spawn(Room room, SpawnTable table, System.Random rng, int floorNumber = 1,
        DungeonConfig config = null, IReadOnlyList<Vector3> fixedPositions = null)
    {
        if (room == null || table == null) return;

        var picks = table.UsesEncounterBudget
            ? table.BuildEncounter(rng, room.DistanceFromStart)
            : new List<SpawnTable.Entry>();

        int count = Mathf.Max(table.RollCount(rng), picks.Count);
        // v0.5.4 楼层数量递增（封顶 8/房）
        if (config != null && floorNumber > 1)
            count = Mathf.Max(Mathf.Min(count + config.enemyCountBonusPerFloor * (floorNumber - 1), 8), picks.Count);
        // v1.1.46 大房面积自适应：战斗房整格扩展后（2×1 ≈2×面积 / 2×2 ≈4×面积），
        // 固定 2~4 只会让大竞技场显空——面积 ≥1.9× 标准房的波次数量 ×1.5（封顶 12，
        // 独立于楼层递增封顶，两波轮次本身已 ×2 时长，单波不过量）
        if (config != null && room.Bounds.width * room.Bounds.height
            >= config.roomWidth * config.roomHeight * 1.9f)
            count = Mathf.Min(Mathf.CeilToInt(count * 1.5f), 12);
        while (picks.Count < count)
        {
            // v0.5.3.1 保底：minCount>0 的条目无视权重先行（如精英房至少 1 精英）
            foreach (SpawnTable.Entry e in table.entries)
                if (e != null && e.prefab != null)
                    for (int k = 0; k < e.minCount; k++) picks.Add(e);

            while (picks.Count < count)
            {
                SpawnTable.Entry e = table.PickEntry(rng);
                if (e == null) break;
                picks.Add(e);
            }
        }

        // 普通房洗牌，避免保底条目固定占据生成顺序前部；固定 Boss 厅则保留表顺序，
        // 让 mandatory Boss 始终消费第 0 个中央法阵插槽。
        for (int i = fixedPositions == null ? picks.Count - 1 : 0; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (picks[i], picks[j]) = (picks[j], picks[i]);
        }

        int spawnCount = fixedPositions != null ? Mathf.Min(picks.Count, fixedPositions.Count) : picks.Count;
        if (fixedPositions != null && spawnCount < picks.Count)
            Debug.LogWarning($"[EnemySpawner] 固定插槽仅 {fixedPositions.Count} 个，已截断 {picks.Count - spawnCount} 个敌人。");

        int fixedPositionIndex = 0;
        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 pos;
            if (fixedPositions != null)
            {
                while (fixedPositionIndex < fixedPositions.Count
                    && !SpawnPositionHelper.IsFixedPositionClear(
                        room, fixedPositions[fixedPositionIndex], ignorePlayer: true))
                {
                    Debug.LogWarning($"[EnemySpawner] 固定插槽 {fixedPositionIndex} 未通过 NonAlloc 物理复核，尝试下一插槽。");
                    fixedPositionIndex++;
                }

                if (fixedPositionIndex >= fixedPositions.Count)
                {
                    Debug.LogWarning($"[EnemySpawner] 固定安全插槽已用尽，停止生成剩余 {spawnCount - i} 个敌人。");
                    break;
                }

                pos = fixedPositions[fixedPositionIndex++];
            }
            else
            {
                if (!SpawnPositionHelper.TryFind(room, rng, out pos)) continue;
                // v2.0.7 前方阵型（V2 §9.2）：普通房敌人不刷玩家身后——出生点须在房间右半（x > 中线-2），
                // 最多重试 4 次取右侧点，失败回退任意点（保生成不保阵型）
                for (int sideTry = 0; sideTry < 4 && pos.x < room.Bounds.center.x - 2f; sideTry++)
                {
                    if (!SpawnPositionHelper.TryFind(room, rng, out Vector3 rightPrefer)) break;
                    if (rightPrefer.x >= room.Bounds.center.x - 2f) { pos = rightPrefer; break; }
                }
            }

            GameObject go = Object.Instantiate(picks[i].prefab, pos, Quaternion.identity, room.ContentRoot);
            go.name = $"{picks[i].prefab.name}_{room.Id}_{i}";

            EnemyAffixConfig affix = table.RollAffix(rng, room.DistanceFromStart);
            if (affix != null)
                go.AddComponent<EnemyAffix>().Apply(affix);

            // M3·v0.8.1 最终 Boss：第 9 层 Boss 房敌人全面强化（数值书 §5.3 压轴）
            if (floorNumber >= 9 && room.Type == RoomType.Boss)
            {
                var eh = go.GetComponent<EnemyHealth>();
                if (eh != null) eh.ScaleMaxHealth(1.5f);
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(1f, 0.45f, 0.3f);
                go.name += "·终焉";
                Debug.Log($"[M3] 最终 Boss 已强化：{go.name}（HP ×1.5）");
            }

            // 每个敌人消费房间 RNG 派生的独立随机源：AI 横移、招式选择与召唤落点
            // 在相同地牢 seed 下保持可复现，同时避免同房敌人行为完全同步。
            var enemyAI = go.GetComponent<EnemyAI>();
            if (enemyAI != null)
                enemyAI.SetBehaviorRng(new System.Random(rng.Next()));

            var enemyCombat = go.GetComponent<EnemyCombat>();
            if (enemyCombat != null)
                enemyCombat.SetCombatRng(new System.Random(rng.Next()));

            // v0.5.4 楼层 HP 缩放（dmgMul 预留恒 1，见 EnemyStats.ApplyFloorScale 注释）
            if (config != null && floorNumber > 1)
                go.GetComponent<EnemyStats>()?.ApplyFloorScale(1f + config.hpMultiplierPerFloor * (floorNumber - 1), 1f);
            room.RegisterEnemy(go.GetComponent<EnemyHealth>());
        }
    }
}
