using UnityEngine;

/// <summary>
/// 技能资产目录（v0.7.4，ClassCatalog 同模式）：运行时获取技能 SO 的兜底入口
/// （ClassData.skillBranches/ultimateSkill、WeaponData.weaponSkill 已接线则用接线值，null 走本目录）。
/// 编辑器 AssetDatabase 硬编码路径；打包构建需把资产复制到 Resources/Skill/ 走 Resources.Load。
/// 资产名清单单点收口（AssetNames）。射手/法师技能未实装：返回 null 并 Warning。
/// </summary>
public static class SkillCatalog
{
    /// <summary>技能资产名清单（Assets/Data/Skill/）——全项目唯一清单。</summary>
    internal static readonly string[] AssetNames =
    {
        "Skill_Warrior_StandFirm",
        "Skill_Warrior_PowerStrike",
        "Skill_Warrior_Ultimate",
        "Skill_Whirlwind_Weapon",
        "SkillBranch_Warrior"
    };

    private static SkillBranchData branchWarrior;
    private static SkillData ultimateWarrior;
    private static SkillData weaponWhirlwind;

    /// <summary>职业小技能分支表（未实装职业返回 null 并 Warning）。</summary>
    public static SkillBranchData GetBranches(ClassType type)
    {
        switch (type)
        {
            case ClassType.Warrior:
                if (branchWarrior == null) branchWarrior = Load<SkillBranchData>("SkillBranch_Warrior");
                return branchWarrior;
            default:
                Debug.LogWarning($"[Skill] {type} 技能未实装（v0.7.5 起逐职业接入），分支表返回 null。");
                return null;
        }
    }

    /// <summary>职业大招（未实装职业返回 null 并 Warning）。</summary>
    public static SkillData GetUltimate(ClassType type)
    {
        switch (type)
        {
            case ClassType.Warrior:
                if (ultimateWarrior == null) ultimateWarrior = Load<SkillData>("Skill_Warrior_Ultimate");
                return ultimateWarrior;
            default:
                Debug.LogWarning($"[Skill] {type} 技能未实装（v0.7.5 起逐职业接入），大招返回 null。");
                return null;
        }
    }

    /// <summary>武器技能（v0.7.4 占位：六武器共用旋风斩；WeaponData.weaponSkill 接线优先，此处为兜底）。</summary>
    public static SkillData GetWeaponSkill(WeaponData weapon)
    {
        if (weapon == null) return null;
        if (weaponWhirlwind == null) weaponWhirlwind = Load<SkillData>("Skill_Whirlwind_Weapon");
        return weaponWhirlwind;
    }

    /// <summary>按资产名加载技能 SO（编辑器 AssetDatabase / 构建 Resources.Load，ClassCatalog 同模式）。</summary>
    private static T Load<T>(string assetName) where T : ScriptableObject
    {
#if UNITY_EDITOR
        T data = UnityEditor.AssetDatabase.LoadAssetAtPath<T>($"Assets/Data/Skill/{assetName}.asset");
#else
        T data = Resources.Load<T>($"Skill/{assetName}");
#endif
        if (data == null)
            Debug.LogWarning($"[Skill] 技能资产 {assetName}.asset 未找到（编辑器应走 AssetDatabase；构建需 Resources/Skill/ 资产）。");
        return data;
    }
}
