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
/// 武器配置数据（v0.6.2 阶段 A，计划书 4.6 设计冻结点的阶段性子集）。纯数据容器。
/// 远程子弹引用（projectileData）与 visualPrefab 留待 v0.6.3 补充。
/// </summary>
[CreateAssetMenu(fileName = "WeaponData", menuName = "Weapon/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField] private ClassType requiredClass;
    [SerializeField] private WeaponBehaviorType behaviorType;

    [Header("近战（behaviorType = Melee 时生效）")]
    [SerializeField] private AttackData attackData;

    [Header("蓄力（v0.6.3）")]
    [SerializeField] private ChargeRule chargeRule = ChargeRule.None;

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

    public string DisplayName => displayName;
    public ClassType RequiredClass => requiredClass;
    public WeaponBehaviorType BehaviorType => behaviorType;
    public AttackData AttackData => attackData;
    public ChargeRule ChargeRule => chargeRule;
    public int ClipSize => clipSize;
    public float ReloadTime => reloadTime;
    public float AutoReloadIdleTime => autoReloadIdleTime;
    public float FireInterval => fireInterval;
    public Color WeaponColor => weaponColor;
    public Sprite MapIcon => mapIcon;
}
