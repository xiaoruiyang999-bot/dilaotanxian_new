using NUnit.Framework;
using UnityEngine;

/// <summary>
/// v1.2.1 T-01 攻击方向灰盒：方向离散纯函数门禁。
/// A=Horizontal 恒左右（失落城堡式）；B=FourWay 主轴离散（对角相等固定优先水平防浮点跳向）。
/// 预览/踏步/Hitbox 同源消费 Resolve 的结果——本测试锁住离散合同，防"只转视觉不转判定"回归。
/// </summary>
public class AttackDirectionResolverTests
{
    private static void AssertCardinal(Vector2 v)
    {
        Assert.That(v.sqrMagnitude, Is.EqualTo(1f).Within(0.0001f), "必须为单位向量");
        bool cardinal = Mathf.Abs(v.x) < 0.0001f || Mathf.Abs(v.y) < 0.0001f;
        Assert.IsTrue(cardinal, $"必须离散到四正向：{v}");
    }

    [Test]
    public void Horizontal_AlwaysLeftOrRight()
    {
        // 朝向优先；朝向为零回退期望方向的水平分量；全零回退右
        Assert.AreEqual(Vector2.right, AttackDirectionResolver.Resolve(
            AttackDirectionPrototypeMode.Horizontal, new Vector2(0.3f, 0.9f), Vector2.right));
        Assert.AreEqual(Vector2.left, AttackDirectionResolver.Resolve(
            AttackDirectionPrototypeMode.Horizontal, Vector2.up, Vector2.left));
        Assert.AreEqual(Vector2.left, AttackDirectionResolver.Resolve(
            AttackDirectionPrototypeMode.Horizontal, new Vector2(-0.7f, 0.7f), Vector2.zero));
        Assert.AreEqual(Vector2.right, AttackDirectionResolver.Resolve(
            AttackDirectionPrototypeMode.Horizontal, Vector2.zero, Vector2.zero));
    }

    [Test]
    public void FourWay_PicksDominantAxis_CardinalOnly()
    {
        Assert.AreEqual(Vector2.up, AttackDirectionResolver.Resolve(
            AttackDirectionPrototypeMode.FourWay, new Vector2(0.2f, 0.98f), Vector2.right));
        Assert.AreEqual(Vector2.left, AttackDirectionResolver.Resolve(
            AttackDirectionPrototypeMode.FourWay, new Vector2(-0.9f, 0.1f), Vector2.right));
        Assert.AreEqual(Vector2.down, AttackDirectionResolver.Resolve(
            AttackDirectionPrototypeMode.FourWay, new Vector2(0.1f, -0.9f), Vector2.right));
    }

    [Test]
    public void FourWay_DiagonalTie_PrefersHorizontal()
    {
        // 精确相等（|x|==|y|）固定取水平；邻域跨界各取主轴（方向在起手时锁定整段，
        // 跨界翻转无实战影响——灰盒阶段不做带状态的迟滞）
        Vector2 exact = AttackDirectionResolver.Resolve(
            AttackDirectionPrototypeMode.FourWay, new Vector2(0.7071068f, 0.7071068f), Vector2.right);
        Assert.AreEqual(Vector2.right, exact, "精确对角固定优先水平");

        Assert.AreEqual(Vector2.up, AttackDirectionResolver.Resolve(
            AttackDirectionPrototypeMode.FourWay, new Vector2(0.7071f, 0.7072f), Vector2.right), "y 略占优取上");
        Assert.AreEqual(Vector2.right, AttackDirectionResolver.Resolve(
            AttackDirectionPrototypeMode.FourWay, new Vector2(0.7072f, 0.7071f), Vector2.right), "x 略占优取右");
    }

    [Test]
    public void FourWay_ZeroDesired_FallsBackToFacing()
    {
        Assert.AreEqual(Vector2.left, AttackDirectionResolver.Resolve(
            AttackDirectionPrototypeMode.FourWay, Vector2.zero, Vector2.left));
        Assert.AreEqual(Vector2.right, AttackDirectionResolver.Resolve(
            AttackDirectionPrototypeMode.FourWay, Vector2.zero, Vector2.right));
    }

    [Test]
    public void AllModes_NeverReturnDiagonal()
    {
        var samples = new[]
        {
            new Vector2(0.6f, 0.8f), new Vector2(-0.6f, 0.8f), new Vector2(0.5f, -0.5f),
            new Vector2(-0.001f, 0.001f), new Vector2(1f, 0f), new Vector2(0f, -1f),
        };
        foreach (var s in samples)
        {
            AssertCardinal(AttackDirectionResolver.Resolve(
                AttackDirectionPrototypeMode.Horizontal, s, Vector2.left));
            AssertCardinal(AttackDirectionResolver.Resolve(
                AttackDirectionPrototypeMode.FourWay, s, Vector2.left));
        }
    }
}
