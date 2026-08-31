using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 小技能分支表（v0.7.4，计划书 §6.1）：每职业一份，局外大厅切换、局内锁定。
/// 切换入口 UI 本版不做（计划书标【待补充】）；选择索引存 RunStateCarrier.ChosenSkillBranchIndex。
/// </summary>
[CreateAssetMenu(fileName = "SkillBranchData", menuName = "Skill/Branch Data")]
public class SkillBranchData : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField] private List<SkillData> branches = new List<SkillData>();

    public string DisplayName => displayName;
    public IReadOnlyList<SkillData> Branches => branches;

    /// <summary>取分支技能（index 越界回退 0；空表返回 null）。</summary>
    public SkillData GetBranch(int index)
    {
        if (branches == null || branches.Count == 0) return null;
        if (index < 0 || index >= branches.Count) index = 0;
        return branches[index];
    }
}
