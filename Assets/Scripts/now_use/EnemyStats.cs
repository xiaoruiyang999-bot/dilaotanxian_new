using UnityEngine;

/// <summary>
/// 敌人属性配置。与PlayerStats对应，但针对敌人AI需求设计。
/// 注意：攻击相关数值（AttackRange / AttackDamage / AttackCooldown）已迁移至 AttackData，
/// 由 EnemyCombat / EnemyAI 统一读取。本类保留旧属性仅作兼容性 fallback，不推荐新代码使用。
/// </summary>
public class EnemyStats : MonoBehaviour
{
    [Header("基础属性")]
    [SerializeField] private float maxHealth = 3f;
    [SerializeField] private float moveSpeed = 3f;

    [Header("AI检测")]
    [SerializeField] private float detectionRange = 5f;     // 发现玩家的距离
    [SerializeField] private float losePlayerRange = 8f;    // 丢失玩家的距离（需大于detectionRange）

    [Header("攻击（已弃用：请使用 AttackData）")]
    [SerializeField] private float attackRange = 1.2f;      // 已迁移至 AttackData
    [SerializeField] private float attackDamage = 1f;       // 已迁移至 AttackData
    [SerializeField] private float attackCooldown = 1f;     // 已迁移至 AttackData

    // 公开访问
    public float MaxHealth => maxHealth;
    public float MoveSpeed => moveSpeed;
    public float DetectionRange => detectionRange;
    public float LosePlayerRange => losePlayerRange;

    [System.Obsolete("攻击距离已迁移至 AttackData，请通过 EnemyCombat / EnemyAI 配置 AttackData。", false)]
    public float AttackRange => attackRange;

    [System.Obsolete("攻击伤害已迁移至 AttackData，请通过 EnemyCombat / EnemyAI 配置 AttackData。", false)]
    public float AttackDamage => attackDamage;

    [System.Obsolete("攻击冷却已迁移至 AttackData，请通过 EnemyCombat / EnemyAI 配置 AttackData。", false)]
    public float AttackCooldown => attackCooldown;

    void OnValidate()
    {
        // 确保丢失距离大于检测距离
        if (losePlayerRange < detectionRange)
            losePlayerRange = detectionRange + 1f;
    }
}
