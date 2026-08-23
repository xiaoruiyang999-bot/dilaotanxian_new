using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 武器实体命中检测器。
/// 挥击期间由 Combat（PlayerCombat/EnemyCombat）状态机驱动：BeginSwing → 每帧 Tick → EndSwing。
/// 每次 Tick 用武器矩形（长 = AttackData.AttackRange，宽 = weaponWidth，
/// 姿态逐帧取自 weaponPivot）做 OverlapBox 检测，命中即调用 IDamageable.TakeDamage。
/// 每次挥击对同一目标只结算一次。
/// 不控制动画、不读取输入、不管理 AI、不自跑 Update；动画（WeaponAnimator）纯视觉。
/// </summary>
public class WeaponHitbox : MonoBehaviour
{
    [Header("攻击数据")]
    [SerializeField] private AttackData attackData;

    [Header("武器几何")]
    [Tooltip("武器挂载点。武器矩形沿其局部 +X 轴伸出，位置与朝向逐帧取自此 Transform")]
    [SerializeField] private Transform weaponPivot;

    [Tooltip("武器矩形宽度（垂直于攻击方向）。若存在 WeaponController，会被其 WeaponWidth 覆盖，保证判定宽度与视觉宽度同源")]
    [SerializeField] private float weaponWidth = 0.15f;

    [Header("调试")]
    [SerializeField] private bool drawGizmos = false;

    /// <summary>
    /// 命中反馈扩展点（特效/音效/未来的击退、HitStop）。
    /// 参数：被命中目标、命中点世界坐标。
    /// 伤害结算不经过此事件，由本组件直接调用 IDamageable.TakeDamage。
    /// </summary>
    public System.Action<IDamageable, Vector2> OnHit;

    private const int MaxHits = 16;
    private static readonly Collider2D[] hitBuffer = new Collider2D[MaxHits];

    private readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();
    private bool isSwinging;

    /// <summary>伤害乘数（M2·v0.7.1）：玩家侧由 PlayerCombat 每次攻击前刷新（升级×兽化叠乘）；敌人侧保持 1。</summary>
    public float DamageMultiplier { get; set; } = 1f;
    /// <summary>本次攻击的暴击乘数（M4：PlayerCombat 每次攻击前 roll——暴击=CD 值，否则 1）。</summary>
    public float CritMultiplier { get; set; } = 1f;

    /// <summary>
    /// 当前判定矩形几何（x=长度, y=宽度），已含 lossyScale（v0.6.2）。
    /// 预警显示（AttackIndicator 的 Box 形状）消费此属性，保证"所见即所判"——
    /// 与 Tick() 用同一套公式，是 v0.4.5.2 Final5"三者同源"原则的回归。
    /// </summary>
    public Vector2 CurrentBoxGeometry
    {
        get
        {
            float scale = weaponPivot != null ? weaponPivot.lossyScale.x : 1f;
            float length = (attackData != null ? attackData.AttackRange : 1f) * scale;
            float width = attackData != null && attackData.OverrideWidth > 0f ? attackData.OverrideWidth : weaponWidth;
            return new Vector2(length, width * scale);
        }
    }

    void Awake()
    {
        // 宽度/pivot 单一数据源：存在 WeaponController 时以其为准，序列化值仅作兜底。
        WeaponController wc = GetComponent<WeaponController>();
        if (wc != null)
        {
            weaponWidth = wc.WeaponWidth;
            if (weaponPivot == null)
                weaponPivot = wc.WeaponPivot;
        }

        if (attackData == null)
            Debug.LogWarning($"[{nameof(WeaponHitbox)}] 未配置 AttackData on {gameObject.name}", this);
        if (weaponPivot == null)
            Debug.LogWarning($"[{nameof(WeaponHitbox)}] 未配置 weaponPivot on {gameObject.name}", this);
    }

