using UnityEngine;

/// <summary>
/// 近战连段一段的定义（v1.1.48，失落城堡式战斗第一批）：
/// 每段拥有独立的三阶段时长（= AttackData 基准 × 倍率，攻速倍率照常叠除）、
/// 判定长度/纵深/伤害倍率（走 WeaponHitbox 既有 LengthMultiplier/DamageMultiplier 通道）、
/// 水平踏步冲量与后摇可取消比例。数据驱动，判定入口仍是 WeaponHitbox（同源原则不破）。
/// </summary>
public struct MeleeComboStep
{
    public float windupMul;     // 前摇倍率
    public float activeMul;     // 判定期倍率
    public float recoveryMul;   // 后摇倍率
    public float reachMul;      // 判定长度倍率（LengthMultiplier）
    public float laneWidth;     // 纵深容错（横向攻击带高度，世界单位；失落城堡"同一水平线"的容错带）
    public float damageMul;     // 段伤害倍率（DamageMultiplier）
    public float stepImpulse;   // 踏步水平冲量（世界单位/秒，持续 ~0.12s）
    public float cancelRatio;   // 后摇可取消比例（0~1：后摇跑到该比例即可被缓冲输入打断进下一段）

    public MeleeComboStep(float windupMul, float activeMul, float recoveryMul,
        float reachMul, float laneWidth, float damageMul, float stepImpulse, float cancelRatio)
    {
        this.windupMul = windupMul;
        this.activeMul = activeMul;
        this.recoveryMul = recoveryMul;
        this.reachMul = reachMul;
        this.laneWidth = laneWidth;
        this.damageMul = damageMul;
        this.stepImpulse = stepImpulse;
        this.cancelRatio = cancelRatio;
    }
}

/// <summary>
/// 近战连段表（v1.1.48）：纯代码定义（SkillTreeDef 先例），第一轮只做长剑三段。
/// 长剑基准（AttackData）：前摇 0.2 / 判定 0.2 / 后摇 0.3——三段实际节奏：
/// 段1 0.14/0.11/0.15（0.40 秒）→ 段2 0.12/0.11/0.15 → 段3 0.22/0.15/0.40（重击，深后摇晚取消）。
/// Fallback = 非剑近战（枪矛等）：单段全倍率 1，但保留输入缓冲与可取消后摇
/// （"提前按键被吞"的修复对所有近战生效），无踏步。
/// </summary>
public static class MeleeComboTable
{
    /// <summary>连段接受窗口（秒）：后摇完毕后缓冲/新输入可接下一段的期限，超时段序归零。</summary>
    public const float ComboWindow = 0.30f;

    /// <summary>长剑三段（FanScale 蓄力剑 / 默认空手近战）。</summary>
    public static readonly MeleeComboStep[] Sword =
    {
        new MeleeComboStep(0.7f, 0.55f, 0.5f,  1.00f, 0.95f, 1.00f, 2.2f, 0.35f),
        new MeleeComboStep(0.6f, 0.55f, 0.5f,  1.05f, 0.95f, 1.05f, 2.6f, 0.35f),
        new MeleeComboStep(1.1f, 0.75f, 1.35f, 1.30f, 1.05f, 1.45f, 1.2f, 0.75f),
    };

    /// <summary>兜底单段（枪矛等非剑近战）：旧节奏 + 输入缓冲 + 可取消后摇，无踏步。</summary>
    public static readonly MeleeComboStep[] Fallback =
    {
        new MeleeComboStep(1f, 1f, 1f, 1f, 0.85f, 1f, 0f, 0.4f),
    };

    /// <summary>按武器取连段表：长剑（FanScale 蓄力规则）与默认空手近战走三段；其余单段。</summary>
    public static MeleeComboStep[] ForWeapon(WeaponInstance weapon)
    {
        if (weapon == null || weapon.Data == null) return Sword;   // 默认近战（空手）也三段
        return weapon.Data.ChargeRule == ChargeRule.FanScale ? Sword : Fallback;
    }
}
