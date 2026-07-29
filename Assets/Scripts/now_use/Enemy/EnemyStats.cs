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

    [Header("掉落（v0.6.3）")]
    [Tooltip("击杀掉落法力球回复量，0=不掉")]
    [SerializeField] private float manaOrbValue = 3f;

    [Header("攻击（已弃用：请使用 AttackData）")]
    [SerializeField] private float attackRange = 1.2f;      // 已迁移至 AttackData
    [SerializeField] private float attackDamage = 1f;       // 已迁移至 AttackData
    [SerializeField] private float attackCooldown = 1f;     // 已迁移至 AttackData

    // 公开访问
    public float MaxHealth => maxHealth;
    public float MoveSpeed => moveSpeed;
    public float DetectionRange => detectionRange;
    public float LosePlayerRange => losePlayerRange;
    /// <summary>击杀掉落法力球回复量（v0.6.3），0=不掉。</summary>
    public float ManaOrbValue => manaOrbValue;

    [System.Obsolete("攻击距离已迁移至 AttackData，请通过 EnemyCombat / EnemyAI 配置 AttackData。", false)]
    public float AttackRange => attackRange;

    [System.Obsolete("攻击伤害已迁移至 AttackData，请通过 EnemyCombat / EnemyAI 配置 AttackData。", false)]
    public float AttackDamage => attackDamage;

    [System.Obsolete("攻击冷却已迁移至 AttackData，请通过 EnemyCombat / EnemyAI 配置 AttackData。", false)]
    public float AttackCooldown => attackCooldown;

    /// <summary>楼层难度缩放（v0.5.4，计划书五-E）：HP 经 EnemyHealth 缩放（血条/伤害链路只读 EnemyHealth，
    /// 其 Awake 已初始化 CurrentHealth，缩放必须同步刷新）；本类 maxHealth 为手工同步字段一并缩放保持一致。
    /// dmgMul 预留：攻击数值已迁移至 AttackData（共享 SO，不能就地缩放），真实攻击递增属未来版本，v0.5.4 恒传 1。</summary>
    public void ApplyFloorScale(float hpMul, float dmgMul)
    {
        maxHealth *= hpMul;
        attackDamage *= dmgMul;   // 已弃用的兼容字段，仅前向兼容
        if (TryGetComponent(out EnemyHealth eh)) eh.ScaleMaxHealth(hpMul);
    }

    void OnValidate()
    {
        // 确保丢失距离大于检测距离
        if (losePlayerRange < detectionRange)
            losePlayerRange = detectionRange + 1f;
    }
}