    /// <summary>
    /// 开始一次挥击。清空命中去重集合。由 Combat 在 EnterActive 时调用。
    /// </summary>
    public void BeginSwing()
    {
        hitThisSwing.Clear();
        isSwinging = true;
    }

    /// <summary>
    /// 执行一次武器矩形检测。由 Combat 在 Active 阶段每帧调用。
    /// 检测盒尺寸乘以 weaponPivot.lossyScale（假设父级等比缩放，与 WeaponController 视觉一致），
    /// 保证判定矩形与渲染出的武器矩形严格一致（如 Player 根物体 scale=0.6 时，武器世界长度也随之缩短）。
    /// </summary>
    public void Tick()
    {
        if (!isSwinging) return;
        if (attackData == null || weaponPivot == null) return;

        float scale = weaponPivot.lossyScale.x;
        float length = attackData.AttackRange * scale;
        float width = (attackData.OverrideWidth > 0f ? attackData.OverrideWidth : weaponWidth) * scale;
        Vector2 dir = weaponPivot.right;
        Vector2 center = (Vector2)weaponPivot.position + dir * (length * 0.5f);
        float angle = weaponPivot.eulerAngles.z;

        // Unity 6 新 API：OverlapBox 改用 ContactFilter2D 传参（结构体，无每帧分配）。
        // useTriggers 保持旧 NonAlloc 行为（命中的 trigger 在下方手动跳过）。
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(attackData.TargetLayer);
        filter.useTriggers = false;

        int count = Physics2D.OverlapBox(
            center, new Vector2(length, width), angle, filter, hitBuffer);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = hitBuffer[i];
            if (hit == null) continue;
            if (hit.isTrigger) continue;                        // 跳过探测圈等 trigger（如 Enemy 半径5的触发器）
            if (hit.transform.IsChildOf(transform)) continue;   // 跳过攻击者自身（含子物体）。
                                                                // 注意不能用 transform.root 比较：v0.5.2 起敌人/障碍物同挂 DungeonSystem 根下，
                                                                // root 相同会被误判为"自身"（敌人武器因此打不到木箱）
            IDamageable damageable = FindDamageableInParents(hit.transform);
            if (damageable != null && hitThisSwing.Add(damageable))
            {
                damageable.TakeDamage(attackData.AttackDamage * DamageMultiplier * CritMultiplier);
                OnHit?.Invoke(damageable, hit.ClosestPoint(center));

                // 命中反馈三件套（M1.5·v0.6.1）：音效 + 打击停顿 + 轻震屏。
                // 玩家与敌人共用本组件，双方命中都有反馈。
                AudioManager.PlaySFX("hit");
                HitStop.Request(0.03f);
                CameraFollow.ShakeMain(0.05f, 0.08f);
            }
        }
    }

    /// <summary>
    /// 结束挥击。由 Combat 在 EnterRecovery 时调用（Active 结束即停挥，不能等到 Recovery 之后）。
    /// </summary>
    public void EndSwing()
    {
        isSwinging = false;
        hitThisSwing.Clear();
    }

    private static IDamageable FindDamageableInParents(Transform current)
    {
        while (current != null)
        {
            if (current.TryGetComponent<IDamageable>(out var damageable))
                return damageable;
            current = current.parent;
        }
        return null;
    }

    /// <summary>
    /// 外部切换攻击数据（v0.5 技能/武器切换）。
    /// </summary>
    public void SetAttackData(AttackData data)
    {
        attackData = data;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos || weaponPivot == null) return;

        float scale = weaponPivot.lossyScale.x;
        float length = (attackData != null ? attackData.AttackRange : 1f) * scale;
        float width = weaponWidth * scale;
        Vector2 center = (Vector2)weaponPivot.position + (Vector2)weaponPivot.right * (length * 0.5f);

        Gizmos.color = isSwinging ? Color.red : new Color(1f, 0f, 0.5f, 0.5f);
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, weaponPivot.eulerAngles.z), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(length, width, 0.01f));
        Gizmos.matrix = oldMatrix;
    }
}
