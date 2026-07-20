using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class WarriorCharacter : MonoBehaviour
{
    [Header("移动属性")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("角色数值")]
    [SerializeField] private float maxHP = 5f;
    [SerializeField] private float maxArmor = 5f;

    // ========== 运行时属性（只读，供外部读取）==========
    public float CurrentHP { get; private set; }
    public float CurrentArmor { get; private set; }
    public float MaxHP => maxHP;
    public float MaxArmor => maxArmor;

    // ========== 事件：数值变化时触发，供UI监听 ==========
    public System.Action OnStatsChanged;

    // ========== 内部组件 ==========
    private Rigidbody2D rb;

    // ========== 移动输入（由CharacterInput调用）==========
    private Vector2 moveInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // 初始化数值（全部=5）
        CurrentHP = maxHP;
        CurrentArmor = maxArmor;
    }

    // ========== 输入接口：由CharacterInput调用 ==========

    /// <summary>
    /// 设置移动输入方向。由CharacterInput脚本调用，不直接读取输入系统。
    /// </summary>
    public void SetMoveInput(Vector2 direction)
    {
        moveInput = direction;
    }

    // ========== 物理更新 ==========

    void FixedUpdate()
    {
        // 使用Rigidbody2D.velocity移动，与Tilemap Collider正确交互
        rb.linearVelocity = moveInput.normalized * moveSpeed;
    }

    // ========== 属性修改接口（后续版本受伤/恢复时调用）==========

    /// <summary>
    /// 修改HP。正值为治疗，负值为伤害。
    /// </summary>
    public void ModifyHP(float delta)
    {
        CurrentHP = Mathf.Clamp(CurrentHP + delta, 0, maxHP);
        OnStatsChanged?.Invoke();
    }

    /// <summary>
    /// 修改护甲。正值为恢复，负值为消耗。
    /// </summary>
    public void ModifyArmor(float delta)
    {
        CurrentArmor = Mathf.Clamp(CurrentArmor + delta, 0, maxArmor);
        OnStatsChanged?.Invoke();
    }

    void OnDisable()
    {
        // 对象禁用时停止移动
        rb.linearVelocity = Vector2.zero;
    }
}
