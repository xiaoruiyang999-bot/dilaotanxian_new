using UnityEngine;

/// <summary>
/// 玩家武器持有与装备入口（v0.6.2 阶段 A；v0.7.2 换武器规则改走武器背包）。
/// 持有当前 WeaponInstance；地牢换武器时旧武器入 WeaponSatchel（1 格），
/// 包满被挤出的武器原地掉落生成 WeaponPickup（可捡回）。
/// 由 WeaponPickup 拾取时 GetComponent-or-AddComponent 挂载（编辑器运行期间不改 prefab YAML）。
/// </summary>
public class PlayerWeaponHolder : MonoBehaviour
{
    /// <summary>当前武器（未装备时为 null）。</summary>
    public WeaponInstance Current { get; private set; }

    /// <summary>武器背包（v0.7.2，1 格）：地牢换武器旧武器入包，包满挤出者原地掉落。</summary>
    public WeaponSatchel Satchel { get; } = new WeaponSatchel();

    /// <summary>换武器时旧武器是否入武器背包（地牢规则，v0.7.2）。准备场景置 false——旧初始武器自动归位展台（OnWeaponChanged 订阅接管）。</summary>
    public bool storeOldWeaponInSatchel = true;

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
    /// 装备武器：旧武器入武器背包（包满被挤出者原地掉落，缩小 0.7× 染色 WeaponPickup 可捡回），
    /// 新武器经 WeaponBehavior 分发接入攻击链路（近战 → PlayerCombat.SetAttackData）。
    /// </summary>
    public void Equip(WeaponData data)
    {
        if (data == null) return;

        // v0.7.2：旧武器入武器背包，包满挤出者原地掉落可捡回（准备场景关闭——由归位规则接管）
        WeaponData oldData = Current != null ? Current.Data : null;
        if (oldData != null && storeOldWeaponInSatchel)
        {
            WeaponData evicted = Satchel.Store(oldData);
            if (evicted != null)
                WeaponPickup.Drop(evicted, transform.position);
        }

        Current = new WeaponInstance(data);
        behavior = WeaponBehavior.Create(Current);
        behavior?.Apply(combat);

        OnWeaponChanged?.Invoke(oldData, data);
        Debug.Log($"[Weapon] 装备武器：{data.DisplayName}（{data.BehaviorType}）");
    }

    /// <summary>
    /// 卸下当前武器（v0.6.2 死亡重开：武器不保留），
    /// 不掉落、不生成 WeaponPickup，PlayerCombat 恢复 prefab 默认近战配置；
    /// v0.7.2：武器背包同步清空（总纲决策：武器不保留）。
    /// </summary>
    public void Unequip()
    {
        Current = null;
        behavior = null;
        Satchel.Clear();
        if (combat != null && defaultAttackData != null)
            combat.SetAttackData(defaultAttackData);
    }
}
