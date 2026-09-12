using UnityEngine;

/// <summary>
/// <summary>
/// v2.0.6 星蓝币封存钱包（V2 §13.1 已定案·首轮测试值）：
/// 随身钱包 = PlayerStats.Coins（Run 内拾取/商店消费，单一真值不双轨）；
/// 守灯厅封存 = 本类（PlayerPrefs 持久），死亡带回随身 30%、击败 Boss 过层带回 100%
///（安全撤离 60% 待 T-06 定案后接入）。结算即清零随身——新 Run 从 0 开始。
/// 比例数据化（V2：不写死在 UI 文案），测试后按经济节奏调整。
/// </summary>
public static class StarCoinBank
{
    private const string KeyBanked = "starcoin_banked";

    [Header("结算比例（V2 §13.1 首轮测试值）")]
    public const float DeathKeepRatio = 0.30f;    // 死亡：随身 30% 封存
    public const float WithdrawKeepRatio = 0.60f; // 安全撤离：60%（T-06 定案后接入）
    public const float BossClearRatio = 1.00f;    // 击败 Boss 过层：100%

    /// <summary>已封存星蓝币（守灯厅钱包，只读）。</summary>
    public static int Banked => PlayerPrefs.GetInt(KeyBanked, 0);

    /// <summary>死亡结算：随身按 30% 封存并清零。返回本次封存数。</summary>
    public static int BankOnDeath(int runCoins)
    {
        int kept = Mathf.FloorToInt(runCoins * DeathKeepRatio);
        Deposit(kept);
        return kept;
    }

    /// <summary>Boss 层通关结算（过层传送门）：随身 100% 封存并清零。</summary>
    public static int BankOnBossClear(int runCoins)
    {
        Deposit(runCoins);
        return runCoins;
    }

    private static void Deposit(int amount)
    {
        if (amount > 0) PlayerPrefs.SetInt(KeyBanked, Banked + amount);
        PlayerPrefs.Save();
    }

    /// <summary>测试/调试：清空封存。</summary>
    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(KeyBanked);
        PlayerPrefs.Save();
    }
}
