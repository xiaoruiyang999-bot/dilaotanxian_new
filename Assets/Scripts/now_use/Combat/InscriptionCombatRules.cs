using System;
using System.Collections.Generic;

/// <summary>一次直接伤害前的刻印快照；只由有效命中消费条件状态。</summary>
public struct InscriptionAttackSnapshot
{
    public float BaseAttack;
    public bool ConsumeRiftCharge;
}

public struct InscriptionHitOutcome
{
    public float Healing;
    public float TemporaryHealth;
    public float BonusDamage;
}

/// <summary>
/// v2.1.3 局内战斗状态。只持有轻量临时状态；刻印所有权始终由 RunBuildState 决定。
/// 调用方提供游戏时间、实际扣血和目标身份，故测试与场景对象解耦。
/// </summary>
public sealed class InscriptionCombatRules
{
    public const float MaxAttackSpeed = 2.5f;
    public const float MaxOverflowAttackBonus = .20f;
    public const float LifeStealPerHitCap = .08f;
    public const float LifeStealPerSecondCap = .20f;
    public const float BossLifeStealMultiplier = .5f;

    private readonly int[] levels = new int[13];
    private readonly float[] values = new float[13];
    private readonly float[] secondary = new float[13];
    private readonly Dictionary<int, int> fractureStacks = new Dictionary<int, int>();
    private readonly HashSet<int> killedTargets = new HashSet<int>();
    private readonly int[] resonanceStages = new int[3];
    private float now;
    private int missStacks;
    private int comboStacks;
    private float lastComboHitAt = -100f;
    private int resonanceHitCount;
    private bool echoReady;
    private float swiftBuffUntil;
    private int huntStacks;
    private float huntUntil;
    private bool riftChargeReady;
    private float riftChargeUntil;
    private float riftCooldownUntil;
    private float bloodDebtUntil;
    private float bloodDebtCooldownUntil;
    private float bloodDebtBudget;
    private float lifeStealWindowAt;
    private float lifeStealWindowAmount;

    public float Now => now;
    public bool HasAnyInscription { get; private set; }
    public int MissStacks => missStacks;
    public int ComboStacks => comboStacks;
    public int HuntStacks => now < huntUntil ? huntStacks : 0;
    public bool EchoReady => echoReady;
    public bool RiftChargeReady => riftChargeReady && now < riftChargeUntil;
    public bool BloodDebtActive => now < bloodDebtUntil && bloodDebtBudget > 0f;

    public void Bind(RunBuildState build)
    {
        Array.Clear(levels, 0, levels.Length);
        Array.Clear(values, 0, values.Length);
        Array.Clear(secondary, 0, secondary.Length);
        Array.Clear(resonanceStages, 0, resonanceStages.Length);
        HasAnyInscription = false;
        if (build != null && build.inscriptions != null)
        {
            foreach (OwnedInscriptionData owned in build.inscriptions)
            {
                if (owned == null) continue;
                SageInscriptionDefinition definition = SageInscriptionCatalog.Find(owned.abilityId);
                if (definition == null || definition.Tier != owned.tier || owned.level < 1) continue;
                int index = (int)definition.Effect;
                int level = Math.Min(SageInscriptionCatalog.MaxOwnedLevel, owned.level);
                if (level <= levels[index]) continue;
                levels[index] = level;
                HasAnyInscription = true;
                values[index] = definition.ValueAt(level);
                secondary[index] = definition.SecondaryAt(level);
            }
            resonanceStages[(int)InscriptionLineage.SwiftBlade] = build.GetResonanceStage(InscriptionLineage.SwiftBlade);
            resonanceStages[(int)InscriptionLineage.StarRift] = build.GetResonanceStage(InscriptionLineage.StarRift);
            resonanceStages[(int)InscriptionLineage.BloodOath] = build.GetResonanceStage(InscriptionLineage.BloodOath);
        }
        ResetTransient();
    }

    public int Level(InscriptionEffect effect) => levels[(int)effect];
    public int ResonanceStage(InscriptionLineage lineage) => resonanceStages[(int)lineage];

    public void Advance(float scaledDelta)
    {
        if (scaledDelta > 0f) now += scaledDelta;
        if (comboStacks > 0 && now - lastComboHitAt > 2f) comboStacks = 0;
        if (huntStacks > 0 && now >= huntUntil) huntStacks = 0;
        if (riftChargeReady && now >= riftChargeUntil) riftChargeReady = false;
    }

