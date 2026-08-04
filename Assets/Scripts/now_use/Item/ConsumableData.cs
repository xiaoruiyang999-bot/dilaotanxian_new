using UnityEngine;

/// <summary>
/// 消耗品效果类型（v0.7.2 数据结构先行，效果接线归 v0.7.3）。
/// </summary>
public enum ConsumableEffectType
{
    HP = 0,
    Armor = 1,
    Mana = 2
}

/// <summary>
/// 消耗品配置数据（v0.7.2 建结构，v0.7.3 正式三包）。纯数据容器。
/// 正式资产：Assets/Data/Item/ 下 Item_HealPack（HP+4）/ Item_ArmorPack（Armor+4）/ Item_ManaPack（Mana+40），
/// 占位数值后续设计定稿只改 SO；使用效果结算在 ItemInventory.UseActive。
/// </summary>
[CreateAssetMenu(fileName = "ConsumableData", menuName = "Item/Consumable Data")]
public class ConsumableData : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField] private ConsumableEffectType effectType;
    [SerializeField] private float value = 10f;

    [Header("表现（程序员美术占位）")]
    [Tooltip("图标染色：槽位色块与掉落物色块共用")]
    [SerializeField] private Color iconColor = Color.white;
    [Tooltip("占位图标，可空（空则色块呈现）")]
    [SerializeField] private Sprite icon;

    public string DisplayName => displayName;
    public ConsumableEffectType EffectType => effectType;
    public float Value => value;
    public Color IconColor => iconColor;
    public Sprite Icon => icon;
}
