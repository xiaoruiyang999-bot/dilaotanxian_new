using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VS 收尾 版本化存档（V2 §19.5）：Profile（守灯厅持久）+ ActiveRun（断点续跑）两层 DTO，
/// JSON 落 Application.persistentDataPath，SchemaVersion 前置字段做迁移分发。
/// 迁移策略：读档时版本低于当前——逐版本升格函数处理；高于当前（降级打开）拒绝并备份原档。
/// 旧 PlayerPrefs 散键（星蓝币封存/叙事已读）在 v1 读档时一次性并入后清理。
/// </summary>
public static class SaveService
{
    public const int CurrentSchemaVersion = 2;

    // ---------- DTO ----------

    [Serializable]
    public class ProfileData
    {
        public int schemaVersion;
        public int bankedStarCoins;                  // 守灯厅封存（自 PlayerPrefs starcoin_banked 迁移）
        public List<string> readNarrativeIds = new List<string>();   // 已读叙事（自 narrative_read 迁移）
        public int totalRuns;
        public int totalBossKills;
    }

    [Serializable]
    public class ActiveRunData
    {
        public int schemaVersion;
        public string runId;
        public int mainSeed;
        public int floorNumber;
        public int currentHp;
        public int currentArmor;
        public float currentMana;
        public float currentClassResource;
        public bool resourcesInitialized;             // false=旧默认值/新 Run，不能覆盖职业满状态
        public int runCoins;                          // 随身星蓝币
        public string playableCharacterId;
        public List<string> relicIds = new List<string>();
        public int killsThisRun;
        public float temporaryRewardAttackBonus;

        // v2.1.0：ActiveRun 是唯一持久化 Run DTO；旧 RunManager 暂不消费这些字段。
        public DungeonGraphData dungeonGraph;
        public int currentNodeId = -1;
        public bool routeInitializationPending = true; // v1 档缺少真实图，不能凭旧房间推断
        public int inscriptionRank = InscriptionRankRules.MinRank;
        public RunBuildState build = new RunBuildState();
        public List<RunOfferData> pendingOffers = new List<RunOfferData>();
        public List<RunShopData> shops = new List<RunShopData>();
        public List<RunItemStackData> inventory = new List<RunItemStackData>();
        public string activeItemId;
        public List<RunBuffData> activeBuffs = new List<RunBuffData>();
        public List<RunTransactionRecord> transactions = new List<RunTransactionRecord>();
    }

    // ---------- 键与路径 ----------

