using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// v2.0.9 局内天赋定义（任务计划 §6）：ID/职业/流派/阶位/前置/名称/描述/数值——
/// 静态数值唯一真源（不复制进 UI/运行层）。首发只录狼人四分支 16 节点（P0）。
/// </summary>
/// <summary>节点兼容类型（v2.0.9 修订版 §3.3）。</summary>
public enum TalentCompat
{
    Universal = 0,      // 完全通用：所有职业可用
    TagUniversal = 1,   // 标签通用：具有指定攻击/状态标签的职业可用
    ClassAdapted = 2,   // 职业适配：名称结构通用，实际回报按职业映射（怒痕/法力/蓄力）
    NotReady = 3,       // 内容未就绪：可预览，不进入奖励池
}

[CreateAssetMenu(menuName = "Dungeon/Talent", fileName = "Talent_")]
public class TalentDefinition : ScriptableObject
{
    [Header("身份（ID 合同：{职业}_{分支}_{阶}，如 Werewolf_F01_T1）")]
    public string talentId;
    [Tooltip("兼容类型（§3.3）：完全通用/标签通用/职业适配/内容未就绪")]
    public TalentCompat compatType = TalentCompat.Universal;
    [Tooltip("所需标签（标签通用时生效）：melee/ranged/dot/armorbreak/charge/buff")]
    public string[] tags = Array.Empty<string>();
    [Tooltip("流派编号（F01~F19，六主枝分类见任务计划 §2.1）")]
    public string branchId;
    public Color branchTint = new Color(0.8f, 0.65f, 0.35f);
    [Range(1, 4)] public int tier;

    [Header("前置（空/自身 ID = 一阶无前置）")]
    public string prerequisiteId;

    [Header("文本（数值写效果字段，描述引用不复制）")]
    public string displayName;
    [TextArea] public string description;
    [TextArea] public string drawback;    // 代价——不得只写收益（任务计划 §5.2）

    [Header("效果载荷（P0 口径：按分支取用，不建通用公式）")]
    [Tooltip("效果参数（各分支运行模块解释）")]
    public float[] values = Array.Empty<float>();
}

/// <summary>
/// 本局天赋状态（任务计划 §6：纯 C#，归 RunStateCarrier/ActiveRun，不依赖 GameObject）。
/// 唯一战力真值 = 已获得天赋 ID 集合；石碑/奖励/暂停页都只读写这份集合。
/// </summary>
[Serializable]
public class RunTalentState
{
    public string StartingInscription;                      // 起始铭文 ID（未选=空）
    public List<string> OwnedIds = new List<string>();
    [NonSerialized] public int RewardSequence;              // 本次 Run 已结算的天赋奖励序号

    public bool Owns(string id)
    {
        foreach (string s in OwnedIds) if (s == id) return true;
        return false;
    }

    public void Grant(string id)
    {
        if (!string.IsNullOrEmpty(id) && !Owns(id)) OwnedIds.Add(id);
    }

    public void Clear()
    {
        StartingInscription = null;
        OwnedIds.Clear();
        RewardSequence = 0;
    }
}
