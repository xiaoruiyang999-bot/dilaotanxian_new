using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>v1.1.51 固定 Boss 仪式厅：四向旋转、四种实际尺寸、玩家口径与固定插槽回归。</summary>
public class BossRitualRoomTemplateTests
{
    [Test]
    public void FourEntranceDirections_RotateOneIdenticalTemplate()
    {
        var interior = new RectInt(10, 20, 61, 37);
        var expected = new[]
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.right, Vector2Int.left,
        };
        var doors = new[]
        {
            HorizontalDoor(interior, interior.yMin - 1),
            HorizontalDoor(interior, interior.yMax),
            VerticalDoor(interior, interior.xMin - 1),
            VerticalDoor(interior, interior.xMax),
        };

        for (int i = 0; i < doors.Length; i++)
        {
            BossRitualRoomLayout layout = BossRitualRoomTemplate.Build(interior, doors[i]);
            Assert.AreEqual(expected[i], layout.Forward, $"入口方向 {i} 映射错误");
            Assert.AreEqual(new Vector2Int(expected[i].y, -expected[i].x), layout.Right);
            Assert.That(layout.Plan.Obstacles.Count, Is.EqualTo(16), "四个 2×2 柱基必须完整");
            AssertLayoutValid(layout);
        }
    }

    [TestCase(61, 37)]
    [TestCase(61, 18)]
    [TestCase(30, 37)]
    [TestCase(30, 18)]
    public void ActualRoomSizes_AreValidAndExposeTwelveFixedEnemySlots(int width, int height)
    {
        var interior = new RectInt(0, 0, width, height);
        BossRitualRoomLayout a = BossRitualRoomTemplate.Build(
            interior, HorizontalDoor(interior, interior.yMin - 1));
        BossRitualRoomLayout b = BossRitualRoomTemplate.Build(
            interior, HorizontalDoor(interior, interior.yMin - 1));

        Assert.That(a.Plan.Obstacles.SetEquals(b.Plan.Obstacles), "固定模板不得随调用改变");
        CollectionAssert.AreEqual(a.Plan.SpawnCells, b.Plan.SpawnCells);
        AssertLayoutValid(a);

        List<Vector3> slots = BossRitualRoomTemplate.BuildEnemySpawnPositions(a);
        Assert.That(slots, Has.Count.EqualTo(12));
        var unique = new HashSet<Vector2Int>();
        foreach (Vector3 slot in slots)
        {
            var cell = new Vector2Int(Mathf.FloorToInt(slot.x), Mathf.FloorToInt(slot.y));
            Assert.That(unique.Add(cell), Is.True, $"敌人插槽重复：{cell}");
            Assert.That(a.Plan.SpawnCells.Contains(cell), Is.True, $"敌人插槽不在最终 3×3 安全白名单：{cell}");
        }
        Assert.AreEqual(a.Cell(0f, 0.44f),
            new Vector2Int(Mathf.FloorToInt(slots[0].x), Mathf.FloorToInt(slots[0].y)),
            "第 0 插槽应为法阵中心 Boss 位");
    }

    private static void AssertLayoutValid(BossRitualRoomLayout layout)
    {
        Vector2Int center = layout.Cell(0f, 0.44f);
        Assert.That(RoomLayoutValidator.Validate(layout.Plan,
            new List<Vector2Int>(layout.DoorAnchors), center), Is.True, "基础布局门禁失败");
        Assert.That(RoomLayoutValidator.ValidatePlayerGauge(layout.Plan,
            new List<Vector2Int>(layout.DoorAnchors), center), Is.True, "玩家 2 格口径门禁失败");
        Assert.That(layout.Plan.SpawnCells, Is.Not.Empty);
    }

    private static List<Vector2Int> HorizontalDoor(RectInt area, int y)
    {
        int x = area.xMin + area.width / 2;
        return new List<Vector2Int> { new Vector2Int(x - 1, y), new Vector2Int(x, y) };
    }

    private static List<Vector2Int> VerticalDoor(RectInt area, int x)
    {
        int y = area.yMin + area.height / 2;
        return new List<Vector2Int> { new Vector2Int(x, y - 1), new Vector2Int(x, y) };
    }
}
