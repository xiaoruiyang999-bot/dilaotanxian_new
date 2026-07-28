using UnityEngine;

/// <summary>
/// 武器拾取物（v0.6.2 阶段 A，实现 v0.6.1 IPickupable）。
/// 拾取时校验玩家当前职业 == WeaponData.requiredClass：
/// 不符 → 提示"职业不符"并拒绝（物品留在原地）；符合 → PlayerWeaponHolder.Equip 装备。
/// 地图掉落形态：缩小 0.7× + weaponColor 染色（mapIcon 可空，空则白块染色）。
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
    /// 原地掉落构建（换武器时旧武器掉落，计划书 4.3）：
    /// 缩小 0.7× + weaponColor 染色，mapIcon 可空（空则白块）。
    /// </summary>
    public static WeaponPickup Drop(WeaponData data, Vector3 position)
    {
        if (data == null) return null;

        GameObject go = new GameObject($"WeaponPickup_{data.DisplayName}");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 0.7f;   // 地图掉落略微缩小，与手持形态区分

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = data.MapIcon;                       // 可空：空则白块染色呈现
        sr.color = data.WeaponColor;
        sr.sortingOrder = 1;

        WeaponPickup pickup = go.AddComponent<WeaponPickup>();
        pickup.Init(data);
        return pickup;
    }

    public void OnPickedUp(GameObject player)
    {
        if (weaponData == null || player == null) return;

        // 职业校验：只能拾取本职业武器（计划书 4.5）
        PlayerStats stats = player.GetComponent<PlayerStats>();
        if (stats == null || stats.CurrentClass == null
            || stats.CurrentClass.ClassType != weaponData.RequiredClass)
        {
            if (player.TryGetComponent(out PlayerInteractor interactor))
                interactor.ShowTemporaryHint("职业不符");
            Debug.Log($"[Weapon] 职业不符，无法拾取 {DisplayName}（需要 {weaponData.RequiredClass}）");
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
