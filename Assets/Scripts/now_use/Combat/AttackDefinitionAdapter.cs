using UnityEngine;

/// <summary>
/// v2.0.3 攻击数据桥：AttackDefinition(SO 同源数据，绝对值)→ MeleeComboStep(PlayerCombat
/// 连段状态机与 WeaponHitbox 的既有倍率结构)。倍率 = SO 绝对值 ÷ 当前 AttackData 基准
///（基准由调用方传入——判定长度/三阶段时长以 SO 为唯一真源，AttackData 只做载体基准）。
/// 旧 MeleeComboTable 仅在角色未配置 AttackDefinition 时兜底。
/// </summary>
public static class AttackDefinitionAdapter
{
    /// <summary>SO 连段 → 运行连段。定义/steps 为空返回 null（调用方回退 MeleeComboTable）。</summary>
    public static MeleeComboStep[] Build(AttackDefinition definition,
        float baseWindup, float baseActive, float baseRecovery, float baseReach)
    {
        if (definition == null || definition.steps == null || definition.steps.Length == 0) return null;
        if (baseWindup <= 0f || baseActive <= 0f || baseRecovery <= 0f || baseReach <= 0f) return null;

        var result = new MeleeComboStep[definition.steps.Length];
        for (int i = 0; i < definition.steps.Length; i++)
        {
            AttackDefinition.Step s = definition.steps[i];
            result[i] = new MeleeComboStep(
                windupMul: s.windup / baseWindup,
                activeMul: s.active / baseActive,
                recoveryMul: s.recovery / baseRecovery,
                reachMul: s.reach / baseReach,
                laneWidth: s.laneWidth,
                damageMul: s.damageMultiplier,
                stepImpulse: s.stepImpulse,
                cancelRatio: s.recoveryCancelRatio);
        }
        return result;
    }

    /// <summary>连段窗口（取段 0 配置；非法时回退 MeleeComboTable 默认）。</summary>
    public static float ComboWindowOf(AttackDefinition definition)
    {
        if (definition == null || definition.steps == null || definition.steps.Length == 0)
            return MeleeComboTable.ComboWindow;
        return definition.steps[0].comboWindow > 0f
            ? definition.steps[0].comboWindow
            : MeleeComboTable.ComboWindow;
    }
}
