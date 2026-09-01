using NUnit.Framework;
using UnityEngine;

public class RoomTriggerTests
{
    private static readonly Rect RoomBounds = new Rect(0f, 0f, 10f, 10f);

    [Test]
    public void FullyInside_PlayerColliderPastDoor_ReturnsTrue()
    {
        var playerBounds = new Bounds(new Vector3(5f, 1f, 0f), new Vector3(1.32f, 1.59f, 0f));

        Assert.That(RoomTrigger.IsFullyInside(RoomBounds, playerBounds), Is.True);
    }

    [Test]
    public void CrossingDoor_PlayerStillOverlapsThreshold_ReturnsFalse()
    {
        var playerBounds = new Bounds(new Vector3(5f, 0.5f, 0f), new Vector3(1.32f, 1.59f, 0f));

        Assert.That(RoomTrigger.IsFullyInside(RoomBounds, playerBounds), Is.False);
    }

    [Test]
    public void EnteringFromSide_PlayerStillOverlapsThreshold_ReturnsFalse()
    {
        var playerBounds = new Bounds(new Vector3(0.5f, 5f, 0f), new Vector3(1.32f, 1.59f, 0f));

        Assert.That(RoomTrigger.IsFullyInside(RoomBounds, playerBounds), Is.False);
    }
}
