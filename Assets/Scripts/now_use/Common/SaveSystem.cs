using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 本地存档系统（M4·v0.9.0）：JSON + persistentDataPath。
/// 只存 Meta 数据（魂/永久升级/历史记录）——局内进度不存档（敌人随层重新生成，无序列化需求，
/// 因此原计划的 Health/EnemyHealth 合并失去必要性，跳过并记录于文档）。
/// 损坏兜底：解析失败把坏档改名 .bak 保留，返回全新档。
/// 内置自检（R14 精神）：ContextMenu 跑一次保存→读回→断言，代替正式测试框架（v1 权衡）。
/// </summary>
public static class SaveSystem
{
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int souls;                  // 魂货币（死亡不清零）
        public string[] ownedUpgrades = Array.Empty<string>();   // 已购永久升级 id
        public int bestFloor = 1;          // 历史最深
        public int totalRuns;              // 总局数
        public int totalKills;             // 总击杀（占位，击杀统计后续接入）
    }

    private static string Path => System.IO.Path.Combine(Application.persistentDataPath, "save_v1.json");

    public static SaveData Load()
    {
        try
        {
            if (!File.Exists(Path)) return new SaveData();
            string json = File.ReadAllText(Path);
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data == null || data.version != 1) throw new Exception("版本不识别");
            return data;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Save] 存档损坏，已备份并重开新档：{e.Message}");
            try { if (File.Exists(Path)) File.Copy(Path, Path + ".bak", true); } catch { }
            return new SaveData();
        }
    }

    public static void Save(SaveData data)
    {
        try
        {
            File.WriteAllText(Path, JsonUtility.ToJson(data, true));
        }
        catch (Exception e)
        {
            Debug.LogError($"[Save] 存档写入失败：{e.Message}");
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Self Test: Save/Load Roundtrip")]
    private static void SelfTest()
    {
        var d = new SaveData { souls = 123, bestFloor = 7, ownedUpgrades = new[] { "tough" } };
        Save(d);
        var r = Load();
        bool ok = r.souls == 123 && r.bestFloor == 7 && r.ownedUpgrades.Length == 1 && r.ownedUpgrades[0] == "tough";
        Debug.Log(ok ? "[Save] 自检通过 ✓" : "[Save] 自检失败 ✗");
    }
#endif
}
