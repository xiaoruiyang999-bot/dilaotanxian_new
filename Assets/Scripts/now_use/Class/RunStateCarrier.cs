using UnityEngine;

/// <summary>
/// v2.0.2 跨场景配置载体。职业身份只保存 PlayableCharacterId；
/// 属性、外形、武器池、技能与资源全部从 PlayableCharacterDefinition 派生。
/// </summary>
public class RunStateCarrier : MonoBehaviour
{
    public static RunStateCarrier Instance { get; private set; }

    /// <summary>职业角色身份的唯一运行真值。</summary>
    public PlayableCharacterId ChosenPlayableCharacterId { get; private set; } = PlayableCharacterId.Werewolf;
    public bool HasPlayableCharacter { get; private set; }
    /// <summary>本局遗物（VS 第四批；新 Run/死亡重开清空——ResetWeaponToCharacterDefault 同点位清理）。</summary>
    public RelicInventory Relics { get; } = new RelicInventory();

    public PlayableCharacterDefinition ChosenPlayableCharacter =>
        HasPlayableCharacter ? PlayableCharacterCatalog.Get(ChosenPlayableCharacterId) : null;

    /// <summary>本局已选武器（死亡时清空——武器不保留）。</summary>
    public WeaponData LastWeapon { get; private set; }

    /// <summary>职业与武器都已选定（进入地牢的前置条件）。</summary>
    public bool HasLoadout => ChosenPlayableCharacter != null
        && LastWeapon != null
        && ChosenPlayableCharacter.SupportsWeapon(LastWeapon);

    /// <summary>小技能分支选择索引（局外切换、局内锁定，死亡保留）。</summary>
    public int ChosenSkillBranchIndex { get; private set; }

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

    /// <summary>
    /// 选择职业角色。选择会同时决定基础武器；已有不兼容武器原子回退到该角色初始武器。
    /// </summary>
    public void SetPlayableCharacter(PlayableCharacterId id)
    {
        PlayableCharacterDefinition definition = PlayableCharacterCatalog.Get(id);
        if (definition == null) return;

        ChosenPlayableCharacterId = id;
        HasPlayableCharacter = true;
        if (!definition.SupportsWeapon(LastWeapon))
            LastWeapon = definition.InitialWeapon;

    }

    public void SetWeapon(WeaponData weapon)
    {
        PlayableCharacterDefinition definition = ChosenPlayableCharacter;
        if (definition != null && !definition.SupportsWeapon(weapon))
        {
            Debug.LogWarning($"[Run] 拒绝记录不兼容武器：{(weapon != null ? weapon.DisplayName : "null")}");
            return;
        }
        LastWeapon = weapon;
    }

    /// <summary>死亡/复活时恢复职业角色的基础武器。</summary>
    public void ResetWeaponToCharacterDefault()
    {
        Relics.Clear();   // VS 第四批：重开清空本局遗物
        LastWeapon = ChosenPlayableCharacter != null ? ChosenPlayableCharacter.InitialWeapon : null;
    }

    /// <summary>设置小技能分支索引（v0.7.4：局外大厅写入，局内由 SkillExecutor 装配时读取后锁定；负值钳 0）。</summary>
    public void SetSkillBranch(int index)
    {
        ChosenSkillBranchIndex = Mathf.Max(0, index);
    }

}
