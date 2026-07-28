using UnityEngine;

/// <summary>
/// 玩家武器持有与装备入口（v0.6.2 阶段 A）。
/// 持有当前 WeaponInstance；装备时旧武器原地掉落生成 WeaponPickup（可捡回）。
/// 由 WeaponPickup 拾取时 GetComponent-or-AddComponent 挂载（编辑器运行期间不改 prefab YAML）。
/// </summary>
public class PlayerWeaponHolder : MonoBehaviour
{
    /// <summary>当前武器（未装备时为 null）。</summary>
    public WeaponInstance Current { get; private set; }

    /// <summary>换武器时旧武器是否原地掉落（地牢规则）。准备场景置 false——旧初始武器自动归位展台。</summary>
    public bool dropOldWeaponOnEquip = true;

    /// <summary>换武器事件（oldData 可为 null）：准备场景订阅实现旧武器自动归位。</summary>
    public System.Action<WeaponData, WeaponData> OnWeaponChanged;

    private PlayerCombat combat;
    private WeaponBehavior behavior;
    private AttackData defaultAttackData;   // prefab 默认近战（卸下武器时恢复）

    void Awake()
    {
        combat = GetComponent<PlayerCombat>();
        if (combat != null)
            defaultAttackData = combat.CurrentAttackData;
    }

    /// <summary>
    /// 装备武器：旧武器原地掉落（缩小 0.7× 染色 WeaponPickup），
    /// 新武器经 WeaponBehavior 分发接入攻击链路（近战 → PlayerCombat.SetAttackData）。
    /// </summary>
    public void Equip(WeaponData data)
    {
        if (data == null) return;

        // 旧武器原地掉落，可捡回（计划书 4.3 换武器规则；准备场景关闭——由归位规则接管）
        WeaponData oldData = Current != null ? Current.Data : null;
        if (oldData != null && dropOldWeaponOnEquip)
            WeaponPickup.Drop(oldData, transform.position);

        Current = new WeaponInstance(data);
        behavior = WeaponBehavior.Create(Current);
        behavior?.Apply(combat);

        OnWeaponChanged?.Invoke(oldData, data);
        Debug.Log($"[Weapon] 装备武器：{data.DisplayName}（{data.BehaviorType}）");
    }

    /// <summary>
    /// 卸下当前武器（v0.6.2 死亡重开：武器不保留），
    /// 不掉落、不生成 WeaponPickup，PlayerCombat 恢复 prefab 默认近战配置。
    /// </summary>
    public void Unequip()
    {
        Current = null;
        behavior = null;
        if (combat != null && defaultAttackData != null)
            combat.SetAttackData(defaultAttackData);
    }
}
