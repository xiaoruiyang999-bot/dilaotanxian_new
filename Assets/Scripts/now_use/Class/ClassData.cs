using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 职业配置数据（v0.6.2，计划书 4.5）。纯数据容器。
/// 三属性上限由 PlayerStats.ApplyClass 应用；classColor 供 UI/表现染色。
/// 可用武器列表为职业武器校验与准备房间展台的唯一数据源。
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

    [Header("表现")]
    [SerializeField] private Color classColor = Color.white;

    [Header("可用武器（本职业可拾取）")]
    [SerializeField] private List<WeaponData> availableWeapons = new List<WeaponData>();

    public ClassType ClassType => classType;
    public string DisplayName => displayName;
    public float MaxHP => maxHP;
    public float MaxArmor => maxArmor;
    public float MaxMana => maxMana;
    public Color ClassColor => classColor;
    public IReadOnlyList<WeaponData> AvailableWeapons => availableWeapons;
}
