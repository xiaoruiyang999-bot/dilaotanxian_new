using NUnit.Framework;
using UnityEngine;

public class CameraFollowBoundsTests
{
    [Test]
    public void ViewportCenter_IsClampedAtAllFourMapEdges()
    {
        var bounds = new Rect(0f, 0f, 69f, 26f);
        Vector2 upperRight = CameraFollow.ClampViewportCenter(
            new Vector2(100f, 100f), bounds, 16f, 9f);
        Assert.AreEqual(53f, upperRight.x);
        Assert.AreEqual(17f, upperRight.y);

        Vector2 lowerLeft = CameraFollow.ClampViewportCenter(
            new Vector2(-100f, -100f), bounds, 16f, 9f);
        Assert.AreEqual(16f, lowerLeft.x);
        Assert.AreEqual(9f, lowerLeft.y);
    }

    [Test]
    public void ViewportCenter_UsesMidpointIfMapIsSmallerThanView()
    {
        Vector2 result = CameraFollow.ClampViewportCenter(
            new Vector2(100f, -100f), new Rect(2f, 4f, 10f, 8f), 6f, 5f);
        Assert.AreEqual(new Vector2(7f, 8f), result);
    }

    [Test]
    public void ViewportCenter_AllowsBlackPaddingBeyondTopAndRightWalls()
    {
        var bounds = new Rect(0f, 0f, 69f, 26f);
        Vector2 result = CameraFollow.ClampViewportCenter(
            new Vector2(100f, 100f), bounds, 16f, 9f, 1f, 1f);
        Assert.AreEqual(54f, result.x);
        Assert.AreEqual(18f, result.y);
        Assert.AreEqual(bounds.xMax + 1f, result.x + 16f);
        Assert.AreEqual(bounds.yMax + 1f, result.y + 9f);
    }
}
