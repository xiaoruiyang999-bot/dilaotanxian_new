using UnityEngine;

/// <summary>运行时词缀应用器。由 EnemySpawner 注入，不参与 AI 决策和攻击状态机。</summary>
public class EnemyAffix : MonoBehaviour
{
    public EnemyAffixConfig Config { get; private set; }
    private bool applied;

    public void Apply(EnemyAffixConfig config)
    {
        if (applied || config == null) return;
        applied = true;
        Config = config;

        EnemyStats stats = GetComponent<EnemyStats>();
        if (stats != null) stats.ApplyMoveSpeedMultiplier(config.moveSpeedMultiplier);

        EnemyHealth health = GetComponent<EnemyHealth>();
        if (health != null) health.ScaleMaxHealth(config.healthMultiplier);

        transform.localScale *= config.scaleMultiplier;
        SpriteRenderer sprite = GetComponent<SpriteRenderer>();
        if (sprite != null) sprite.color *= config.tint;

        name += $" [{config.displayName}]";
    }
}
