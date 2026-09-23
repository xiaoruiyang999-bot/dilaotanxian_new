using System.Linq;
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// VS 收尾 存档门禁（V2 §19.5/§23.1）：Profile/ActiveRun JSON 往返、SchemaVersion 写入、
/// 版本降级拒用、Profile 字段健全。文件 IO 走 persistentDataPath 真实读写（测试后清理）。
/// </summary>
public class SaveServiceTests
{
    private string testRoot;
    private string ProfilePath => Path.Combine(testRoot, "profile.json");
    private string RunPath => Path.Combine(testRoot, "active_run.json");

    [SetUp]
    public void SetUp()
    {
        testRoot = Path.Combine(Path.GetTempPath(), "v210-save-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        SaveService.EditorStorageRootOverride = testRoot;
    }

    [TearDown]
    public void TearDown()
    {
        SaveService.EditorStorageRootOverride = null;
        if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
    }

    [Test]
    public void Profile_RoundTripsWithSchemaVersion()
    {
        var profile = new SaveService.ProfileData
        {
            bankedStarCoins = 123,
            readNarrativeIds = { "open_awaken", "prog_lamp" },
            totalRuns = 7,
        };
        SaveService.SaveProfile(profile);

        SaveService.ProfileData loaded = SaveService.LoadProfile();
        Assert.AreEqual(SaveService.CurrentSchemaVersion, loaded.schemaVersion, "保存即写当前版本");
        Assert.AreEqual(123, loaded.bankedStarCoins);
        Assert.AreEqual(2, loaded.readNarrativeIds.Count);
        Assert.AreEqual("prog_lamp", loaded.readNarrativeIds[1]);
        Assert.AreEqual(7, loaded.totalRuns);
    }

    [Test]
    public void ActiveRun_RoundTrips()
    {
        var run = new SaveService.ActiveRunData
        {
            runId = "test-run",
            mainSeed = 987654,
            floorNumber = 3,
            currentHp = 80,
            currentMana = 29,
            resourcesInitialized = true,
            runCoins = 45,
            playableCharacterId = "Werewolf",
            relicIds = { "bleed_fang", "moon_fur" },
            killsThisRun = 31,
            inscriptionRank = 2,
            currentNodeId = 3,
            routeInitializationPending = false,
            dungeonGraph = DungeonGraphGenerator.Generate(987654),
        };
        DungeonGraphNode node = run.dungeonGraph.Get(3);
        node.Discovered = true;
        node.TrySelect();
        node.TryVisit();
        node.TryCompleteObjective();
        run.build.TryAcquire("rift_sharp_eye", PlayableCharacterId.Werewolf, out _);
        run.pendingOffers.Add(new RunOfferData
        {
            offerId = "test-offer", nodeId = 3, rankAtGeneration = 2,
            candidateAbilityIds = { "rift_sharp_eye", "swift_edge", "oath_drinking_blade" },
        });
        run.shops.Add(new RunShopData { nodeId = 5, seed = 42,
            stock = { new RunShopStockData { itemId = "potion", price = 8, remaining = 2 } } });
        run.inventory.Add(new RunItemStackData { itemId = "potion", count = 1 });
        run.activeBuffs.Add(new RunBuffData { buffId = "haste", remainingSeconds = 4.5f, stacks = 1 });
        run.transactions.Add(new RunTransactionRecord
        {
            transactionId = "test-run:5:3:2", source = RunTransactionSource.RankUpgrade,
            nodeId = 5, sequence = 2, coinDelta = -25, rankAfter = 2,
        });
        SaveService.SaveRun(run);

        SaveService.ActiveRunData loaded = SaveService.LoadRun();
        Assert.AreEqual(987654, loaded.mainSeed);
        Assert.AreEqual(3, loaded.floorNumber);
        Assert.AreEqual(45, loaded.runCoins);
        Assert.AreEqual("Werewolf", loaded.playableCharacterId);
        Assert.IsTrue(loaded.relicIds.SequenceEqual(new[] { "bleed_fang", "moon_fur" }));
        Assert.AreEqual(31, loaded.killsThisRun);
        Assert.AreEqual(29, loaded.currentMana);
        Assert.IsTrue(loaded.resourcesInitialized);
        Assert.AreEqual(2, loaded.inscriptionRank);
        Assert.AreEqual(3, loaded.currentNodeId);
        Assert.IsFalse(loaded.routeInitializationPending);
        Assert.AreEqual(run.dungeonGraph.Nodes.Count, loaded.dungeonGraph.Nodes.Count);
        Assert.IsTrue(loaded.dungeonGraph.Get(3).ObjectiveCompleted);
        Assert.IsFalse(loaded.dungeonGraph.Get(3).RewardResolved, "目标完成不等于领奖");
        Assert.AreEqual(run.dungeonGraph.Get(3).RewardSeed, loaded.dungeonGraph.Get(3).RewardSeed);
        Assert.AreEqual("rift_sharp_eye", loaded.build.inscriptions[0].abilityId);
        Assert.AreEqual(3, loaded.pendingOffers[0].candidateAbilityIds.Count);
        Assert.AreEqual("potion", loaded.shops[0].stock[0].itemId);
        Assert.AreEqual(1, loaded.inventory[0].count);
        Assert.AreEqual(4.5f, loaded.activeBuffs[0].remainingSeconds);
        Assert.AreEqual(-25, loaded.transactions[0].coinDelta);
        Assert.IsTrue(RunTransactionRules.Contains(loaded, "test-run:5:3:2"));
    }

    [Test]
    public void MissingFiles_ReturnNull_FreshProfileHasDefaults()
    {
        Assert.IsNull(SaveService.LoadRun(), "无 ActiveRun 文件 = null（新 Run）");

        SaveService.ProfileData fresh = SaveService.LoadProfile();
        Assert.IsNotNull(fresh, "无 Profile 文件 = 新建默认档");
        Assert.AreEqual(SaveService.CurrentSchemaVersion, fresh.schemaVersion);
        Assert.AreEqual(0, fresh.bankedStarCoins);
        Assert.IsNotNull(fresh.readNarrativeIds);
    }

    [Test]
    public void CorruptFile_DoesNotThrow_ReturnsFresh()
    {
        File.WriteAllText(ProfilePath, "{ not valid json !!!");
        Assert.DoesNotThrow(() =>
        {
            SaveService.ProfileData profile = SaveService.LoadProfile();
            Assert.AreEqual(0, profile.bankedStarCoins, "坏档回退默认");
        }, "坏 JSON 静默回退（并自动备份原档）");
        Assert.IsTrue(File.Exists(ProfilePath + ".bak"), "坏档已备份");
    }

    [Test]
    public void DeleteRun_RemovesFile()
    {
        SaveService.SaveRun(new SaveService.ActiveRunData { mainSeed = 1 });
        SaveService.DeleteRun();
        Assert.IsNull(SaveService.LoadRun());
    }

    [Test]
    public void V1ActiveRun_UpgradesWithoutInventingGraph()
    {
        File.WriteAllText(RunPath,
            "{\"schemaVersion\":1,\"mainSeed\":77,\"floorNumber\":2,\"currentHp\":38," +
            "\"currentArmor\":12,\"runCoins\":64,\"playableCharacterId\":\"Werewolf\"," +
            "\"relicIds\":[\"legacy_relic\"],\"killsThisRun\":9}");

        SaveService.ActiveRunData run = SaveService.LoadRun();
        Assert.AreEqual(SaveService.CurrentSchemaVersion, run.schemaVersion);
        Assert.AreEqual("legacy-77-2", run.runId);
        Assert.AreEqual(38, run.currentHp);
        Assert.AreEqual(12, run.currentArmor);
        Assert.AreEqual(64, run.runCoins);
        Assert.AreEqual("legacy_relic", run.relicIds[0]);
        Assert.AreEqual(1, run.inscriptionRank);
        Assert.IsNull(run.dungeonGraph);
        Assert.AreEqual(-1, run.currentNodeId);
        Assert.IsTrue(run.routeInitializationPending);
        Assert.IsTrue(File.Exists(RunPath + ".bak"));
        Assert.AreEqual(2, JsonUtility.FromJson<SaveService.ActiveRunData>(File.ReadAllText(RunPath)).schemaVersion);
    }

    [Test]
    public void NewRun_HasUniqueIdentityAndNoInventedRoute()
    {
        SaveService.ActiveRunData first = SaveService.CreateNewRun(15, PlayableCharacterId.Werewolf);
        SaveService.ActiveRunData second = SaveService.CreateNewRun(15, PlayableCharacterId.Werewolf);
        Assert.AreNotEqual(first.runId, second.runId);
        Assert.AreEqual("Werewolf", first.playableCharacterId);
        Assert.AreEqual(1, first.inscriptionRank);
        Assert.IsNull(first.dungeonGraph);
        Assert.AreEqual(-1, first.currentNodeId);
        Assert.IsTrue(first.routeInitializationPending);
        Assert.IsFalse(first.resourcesInitialized, "新 Run 尚未应用职业属性，资源快照必须保持未初始化");
    }

    [Test]
    public void WerewolfEntry_IgnoresPrefabDefaultSnapshot_ButPreservesRealLegacyResources()
    {
        GameObject player = new GameObject("WerewolfResourceRestoreTest");
        try
        {
            PlayerStats stats = player.AddComponent<PlayerStats>();
            Health health = player.AddComponent<Health>();
            PlayableCharacterDefinition definition = Resources.Load<PlayableCharacterDefinition>(
                "Characters/Character_Werewolf");
            Assert.IsNotNull(definition);

            stats.ApplyPlayableCharacter(definition);
            var broken = SaveService.CreateNewRun(1, PlayableCharacterId.Werewolf);
            broken.currentHp = 5;
            broken.currentArmor = 0;
            broken.currentMana = 0f;
            broken.runCoins = 17;
            RunManager.RestoreVitalResourcesForEntry(broken, health, stats);
            Assert.AreEqual(health.MaxHealth, health.CurrentHealth);
            Assert.AreEqual(stats.MaxArmor, stats.CurrentArmor);
            Assert.AreEqual(stats.MaxMana, stats.CurrentMana);
            Assert.AreEqual(17, stats.Coins);

            stats.ApplyPlayableCharacter(definition);
            var realLegacy = SaveService.CreateNewRun(2, PlayableCharacterId.Werewolf);
            realLegacy.currentHp = 37;
            realLegacy.currentArmor = 12;
            realLegacy.currentMana = 8f;
            RunManager.RestoreVitalResourcesForEntry(realLegacy, health, stats);
            Assert.AreEqual(37f, health.CurrentHealth);
            Assert.AreEqual(12f, stats.CurrentArmor);
            Assert.AreEqual(8f, stats.CurrentMana);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(player);
        }
    }
}
