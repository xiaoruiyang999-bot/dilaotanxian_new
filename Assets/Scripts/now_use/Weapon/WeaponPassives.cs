using UnityEngine;

/// <summary>
/// 武器被动（v0.7.5 二期）：玩家组件，SkillExecutor.Awake 运行时 Get-or-Add（无 RequireComponent，补挂安全）。
/// 长剑·连击加速：武器命中敌人时刷新短 Buff（攻速 +5%/层、上限 3 层 +15%，约 1.5s 未命中清零）——
///   层数计数器在本组件，Buff 走 BuffManager 同 id 刷新机制；
/// 长枪·贯穿：单次挥击命中 ≥2 目标时该次全部命中 ×1.15——只置 WeaponHitbox.MultiHitDamageMul，
///   计数与加倍率（含第 1 目标追补）在 hitbox 结算处。
/// 武器识别判据 = WeaponData.ChargeRule（FanScale=长剑/刀、RectScale=长枪/矛，PlayerCombat 蓄力同口径，比名字匹配稳）；
/// 未装备武器 / 默认近战（weapon == null）两个被动都不生效，旧场景零行为差异。
/// </summary>
public class WeaponPassives : MonoBehaviour
{
    [Header("长剑·连击加速")]
    [Tooltip("每层攻速加成（0.05 = +5%/层）")]
    [SerializeField] private float swordAttackSpeedPerStack = 0.05f;
    [Tooltip("层数上限（3 层 = +15%）")]
    [SerializeField] private int swordMaxStacks = 3;
    [Tooltip("连击窗口（秒）：未命中超过该时长清零层数（= buff 时长）")]
    [SerializeField] private float swordComboDuration = 1.5f;

    [Header("长枪·贯穿")]
    [Tooltip("单次挥击命中 ≥2 目标时全部命中的伤害倍率")]
    [SerializeField] private float spearMultiHitMul = 1.15f;

    private const string SwordComboBuffId = "WeaponPassive_SwordCombo";

    private BuffManager buffManager;
    private PlayerWeaponHolder holder;
    private WeaponHitbox hitbox;

    private int swordStacks;
    private float swordExpireTime = float.NegativeInfinity;

    void Awake()
    {
        buffManager = GetComponent<BuffManager>();
        holder = GetComponent<PlayerWeaponHolder>();
        hitbox = GetComponent<WeaponHitbox>();
        if (hitbox != null) hitbox.OnHit += OnWeaponHit;
    }

    void OnDestroy()
    {
        if (hitbox != null) hitbox.OnHit -= OnWeaponHit;
    }

    void Update()
    {
        // 长枪贯穿：按当前武器刷新 hitbox 倍率（赋值幂等，换武器即时生效；
        // 装备时序不定——RunManager.Start 应用武器与本组件 Awake 顺序不保证，故每帧对账）
        if (hitbox != null)
            hitbox.MultiHitDamageMul = IsSpear() ? spearMultiHitMul : 1f;

        // 长剑连击：超时未命中清零层数（buff 本体由 BuffManager 到期自清，两边同口径 1.5s）
        if (swordStacks > 0 && Time.time >= swordExpireTime)
            swordStacks = 0;
    }

    /// <summary>武器命中回调（WeaponHitbox.OnHit）：长剑命中敌人才计层/刷新 buff。</summary>
    private void OnWeaponHit(IDamageable target, Vector2 point)
    {
        if (!IsSword()) return;
        if (!(target is EnemyHealth)) return;   // 只对敌人计层（木桩也是 EnemyHealth；障碍/箱子不计）

        swordStacks = Mathf.Min(swordStacks + 1, swordMaxStacks);
        swordExpireTime = Time.time + swordComboDuration;
        buffManager?.AddBuff(SwordComboBuffId, swordComboDuration,
            attackSpeedMul: 1f + swordAttackSpeedPerStack * swordStacks);
    }

    private WeaponData CurrentWeapon()
        => holder != null && holder.Current != null ? holder.Current.Data : null;

    private bool IsSword()
    {
        WeaponData w = CurrentWeapon();
        return w != null && w.ChargeRule == ChargeRule.FanScale;
    }

    private bool IsSpear()
    {
        WeaponData w = CurrentWeapon();
        return w != null && w.ChargeRule == ChargeRule.RectScale;
    }
}
