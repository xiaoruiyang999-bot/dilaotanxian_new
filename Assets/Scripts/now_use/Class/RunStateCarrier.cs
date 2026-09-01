using UnityEngine;

/// <summary>角色外形（v1.0.6）：与职业独立——职业决定数值/武器，外形决定视觉（狼人为纯表现，不冲突）。</summary>
public enum CharacterSkin { Warrior, Werewolf }

/// <summary>
/// 跨场景配置载体（v0.6.2 阶段 C，计划书 R4）。
/// DontDestroyOnLoad 持久单例，在准备场景与地牢场景之间携带玩家选择：
/// LastChosenClass（职业，死亡保留）+ LastWeapon（武器，死亡清空需重拿）+ ChosenCharacter（外形，死亡保留）。
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

    /// <summary>小技能分支选择索引（v0.7.4，局外切换、局内锁定；死亡保留，与 LastChosenClass 同规则）。</summary>
    public int ChosenSkillBranchIndex { get; private set; }

    /// <summary>角色外形（v1.0.6 角色选择行：狼人=纯视觉外形，与职业独立，死亡保留）。</summary>
    public CharacterSkin ChosenCharacter { get; private set; } = CharacterSkin.Warrior;

    /// <summary>是否已在角色选择页确认过外形（v1.0.8：首次进入先弹角色页再弹职业页的流程依据；死亡保留）。</summary>
    public bool CharacterChosen { get; private set; }

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

    /// <summary>设置小技能分支索引（v0.7.4：局外大厅写入，局内由 SkillExecutor 装配时读取后锁定；负值钳 0）。</summary>
    public void SetSkillBranch(int index)
    {
        ChosenSkillBranchIndex = Mathf.Max(0, index);
    }

    /// <summary>设置角色外形（v1.0.8：CharacterSelectUI 确认时写入；视觉在场景加载时由 FrameAnimator 应用）。</summary>
    public void SetCharacter(CharacterSkin skin)
    {
        ChosenCharacter = skin;
        CharacterChosen = true;
    }
}
