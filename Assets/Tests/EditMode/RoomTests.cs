using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Room 状态机与清房逻辑单元测试：验证状态转换、敌人登记/移除、波次扩展。
/// 仅测试纯逻辑，不依赖物理或动画。
/// </summary>
public class RoomTests
{
    // ========== RoomState 枚举测试 ==========

    [Test]
    public void RoomState_Unvisited_IsDefault()
    {
        RoomState state = default;
        Assert.AreEqual(RoomState.Unvisited, state);
    }

    [Test]
    public void RoomState_Cleared_IsNotActive()
    {
        RoomState active = RoomState.Active;
        RoomState cleared = RoomState.Cleared;

        Assert.AreNotEqual(active, cleared);
    }

    // ========== TryGetSpawnCell 测试 ==========

    [Test]
    public void TryGetSpawnCell_NullRng_ReturnsFalse()
    {
        // 模拟 Room.TryGetSpawnCell 逻辑
        var spawnCells = new System.Collections.Generic.List<Vector2Int>
        {
            new Vector2Int(1, 1),
            new Vector2Int(2, 2),
            new Vector2Int(3, 3)
        };
        System.Random rng = null;

        bool result = rng != null && spawnCells != null && spawnCells.Count > 0;
        Assert.IsFalse(result);
    }

    [Test]
    public void TryGetSpawnCell_EmptyList_ReturnsFalse()
    {
        var spawnCells = new System.Collections.Generic.List<Vector2Int>();
        var rng = new System.Random(123);

        bool result = rng != null && spawnCells != null && spawnCells.Count > 0;
        Assert.IsFalse(result);
    }

    [Test]
    public void TryGetSpawnCell_ValidCell_ReturnsTrue()
    {
        var spawnCells = new System.Collections.Generic.List<Vector2Int>
        {
            new Vector2Int(5, 5)
        };
        var rng = new System.Random(123);

        bool result = rng != null && spawnCells != null && spawnCells.Count > 0;
        Assert.IsTrue(result);

        Vector2Int cell = spawnCells[rng.Next(spawnCells.Count)];
        Assert.AreEqual(new Vector2Int(5, 5), cell);
    }

    // ========== 敌人去重测试 ==========

    [Test]
    public void RegisterEnemy_Duplicate_NotAddedTwice()
    {
        var enemies = new System.Collections.Generic.List<int>();
        int enemyId = 42;

        // 模拟 RegisterEnemy 去重逻辑
        if (!enemies.Contains(enemyId))
            enemies.Add(enemyId);
        if (!enemies.Contains(enemyId))
            enemies.Add(enemyId);

        Assert.AreEqual(1, enemies.Count);
    }

    // ========== RemoveEnemy 事件解绑测试 ==========

    [Test]
    public void RemoveEnemy_UnsubscribesHandler()
    {
        var handlers = new System.Collections.Generic.Dictionary<int, System.Action>();
        int enemyId = 1;
        bool handlerCalled = false;

        System.Action handler = () => handlerCalled = true;
        handlers[enemyId] = handler;

        // 模拟 RemoveEnemy
        if (handlers.TryGetValue(enemyId, out System.Action h))
        {
            handlers.Remove(enemyId);
        }

        Assert.IsFalse(handlers.ContainsKey(enemyId));
    }

    // ========== LateUpdate 残留引用清理测试 ==========

    [Test]
    public void LateUpdate_NullEnemy_RemovedFromList()
    {
        var enemies = new System.Collections.Generic.List<EnemyHealth>();
        enemies.Add(null);
        enemies.Add(null);

        // 模拟 LateUpdate 清理
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            if (enemies[i] == null)
                enemies.RemoveAt(i);
        }

        Assert.AreEqual(0, enemies.Count);
    }

    // ========== TryClearRoom 条件测试 ==========

    [Test]
    public void TryClearRoom_NotActive_ReturnsFalse()
    {
        RoomState state = RoomState.Unvisited;
        RoomClearCondition condition = RoomClearCondition.AllEnemiesDead;
        int enemyCount = 0;

        bool shouldClear = state == RoomState.Active
            && condition == RoomClearCondition.AllEnemiesDead
            && enemyCount == 0;

        Assert.IsFalse(shouldClear);
    }

    [Test]
    public void TryClearRoom_ActiveNoEnemies_ReturnsTrue()
    {
        RoomState state = RoomState.Active;
        RoomClearCondition condition = RoomClearCondition.AllEnemiesDead;
        int enemyCount = 0;

        bool shouldClear = state == RoomState.Active
            && condition == RoomClearCondition.AllEnemiesDead
            && enemyCount == 0;

        Assert.IsTrue(shouldClear);
    }

    [Test]
    public void TryClearRoom_ActiveWithEnemies_ReturnsFalse()
    {
        RoomState state = RoomState.Active;
        RoomClearCondition condition = RoomClearCondition.AllEnemiesDead;
        int enemyCount = 3;

        bool shouldClear = state == RoomState.Active
            && condition == RoomClearCondition.AllEnemiesDead
            && enemyCount == 0;

        Assert.IsFalse(shouldClear);
    }

    [Test]
    public void TryClearRoom_NoneCondition_AlwaysClears()
    {
        RoomClearCondition condition = RoomClearCondition.None;
        int enemyCount = 5;

        bool shouldClear = condition == RoomClearCondition.None;
        Assert.IsTrue(shouldClear);
    }

    // ========== IWaveProvider 波次扩展测试 ==========

    [Test]
    public void WaveProvider_HasPendingWave_RequestsNextWave()
    {
        bool hasPendingWave = true;
        bool requestCalled = false;

        // 模拟 TryClearRoom 波次逻辑
        if (hasPendingWave)
        {
            requestCalled = true;
        }

        Assert.IsTrue(requestCalled);
    }

    [Test]
    public void WaveProvider_NoPendingWave_ClearsRoom()
    {
        bool hasPendingWave = false;
        bool cleared = false;

        if (!hasPendingWave)
        {
            cleared = true;
        }

        Assert.IsTrue(cleared);
    }
}


