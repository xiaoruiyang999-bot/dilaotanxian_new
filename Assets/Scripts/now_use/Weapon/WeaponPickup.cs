using UnityEngine;

/// <summary>
/// 武器拾取物（v0.6.2 阶段 A，实现 v0.6.1 IPickupable；v0.6.3 升级掉落视觉）。
/// 拾取时校验武器是否位于当前 PlayableCharacterDefinition 的兼容池：
/// 不符 → 提示"职业不符"并拒绝（物品留在原地）；符合 → PlayerWeaponHolder.Equip 装备。
/// 地图掉落形态：WeaponVisualBuilder.BuildMapIcon 运行时多色块小图标 + 职业色底板（缩放 0.7 内置在图标根）。
/// </summary>
public class WeaponPickup : MonoBehaviour, IPickupable
{
    [SerializeField] private WeaponData weaponData;

    public string DisplayName => weaponData != null ? weaponData.DisplayName : "未知武器";

    private void Awake()
    {
        // 运行时 Drop / 未来 prefab 摆放两种来源统一保证有可探测的触发器
        if (GetComponent<Collider2D>() == null)
        {
            CircleCollider2D col = gameObject.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;
        }
    }

    /// <summary>设置武器数据（运行时 Drop 构建用）。</summary>
    public void Init(WeaponData data)
    {
        weaponData = data;
    }

    /// <summary>
    /// 原地掉落构建（换武器时旧武器掉落，计划书 4.3；v0.6.3 视觉升级）：
    /// WeaponVisualBuilder.BuildMapIcon 运行时小图标（含职业色底板，0.7 缩放内置在图标根，
    /// go 本身不再缩放，避免重复缩放）。
    /// </summary>
    public static WeaponPickup Drop(WeaponData data, Vector3 position)
    {
        if (data == null) return null;

        GameObject go = new GameObject($"WeaponPickup_{data.DisplayName}");
        go.transform.position = position;

        GameObject icon = WeaponVisualBuilder.BuildMapIcon(data);
        icon.transform.SetParent(go.transform, false);

        WeaponPickup pickup = go.AddComponent<WeaponPickup>();
        pickup.Init(data);
        return pickup;
    }

    public void OnPickedUp(GameObject player)
    {
        if (weaponData == null || player == null) return;

        // 兼容性只由当前职业角色定义的武器池决定，不再读取 WeaponData.requiredClass。
        PlayerStats stats = player.GetComponent<PlayerStats>();
        PlayableCharacterDefinition definition =
            stats != null ? stats.CurrentPlayableCharacter : null;
        if (definition == null || !definition.SupportsWeapon(weaponData))
        {
            if (player.TryGetComponent(out PlayerInteractor interactor))
                interactor.ShowTemporaryHint("该职业角色无法使用");
            Debug.Log($"[Weapon] {definition?.DisplayName ?? "未选择角色"} 无法使用 {DisplayName}");
            return;   // 拒绝拾取，物品留在原地
        }

        PlayerWeaponHolder holder = player.GetComponent<PlayerWeaponHolder>();
        if (holder == null)
            holder = player.AddComponent<PlayerWeaponHolder>();

        holder.Equip(weaponData);
        RunStateCarrier.Ensure().SetWeapon(weaponData);   // 跨场景载体记录（进地牢时应用到新玩家）
        Debug.Log($"[Weapon] 拾取武器：{DisplayName}");
        Destroy(gameObject);
    }
}
