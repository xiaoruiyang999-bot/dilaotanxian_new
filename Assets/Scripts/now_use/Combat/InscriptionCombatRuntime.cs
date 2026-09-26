using UnityEngine;

/// <summary>把纯刻印规则接到当前 Player；不持有 Build 真值，不参与 UI 或候选生成。</summary>
public sealed class InscriptionCombatRuntime : MonoBehaviour
{
    private readonly InscriptionCombatRules rules = new InscriptionCombatRules();
    private PlayerStats stats;
    private Health health;
    private float rawAttackSpeed = 1f;
    private float temporaryHealthUntil;

    public InscriptionCombatRules Rules => rules;

    public void Bind(RunBuildState build)
    {
        if (stats == null) stats = GetComponent<PlayerStats>();
        if (health == null)
        {
            health = GetComponent<Health>();
            if (health != null)
            {
                health.OnEnemyHealthLost += OnEnemyHealthLost;
                health.OnDeath += OnPlayerDeath;
            }
        }
        if (stats != null) stats.CombatInscriptions = this;
        rawAttackSpeed = 1f;
        rules.Bind(build);
        if (health != null) health.ClearTemporaryHealth();
        temporaryHealthUntil = 0f;
    }

    /// <summary>刻印获得或升级后，以 ActiveRun.build 重新聚合。</summary>
    public void RefreshBuild(RunBuildState build) => Bind(build);

    public void ResetForRoom()
    {
        rules.ResetTransient();
        if (health != null) health.ClearTemporaryHealth();
        temporaryHealthUntil = 0f;
    }

    private void Update()
    {
        rules.Advance(Time.deltaTime);
        if (temporaryHealthUntil > 0f && rules.Now >= temporaryHealthUntil)
        {
            temporaryHealthUntil = 0f;
            if (health != null) health.ClearTemporaryHealth();
        }
    }

    public float GetAttackSpeedMultiplier(float baseMultiplier)
    {
        rawAttackSpeed = baseMultiplier;
        return rules.HasAnyInscription
            ? rules.GetEffectiveAttackSpeed(baseMultiplier, health != null ? health.HealthRatio : 1f)
            : baseMultiplier;
    }

    /// <summary>普通攻击与直接技能共用；ordinaryAttack 仅控制普通攻击专属共鸣。</summary>
    public float DealDirect(EnemyHealth enemy, DamageContext context, bool ordinaryAttack)
    {
        if (enemy == null || enemy.IsDead || health == null) return 0f;
        float ratioBefore = enemy.MaxHealth > 0f ? enemy.CurrentHealth / enemy.MaxHealth : 0f;
        bool canCrit = context.trueDamage <= 0f;
        InscriptionAttackSnapshot snapshot = canCrit
            ? rules.PrepareAttack(ref context, health.HealthRatio, rawAttackSpeed, ordinaryAttack)
            : new InscriptionAttackSnapshot { BaseAttack = 0f };
        float actual = DamageResolver.Deal(enemy, context, out bool critical);
        if (actual <= 0f) return actual;

        int targetId = enemy.GetEntityId().GetHashCode();
        InscriptionHitOutcome outcome = rules.OnDirectHit(targetId, actual, ratioBefore,
            critical, canCrit, ordinaryAttack, enemy.IsBoss, enemy.IsDead, snapshot.BaseAttack,
            health.CurrentHealth, health.MaxHealth, snapshot, enemy.EligibleForKillRewards);
        if (outcome.BonusDamage > 0f && !enemy.IsDead)
        {
            // 回响与裂痕无暴击、吸血或其他触发，故不再进入 DamageResolver/DealDirect。
            enemy.TakeDamage(outcome.BonusDamage);
            if (enemy.IsDead && enemy.EligibleForKillRewards)
                rules.OnTargetKilled(targetId, health.MaxHealth, ref outcome);
        }
        if (outcome.Healing > 0f) health.Heal(outcome.Healing);
        if (outcome.TemporaryHealth > 0f)
        {
            health.AddTemporaryHealth(outcome.TemporaryHealth,
                rules.GetTemporaryHealthCap(health.MaxHealth));
            temporaryHealthUntil = rules.Now + rules.GetTemporaryHealthDuration();
        }
        return actual;
    }

    public void RegisterExternalKill(EnemyHealth enemy)
    {
        if (enemy == null || health == null || !enemy.IsDead || !enemy.EligibleForKillRewards) return;
        var outcome = new InscriptionHitOutcome();
        rules.OnTargetKilled(enemy.GetEntityId().GetHashCode(), health.MaxHealth, ref outcome);
        if (outcome.Healing > 0f) health.Heal(outcome.Healing);
    }

    private void OnEnemyHealthLost(float loss) => rules.OnEnemyDamageTaken(loss);
    private void OnPlayerDeath() => ResetForRoom();

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnEnemyHealthLost -= OnEnemyHealthLost;
            health.OnDeath -= OnPlayerDeath;
            health.ClearTemporaryHealth();
        }
        if (stats != null && stats.CombatInscriptions == this)
            stats.CombatInscriptions = null;
    }
}