    public void ResetTransient()
    {
        fractureStacks.Clear();
        killedTargets.Clear();
        missStacks = comboStacks = resonanceHitCount = huntStacks = 0;
        lastComboHitAt = -100f;
        echoReady = riftChargeReady = false;
        swiftBuffUntil = huntUntil = riftChargeUntil = riftCooldownUntil = 0f;
        bloodDebtUntil = bloodDebtCooldownUntil = bloodDebtBudget = 0f;
        lifeStealWindowAt = now;
        lifeStealWindowAmount = 0f;
    }

    private float Value(InscriptionEffect effect) => values[(int)effect];
    private float Secondary(InscriptionEffect effect) => secondary[(int)effect];
    private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    private static float LowHealthFactor(float healthRatio) => Clamp((1f - healthRatio) / .75f, 0f, 1f);

    public float GetRawAttackSpeed(float baseMultiplier, float healthRatio)
    {
        float extra = Value(InscriptionEffect.SwiftEdge)
            + Value(InscriptionEffect.PerilStep) * LowHealthFactor(healthRatio)
            + Value(InscriptionEffect.SwiftCombo) * comboStacks;
        if (ResonanceStage(InscriptionLineage.SwiftBlade) >= 1 && now < swiftBuffUntil)
            extra += .05f;
        return Math.Max(.01f, baseMultiplier) * (1f + extra);
    }

    public float GetEffectiveAttackSpeed(float baseMultiplier, float healthRatio)
        => Math.Min(MaxAttackSpeed, GetRawAttackSpeed(baseMultiplier, healthRatio));

    public float GetOverflowAttackBonus(float baseMultiplier, float healthRatio)
    {
        if (Level(InscriptionEffect.Overflow) == 0) return 0f;
        float overflow = Math.Max(0f, GetRawAttackSpeed(baseMultiplier, healthRatio) - MaxAttackSpeed);
        return Math.Min(MaxOverflowAttackBonus, overflow * Value(InscriptionEffect.Overflow));
    }

    public InscriptionAttackSnapshot PrepareAttack(ref DamageContext context, float healthRatio,
        float baseSpeedMultiplier, bool ordinaryAttack)
    {
        float panelCrit = Clamp(context.critRate + Value(InscriptionEffect.SharpEye), 0f, 1f);
        context.critRate = Clamp(panelCrit + Math.Min(Secondary(InscriptionEffect.GatheredGap),
            missStacks * Value(InscriptionEffect.GatheredGap)), 0f, 1f);
        context.critDamage += Value(InscriptionEffect.HeavyScar)
            + Math.Min(Secondary(InscriptionEffect.Refraction),
                (float)Math.Floor(panelCrit / .10f) * Value(InscriptionEffect.Refraction))
            + Value(InscriptionEffect.Desperation) * LowHealthFactor(healthRatio);
        bool consumeCharge = ordinaryAttack && RiftChargeReady;
        if (consumeCharge) context.critDamage += .20f;
        context.multiplier *= 1f + GetOverflowAttackBonus(baseSpeedMultiplier, healthRatio);
        return new InscriptionAttackSnapshot
        {
            BaseAttack = context.baseAttack,
            ConsumeRiftCharge = consumeCharge,
        };
    }

    /// <summary>受敌人伤害导致的实际 HP 损失；环境和自伤不调用此入口。</summary>
    public void OnEnemyDamageTaken(float healthLost)
    {
        if (healthLost <= 0f || ResonanceStage(InscriptionLineage.BloodOath) < 2
            || now < bloodDebtCooldownUntil) return;
        bloodDebtUntil = now + 5f;
        bloodDebtCooldownUntil = bloodDebtUntil + 8f;
        bloodDebtBudget = healthLost * .5f;
    }

