using UnityEngine;

/// <summary>
/// 武器行为类型。
/// Melee：近战，转发 PlayerCombat 三件套链路；
/// Ranged：远程（Projectile，v0.6.3 实现）；
/// SelfCast：自身施法（治疗法杖等，v0.6.3 实现）。
/// </summary>
public enum WeaponBehaviorType
{
    Melee = 0,
    Ranged = 1,
    SelfCast = 2
}

/// <summary>
/// 蓄力规则（计划书 4.6，v0.6.3 实现蓄力系统）。
/// None：无蓄力；FanScale：扇形缩放；RectScale：矩形缩放；ProjectileBoost：子弹增益。
/// </summary>
public enum ChargeRule
{
    None = 0,
    FanScale = 1,
    RectScale = 2,
    ProjectileBoost = 3
}

/// <summary>
/// 武器配置数据（v0.6.2 阶段 A 立框架，v0.6.3 补远程子弹/自疗/蓄力参数）。纯数据容器。
/// 手持与地图掉落视觉由 WeaponVisualBuilder 运行时按行为类型构建（程序化多色块，无 visualPrefab）。
/// </summary>
[CreateAssetMenu(fileName = "WeaponData", menuName = "Weapon/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField] private ClassType requiredClass;
    [SerializeField] private WeaponBehaviorType behaviorType;

    [Header("近战（behaviorType = Melee 时生效）")]
    [SerializeField] private AttackData attackData;

    [Header("远程（behaviorType = Ranged 时生效，v0.6.3）")]
    [SerializeField] private ProjectileData projectileData;

    [Header("自身施法（behaviorType = SelfCast 时生效，v0.6.3）")]
    [Tooltip("每次平击回复自身 HP（治疗法杖）")]
    [SerializeField] private float healAmount = 0f;

    [Header("蓄力（v0.6.3）")]
    [SerializeField] private ChargeRule chargeRule = ChargeRule.None;

    [Header("蓄力参数（chargeRule ≠ None 时生效，v0.6.3）")]
    [Tooltip("蓄满所需时间（秒）")]
    [SerializeField] private float chargeMaxTime = 1.2f;
    [Tooltip("FanScale：蓄满时攻击范围倍率（刀）")]
    [SerializeField] private float chargeRangeMul = 1.8f;
    [Tooltip("FanScale：蓄满时扇形角度倍率（刀）")]
    [SerializeField] private float chargeAngleMul = 1.8f;
    [Tooltip("RectScale：蓄满时长度倍率（枪矛）")]
    [SerializeField] private float chargeLengthMul = 2f;
    [Tooltip("RectScale：蓄满时宽度倍率（枪矛）")]
    [SerializeField] private float chargeWidthMul = 1.2f;
    [Tooltip("ProjectileBoost：蓄满时伤害倍率（弓箭）")]
    [SerializeField] private float chargeDamageMul = 2f;
    [Tooltip("ProjectileBoost：蓄满时弹速倍率（弓箭）")]
    [SerializeField] private float chargeSpeedMul = 2f;
    [Tooltip("近战满蓄（蓄力达到上限）当次攻击伤害倍率，1 = 不加成")]
    [SerializeField] private float chargeFullDamageMul = 1.5f;

    [Header("弹夹与射速")]
    [Tooltip("弹夹容量，0 = 无弹夹")]
    [SerializeField] private int clipSize = 0;
    [SerializeField] private float reloadTime = 0f;
    [Tooltip("闲置自动换弹时间（连弩用），0 = 不自动")]
    [SerializeField] private float autoReloadIdleTime = 0f;
    [SerializeField] private float fireInterval = 0f;

    [Header("表现（程序员美术）")]
    [Tooltip("武器染色：手持与地图掉落物共用")]
    [SerializeField] private Color weaponColor = Color.white;
    [Tooltip("地图掉落固定图标，可空（空则白块染色呈现）")]
    [SerializeField] private Sprite mapIcon;

    [Header("武器技能（v0.7.4 框架；本版资产未接线，null 时运行时走 SkillCatalog 兜底）")]
    [SerializeField] private SkillData weaponSkill;

    public string DisplayName => displayName;
    public ClassType RequiredClass => requiredClass;
    public WeaponBehaviorType BehaviorType => behaviorType;
    public AttackData AttackData => attackData;
    public ProjectileData ProjectileData => projectileData;
    public float HealAmount => healAmount;
    public ChargeRule ChargeRule => chargeRule;
    public float ChargeMaxTime => chargeMaxTime;
    public float ChargeRangeMul => chargeRangeMul;
    public float ChargeAngleMul => chargeAngleMul;
    public float ChargeLengthMul => chargeLengthMul;
    public float ChargeWidthMul => chargeWidthMul;
    public float ChargeDamageMul => chargeDamageMul;
    public float ChargeSpeedMul => chargeSpeedMul;
    public float ChargeFullDamageMul => chargeFullDamageMul;
    public int ClipSize => clipSize;
    public float ReloadTime => reloadTime;
    public float AutoReloadIdleTime => autoReloadIdleTime;
    public float FireInterval => fireInterval;
    public Color WeaponColor => weaponColor;
    public Sprite MapIcon => mapIcon;
    public SkillData WeaponSkill => weaponSkill;
}
