using System.Collections;
using UnityEngine;

/// <summary>
/// 已弃用：旧版战士攻击实现。v0.4.5.1 起玩家攻击统一由 PlayerCombat + AttackData + WeaponAnimator 处理。
/// 保留空实现仅作兼容性占位，避免场景中残留的组件引用导致 Missing Script。
/// </summary>
[System.Obsolete("v0.4.5.1 起统一使用 PlayerCombat + AttackData + WeaponAnimator，不再使用 WarriorAttack。", false)]
public class WarriorAttack : MonoBehaviour, IAttack
{
    [Header("攻击属性")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackDamage = 1f;

    public float AttackRange => attackRange;
    public float AttackDamage => attackDamage;

    public void Execute()
    {
        // 旧攻击逻辑已移除。攻击判定与动画统一由 PlayerCombat 处理。
    }
}
