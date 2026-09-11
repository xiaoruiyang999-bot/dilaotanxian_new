using NUnit.Framework;
using UnityEngine;

/// <summary>
/// V2 最终左右攻击与差异化 Dash 门禁。
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

    [Test]
    public void EnemyHorizontalGeometry_UsesSameReachAndLaneOnBothSides()
    {
        var data = ScriptableObject.CreateInstance<AttackData>();
        data.SetHorizontalGeometry(3f, 1f, 0.35f);

        Assert.IsTrue(EnemyCombat.IsTargetWithinAttackGeometry(
            Vector2.zero, new Vector2(3.35f, 0.5f), data));
        Assert.IsTrue(EnemyCombat.IsTargetWithinAttackGeometry(
            Vector2.zero, new Vector2(-3.35f, -0.5f), data));
        Assert.IsFalse(EnemyCombat.IsTargetWithinAttackGeometry(
            Vector2.zero, new Vector2(3.36f, 0f), data));
        Assert.IsFalse(EnemyCombat.IsTargetWithinAttackGeometry(
            Vector2.zero, new Vector2(0.34f, 0f), data));
        Assert.IsFalse(EnemyCombat.IsTargetWithinAttackGeometry(
            Vector2.zero, new Vector2(2f, 0.51f), data));

        Object.DestroyImmediate(data);
    }

    [Test]
    public void ChargeWarningReach_CoversWeaponAndConfiguredTravelOnly()
    {
        var data = ScriptableObject.CreateInstance<AttackData>();
        data.SetHorizontalGeometry(1.5f, 1.1f, 0.35f);
        data.SetChargeGeometry(2.5f);

        Assert.AreEqual(4f, EnemyCombat.GetHorizontalWarningReach(data));
        Assert.IsTrue(EnemyCombat.IsTargetWithinAttackGeometry(
            Vector2.zero, new Vector2(4.35f, 0f), data));
        Assert.IsFalse(EnemyCombat.IsTargetWithinAttackGeometry(
            Vector2.zero, new Vector2(4.36f, 0f), data));

        Object.DestroyImmediate(data);
    }

    [Test]
    public void Dash_AxisDistancesAreIndependent()
    {
        Assert.AreEqual(new Vector2(3f, 0f), WerewolfDash.ResolveDashDisplacement(
            Vector2.right, Vector2.left, 3f, 1.5f, 0.8f));
        Assert.AreEqual(new Vector2(0f, 1.5f), WerewolfDash.ResolveDashDisplacement(
            Vector2.up, Vector2.left, 3f, 1.5f, 0.8f));
    }

    [Test]
    public void Dash_DiagonalScaleKeepsDiagonalShorterThanHorizontal()
    {
        Vector2 diagonal = WerewolfDash.ResolveDashDisplacement(
            Vector2.one, Vector2.right, 3f, 1.5f, 0.8f);

        Assert.AreEqual(new Vector2(2.4f, 1.2f), diagonal);
        Assert.Less(diagonal.magnitude, 3f);
    }

    [Test]
    public void Dash_NoInputFallsBackToLastHorizontalFacing()
    {
        Assert.AreEqual(new Vector2(-3f, 0f), WerewolfDash.ResolveDashDisplacement(
            Vector2.zero, Vector2.left, 3f, 1.5f, 0.8f));
    }
}
