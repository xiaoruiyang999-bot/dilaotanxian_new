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
    public const int CurrentSchemaVersion = 1;

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
        public int mainSeed;
        public int floorNumber;
        public int currentHp;
        public int currentArmor;
        public int runCoins;                          // 随身星蓝币
        public string playableCharacterId;
        public List<string> relicIds = new List<string>();
        public int killsThisRun;
    }

    // ---------- 键与路径 ----------

    private static string ProfilePath => Application.persistentDataPath + "/profile.json";
    private static string RunPath => Application.persistentDataPath + "/active_run.json";
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

        while (profile.schemaVersion < CurrentSchemaVersion)
            profile = UpgradeProfile(profile, profile.schemaVersion);
        return profile;
    }

    public static void SaveProfile(ProfileData profile)
    {
        profile.schemaVersion = CurrentSchemaVersion;
        WriteFile(ProfilePath, profile);
    }

    // ---------- ActiveRun ----------

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
        while (run.schemaVersion < CurrentSchemaVersion)
            run = UpgradeRun(run, run.schemaVersion);
        return run;
    }

    public static void SaveRun(ActiveRunData run)
    {
        run.schemaVersion = CurrentSchemaVersion;
        WriteFile(RunPath, run);
    }

    public static void DeleteRun() => System.IO.File.Delete(RunPath);

    // ---------- 迁移（版本升格链；v1 = PlayerPrefs 散键并入） ----------

    private static void MigrateFromPlayerPrefs(ProfileData profile)
    {
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
        r.schemaVersion = fromVersion + 1;
        return r;
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
