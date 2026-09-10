using UnityEngine;

/// <summary>
/// 技能资产目录：PlayableCharacterDefinition/WeaponData 未接线时的兼容兜底入口。
/// 编辑器 AssetDatabase 硬编码路径；打包构建需把资产复制到 Resources/Skill/ 走 Resources.Load。
/// 资产名清单单点收口（AssetNames）。射手/法师技能未实装：返回 null 并 Warning。
/// </summary>
public static class SkillCatalog
{
    /// <summary>技能资产名清单（Assets/Resources/Skill/）——全项目唯一清单。</summary>
    internal static readonly string[] AssetNames =
    {
        "Skill_Werewolf_StandFirm",
        "Skill_Werewolf_PowerStrike",
        "Skill_Werewolf_Garrote",
        "Skill_Werewolf_Ultimate",
        "Skill_Whirlwind_Weapon",
        "SkillBranch_Werewolf"
    };

    private static SkillBranchData branchWerewolf;
    private static SkillData ultimateWerewolf;
    private static SkillData weaponWhirlwind;

    /// <summary>职业小技能分支表（未实装职业返回 null 并 Warning）。</summary>
    public static SkillBranchData GetBranches(PlayableCharacterId id)
    {
        switch (id)
        {
            case PlayableCharacterId.Werewolf:
                if (branchWerewolf == null) branchWerewolf = Load<SkillBranchData>("SkillBranch_Werewolf");
                return branchWerewolf;
            default:
                Debug.LogWarning($"[Skill] {id} 技能未实装，分支表返回 null。");
                return null;
        }
    }

    /// <summary>职业大招（未实装职业返回 null 并 Warning）。</summary>
    public static SkillData GetUltimate(PlayableCharacterId id)
    {
        switch (id)
        {
            case PlayableCharacterId.Werewolf:
                if (ultimateWerewolf == null) ultimateWerewolf = Load<SkillData>("Skill_Werewolf_Ultimate");
                return ultimateWerewolf;
            default:
                Debug.LogWarning($"[Skill] {id} 技能未实装，大招返回 null。");
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
        T data = UnityEditor.AssetDatabase.LoadAssetAtPath<T>($"Assets/Resources/Skill/{assetName}.asset");
#else
        T data = Resources.Load<T>($"Skill/{assetName}");
#endif
        if (data == null)
            Debug.LogWarning($"[Skill] 技能资产 {assetName}.asset 未找到（编辑器应走 AssetDatabase；构建需 Resources/Skill/ 资产）。");
        return data;
    }
}
