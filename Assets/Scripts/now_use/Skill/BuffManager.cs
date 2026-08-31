using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Buff 运行时系统（v0.7.5 技能线一期 Track B）：玩家组件，由 SkillExecutor.Awake 运行时 Get-or-Add
/// （ItemInventory/SkillExecutor 同模式；无 RequireComponent，运行时补挂安全）。
/// 管理 BuffInstance 列表（id/剩余时长/四条修饰值），Update 倒计时，到期移除。
/// 四条修饰通道（均为乘区，多 buff 叠乘；无 buff / 无本组件时各处返回 1，零行为差异）：
///   攻速倍率 AttackSpeedMultiplier → PlayerCombat 攻击间隔处读取（间隔 ÷ 倍率）；
///   移速倍率 MoveSpeedMultiplier   → PlayerMovement.FixedUpdate 读取（速度 × 倍率）；
///   受击减伤 DamageTakenMultiplier → Health.TakeDamage 护甲结算之前乘（敌人侧无本组件行为不变）；
///   输出伤害 DamageDealtMultiplier → 玩家侧全部 DamageContext 构建点乘进 multiplier 区
///     （WeaponHitbox / Projectile / SkillExecutor.MeleeAoE；敌人构建路径不经过本系统）。
/// 虚弱链：BuffInstance 可携带虚弱参数，自然到期（非刷新替换）时自动挂一条负值虚弱 Buff
///   （屹立不倒：持续结束 → 虚弱 3s）。虚弱 Buff 自身无虚弱链，不会递归。
/// 同 id 再挂 = 刷新替换（不触发旧实例的虚弱链）。
/// 二期复用（裸绞/燃命）：施放侧调 AddBuff；新输出方调静态 DamageDealtMulOf。
/// v0.7.5 二期新增：ClearAll（燃命清全部 Buff，不触发虚弱链）+ SetImmune 免疫窗口
///   （窗口内 AddBuff 对负面 Buff 直接忽略，负面 = 攻速/移速/输出 &lt;1 或受击 &gt;1）。
/// </summary>
public class BuffManager : MonoBehaviour
{
    /// <summary>单个 Buff 实例（纯运行时状态，不序列化）。各修饰值默认 1 = 不修饰。</summary>
    public class BuffInstance
    {
        public string Id;
        public float Remaining;

        public float AttackSpeedMul = 1f;   // 攻速倍率（攻击间隔 ÷ 此值；0.5 = 攻速减半）
        public float MoveSpeedMul = 1f;     // 移速倍率
        public float DamageTakenMul = 1f;   // 受击伤害倍率（0.5 = 受伤减半，护甲结算之前乘）
        public float DamageDealtMul = 1f;   // 输出伤害倍率（乘进 DamageContext.multiplier 区）

        // 到期虚弱链（0 = 无虚弱；虚弱 Buff 自身不带链）
        public float WeaknessDuration;
        public float WeaknessAttackSpeedMul = 1f;
        public float WeaknessMoveSpeedMul = 1f;
    }

    private readonly List<BuffInstance> buffs = new List<BuffInstance>();
    private readonly List<BuffInstance> expiredBuffer = new List<BuffInstance>();   // Update 到期暂存（避免迭代中改列表）

    // 免疫窗口（v0.7.5 二期燃命）：期间 AddBuff 对负面 Buff 直接忽略（正面 Buff 正常挂）
    private float immuneRemaining;

    /// <summary>当前生效的 Buff 列表（只读，供 UI/调试）。</summary>
    public IReadOnlyList<BuffInstance> Buffs => buffs;

    /// <summary>免疫窗口剩余秒数（v0.7.5 燃命）；&gt; 0 期间负面 Buff 挂不上。</summary>
    public float ImmuneRemaining => immuneRemaining;

    public float AttackSpeedMultiplier => Aggregate(b => b.AttackSpeedMul);
    public float MoveSpeedMultiplier => Aggregate(b => b.MoveSpeedMul);
    public float DamageTakenMultiplier => Aggregate(b => b.DamageTakenMul);
    public float DamageDealtMultiplier => Aggregate(b => b.DamageDealtMul);

