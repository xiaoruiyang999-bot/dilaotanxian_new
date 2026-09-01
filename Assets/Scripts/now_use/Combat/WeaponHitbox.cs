using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 武器实体命中检测器。
/// 挥击期间由 Combat（PlayerCombat/EnemyCombat）状态机驱动：BeginSwing → 每帧 Tick → EndSwing。
/// 每次 Tick 用武器矩形（长 = AttackData.AttackRange，宽 = weaponWidth，
/// 姿态逐帧取自 weaponPivot）做 OverlapBox 检测。
/// 伤害结算（v0.7.0 分流）：玩家 → DamageResolver.Deal（新管线：基础攻击×倍率×暴击）；
/// 敌人 → IDamageable.TakeDamage 原路径。
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
    /// 伤害结算不经过此事件，由本组件结算（v0.7.0 玩家走 DamageResolver，敌人直扣）。
    /// </summary>
    public System.Action<IDamageable, Vector2> OnHit;

    /// <summary>
    /// 判定长度倍率（v0.6.3 枪矛戳击）：判定长度 = AttackRange × LengthMultiplier。
    /// 默认 1，戳击动画期间由 PlayerCombat 随伸展进度驱动；BeginSwing/SetAttackData 时复位。
    /// </summary>
    public float LengthMultiplier { get; set; } = 1f;

    /// <summary>
    /// 伤害倍率（v0.7.0 蓄力倍率归位）：玩家侧结算走 DamageContext.multiplier，
    /// 倍率区独立于基础攻击（伤害计算公式文档 §2.1）。默认 1，满蓄时由 PlayerCombat
    /// 设为 ChargeFullDamageMul，BeginSwing 复位（LengthMultiplier 同款先例）。
    /// 仅玩家路径生效；敌人路径保持 AttackData 直扣不受影响。
    /// </summary>
    public float DamageMultiplier { get; set; } = 1f;

    /// <summary>
    /// 多目标命中伤害倍率（v0.7.5 二期长枪贯穿被动）：单次挥击命中 ≥2 目标时该次全部命中 ×此倍率。
    /// 由 WeaponPassives 按当前武器设置（长枪 1.15，其余 1）。仅玩家路径生效；敌人路径保持默认 1 不受影响。
    /// 第 2 个目标命中时对第 1 个目标追补倍率差（走正常 TakeDamage，护甲同比例结算），保证"全部命中"同倍率。
    /// </summary>
    public float MultiHitDamageMul { get; set; } = 1f;

    private const int MaxHits = 16;
    private static readonly Collider2D[] hitBuffer = new Collider2D[MaxHits];

    private readonly HashSet<Collider2D> hitThisSwing = new HashSet<Collider2D>();
    private bool isSwinging;
    private WeaponController wc;   // v0.6.3：缓存引用，Tick 实时读其 WeaponWidth（蓄力宽度缩放）
    private PlayerStats attackerStats;   // v0.7.0：Awake 缓存攻击者根的 PlayerStats；非空 = 玩家（走新管线），空 = 敌人（原路径）

    // v0.7.5 二期贯穿追补：本挥击第 1 个命中目标与其结算伤害（BeginSwing 复位）
    private IDamageable swingFirstTarget;
    private float swingFirstDealt;

    void Awake()
    {
        // 宽度/pivot 单一数据源：存在 WeaponController 时以其为准，序列化值仅作兜底。
        wc = GetComponent<WeaponController>();
        if (wc != null)
        {
            weaponWidth = wc.WeaponWidth;
            if (weaponPivot == null)
                weaponPivot = wc.WeaponPivot;
        }

        // v0.7.0 玩家/敌人结算分流：按攻击者根上 PlayerStats 存在性判定（敌人根无 PlayerStats）
        attackerStats = GetComponentInParent<PlayerStats>();

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
        LengthMultiplier = 1f;   // 戳击倍率复位（v0.6.3）
        DamageMultiplier = 1f;   // 蓄力伤害倍率复位（v0.7.0）
        swingFirstTarget = null;   // 贯穿追补复位（v0.7.5 二期）
        swingFirstDealt = 0f;
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
        float length = attackData.AttackRange * LengthMultiplier * scale;
        // v0.6.3：宽度实时读 WeaponController.WeaponWidth（含蓄力宽度倍率），判定逻辑其余零改动
        float width = (wc != null ? wc.WeaponWidth : weaponWidth) * scale;
        Vector2 dir = weaponPivot.right;
        Vector2 center = (Vector2)weaponPivot.position + dir * (length * 0.5f);
        float angle = weaponPivot.eulerAngles.z;

        // Unity 6 新 API：OverlapBox 改用 ContactFilter2D 传参（结构体，无每帧分配）。
        // useTriggers 保持旧 NonAlloc 行为（命中的 trigger 在下方手动跳过）。
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(attackData.TargetLayer);
        filter.useTriggers = true;

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
            if (hitThisSwing.Contains(hit)) continue;           // 每次挥击每目标只结算一次

            hitThisSwing.Add(hit);

            if (hit.TryGetComponent<IDamageable>(out var damageable))
            {
                // v0.7.0 结算分流：玩家走新管线（角色攻击+武器攻击）×倍率×暴击；敌人保持原路径
                if (attackerStats != null)
                {
                    // v0.7.5 二期长枪贯穿：本挥击第 2 个及以后目标 ×MultiHitDamageMul（默认 1 = 零差异）
                    float pierceMul = MultiHitDamageMul > 1f && hitThisSwing.Count >= 2 ? MultiHitDamageMul : 1f;
                    DamageContext ctx = new DamageContext
                    {
                        baseAttack = attackerStats.Attack + attackData.AttackDamage,
                        // v0.7.5：输出倍率通道（强力一击期间普攻/蓄力也吃加成）；无 BuffManager 为 1 零差异
                        multiplier = DamageMultiplier * pierceMul * BuffManager.DamageDealtMulOf(attackerStats.gameObject),
                        critRate = attackerStats.CritRate,
                        critDamage = attackerStats.CritDamage
                    };
                    float dealt = DamageResolver.Deal(damageable, ctx);

                    if (MultiHitDamageMul > 1f)
                    {
                        if (hitThisSwing.Count == 1)
                        {
                            // 记录本挥击第 1 个目标（追补用）
                            swingFirstTarget = damageable;
                            swingFirstDealt = dealt;
                        }
                        else if (hitThisSwing.Count == 2 && swingFirstTarget != null)
                        {
                            // 达 2 目标：给第 1 个目标追补倍率差（走正常 TakeDamage，护甲同比例结算）
                            swingFirstTarget.TakeDamage(swingFirstDealt * (MultiHitDamageMul - 1f));
                            swingFirstTarget = null;
                        }
                    }
                }
                else
                {
                    damageable.TakeDamage(attackData.AttackDamage);
                }
                OnHit?.Invoke(damageable, hit.ClosestPoint(center));

                // 命中反馈三件套（M1.5·v0.6.1，v1.0.8 自 MCP 分支恢复）：音效 + 打击停顿 + 轻震屏。
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
