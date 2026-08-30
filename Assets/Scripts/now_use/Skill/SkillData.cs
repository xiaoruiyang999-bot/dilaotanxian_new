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

    [Header("Buff（SkillType=Buff 时生效，v0.7.5）")]
    [Tooltip("Buff 持续时间（秒）")]
    [SerializeField] private float buffDuration = 5f;
    [Tooltip("受击伤害倍率：0.5 = 受伤减半（护甲结算之前乘）")]
    [SerializeField] private float buffDamageTakenMul = 1f;
    [Tooltip("攻速倍率：1.25 = 攻击间隔 ÷1.25")]
    [SerializeField] private float buffAttackSpeedMul = 1f;
    [Tooltip("移速倍率")]
    [SerializeField] private float buffMoveSpeedMul = 1f;
    [Tooltip("输出伤害倍率：期间玩家全部输出（普攻/蓄力/弹道/技能）乘此值")]
    [SerializeField] private float buffDamageDealtMul = 1f;

    [Header("结束后虚弱（0 = 无虚弱，v0.7.5 屹立不倒）")]
    [SerializeField] private float weaknessDuration = 0f;
    [Tooltip("虚弱攻速倍率：0.5 = 攻速减半")]
    [SerializeField] private float weaknessAttackSpeedMul = 1f;
    [Tooltip("虚弱移速倍率")]
    [SerializeField] private float weaknessMoveSpeedMul = 1f;

    [Header("裸绞（SkillType=DashExecute 时生效，v0.7.5 二期）")]
    [Tooltip("冲刺距离（世界单位）")]
    [SerializeField] private float dashDistance = 3f;
    [Tooltip("冲刺时长（秒）")]
    [SerializeField] private float dashDuration = 0.2f;
    [Tooltip("冲刺期间自身受击伤害倍率：0.3 = 减伤 70%（走 BuffManager 短 buff）")]
    [SerializeField] private float dashDamageTakenMul = 0.3f;
    [Tooltip("斩杀阈值（普通怪）：当前 HP 比例 ≤ 此值直接处决")]
    [SerializeField] private float executeThresholdNormal = 0.3f;
    [Tooltip("斩杀阈值（精英）：当前 HP 比例 ≤ 此值直接处决；Boss 不可斩杀")]
    [SerializeField] private float executeThresholdElite = 0.15f;
    [Tooltip("未达斩杀阈值时的真实伤害（绕过护甲结算）")]
    [SerializeField] private float trueDamage = 45f;
    [Tooltip("冲刺终点命中判定半径（OverlapCircle 取最近敌人）")]
    [SerializeField] private float dashHitRadius = 1f;

    [Header("燃命（SkillType=BurnLife 时生效，v0.7.5 二期）")]
    [Tooltip("开启后免疫窗口（秒）：期间负面 Buff 不生效")]
    [SerializeField] private float immuneDuration = 3f;
    [Tooltip("联动窗口（秒）：窗口内下一次施放小技能分支用强化数值")]
    [SerializeField] private float empowerWindow = 10f;
    [Tooltip("分支0屹立不倒强化：持续时间")]
    [SerializeField] private float empowerStandFirmDuration = 6.5f;
    [Tooltip("分支0屹立不倒强化：虚弱时长")]
    [SerializeField] private float empowerStandFirmWeaknessDuration = 1.5f;
    [Tooltip("分支1强力一击强化：持续时间")]
    [SerializeField] private float empowerPowerStrikeDuration = 7.5f;
    [Tooltip("分支1强力一击强化：输出伤害倍率")]
    [SerializeField] private float empowerPowerStrikeDamageDealtMul = 2.2f;
    [Tooltip("分支2裸绞强化：真实伤害")]
    [SerializeField] private float empowerExecuteTrueDamage = 75f;
    [Tooltip("分支2裸绞强化：斩杀成功回血")]
    [SerializeField] private float empowerExecuteHeal = 10f;

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

    // Buff 区（SkillType=Buff 时生效）
    public float BuffDuration => buffDuration;
    public float BuffDamageTakenMul => buffDamageTakenMul;
    public float BuffAttackSpeedMul => buffAttackSpeedMul;
    public float BuffMoveSpeedMul => buffMoveSpeedMul;
    public float BuffDamageDealtMul => buffDamageDealtMul;
    public float WeaknessDuration => weaknessDuration;
    public float WeaknessAttackSpeedMul => weaknessAttackSpeedMul;
    public float WeaknessMoveSpeedMul => weaknessMoveSpeedMul;

    // 裸绞区（SkillType=DashExecute 时生效）
    public float DashDistance => dashDistance;
    public float DashDuration => dashDuration;
    public float DashDamageTakenMul => dashDamageTakenMul;
    public float ExecuteThresholdNormal => executeThresholdNormal;
    public float ExecuteThresholdElite => executeThresholdElite;
    public float TrueDamage => trueDamage;
    public float DashHitRadius => dashHitRadius;

    // 燃命区（SkillType=BurnLife 时生效；联动强化数值存于大招资产，SkillExecutor 施放分支技能时读取）
    public float ImmuneDuration => immuneDuration;
    public float EmpowerWindow => empowerWindow;
    public float EmpowerStandFirmDuration => empowerStandFirmDuration;
    public float EmpowerStandFirmWeaknessDuration => empowerStandFirmWeaknessDuration;
    public float EmpowerPowerStrikeDuration => empowerPowerStrikeDuration;
    public float EmpowerPowerStrikeDamageDealtMul => empowerPowerStrikeDamageDealtMul;
    public float EmpowerExecuteTrueDamage => empowerExecuteTrueDamage;
    public float EmpowerExecuteHeal => empowerExecuteHeal;

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
        buffDuration = Mathf.Max(0f, buffDuration);
        weaknessDuration = Mathf.Max(0f, weaknessDuration);
        dashDistance = Mathf.Max(0f, dashDistance);
        dashDuration = Mathf.Max(0.01f, dashDuration);
        dashHitRadius = Mathf.Max(0f, dashHitRadius);
        executeThresholdNormal = Mathf.Clamp01(executeThresholdNormal);
        executeThresholdElite = Mathf.Clamp01(executeThresholdElite);
        trueDamage = Mathf.Max(0f, trueDamage);
        immuneDuration = Mathf.Max(0f, immuneDuration);
        empowerWindow = Mathf.Max(0f, empowerWindow);
    }
}
