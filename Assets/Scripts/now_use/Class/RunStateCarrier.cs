using UnityEngine;

/// <summary>
/// 跨场景配置载体（v0.6.2 阶段 C，计划书 R4）。
/// DontDestroyOnLoad 持久单例，在准备场景与地牢场景之间携带玩家选择：
/// LastChosenClass（职业，死亡保留）+ LastWeapon（武器，死亡清空需重拿）。
/// ClassSelectUI 确认 / WeaponPickup 拾取时写入；RunManager 在地牢场景 Start 读取并应用。
/// </summary>
public class RunStateCarrier : MonoBehaviour
{
    public static RunStateCarrier Instance { get; private set; }

    /// <summary>本局已选职业（跨场景与死亡保留）。</summary>
    public ClassData LastChosenClass { get; private set; }

    /// <summary>本局已选武器（死亡时清空——武器不保留）。</summary>
    public WeaponData LastWeapon { get; private set; }

    /// <summary>职业与武器都已选定（进入地牢的前置条件）。</summary>
    public bool HasLoadout => LastChosenClass != null && LastWeapon != null;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>获取现有实例，没有则运行时创建（任何场景都安全调用）。</summary>
    public static RunStateCarrier Ensure()
    {
        if (Instance == null)
            new GameObject("RunStateCarrier").AddComponent<RunStateCarrier>();
        return Instance;
    }

    public void SetClass(ClassData classData)
    {
        LastChosenClass = classData;
    }

    public void SetWeapon(WeaponData weapon)
    {
        LastWeapon = weapon;
    }

    /// <summary>清空武器（死亡重开：武器不保留，回准备场景重新拾取）。</summary>
    public void ClearWeapon()
    {
        LastWeapon = null;
    }
}
