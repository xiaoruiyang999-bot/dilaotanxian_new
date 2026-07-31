using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 职业配置数据（v0.6.2 / v0.7.0 六维，计划书 4.5 / 决策 6）。纯数据容器。
/// 属性由 PlayerStats.ApplyClass 应用；classColor 供 UI/表现染色。
/// 可用武器列表为职业武器校验与准备房间展台的唯一数据源。
/// 六维占位数值（攻击 5/暴击 0.2/暴伤 1.5/R 0.3/L 1.0）待设计 D 定稿后填。
/// </summary>
[CreateAssetMenu(fileName = "ClassData", menuName = "Class/Class Data")]
public class ClassData : ScriptableObject
{
    [SerializeField] private ClassType classType;
    [SerializeField] private string displayName;

    [Header("属性上限")]
    [SerializeField] private float maxHP = 5f;
    [SerializeField] private float maxArmor = 5f;
    [SerializeField] private float maxMana = 0f;

    [Header("六维（v0.7.0，占位默认值同 PlayerStats）")]
    [SerializeField] private float attack = 5f;                 // 角色攻击力
    [SerializeField, Range(0f, 1f)] private float critRate = 0.2f;  // 暴击率
    [SerializeField] private float critDamage = 1.5f;           // 暴击伤害倍率
    [SerializeField] private float armorReduceMul = 0.3f;       // 护甲免伤率 R（v0.7.1 接线）
    [SerializeField] private float armorLossMul = 1.0f;         // 护甲扣减率 L（v0.7.1 接线）

    [Header("表现")]
    [SerializeField] private Color classColor = Color.white;

    [Header("可用武器（本职业可拾取）")]
    [SerializeField] private List<WeaponData> availableWeapons = new List<WeaponData>();

    public ClassType ClassType => classType;
    public string DisplayName => displayName;
    public float MaxHP => maxHP;
    public float MaxArmor => maxArmor;
    public float MaxMana => maxMana;
    public float Attack => attack;
    public float CritRate => critRate;
    public float CritDamage => critDamage;
    public float ArmorReduceMul => armorReduceMul;
    public float ArmorLossMul => armorLossMul;
    public Color ClassColor => classColor;
    public IReadOnlyList<WeaponData> AvailableWeapons => availableWeapons;
}
