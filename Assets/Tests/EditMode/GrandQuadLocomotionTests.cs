using NUnit.Framework;
using UnityEngine;

/// <summary>
/// v2.0.10 批4 四足追猎决策器门禁（修缮计划 §8.1 纯 C# 可测）：
/// 限速转向(不瞬移)、近距绕侧(不直追)、奔袭条件(距离+冷却)、退距条件。
/// </summary>
public class GrandQuadLocomotionTests
{
    [Test]
    public void LimitTurn_NeverInstantReverse()
    {
        // 面向右(1,0),目标向左(-1,0)——限转 5° 后不可能一步到左
        Vector2 current = Vector2.right;
        Vector2 target = Vector2.left;
        Vector2 result = GrandQuadLocomotion.LimitTurn(current, target, 5f * Mathf.Deg2Rad);
        float angle = Vector2.Angle(current, result);
        Assert.LessOrEqual(angle, 5.1f, "单帧限转 5° 不可能瞬间反向");
        Assert.Greater(angle, 0f, "应该有转动");
    }

    [Test]
    public void Decide_FarDistance_ChargeReady()
    {
        var rng = new System.Random(42);
        // Boss 在原点,玩家在 10 格外——奔袭就绪
        var d = GrandQuadLocomotion.Decide(
            Vector2.zero, new Vector2(10f, 0f), Vector2.right,
            new Rect(-15, -10, 30, 20), 99f, 99f, rng);
        Assert.IsTrue(d.ReadyToCharge, "距离 ≥6 且冷却好 → 奔袭就绪");
    }

    [Test]
    public void Decide_CloseDistance_OrbitNotChase()
    {
        var rng = new System.Random(42);
        // 玩家在 3 格内——不直追,绕侧
        var d = GrandQuadLocomotion.Decide(
            Vector2.zero, new Vector2(3f, 0f), Vector2.right,
            new Rect(-15, -10, 30, 20), 99f, 99f, rng);
        Assert.AreEqual(GrandQuadLocomotion.MoveMode.Orbit, d.Mode,
            "近距(≤4)应 Orbit 绕侧,不 Chase 直追");
        Assert.IsFalse(d.ReadyToCharge, "距离不足不满足奔袭");
    }

    [Test]
    public void Decide_CooldownNotReady_NoCharge()
    {
        var rng = new System.Random(42);
        // 距离够但冷却没好(<3s)
        var d = GrandQuadLocomotion.Decide(
            Vector2.zero, new Vector2(10f, 0f), Vector2.right,
            new Rect(-15, -10, 30, 20), 99f, 1f, rng);
        Assert.IsFalse(d.ReadyToCharge, "冷却不足(1s<3s)不满足奔袭");
    }

    [Test]
    public void LimitTurn_ZeroDelta_NoRotation()
    {
        Vector2 result = GrandQuadLocomotion.LimitTurn(Vector2.up, Vector2.up, 0.1f);
        Assert.AreEqual(Vector2.up.x, result.x, 0.01f);
        Assert.AreEqual(Vector2.up.y, result.y, 0.01f);
    }
}
