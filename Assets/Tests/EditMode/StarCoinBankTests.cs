using NUnit.Framework;

/// <summary>
/// v2.0.6 星蓝币双钱包门禁（V2 §13.1 首轮测试值）：死亡 30% / 撤离 60%（待接入）/Boss 100%
/// 封存比例、封存持久、PlayerPrefs 测试隔离。
/// </summary>
public class StarCoinBankTests
{
    [SetUp]
    public void SetUp() => StarCoinBank.ResetAll();

    [TearDown]
    public void TearDown() => StarCoinBank.ResetAll();

    [Test]
    public void DeathSettlement_Keeps30Percent_Floored()
    {
        Assert.AreEqual(3, StarCoinBank.BankOnDeath(10), "10 币死亡带回 30% = 3");
        Assert.AreEqual(0, StarCoinBank.BankOnDeath(2), "2 币 30%=0.6 向下取整 = 0");
        Assert.AreEqual(3, StarCoinBank.Banked, "封存累计");
        Assert.AreEqual(6, StarCoinBank.BankOnDeath(20), "第二次结算叠加前存");
        Assert.AreEqual(9, StarCoinBank.Banked);
    }

    [Test]
    public void BossClearSettlement_Keeps100Percent()
    {
        Assert.AreEqual(57, StarCoinBank.BankOnBossClear(57));
        Assert.AreEqual(57, StarCoinBank.Banked);
    }

    [Test]
    public void Ratios_MatchV2TestValues()
    {
        Assert.AreEqual(0.30f, StarCoinBank.DeathKeepRatio, 0.0001f);
        Assert.AreEqual(0.60f, StarCoinBank.WithdrawKeepRatio, 0.0001f);
        Assert.AreEqual(1.00f, StarCoinBank.BossClearRatio, 0.0001f);
    }

    [Test]
    public void ZeroCoins_SettleNothing()
    {
        Assert.AreEqual(0, StarCoinBank.BankOnDeath(0));
        Assert.AreEqual(0, StarCoinBank.Banked);
    }
}
