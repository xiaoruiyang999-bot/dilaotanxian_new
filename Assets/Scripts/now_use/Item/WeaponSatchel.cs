using UnityEngine;

/// <summary>
/// 武器背包（v0.7.2，1 格，无叠加，纯 C# 数据类，由 PlayerWeaponHolder 持有）。
/// 地牢换武器规则：旧武器入包替代原地掉落；包满时包内武器被挤出、原地掉落可捡回
/// （PlayerWeaponHolder.Equip 调用 Store 后对挤出者 WeaponPickup.Drop）。
/// 死亡重开 Unequip 时 Clear（总纲决策：武器不保留）。本版无 UI。
/// </summary>
public class WeaponSatchel
{
    /// <summary>包内存放的武器（空包为 null）。</summary>
    public WeaponData Stored { get; private set; }

    /// <summary>存入武器，返回被挤出的原存武器（空包返回 null）。</summary>
    public WeaponData Store(WeaponData data)
    {
        WeaponData evicted = Stored;
        Stored = data;
        return evicted;
    }

    /// <summary>清空（死亡重开 Unequip 调用）。</summary>
    public void Clear()
    {
        Stored = null;
    }
}
