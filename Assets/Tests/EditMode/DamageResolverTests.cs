using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 减伤甲结算纯函数边界值（v1.0.4，审查报告 §六最小测试集第 1 项）。
/// 公式（v0.7.1 §三）：armor>0 → 扣血=D×(1−R)、扣甲=D×L（钳 0、溢出不转嫁）；armor≤0 → 全额扣血。
/// </summary>
public class DamageResolverTests
{
    private const float Eps = 1e-4f;

    [Test]
    public void WithArmor_SplitsHpDamageAndArmorLoss()
    {
        float hp = DamageResolver.ApplyArmor(10f, 20f, 0.3f, 0.5f, out float armorAfter);
        Assert.AreEqual(7f, hp, Eps);          // 10×(1−0.3)
        Assert.AreEqual(15f, armorAfter, Eps); // 20−10×0.5
    }

    [Test]
    public void ArmorExhausted_ClampsToZero_NoHpSpillover()
    {
        float hp = DamageResolver.ApplyArmor(10f, 3f, 0.5f, 1f, out float armorAfter);
        Assert.AreEqual(5f, hp, Eps);   // 本次仍按有甲结算（溢出不转嫁）
        Assert.AreEqual(0f, armorAfter, Eps);
    }

    [Test]
    public void ZeroArmor_FullDamage_ReduceIgnored()
    {
        float hp = DamageResolver.ApplyArmor(10f, 0f, 0.9f, 1f, out float armorAfter);
        Assert.AreEqual(10f, hp, Eps);
        Assert.AreEqual(0f, armorAfter, Eps);
    }

    [Test]
    public void ZeroDamage_NoChange()
    {
        float hp = DamageResolver.ApplyArmor(0f, 5f, 0.3f, 0.5f, out float armorAfter);
        Assert.AreEqual(0f, hp, Eps);
        Assert.AreEqual(5f, armorAfter, Eps);
    }

    [Test]
    public void MaxReduce_90Percent_MinLoss()
    {
        float hp = DamageResolver.ApplyArmor(10f, 20f, 0.9f, 0.01f, out float armorAfter);
        Assert.AreEqual(1f, hp, Eps);
        Assert.AreEqual(19.9f, armorAfter, 1e-3f);
    }
}
