using UnityEngine;

/// <summary>
/// 技能配置数据（v0.7.4 技能框架，计划书 §6.1）。纯数据容器，ClassData/WeaponData 同风格。
/// 等级数值表 damageMultiplierByLevel：空数组 = 平直（全程用基值 damageMultiplier），
/// 非空按 level 查表（level 1 = 表[0]，供 v0.7.6 天赋升级读取），越界回退基值。
/// 资产在 Assets/Data/Skill/；占位数值【待补充·数值】，v0.7.5 按设计稿填。
/// </summary>
[CreateAssetMenu(fileName = "SkillData", menuName = "Skill/Skill Data")]
public class SkillData : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField] private SkillType skillType = SkillType.MeleeAoE;

    [Header("消耗与冷却")]
    [SerializeField] private float manaCost = 10f;
    [SerializeField] private float cooldown = 5f;

    [Header("伤害（技能倍率区，独立于武器攻击）")]
    [SerializeField] private float damageMultiplier = 1f;
    [Tooltip("MeleeAoE：以自身为中心的圆形半径（世界单位）")]
    [SerializeField] private float aoeRadius = 2f;

    [Header("表现（占位：色块 + 文字）")]
    [SerializeField] private Color iconColor = Color.white;

    [Header("等级（v0.7.6 天赋升级读取；空表 = 平直）")]
    [SerializeField] private int level = 1;
    [SerializeField] private float[] damageMultiplierByLevel = new float[0];

    public string DisplayName => displayName;
    public SkillType SkillType => skillType;
    public float ManaCost => manaCost;
    public float Cooldown => cooldown;
    public float DamageMultiplier => damageMultiplier;
    public float AoeRadius => aoeRadius;
    public Color IconColor => iconColor;
    public int Level => level;

    /// <summary>当前等级的伤害倍率：等级表非空按 level 查表（level 1 = 表[0]），越界/空表回退基值。</summary>
    public float GetDamageMultiplier()
    {
        int index = level - 1;
        if (damageMultiplierByLevel != null && index >= 0 && index < damageMultiplierByLevel.Length)
            return damageMultiplierByLevel[index];
        return damageMultiplier;
    }

    void OnValidate()
    {
        level = Mathf.Max(1, level);
    }
}
