using NUnit.Framework;
using UnityEngine;

/// <summary>
/// V2 v2.0.1 左右攻击门禁：有效鼠标瞄准按 X 正负定向，Y 不参与；
/// 无有效瞄准时沿用角色最后水平朝向。
/// </summary>
public class AttackDirectionResolverTests
{
    [Test]
    public void ValidMouseAim_UsesHorizontalSignAndIgnoresY()
    {
        Assert.AreEqual(Vector2.right, AttackDirectionResolver.ResolveHorizontal(
            new Vector2(0.01f, 0.999f), true, Vector2.left));
        Assert.AreEqual(Vector2.left, AttackDirectionResolver.ResolveHorizontal(
            new Vector2(-0.01f, -0.999f), true, Vector2.right));
    }

    [Test]
    public void NoValidAim_UsesLastHorizontalFacing()
    {
        Assert.AreEqual(Vector2.left, AttackDirectionResolver.ResolveHorizontal(
            Vector2.right, false, Vector2.left));
        Assert.AreEqual(Vector2.right, AttackDirectionResolver.ResolveHorizontal(
            Vector2.left, false, Vector2.right));
    }

    [Test]
    public void MouseOnSameX_UsesLastHorizontalFacing()
    {
        Assert.AreEqual(Vector2.left, AttackDirectionResolver.ResolveHorizontal(
            Vector2.up, true, Vector2.left));
    }

    [Test]
    public void MissingAimAndFacing_DefaultsRight()
    {
        Assert.AreEqual(Vector2.right, AttackDirectionResolver.ResolveHorizontal(
            Vector2.zero, false, Vector2.zero));
    }
}
