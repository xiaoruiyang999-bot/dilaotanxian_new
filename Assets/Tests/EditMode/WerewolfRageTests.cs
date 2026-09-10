using NUnit.Framework;

public class WerewolfRageTests
{
    [Test]
    public void DamageGain_UsesDamageCoefficient()
    {
        float gain = WerewolfRage.CalculateDamageGain(20f, 0.5f, 15f, 0f, 20f);
        Assert.AreEqual(10f, gain, 0.0001f);
    }

    [Test]
    public void DamageGain_RespectsPerEventCap()
    {
        float gain = WerewolfRage.CalculateDamageGain(200f, 0.5f, 15f, 0f, 20f);
        Assert.AreEqual(15f, gain, 0.0001f);
    }

    [Test]
    public void DamageGain_RespectsRemainingFrameBudget()
    {
        float gain = WerewolfRage.CalculateDamageGain(100f, 0.5f, 15f, 17f, 20f);
        Assert.AreEqual(3f, gain, 0.0001f);
    }

    [Test]
    public void DamageGain_InvalidDamageDoesNotCharge()
    {
        Assert.AreEqual(0f, WerewolfRage.CalculateDamageGain(0f, 0.5f, 15f, 0f, 20f));
        Assert.AreEqual(0f, WerewolfRage.CalculateDamageGain(-10f, 0.5f, 15f, 0f, 20f));
    }
}
