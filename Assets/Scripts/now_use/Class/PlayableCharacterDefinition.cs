using UnityEngine;
using System.Collections.Generic;

/// <summary>职业角色唯一身份。枚举值会进入运行存档，既有值不得重排。</summary>
public enum PlayableCharacterId
{
    Werewolf = 0,
    Mage = 1,
    Archer = 2
}

/// <summary>
/// v2.0.2 职业角色唯一真值：
/// 一次选择同时确定外观、属性、武器类型、职业资源、动画模组与奖励标签。
/// 新消费方只读 PlayableCharacterId + 本定义。
/// 动画模组：狼人左右两套帧不翻转，目录名即模组合同（见 WerewolfClipFactory）。
/// </summary>
[CreateAssetMenu(menuName = "Dungeon/PlayableCharacter", fileName = "Character_")]
public class PlayableCharacterDefinition : ScriptableObject
{
    [Header("身份")]
    [SerializeField] private PlayableCharacterId id = PlayableCharacterId.Werewolf;
    [SerializeField] private string displayName = "狼人";
    [SerializeField] private Color characterColor = new Color(0.35f, 0.85f, 0.45f, 1f);

    [Header("基础属性")]
    [Min(1f)] public float maxHp = 100f;
    [Min(0f)] public float maxArmor = 25f;
    [Min(0.1f)] public float moveSpeed = 5.2f;
    [Min(0f)] public float attack = 10f;
    [Range(0f, 1f)] public float critRate = 0.05f;
    [Min(1f)] public float critDamage = 1.5f;
    [Min(0f)] public float armorReduceMul = 0.3f;
    [Min(0.01f)] public float armorLossMul = 1f;
    [Min(0f)] public float maxMana = 50f;

    [Header("武器")]
    [Tooltip("本职业角色可拾取与掉落的武器池；兼容性以此列表为唯一真值。")]
    [SerializeField] private List<WeaponData> availableWeapons = new List<WeaponData>();
    [Tooltip("选择、复活与大招结束时使用的基础武器，必须同时位于武器池中。")]
    [SerializeField] private WeaponData initialWeapon;

    [Header("技能")]
    [SerializeField] private SkillBranchData skillBranches;
    [SerializeField] private SkillData ultimateSkill;

    [Header("职业资源：兽性（GDD §8.2 侵略节奏）")]
    [Min(1f)] public float beastResourceMax = 100f;
    [Tooltip("命中充能（每次/秒——正式口径随 T-07 技能键定案，灰盒先用命中固定值）")]
    [Min(0f)] public float beastPerHit = 4f;
    [Tooltip("满能变身持续时间（秒）")]
    [Min(1f)] public float beastDuration = 12f;

    [Header("普攻模组（同源数据）")]
    [Tooltip("普攻连段定义（空 = 未配置，灰盒期回退 MeleeComboTable）")]
    public AttackDefinition basicAttack;

    [Header("动画模组（WerewolfClipFactory 目录合同）")]
    [Tooltip("Resources/Art/Characters/ 下的角色目录名")]
    public string artFolder = "Werewolf";
    [Tooltip("各形态帧组子目录名（空 = 该形态未产出，回退普通形态）")]
    public string idleFolder = "Idle";
    public string walkFolder = "Walk";
    public string beastIdleFolder = "BeastIdle";
    public string beastWalkFolder = "BeastWalk";
    public string transformFolder = "Transform";

    public PlayableCharacterId Id => id;
    public string DisplayName => displayName;
    public Color CharacterColor => characterColor;
    public IReadOnlyList<WeaponData> AvailableWeapons => availableWeapons;
    public WeaponData InitialWeapon => initialWeapon;
    public SkillBranchData SkillBranches => skillBranches;
    public SkillData UltimateSkill => ultimateSkill;

    public bool SupportsWeapon(WeaponData weapon)
    {
        return weapon != null && availableWeapons != null && availableWeapons.Contains(weapon);
    }
}
