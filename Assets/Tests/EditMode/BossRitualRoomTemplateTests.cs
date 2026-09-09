using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>v1.1.52 固定 Boss 仪式厅：正式南入口合同、防御门向映射、玩家口径与固定插槽回归。</summary>
public class BossRitualRoomTemplateTests
{
    [Test]
    public void TemplateBuild_DefensivelyMapsAllDoorDirections()
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
            Assert.That(layout.PillarCenters, Has.Count.EqualTo(4));
            AssertLayoutValid(layout);
        }
    }

    [Test]
    public void DefensiveNorthBuild_IsHalfTurnEquivalent_AndDoorClampsNearCenter()
    {
        var interior = new RectInt(1, 1, 61, 37);
        BossRitualRoomLayout south = BossRitualRoomTemplate.Build(
            interior, HorizontalDoor(interior, interior.yMin - 1));
        BossRitualRoomLayout north = BossRitualRoomTemplate.Build(
            interior, HorizontalDoor(interior, interior.yMax));

        foreach (Vector2Int cell in south.Plan.Obstacles)
        {
            Vector2 cellCenter = (Vector2)cell + new Vector2(0.5f, 0.5f);
            Vector2 mirroredCenter = interior.center * 2f - cellCenter;
            var mirroredCell = new Vector2Int(
                Mathf.FloorToInt(mirroredCenter.x), Mathf.FloorToInt(mirroredCenter.y));
            Assert.That(north.Plan.Obstacles.Contains(mirroredCell), Is.True,
                $"南北入口柱基未形成 180° 同一模板：{cell} -> {mirroredCell}");
        }

        var southNeighborCell = new RectInt(0, -18, 31, 19);
        var northNeighborCell = new RectInt(31, 38, 31, 19);
        var bossInterior = new Rect(interior.x, interior.y, interior.width, interior.height);
        int southStart = BossRitualRoomTemplate.CenteredVerticalDoorStartX(
            bossInterior, southNeighborCell, 2);
        int northStart = BossRitualRoomTemplate.CenteredVerticalDoorStartX(
            bossInterior, northNeighborCell, 2);
        float southDoorCenter = southStart + 1f;
        float northDoorCenter = northStart + 1f;
        Assert.That(Mathf.Abs(southDoorCenter - interior.center.x), Is.LessThanOrEqualTo(1.5f));
        Assert.That(Mathf.Abs(northDoorCenter - interior.center.x), Is.LessThanOrEqualTo(1.5f));
        Assert.That(southStart, Is.GreaterThanOrEqualTo(southNeighborCell.xMin + 1));
        Assert.That(southStart + 2, Is.LessThanOrEqualTo(southNeighborCell.xMax));
        Assert.That(northStart, Is.GreaterThanOrEqualTo(northNeighborCell.xMin + 1));
        Assert.That(northStart + 2, Is.LessThanOrEqualTo(northNeighborCell.xMax));
        Assert.That(northStart, Is.EqualTo(Mathf.RoundToInt(interior.center.x * 2f) - southStart - 2),
            "南北门洞也必须是同一模板的 180° 映射");
    }

    [TestCase(61, 37)]
    [TestCase(17, 17)]
    [TestCase(97, 97)]
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

        List<Vector3> rewards = BossRitualRoomTemplate.BuildRewardSpawnPositions(a);
        Assert.That(rewards, Has.Count.EqualTo(6));
        foreach (Vector3 reward in rewards)
        {
            var cell = new Vector2Int(Mathf.FloorToInt(reward.x), Mathf.FloorToInt(reward.y));
            Assert.That(a.Plan.SpawnCells.Contains(cell), Is.True, $"奖励插槽不安全：{cell}");
        }
        for (int chest = 0; chest < rewards.Count; chest += 2)
            for (int portal = 1; portal < rewards.Count; portal += 2)
                Assert.That(Vector3.Distance(rewards[chest], rewards[portal]),
                    Is.GreaterThanOrEqualTo(BossRitualRoomDecorator.RewardMinSeparation),
                    $"宝箱插槽 {chest / 2} 与传送门插槽 {portal / 2} 会重叠");
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
