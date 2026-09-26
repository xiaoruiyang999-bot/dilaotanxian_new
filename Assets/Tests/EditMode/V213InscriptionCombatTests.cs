using NUnit.Framework;

public class V213InscriptionCombatTests
{
    private static RunBuildState Build(params string[] ids)
    {
        var build = new RunBuildState();
        foreach (string id in ids)
            Assert.IsTrue(build.TryAcquire(id, PlayableCharacterId.Werewolf, out _));
        return build;
    }

    private static DamageContext Attack(float baseAttack = 10f, float critRate = 0f)
        => new DamageContext { baseAttack = baseAttack, multiplier = 1f,
            critRate = critRate, critDamage = 1.5f };

    private static InscriptionHitOutcome Hit(InscriptionCombatRules rules, int target = 1,
        bool crit = false, bool ordinary = true, bool boss = false, bool killed = false,
        float targetRatio = 1f, float actual = 10f, float hp = 50f, float maxHp = 100f)
        => rules.OnDirectHit(target, actual, targetRatio, crit, true, ordinary, boss,
            killed, 10f, hp, maxHp, default);

    [Test]
    public void StaticCatalog_UsesThreeExplicitLevelsForEveryEffect()
    {
        foreach (SageInscriptionDefinition definition in SageInscriptionCatalog.All)
        {
            Assert.Greater(definition.ValueAt(1), 0f, definition.AbilityId);
            Assert.GreaterOrEqual(definition.ValueAt(2), definition.ValueAt(1));
            Assert.GreaterOrEqual(definition.ValueAt(3), definition.ValueAt(2));
            Assert.AreEqual(0f, definition.ValueAt(4));
        }
        Assert.AreEqual(.15f, SageInscriptionCatalog.Find("rift_heavy_scar").ValueAt(1));
        Assert.AreEqual(.22f, SageInscriptionCatalog.Find("rift_heavy_scar").ValueAt(2));
    }

    [Test]
    public void SwiftBlade_CapsSpeedAndConvertsOnlyPositiveOverflowOnce()
    {
        var rules = new InscriptionCombatRules();
        rules.Bind(Build("swift_edge", "swift_peril_step", "swift_overflow", "swift_combo"));
        Assert.AreEqual(2, rules.ResonanceStage(InscriptionLineage.SwiftBlade));
        Assert.AreEqual(2.5f, rules.GetEffectiveAttackSpeed(3f, .25f), .0001f);
        Assert.AreEqual(.2f, rules.GetOverflowAttackBonus(3f, .25f), .0001f);
        var context = Attack();
        rules.PrepareAttack(ref context, .25f, 3f, true);
        Assert.AreEqual(1.2f, context.multiplier, .0001f);
        Assert.AreEqual(0f, rules.GetOverflowAttackBonus(1f, 1f));

        for (int i = 0; i < 10; i++) Hit(rules, i + 1);
        Assert.IsTrue(rules.EchoReady);
        var echo = Hit(rules, 11);
        Assert.AreEqual(3.5f, echo.BonusDamage, .0001f);
        Assert.IsFalse(rules.EchoReady);
        rules.Advance(2.1f);
        Assert.AreEqual(0, rules.ComboStacks);
    }

    [Test]
    public void StarRift_CritBonusesMissCompensationAndFractureRespectCaps()
    {
        var rules = new InscriptionCombatRules();
        rules.Bind(Build("rift_sharp_eye", "rift_heavy_scar", "rift_refraction",
            "rift_gathered_gap", "rift_desperation"));
        Assert.AreEqual(2, rules.ResonanceStage(InscriptionLineage.StarRift));
        for (int i = 0; i < 8; i++) Hit(rules, 1, crit: false);
        var context = Attack(10f, .2f);
        rules.PrepareAttack(ref context, .25f, 1f, true);
        Assert.AreEqual(.45f, context.critRate, .0001f); // .20 + .05 + capped .20
        Assert.AreEqual(2.26f, context.critDamage, .0001f); // 1.5 + .15 + .16 + .45
        Hit(rules, 1, crit: true);
        Assert.AreEqual(0, rules.MissStacks);
        Hit(rules, 1, crit: true);
        var third = Hit(rules, 1, crit: true);
        Assert.AreEqual(3f, third.BonusDamage, .0001f);
        Assert.IsTrue(rules.RiftChargeReady);
        var charged = Attack();
        var snapshot = rules.PrepareAttack(ref charged, 1f, 1f, true);
        Assert.AreEqual(.20f, charged.critDamage - 1.5f - .15f, .0001f);
        Assert.IsTrue(snapshot.ConsumeRiftCharge);
        rules.OnDirectHit(2, 5f, 1f, false, true, true, false, false,
            10f, 50f, 100f, snapshot);
        Assert.IsFalse(rules.RiftChargeReady);
        rules.Advance(8f);
        Hit(rules, 3, crit: true, boss: true);
        Hit(rules, 3, crit: true, boss: true);
        Assert.AreEqual(1.5f, Hit(rules, 3, crit: true, boss: true).BonusDamage, .0001f);
    }

