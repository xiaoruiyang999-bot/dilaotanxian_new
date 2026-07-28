using UnityEngine;

/// <summary>
/// 武器行为基类 / 分发骨架（v0.6.2 阶段 A：只立结构）。
/// 近战分支转发现有 PlayerCombat 三件套链路（SetAttackData）；
/// 远程 / 自身施法分支为 v0.6.3 挂点，本阶段不实现。
/// </summary>
public abstract class WeaponBehavior
{
    protected readonly WeaponInstance instance;

    protected WeaponBehavior(WeaponInstance instance)
    {
        this.instance = instance;
    }

    /// <summary>装备时应用（把武器能力接到玩家攻击链路）。</summary>
    public abstract void Apply(PlayerCombat combat);

    /// <summary>按 behaviorType 分发创建行为对象。</summary>
    public static WeaponBehavior Create(WeaponInstance inst)
    {
        if (inst == null || inst.Data == null) return null;

        switch (inst.Data.BehaviorType)
        {
            case WeaponBehaviorType.Melee:
                return new MeleeWeaponBehavior(inst);
            // TODO(v0.6.3): Ranged → RangedWeaponBehavior（Projectile/ProjectileData，子弹引用补进 WeaponData）
            // TODO(v0.6.3): SelfCast → SelfCastWeaponBehavior（治疗法杖等自身效果）
            default:
                Debug.LogWarning($"[Weapon] {inst.Data.DisplayName} 的行为类型 {inst.Data.BehaviorType} 尚未实现（v0.6.3），暂按近战链路处理。");
                return new MeleeWeaponBehavior(inst);
        }
    }
}

/// <summary>
/// 近战武器行为：把 WeaponData.attackData 接入现有 PlayerCombat 三件套
/// （WeaponController / WeaponAnimator / WeaponHitbox 链路不变）。
/// </summary>
public class MeleeWeaponBehavior : WeaponBehavior
{
    public MeleeWeaponBehavior(WeaponInstance instance) : base(instance) { }

    public override void Apply(PlayerCombat combat)
    {
        if (combat == null || instance.Data.AttackData == null) return;
        combat.SetAttackData(instance.Data.AttackData);
    }
}