    // EditMode 使用独立临时目录，不能清理玩家真实 persistentDataPath。
#if UNITY_EDITOR
    public static string EditorStorageRootOverride { get; set; }
#endif
    private static string StorageRoot
    {
        get
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(EditorStorageRootOverride)) return EditorStorageRootOverride;
#endif
            return Application.persistentDataPath;
        }
    }
    private static string ProfilePath => System.IO.Path.Combine(StorageRoot, "profile.json");
    private static string RunPath => System.IO.Path.Combine(StorageRoot, "active_run.json");
    private const string PrefsKeyCoins = "starcoin_banked";
    private const string PrefsKeyNarrative = "narrative_read";

    // ---------- Profile ----------

    public static ProfileData LoadProfile()
    {
        ProfileData profile = ReadFile<ProfileData>(ProfilePath);
        if (profile == null)
        {
            profile = new ProfileData { schemaVersion = CurrentSchemaVersion };
            MigrateFromPlayerPrefs(profile);
            SaveProfile(profile);
            return profile;
        }

        if (profile.schemaVersion > CurrentSchemaVersion)
        {
            Debug.LogWarning($"[Save] Profile 版本 {profile.schemaVersion} 高于当前 {CurrentSchemaVersion}（降级打开）——弃用并备份");
            BackupFile(ProfilePath);
            profile = new ProfileData { schemaVersion = CurrentSchemaVersion };
            SaveProfile(profile);
            return profile;
        }

        bool upgraded = profile.schemaVersion < CurrentSchemaVersion;
        while (profile.schemaVersion < CurrentSchemaVersion)
            profile = UpgradeProfile(profile, profile.schemaVersion);
        if (profile.readNarrativeIds == null) profile.readNarrativeIds = new List<string>();
        if (upgraded)
        {
            BackupFile(ProfilePath);
            SaveProfile(profile);
        }
        return profile;
    }

    public static void SaveProfile(ProfileData profile)
    {
        profile.schemaVersion = CurrentSchemaVersion;
        WriteFile(ProfilePath, profile);
    }

    // ---------- ActiveRun ----------

    /// <summary>只建立新 Run 的持久数据；图与当前节点由 v2.1.1 的路线入口初始化。</summary>
    public static ActiveRunData CreateNewRun(int mainSeed, PlayableCharacterId characterId)
    {
        return new ActiveRunData
        {
            schemaVersion = CurrentSchemaVersion,
            runId = Guid.NewGuid().ToString("N"),
            mainSeed = mainSeed,
            floorNumber = 1,
            playableCharacterId = characterId.ToString(),
            inscriptionRank = InscriptionRankRules.MinRank,
            currentNodeId = -1,
            routeInitializationPending = true,
        };
    }

    public static ActiveRunData LoadRun()
    {
        ActiveRunData run = ReadFile<ActiveRunData>(RunPath);
        if (run == null) return null;
        if (run.schemaVersion > CurrentSchemaVersion)
        {
            Debug.LogWarning("[Save] ActiveRun 版本高于当前——弃用（新 Run 重新生成）");
            BackupFile(RunPath);
            DeleteRun();
            return null;
        }
        bool upgraded = run.schemaVersion < CurrentSchemaVersion;
        while (run.schemaVersion < CurrentSchemaVersion)
            run = UpgradeRun(run, run.schemaVersion);
        EnsureRunDefaults(run);
        if (upgraded)
        {
            BackupFile(RunPath);
            SaveRun(run);
        }
        return run;
    }

    public static void SaveRun(ActiveRunData run)
    {
        if (run == null) throw new ArgumentNullException(nameof(run));
        EnsureRunDefaults(run);
        run.schemaVersion = CurrentSchemaVersion;
        WriteFile(RunPath, run);
    }

    public static void DeleteRun() => System.IO.File.Delete(RunPath);

    // ---------- 迁移（版本升格链；v1 = PlayerPrefs 散键并入） ----------

    private static void MigrateFromPlayerPrefs(ProfileData profile)
    {
#if UNITY_EDITOR
        // 测试目录不消费玩家真实 PlayerPrefs；实际存档目录仍执行一次性迁移。
        if (!string.IsNullOrEmpty(EditorStorageRootOverride)) return;
#endif
        if (PlayerPrefs.HasKey(PrefsKeyCoins))
        {
            profile.bankedStarCoins = PlayerPrefs.GetInt(PrefsKeyCoins, 0);
            PlayerPrefs.DeleteKey(PrefsKeyCoins);
        }
        string narrative = PlayerPrefs.GetString(PrefsKeyNarrative, "");
        if (!string.IsNullOrEmpty(narrative))
        {
            profile.readNarrativeIds.AddRange(narrative.Split(','));
            PlayerPrefs.DeleteKey(PrefsKeyNarrative);
        }
        PlayerPrefs.Save();
        Debug.Log($"[Save] v0 PlayerPrefs 迁移完成：封存 {profile.bankedStarCoins} / 叙事 {profile.readNarrativeIds.Count} 条");
    }

    private static ProfileData UpgradeProfile(ProfileData p, int fromVersion)
    {
        // fromVersion → fromVersion+1；无结构变更的版本直通（占位示范迁移链）
        p.schemaVersion = fromVersion + 1;
        return p;
    }

    private static ActiveRunData UpgradeRun(ActiveRunData r, int fromVersion)
    {
        if (fromVersion == 1)
        {
            // 旧档无 DAG / 当前节点真值。保留旧战斗字段，路线由 v2.1.1 明确初始化。
            r.runId = "legacy-" + r.mainSeed + "-" + r.floorNumber;
            r.dungeonGraph = null;
            r.currentNodeId = -1;
            r.routeInitializationPending = true;
            r.inscriptionRank = InscriptionRankRules.MinRank;
        }
        r.schemaVersion = fromVersion + 1;
        return r;
    }

    private static void EnsureRunDefaults(ActiveRunData run)
    {
        if (string.IsNullOrEmpty(run.runId)) run.runId = Guid.NewGuid().ToString("N");
        if (run.relicIds == null) run.relicIds = new List<string>();
        if (run.inscriptionRank < InscriptionRankRules.MinRank)
            run.inscriptionRank = InscriptionRankRules.MinRank;
        if (run.build == null) run.build = new RunBuildState();
        if (run.build.inscriptions == null) run.build.inscriptions = new List<OwnedInscriptionData>();
        if (run.pendingOffers == null) run.pendingOffers = new List<RunOfferData>();
        if (run.shops == null) run.shops = new List<RunShopData>();
        if (run.inventory == null) run.inventory = new List<RunItemStackData>();
        if (run.activeBuffs == null) run.activeBuffs = new List<RunBuffData>();
        if (run.transactions == null) run.transactions = new List<RunTransactionRecord>();
    }

    // ---------- IO ----------

    private static T ReadFile<T>(string path) where T : class
    {
        try
        {
            if (!System.IO.File.Exists(path)) return null;
            T data = JsonUtility.FromJson<T>(System.IO.File.ReadAllText(path));
            return data;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Save] 读档失败（{typeof(T).Name}）：{e.Message}——视为无档");
            BackupFile(path);
            return null;
        }
    }

    private static void WriteFile(string path, object data)
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllText(path, JsonUtility.ToJson(data, true));
        }
        catch (Exception e)
        {
            Debug.LogError($"[Save] 写档失败（{path}）：{e.Message}");
        }
    }

    private static void BackupFile(string path)
    {
        try
        {
            if (System.IO.File.Exists(path))
                System.IO.File.Copy(path, path + ".bak", true);
        }
        catch { }   // 备份失败不阻断主流程
    }
}