    [Test]
    public void BloodOath_UsesActualDamageBossCapOverlifeAndUniqueKills()
    {
        var rules = new InscriptionCombatRules();
        rules.Bind(Build("oath_drinking_blade", "oath_overlife", "oath_hunt", "oath_final_devour"));
        Assert.AreEqual(2, rules.ResonanceStage(InscriptionLineage.BloodOath));
        Assert.AreEqual(0f, Hit(rules, actual: 0f).Healing);
        var first = Hit(rules, killed: true, targetRatio: .2f);
        Assert.AreEqual(2.5f, first.Healing, .0001f); // 2% + 3% on 10 actual, plus 2% max HP kill
        Assert.AreEqual(1, rules.HuntStacks);
        var duplicate = Hit(rules, killed: true, targetRatio: .2f);
        Assert.Less(duplicate.Healing, first.Healing);
        var atFull = Hit(rules, target: 2, hp: 100f, maxHp: 100f, targetRatio: .2f);
        Assert.Greater(atFull.TemporaryHealth, 0f);
        Assert.AreEqual(0f, atFull.Healing);
        Assert.AreEqual(15f, rules.GetTemporaryHealthCap(100f), .0001f);
        rules.Advance(1.1f);
        var boss = Hit(rules, target: 3, boss: true, actual: 1000f, targetRatio: .2f);
        Assert.AreEqual(8f, boss.Healing, .0001f); // boss discount before per-hit cap
    }

    [Test]
    public void BloodDebt_OnlyEnemyHealthLossStartsWindowAndBudgetIsBounded()
    {
        var rules = new InscriptionCombatRules();
        rules.Bind(Build("oath_drinking_blade", "oath_overlife", "oath_hunt", "oath_final_devour"));
        Assert.IsFalse(rules.BloodDebtActive);
        rules.OnEnemyDamageTaken(20f);
        Assert.IsTrue(rules.BloodDebtActive);
        float total = 0f;
        for (int i = 0; i < 5; i++)
        {
            total += Hit(rules, target: i + 1, actual: 100f).Healing;
            rules.Advance(1.1f);
        }
        Assert.LessOrEqual(total, 10f + 5f * 4f + .001f); // debt ≤ 50% of 20 lost HP, base drinking separate
        Assert.IsFalse(rules.BloodDebtActive);
        rules.OnEnemyDamageTaken(20f); // 8 秒冷却尚未结束
        Assert.IsFalse(rules.BloodDebtActive);
        rules.Advance(8f);
        rules.OnEnemyDamageTaken(10f);
        Assert.IsTrue(rules.BloodDebtActive);
    }

    [Test]
    public void RebindAndRoomReset_DiscardTransientStateButKeepOwnedLevels()
    {
        var build = Build("swift_edge", "swift_peril_step", "swift_overflow", "swift_combo");
        var rules = new InscriptionCombatRules();
        rules.Bind(build);
        Hit(rules);
        Assert.AreEqual(1, rules.ComboStacks);
        rules.ResetTransient();
        Assert.AreEqual(0, rules.ComboStacks);
        Assert.AreEqual(1, rules.Level(InscriptionEffect.SwiftEdge));
        Assert.IsTrue(build.TryAcquire("swift_edge", PlayableCharacterId.Werewolf, out bool upgraded));
        Assert.IsTrue(upgraded);
        rules.Bind(build);
        Assert.AreEqual(2, rules.Level(InscriptionEffect.SwiftEdge));
        Assert.AreEqual(2, rules.ResonanceStage(InscriptionLineage.SwiftBlade));
    }

    [Test]
    public void InvalidHitAndTrueDamage_DoNotAdvanceCritCompensation()
    {
        var rules = new InscriptionCombatRules();
        rules.Bind(Build("rift_gathered_gap"));
        Hit(rules, actual: 0f);
        Assert.AreEqual(0, rules.MissStacks);
        rules.OnDirectHit(1, 10f, 1f, false, false, false, false, false,
            0f, 50f, 100f, default);
        Assert.AreEqual(0, rules.MissStacks);
        Hit(rules);
        Assert.AreEqual(1, rules.MissStacks);
    }

    [Test]
    public void EmptyBuild_DoesNotIntroduceCombatBonuses()
    {
        var rules = new InscriptionCombatRules();
        rules.Bind(new RunBuildState());
        Assert.IsFalse(rules.HasAnyInscription);
        Assert.AreEqual(0f, rules.GetOverflowAttackBonus(3f, .1f));
        var context = Attack();
        rules.PrepareAttack(ref context, .1f, 1f, true);
        Assert.AreEqual(0f, context.critRate);
        Assert.AreEqual(1.5f, context.critDamage);
        Assert.AreEqual(1f, context.multiplier);
    }

    [Test]
    public void NoRewardSummonKill_DoesNotGrantHuntOrKillHealing()
    {
        var rules = new InscriptionCombatRules();
        rules.Bind(Build("oath_drinking_blade", "oath_overlife", "oath_hunt", "oath_final_devour"));
        var outcome = rules.OnDirectHit(5, 10f, 1f, false, true, true, false, true,
            10f, 50f, 100f, default, rewardEligible: false);
        Assert.AreEqual(.2f, outcome.Healing, .0001f); // 直接伤害仍可吸血，击杀奖励不生效
        Assert.AreEqual(0, rules.HuntStacks);
    }
}
