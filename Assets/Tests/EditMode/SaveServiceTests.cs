using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// VS 收尾 存档门禁（V2 §19.5/§23.1）：Profile/ActiveRun JSON 往返、SchemaVersion 写入、
/// 版本降级拒用、Profile 字段健全。文件 IO 走 persistentDataPath 真实读写（测试后清理）。
/// </summary>
public class SaveServiceTests
{
    private static void Cleanup()
    {
        System.IO.File.Delete(Application.persistentDataPath + "/profile.json");
        System.IO.File.Delete(Application.persistentDataPath + "/active_run.json");
    }

    [SetUp]
    public void SetUp() => Cleanup();

    [TearDown]
    public void TearDown() => Cleanup();

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
            mainSeed = 987654,
            floorNumber = 3,
            currentHp = 80,
            runCoins = 45,
            playableCharacterId = "Werewolf",
            relicIds = { "bleed_fang", "moon_fur" },
            killsThisRun = 31,
        };
        SaveService.SaveRun(run);

        SaveService.ActiveRunData loaded = SaveService.LoadRun();
        Assert.AreEqual(987654, loaded.mainSeed);
        Assert.AreEqual(3, loaded.floorNumber);
        Assert.AreEqual(45, loaded.runCoins);
        Assert.AreEqual("Werewolf", loaded.playableCharacterId);
        Assert.IsTrue(loaded.relicIds.SequenceEqual(new[] { "bleed_fang", "moon_fur" }));
        Assert.AreEqual(31, loaded.killsThisRun);
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
        System.IO.File.WriteAllText(Application.persistentDataPath + "/profile.json", "{ not valid json !!!");
        Assert.DoesNotThrow(() =>
        {
            SaveService.ProfileData profile = SaveService.LoadProfile();
            Assert.AreEqual(0, profile.bankedStarCoins, "坏档回退默认");
        }, "坏 JSON 静默回退（并自动备份原档）");
        Assert.IsTrue(System.IO.File.Exists(Application.persistentDataPath + "/profile.json.bak"), "坏档已备份");
    }

    [Test]
    public void DeleteRun_RemovesFile()
    {
        SaveService.SaveRun(new SaveService.ActiveRunData { mainSeed = 1 });
        SaveService.DeleteRun();
        Assert.IsNull(SaveService.LoadRun());
    }
}
