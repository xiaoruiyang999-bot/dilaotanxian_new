using UnityEngine;

/// <summary>
/// 武器行为基类 / 分发（v0.6.3：三分支全部接入）。
/// 近战 → PlayerCombat.SetMeleeWeapon（AttackData 运行时副本 + 蓄力规则）；
/// 远程 → PlayerCombat.SetRangedWeapon（Projectile.Launch + 弹夹/换弹）；
/// 自身施法 → PlayerCombat.SetSelfCastWeapon（治疗法杖等自身效果）。
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
            case WeaponBehaviorType.Ranged:
                return new RangedWeaponBehavior(inst);
            case WeaponBehaviorType.SelfCast:
                return new SelfCastWeaponBehavior(inst);
            default:
                Debug.LogWarning($"[Weapon] {inst.Data.DisplayName} 的行为类型 {inst.Data.BehaviorType} 未知，暂按近战链路处理。");
                return new MeleeWeaponBehavior(inst);
        }
    }
}

/// <summary>
/// 近战武器行为：WeaponData.attackData 经运行时副本接入 PlayerCombat 三件套
/// （WeaponController / WeaponAnimator / WeaponHitbox 链路不变，v0.6.3 支持蓄力缩放）。
/// </summary>
public class MeleeWeaponBehavior : WeaponBehavior
{
    public MeleeWeaponBehavior(WeaponInstance instance) : base(instance) { }

    public override void Apply(PlayerCombat combat)
    {
        if (combat == null || instance.Data.AttackData == null) return;
        combat.SetMeleeWeapon(instance);
    }
}

/// <summary>
/// 远程武器行为（v0.6.3）：Projectile.Launch 开火 + 弹夹/换弹，不碰近战三件套链路。
/// </summary>
public class RangedWeaponBehavior : WeaponBehavior
{
    public RangedWeaponBehavior(WeaponInstance instance) : base(instance) { }

    public override void Apply(PlayerCombat combat)
    {
        if (combat == null || instance.Data.ProjectileData == null) return;
        combat.SetRangedWeapon(instance);
    }
}

/// <summary>
/// 自身施法武器行为（v0.6.3：治疗法杖）：Heal + 绿环特效 + 弹夹/换弹。
/// </summary>
public class SelfCastWeaponBehavior : WeaponBehavior
{
    public SelfCastWeaponBehavior(WeaponInstance instance) : base(instance) { }

    public override void Apply(PlayerCombat combat)
    {
        if (combat == null) return;
        combat.SetSelfCastWeapon(instance);
    }
}
