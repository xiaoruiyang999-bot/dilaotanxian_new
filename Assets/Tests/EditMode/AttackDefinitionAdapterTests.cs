using NUnit.Framework;
using UnityEngine;

/// <summary>
/// v2.0.3 攻击数据桥门禁：AttackDefinition(绝对值)→ MeleeComboStep(倍率)的一一映射、
/// 连段窗口取值、非法输入回退。狼人刺刀/狼爪双组资产合同一并锁定（V2 §6.2：狼爪是
/// 独立招式组——高速短距宽带，不得退化为刺刀数值乘倍）。
/// </summary>
public class AttackDefinitionAdapterTests
{
    private const string BayonetPath = "Characters/Attack_WerewolfBayonet";
    private const string ClawsPath = "Characters/Attack_WerewolfBeastClaws";

    private static AttackDefinition.Step S(float windup, float active, float recovery,
        float reach, float lane, float dmg, float step, float cancel)
        => new AttackDefinition.Step
        {
            windup = windup, active = active, recovery = recovery,
            reach = reach, laneWidth = lane, damageMultiplier = dmg,
            stepImpulse = step, recoveryCancelRatio = cancel, comboWindow = 0.3f,
        };

    [Test]
    public void Build_MapsAbsoluteValuesToMultipliers()
    {
        var def = ScriptableObject.CreateInstance<AttackDefinition>();
        def.steps = new[] { S(0.1f, 0.05f, 0.15f, 2.0f, 1.0f, 1.2f, 2.5f, 0.4f) };

        // 基准：前摇 0.2 / 判定 0.1 / 后摇 0.3 / 距离 1.0
        MeleeComboStep[] built = AttackDefinitionAdapter.Build(def, 0.2f, 0.1f, 0.3f, 1.0f);
        Assert.NotNull(built);
        Assert.AreEqual(1, built.Length);
        Assert.AreEqual(0.5f, built[0].windupMul, 0.0001f, "前摇倍率 = 绝对值 ÷ 基准");
        Assert.AreEqual(0.5f, built[0].activeMul, 0.0001f);
        Assert.AreEqual(0.5f, built[0].recoveryMul, 0.0001f);
        Assert.AreEqual(2.0f, built[0].reachMul, 0.0001f, "距离倍率 = 绝对 reach ÷ 基准 reach");
        Assert.AreEqual(1.0f, built[0].laneWidth, 0.0001f);
        Assert.AreEqual(1.2f, built[0].damageMul, 0.0001f);
        Assert.AreEqual(2.5f, built[0].stepImpulse, 0.0001f);
        Assert.AreEqual(0.4f, built[0].cancelRatio, 0.0001f);
    }

    [Test]
    public void Build_InvalidInputs_FallBackNull()
    {
        Assert.IsNull(AttackDefinitionAdapter.Build(null, 0.2f, 0.1f, 0.3f, 1f));
        var empty = ScriptableObject.CreateInstance<AttackDefinition>();
        empty.steps = new AttackDefinition.Step[0];
        Assert.IsNull(AttackDefinitionAdapter.Build(empty, 0.2f, 0.1f, 0.3f, 1f));
        Assert.IsNull(AttackDefinitionAdapter.Build(null, 0f, 0.1f, 0.3f, 1f), "非法基准返回 null");
    }

    [Test]
    public void ComboWindow_TakesStepZero_WithFallback()
    {
        var def = ScriptableObject.CreateInstance<AttackDefinition>();
        def.steps = new[] { S(0.1f, 0.1f, 0.1f, 1f, 1f, 1f, 0f, 0.3f) };
        Assert.AreEqual(0.3f, AttackDefinitionAdapter.ComboWindowOf(def));
        Assert.AreEqual(MeleeComboTable.ComboWindow, AttackDefinitionAdapter.ComboWindowOf(null));
    }

    [Test]
    public void WerewolfBayonetAndBeastClaws_BothLoad_WithDistinctIdentities()
    {
        var bayonet = Resources.Load<AttackDefinition>(BayonetPath);
        var claws = Resources.Load<AttackDefinition>(ClawsPath);
        Assert.NotNull(bayonet, $"缺少 {BayonetPath}");
        Assert.NotNull(claws, $"缺少 {ClawsPath}");

        // V2 §6.2 合同：狼爪 = 高速（更短前摇）+ 短距（更短 reach）+ 宽带（更大 lane）——
        // 与刺刀是不同招式身份，不是倍率缩放
        Assert.Greater(bayonet.steps[0].reach, claws.steps[0].reach, "刺刀 reach 必须长于狼爪");
        Assert.Less(claws.steps[0].windup, bayonet.steps[0].windup, "狼爪前摇必须快于刺刀");
        Assert.Greater(claws.steps[0].laneWidth, bayonet.steps[0].laneWidth, "狼爪纵深带必须宽于刺刀");

        var character = Resources.Load<PlayableCharacterDefinition>("Characters/Character_Werewolf");
        Assert.NotNull(character);
        Assert.AreEqual(bayonet, character.basicAttack, "狼人常态组必须是刺刀");
        Assert.AreEqual(claws, character.beastAttack, "狼人兽化组必须是狼爪");
    }
}
