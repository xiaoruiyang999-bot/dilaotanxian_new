using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能树持久化（v1.1.47，M4 meta 层首块）：魂晶余额 + 已解锁节点 id。
/// 存储走 PlayerPrefs（项目先例：音量即此路；无外部存档依赖）：
/// - "skilltree_unlocked"：逗号分隔节点 id（增量追加，读时容错解析）；
/// - "skilltree_essence"：int 余额；
/// - "skilltree_init"：首启赠送 10 魂晶的一次性标记（让新玩家立刻能点开第一个节点）。
/// 魂晶来源：死亡结算按击杀数入账（DeathPanel → GrantRunEssence，每击杀 1 枚）。
/// 本类只管数据，不校验树规则——解锁前置/余额校验由 SkillTreeUI 消费 SkillTreeDef 完成。
/// </summary>
public static class SkillTreeSave
{
    private const string KeyUnlocked = "skilltree_unlocked";
    private const string KeyEssence = "skilltree_essence";
    private const string KeyInit = "skilltree_init";
    private const int FirstRunGift = 10;   // 首启赠送：够点任意一条线的首节点

    private static readonly List<int> unlocked = new List<int>();
    private static bool loaded;

    /// <summary>当前魂晶余额。</summary>
    public static int Essence => Load();   // 触发懒加载（含首启赠送）

    /// <summary>已解锁节点 id（只读）。</summary>
    public static IReadOnlyList<int> UnlockedIds { get { Load(); return unlocked; } }

    public static bool IsUnlocked(int id) { Load(); return unlocked.Contains(id); }

    /// <summary>追加魂晶（死亡结算/未来其他来源）。</summary>
    public static void AddEssence(int amount)
    {
        if (amount <= 0) return;
        Load();
        PlayerPrefs.SetInt(KeyEssence, PlayerPrefs.GetInt(KeyEssence, 0) + amount);
        PlayerPrefs.Save();
    }

    /// <summary>尝试花费魂晶：不足不扣返回 false。</summary>
    public static bool TrySpendEssence(int amount)
    {
        if (amount < 0) return false;
        Load();
        int balance = PlayerPrefs.GetInt(KeyEssence, 0);
        if (balance < amount) return false;
        PlayerPrefs.SetInt(KeyEssence, balance - amount);
        PlayerPrefs.Save();
        return true;
    }

    /// <summary>记录解锁（只追加去重；前置/余额校验在调用方）。</summary>
    public static void Unlock(int id)
    {
        Load();
        if (unlocked.Contains(id)) return;
        unlocked.Add(id);
        PlayerPrefs.SetString(KeyUnlocked, string.Join(",", unlocked));
        PlayerPrefs.Save();
    }

    /// <summary>测试/调试：清空全部进度（清档 ≠ 新玩家——保留初始化标记，不重复触发首启赠送；
    /// 真正的新玩家首启 = 三个键全部不存在时的首次 Load）。</summary>
    public static void ResetAll()
    {
        unlocked.Clear();
        loaded = true;   // 维持已加载态：后续读取不再走 Load 的赠送分支
        PlayerPrefs.DeleteKey(KeyUnlocked);
        PlayerPrefs.DeleteKey(KeyEssence);
        PlayerPrefs.SetInt(KeyInit, 1);
        PlayerPrefs.Save();
    }

    private static int Load()
    {
        if (!loaded)
        {
            loaded = true;
            unlocked.Clear();
            string raw = PlayerPrefs.GetString(KeyUnlocked, "");
            if (!string.IsNullOrEmpty(raw))
                foreach (var part in raw.Split(','))
                    if (int.TryParse(part.Trim(), out int id) && !unlocked.Contains(id))
                        unlocked.Add(id);

            // 首启赠送（一次性）：只看标记，不追加余额叠加
            if (!PlayerPrefs.HasKey(KeyInit))
            {
                PlayerPrefs.SetInt(KeyInit, 1);
                if (!PlayerPrefs.HasKey(KeyEssence))
                    PlayerPrefs.SetInt(KeyEssence, FirstRunGift);
                PlayerPrefs.Save();
            }
        }
        return PlayerPrefs.GetInt(KeyEssence, 0);
    }
}
