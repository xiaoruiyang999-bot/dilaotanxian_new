using UnityEngine;

/// <summary>
/// 已弃用：攻击接口。v0.4.5.1 起统一使用 AttackData + WeaponAnimator 框架，
/// PlayerCombat 不再依赖 IAttack，仅保留以避免破坏旧引用。
/// </summary>
[System.Obsolete("v0.4.5.1 起统一使用 AttackData + WeaponAnimator，不再使用 IAttack。", false)]
public interface IAttack
{
    float AttackRange { get; }
    float AttackDamage { get; }
    void Execute();
}
