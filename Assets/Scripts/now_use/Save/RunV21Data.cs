using System;
using System.Collections.Generic;

/// <summary>稳定事务来源；枚举值写入存档，不得重排。</summary>
public enum RunTransactionSource
{
    SageOffer = 0,
    SupplyReward = 1,
    ShopOfferPurchase = 2,
    RankUpgrade = 3,
}

[Serializable]
public sealed class RunTransactionRecord
{
    public string transactionId;
    public RunTransactionSource source;
    public int nodeId;
    public int sequence;
    public int coinDelta;
    public int rankAfter;
    public string selectedAbilityId;
}

[Serializable]
public sealed class RunOfferData
{
    public string offerId;
    public int nodeId;
    public RunTransactionSource source;
    public int sequence;
    public int seed;
    public int rankAtGeneration;
    public List<string> candidateAbilityIds = new List<string>();
    public string selectedAbilityId;
    public bool paymentCommitted;
}

[Serializable]
public sealed class RunShopStockData
{
    public string itemId;
    public int price;
    public int remaining;
}

[Serializable]
public sealed class RunShopData
{
    public int nodeId;
    public int seed;
    public List<RunShopStockData> stock = new List<RunShopStockData>();
    public bool paidOfferPurchased;
}

[Serializable]
public sealed class RunItemStackData
{
    public string itemId;
    public int count;
}

[Serializable]
public sealed class RunBuffData
{
    public string buffId;
    public float remainingSeconds;
    public int stacks;
}

public enum RunTransactionResult
{
    Applied,
    AlreadyApplied,
    InvalidState,
    InsufficientFunds,
}

/// <summary>低频纯数据交易规则。运行时调用方在 Applied 后应立即 SaveRun。</summary>
public static class RunTransactionRules
{
    public static string BuildId(string runId, int nodeId, RunTransactionSource source, int sequence)
    {
        if (string.IsNullOrEmpty(runId) || nodeId < 0 || sequence < 0)
            throw new ArgumentException("事务必须具有 RunId、节点 ID 和非负序号");
        return runId + ":" + nodeId + ":" + (int)source + ":" + sequence;
    }

    public static bool Contains(SaveService.ActiveRunData run, string transactionId)
    {
        if (run?.transactions == null || string.IsNullOrEmpty(transactionId)) return false;
        foreach (RunTransactionRecord transaction in run.transactions)
            if (transaction != null && transaction.transactionId == transactionId) return true;
        return false;
    }

    /// <summary>expectedRank 是操作前等级；同一商店同一级的重复回调返回 AlreadyApplied。</summary>
    public static RunTransactionResult TryUpgradeRank(SaveService.ActiveRunData run,
        int shopNodeId, int expectedRank)
    {
        if (run == null || string.IsNullOrEmpty(run.runId) || shopNodeId < 0
            || expectedRank < InscriptionRankRules.MinRank
            || expectedRank >= InscriptionRankRules.MaxRank)
            return RunTransactionResult.InvalidState;

        DungeonGraphNode shop = run.dungeonGraph?.Get(shopNodeId);
        if (shop == null || shop.Type != NodeType.Shop || run.currentNodeId != shopNodeId
            || !shop.Visited || shop.Completed)
            return RunTransactionResult.InvalidState;

        int targetRank = expectedRank + 1;
        string transactionId = BuildId(run.runId, shopNodeId,
            RunTransactionSource.RankUpgrade, targetRank);
        if (Contains(run, transactionId)) return RunTransactionResult.AlreadyApplied;
        if (run.inscriptionRank != expectedRank) return RunTransactionResult.InvalidState;

        int price = InscriptionRankRules.GetPriceToReach(targetRank);
        if (run.runCoins < price) return RunTransactionResult.InsufficientFunds;

        if (run.transactions == null) run.transactions = new List<RunTransactionRecord>();
        run.runCoins -= price;
        run.inscriptionRank = targetRank;
        run.transactions.Add(new RunTransactionRecord
        {
            transactionId = transactionId,
            source = RunTransactionSource.RankUpgrade,
            nodeId = shopNodeId,
            sequence = targetRank,
            coinDelta = -price,
            rankAfter = targetRank,
        });
        return RunTransactionResult.Applied;
    }
}