    /// <summary>挂一个 Buff（同 id 刷新替换，不触发旧实例虚弱链；duration ≤ 0 直接忽略）。
    /// 免疫窗口内（v0.7.5 燃命）负面 Buff 直接忽略，正面 Buff 正常挂。</summary>
    public void AddBuff(string id, float duration,
        float attackSpeedMul = 1f, float moveSpeedMul = 1f,
        float damageTakenMul = 1f, float damageDealtMul = 1f,
        float weaknessDuration = 0f, float weaknessAttackSpeedMul = 1f, float weaknessMoveSpeedMul = 1f)
    {
        if (duration <= 0f) return;

        // 免疫窗口：负面 Buff（任一通道为减益）不生效
        if (immuneRemaining > 0f
            && (attackSpeedMul < 1f || moveSpeedMul < 1f || damageTakenMul > 1f || damageDealtMul < 1f))
            return;

        for (int i = buffs.Count - 1; i >= 0; i--)
        {
            if (buffs[i].Id == id)
            {
                buffs.RemoveAt(i);   // 刷新替换：不走到期逻辑，不挂虚弱
                break;
            }
        }

        buffs.Add(new BuffInstance
        {
            Id = id,
            Remaining = duration,
            AttackSpeedMul = attackSpeedMul,
            MoveSpeedMul = moveSpeedMul,
            DamageTakenMul = damageTakenMul,
            DamageDealtMul = damageDealtMul,
            WeaknessDuration = weaknessDuration,
            WeaknessAttackSpeedMul = weaknessAttackSpeedMul,
            WeaknessMoveSpeedMul = weaknessMoveSpeedMul
        });
    }

    /// <summary>按 id 移除（不触发虚弱链）；不存在时无操作。</summary>
    public void RemoveBuff(string id)
    {
        for (int i = buffs.Count - 1; i >= 0; i--)
        {
            if (buffs[i].Id == id)
            {
                buffs.RemoveAt(i);
                return;
            }
        }
    }

    /// <summary>清除全部 Buff（v0.7.5 燃命）：直接清空，不触发任何虚弱链。</summary>
    public void ClearAll()
    {
        buffs.Clear();
    }

    /// <summary>开启免疫窗口（v0.7.5 燃命）：期间 AddBuff 对负面 Buff 不生效；重复开启取较长剩余。</summary>
    public void SetImmune(float duration)
    {
        immuneRemaining = Mathf.Max(immuneRemaining, duration);
    }

    void Update()
    {
        if (immuneRemaining > 0f)
            immuneRemaining = Mathf.Max(0f, immuneRemaining - Time.deltaTime);

        if (buffs.Count == 0) return;

        expiredBuffer.Clear();
        for (int i = buffs.Count - 1; i >= 0; i--)
        {
            BuffInstance b = buffs[i];
            b.Remaining -= Time.deltaTime;
            if (b.Remaining <= 0f)
            {
                buffs.RemoveAt(i);
                expiredBuffer.Add(b);
            }
        }

        // 自然到期才挂虚弱链（刷新替换在 AddBuff 内直接移除，不经此路径）
        for (int i = 0; i < expiredBuffer.Count; i++)
        {
            BuffInstance b = expiredBuffer[i];
            if (b.WeaknessDuration > 0f)
            {
                AddBuff(b.Id + "_Weakness", b.WeaknessDuration,
                    attackSpeedMul: b.WeaknessAttackSpeedMul,
                    moveSpeedMul: b.WeaknessMoveSpeedMul);
            }
        }
        expiredBuffer.Clear();
    }

    /// <summary>四通道聚合：全列表叠乘；空列表返回 1（零行为差异）。</summary>
    private float Aggregate(System.Func<BuffInstance, float> selector)
    {
        float mul = 1f;
        for (int i = 0; i < buffs.Count; i++)
            mul *= selector(buffs[i]);
        return mul;
    }

    /// <summary>静态查询：物体（或其父级）上的输出伤害倍率；无 BuffManager 返回 1（敌人/旧场景零差异）。</summary>
    public static float DamageDealtMulOf(GameObject go)
    {
        if (go == null) return 1f;
        BuffManager b = go.GetComponentInParent<BuffManager>();
        return b != null ? b.DamageDealtMultiplier : 1f;
    }
}
