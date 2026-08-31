using UnityEngine;

/// <summary>
/// 准备房间展台布置（v0.6.2 阶段 B/C，计划书 4.5/R4）：
/// 职业选择台居中 + 两个武器展示台两侧，横向排开。
/// 阶段 C 起仅供独立准备场景（PrepRoomManager）调用，签名直接给位置/父级；
/// 持有展台 → 展示武器的映射，支持 R4 初始武器"自动归位"（ReturnWeapon）。
/// 静态引用在每次 Spawn 时重建（场景切换后旧引用为假 null，天然失效）。
/// </summary>
public static class PrepRoomPlacer
{
    private static PrepPedestal classPedestal;
    private static PrepPedestal weaponPedestalL;
    private static PrepPedestal weaponPedestalR;

    // 展台 → 当前展示武器映射（归位依据；RefreshWeapons 时更新）
    private static WeaponData shownL;
    private static WeaponData shownR;

    /// <summary>以 center 为职业选择台位置生成三展台（武器展示台左右各 2.6）。</summary>
    public static void Spawn(Vector3 center, Transform parent)
    {
        classPedestal = PrepPedestal.Create(PrepPedestalType.ClassSelector, center, parent);
        weaponPedestalL = PrepPedestal.Create(PrepPedestalType.WeaponDisplay,
            center + new Vector3(-2.6f, 0f, 0f), parent);
        weaponPedestalR = PrepPedestal.Create(PrepPedestalType.WeaponDisplay,
            center + new Vector3(2.6f, 0f, 0f), parent);
        shownL = null;
        shownR = null;

        Debug.Log("[Run] 准备房间：三展台已生成（职业选择台 + 武器展示台 ×2）");
    }

    /// <summary>确认职业后刷新两个武器展台，各呈现该职业的一把初始武器。</summary>
    public static void RefreshWeapons(ClassData classData)
    {
        if (classData == null || classData.AvailableWeapons.Count == 0) return;

        shownL = classData.AvailableWeapons[0];
        shownR = classData.AvailableWeapons.Count > 1 ? classData.AvailableWeapons[1] : null;

        if (weaponPedestalL != null) weaponPedestalL.ShowWeapon(shownL);
        if (weaponPedestalR != null) weaponPedestalR.ShowWeapon(shownR);

        Debug.Log($"[Run] 武器展台已刷新：{classData.DisplayName} 两把初始武器");
    }

    /// <summary>
    /// 初始武器自动归位（R4）：换武器时，旧武器若是某展台的展示武器，
    /// 该展台恢复呈现（展示位重新生成 WeaponPickup），而不是掉在地上。
    /// 非展台武器（地牢掉落等）不归位，返回 false。
    /// </summary>
    public static bool ReturnWeapon(WeaponData oldData)
    {
        if (oldData == null) return false;

        if (weaponPedestalL != null && shownL == oldData)
        {
            weaponPedestalL.ShowWeapon(oldData);
            Debug.Log($"[Run] 初始武器归位：{oldData.DisplayName} → 左展台");
            return true;
        }
        if (weaponPedestalR != null && shownR == oldData)
        {
            weaponPedestalR.ShowWeapon(oldData);
            Debug.Log($"[Run] 初始武器归位：{oldData.DisplayName} → 右展台");
            return true;
        }
        return false;
    }
}