    /// <summary>只调用一次，且仅用于目标实际扣血的直接伤害。附加伤害不回调本方法。</summary>
    public InscriptionHitOutcome OnDirectHit(int targetId, float actualDamage, float targetRatioBefore,
        bool critical, bool canCrit, bool ordinaryAttack, bool targetIsBoss, bool killed,
        float baseAttack, float playerHealth, float playerMaxHealth,
        InscriptionAttackSnapshot snapshot, bool rewardEligible = true)
    {
        var outcome = new InscriptionHitOutcome();
        if (actualDamage <= 0f || playerMaxHealth <= 0f) return outcome;

        if (snapshot.ConsumeRiftCharge) riftChargeReady = false;
        if (canCrit && Level(InscriptionEffect.GatheredGap) > 0)
            missStacks = critical ? 0 : Math.Min(16, missStacks + 1);

        if (ordinaryAttack)
        {
            bool echoThisHit = echoReady && ResonanceStage(InscriptionLineage.SwiftBlade) >= 2;
            if (echoThisHit)
            {
                outcome.BonusDamage += baseAttack * .35f;
                echoReady = false;
                resonanceHitCount = 0;
            }
            if (now - lastComboHitAt > 2f) comboStacks = resonanceHitCount = 0;
            lastComboHitAt = now;
            comboStacks = Math.Min(5, comboStacks + 1);
            resonanceHitCount++;
            if (ResonanceStage(InscriptionLineage.SwiftBlade) >= 1 && comboStacks >= 3)
                swiftBuffUntil = now + 3f;
            if (ResonanceStage(InscriptionLineage.SwiftBlade) >= 2 && resonanceHitCount >= 10)
                echoReady = true;
        }

        if (critical && ResonanceStage(InscriptionLineage.StarRift) >= 1 && !killed)
        {
            fractureStacks.TryGetValue(targetId, out int stacks);
            stacks++;
            if (stacks >= 3)
            {
                fractureStacks.Remove(targetId);
                outcome.BonusDamage += baseAttack * (targetIsBoss ? .15f : .30f);
                if (ResonanceStage(InscriptionLineage.StarRift) >= 2 && now >= riftCooldownUntil)
                {
                    riftChargeReady = true;
                    riftChargeUntil = now + 3f;
                    riftCooldownUntil = now + 8f;
                }
            }
            else fractureStacks[targetId] = stacks;
        }

        float lifeStealRate = Value(InscriptionEffect.DrinkingBlade);
        if (huntStacks > 0 && now < huntUntil)
            lifeStealRate += huntStacks * Value(InscriptionEffect.Hunt);
        if (targetRatioBefore < .35f)
            lifeStealRate += Value(InscriptionEffect.FinalDevour);
        float bloodDebtHealing = BloodDebtActive ? Math.Min(bloodDebtBudget, actualDamage * .05f) : 0f;
        if (targetIsBoss) bloodDebtHealing *= BossLifeStealMultiplier;
        float proposedHealing = actualDamage * lifeStealRate
            * (targetIsBoss ? BossLifeStealMultiplier : 1f) + bloodDebtHealing;
        if (now - lifeStealWindowAt >= 1f)
        {
            lifeStealWindowAt = now;
            lifeStealWindowAmount = 0f;
        }
        float allowed = Math.Min(proposedHealing, playerMaxHealth * LifeStealPerHitCap);
        allowed = Math.Max(0f, Math.Min(allowed,
            playerMaxHealth * LifeStealPerSecondCap - lifeStealWindowAmount));
        lifeStealWindowAmount += allowed;
        if (BloodDebtActive && proposedHealing > 0f)
            bloodDebtBudget = Math.Max(0f, bloodDebtBudget - allowed * bloodDebtHealing / proposedHealing);
        if (playerHealth >= playerMaxHealth && Level(InscriptionEffect.Overlife) > 0)
            outcome.TemporaryHealth = allowed;
        else outcome.Healing = allowed;

        if (killed && rewardEligible) OnTargetKilled(targetId, playerMaxHealth, ref outcome);
        return outcome;
    }

    public void OnTargetKilled(int targetId, float playerMaxHealth, ref InscriptionHitOutcome outcome)
    {
        if (!killedTargets.Add(targetId)) return;
        fractureStacks.Remove(targetId);
        if (Level(InscriptionEffect.Hunt) > 0)
        {
            huntStacks = Math.Min(3, now < huntUntil ? huntStacks + 1 : 1);
            huntUntil = now + 6f;
        }
        if (ResonanceStage(InscriptionLineage.BloodOath) >= 1)
            outcome.Healing += playerMaxHealth * .02f;
    }

    public float GetTemporaryHealthCap(float maxHealth)
        => maxHealth * Value(InscriptionEffect.Overlife);
    public float GetTemporaryHealthDuration() => Secondary(InscriptionEffect.Overlife);
}
