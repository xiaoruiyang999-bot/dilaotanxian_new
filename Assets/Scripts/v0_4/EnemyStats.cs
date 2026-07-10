using UnityEngine;

/// <summary>
/// 敌人属性配置。与PlayerStats对应，但针对敌人AI需求设计。
/// </summary>
public class EnemyStats : MonoBehaviour
{
    [Header("基础属性")]
    [SerializeField] private float maxHealth = 3f;
    [SerializeField] private float moveSpeed = 3f;

    [Header("AI检测")]
    [SerializeField] private float detectionRange = 5f;     // 发现玩家的距离
    [SerializeField] private float losePlayerRange = 8f;    // 丢失玩家的距离（需大于detectionRange）

    [Header("攻击")]
    [SerializeField] private float attackRange = 1.2f;      // 攻击距离
    [SerializeField] private float attackDamage = 1f;       // 每次攻击伤害
    [SerializeField] private float attackCooldown = 1f;     // 攻击冷却（秒）

    // 公开访问
    public float MaxHealth => maxHealth;
    public float MoveSpeed => moveSpeed;
    public float DetectionRange => detectionRange;
    public float LosePlayerRange => losePlayerRange;
    public float AttackRange => attackRange;
    public float AttackDamage => attackDamage;
    public float AttackCooldown => attackCooldown;

    void OnValidate()
    {
        // 确保丢失距离大于检测距离
        if (losePlayerRange < detectionRange)
            losePlayerRange = detectionRange + 1f;
    }
}
