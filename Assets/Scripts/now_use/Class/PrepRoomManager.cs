using UnityEngine;

/// <summary>
/// 准备场景管理器（v0.6.2 阶段 C，计划书 R4：独立准备场景）。
/// Start 时运行时代码构建：准备房间地板与围墙（多色块）→ 三展台（PrepRoomPlacer）
/// → 进入地牢的传送门（PrepPortalInteractable）→ 玩家摆到展台下方出生位。
/// 准备场景专属武器规则：PlayerWeaponHolder.storeOldWeaponInSatchel = false（v0.7.2 字段换代），
/// 订阅 OnWeaponChanged —— 换武器时旧初始武器自动归位原展台（不掉落、不入武器背包）。
/// 死亡回来时（RunStateCarrier 有上次职业）按上次职业立即刷新武器展台。
/// v0.7.3：地面运行时投放三种正式消耗包各 1 个（出生位前方横排，运行时创建不变）。
/// </summary>
public class PrepRoomManager : MonoBehaviour
{
    [SerializeField] private string dungeonSceneName = "v0_7_ClassWeapon";
    [SerializeField] private Vector2 roomSize = new Vector2(16f, 12f);
    [SerializeField] private Vector3 playerSpawn = new Vector3(0f, -2.2f, 0f);   // 三展台下方，面向展台

    private static readonly Color floorColor = new Color(0.17f, 0.24f, 0.31f);   // 深灰蓝 #2C3E50
    private static readonly Color wallColor = new Color(0.10f, 0.15f, 0.18f);    // 深色 #1A252F

    private static Sprite whiteSprite;
    private PlayerWeaponHolder holder;

    void Start()
    {
        BuildRoom();

        // 三展台：职业选择台居中 + 武器展示台两侧（房中心上方横排）
        PrepRoomPlacer.Spawn(new Vector3(0f, 1.5f, 0f), transform);

        // 进入地牢的传送门（房间北侧）
        PrepPortalInteractable.Create(new Vector3(0f, 4.6f, 0f), transform, dungeonSceneName);

        // 玩家出生位 + 准备场景专属换武器规则
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            p.transform.position = playerSpawn;

            holder = p.GetComponent<PlayerWeaponHolder>();
            if (holder == null) holder = p.AddComponent<PlayerWeaponHolder>();
            holder.storeOldWeaponInSatchel = false;   // 准备阶段：换武器不入包不掉落，旧武器自动归位展台
            holder.OnWeaponChanged += OnWeaponChanged;
        }

        // v0.7.3 正式投放：地面三种消耗包各 1 个（出生位前方横排）
        SpawnDemoItems();

        // 死亡回来：职业保留，按上次职业立即摆好武器展台（武器需重新拾取）
        RunStateCarrier carrier = RunStateCarrier.Ensure();
        ClassData last = carrier.LastChosenClass;
        if (last != null)
            PrepRoomPlacer.RefreshWeapons(last);

        // v1.0.8 统一初始入口（两级选择，均为首次弹出、死亡不重弹）：
        // 未选过角色 → 先角色选择页（战士/狼人，确认后自动接续职业选择页）；选过角色但未选职业 → 直接职业选择页
        if (!carrier.CharacterChosen)
            CharacterSelectUI.Open();
        else if (last == null)
            ClassSelectUI.Open();

        // v1.0.6 角色外形：准备房间也同步应用（选择页即时改外形，回看玩家已是新视觉）
        if (RunStateCarrier.Ensure().ChosenCharacter == CharacterSkin.Werewolf && p != null)
        {
            FrameAnimator animator = p.GetComponent<FrameAnimator>();
            if (animator != null) animator.SetWerewolfVisual(true);
            WerewolfTransformation.EnsureOn(p);   // v1.0.9：准备房间也能按 T 试变身
        }

        Debug.Log("[Run] 准备场景就绪：选职业 → 拿武器 → E 传送门进地牢");
    }

    void OnDestroy()
    {
        if (holder != null)
            holder.OnWeaponChanged -= OnWeaponChanged;
    }

    /// <summary>换武器回调：旧初始武器自动归位回它原来的展台（R4 初始武器规则）。</summary>
    private void OnWeaponChanged(WeaponData oldData, WeaponData newData)
    {
        PrepRoomPlacer.ReturnWeapon(oldData);
    }

    // ========== v0.7.3 正式消耗包投放 ==========

    /// <summary>三种正式消耗包资产名（Assets/Resources/Item/）——全项目唯一清单，
    /// 准备房间投放 / 宝箱奖励池 / 商店陈列共用（v0.7.3 单点收口）。</summary>
    internal static readonly string[] ConsumableAssetNames = { "Item_HealPack", "Item_ArmorPack", "Item_ManaPack" };

    /// <summary>地面横排三种正式消耗包各 1 个（出生位前方），加载走 ClassCatalog 同款编辑器路径分支。</summary>
    private void SpawnDemoItems()
    {
        for (int i = 0; i < ConsumableAssetNames.Length; i++)
        {
            ConsumableData data = LoadConsumable(ConsumableAssetNames[i]);
            if (data == null) continue;
            ItemPickup.Spawn(data, playerSpawn + new Vector3((i - 1) * 1.2f, -1.2f, 0f));
        }
    }

    /// <summary>随机加载一种正式消耗包（宝箱奖励池等随机掉落用，v0.7.3 收口）。</summary>
    internal static ConsumableData LoadRandomConsumable()
    {
        return LoadConsumable(ConsumableAssetNames[Random.Range(0, ConsumableAssetNames.Length)]);
    }

    /// <summary>按资产名加载消耗品 SO（编辑器 AssetDatabase / 构建 Resources.Load，与 ClassCatalog 同模式）。</summary>
    internal static ConsumableData LoadConsumable(string assetName)
    {
#if UNITY_EDITOR
        ConsumableData data = UnityEditor.AssetDatabase.LoadAssetAtPath<ConsumableData>(
            $"Assets/Resources/Item/{assetName}.asset");
        if (data == null)
            Debug.LogWarning($"[Item] 消耗品 {assetName}.asset 未找到，跳过投放。");
        return data;
#else
        return Resources.Load<ConsumableData>($"Item/{assetName}");
#endif
    }

    // ========== 房间视觉（程序员美术多色块） ==========

    private void BuildRoom()
    {
        CreateBlock("Floor", roomSize, Vector3.zero, floorColor, -1);

        float w = roomSize.x, h = roomSize.y;
        const float t = 0.5f;   // 墙厚
        CreateBlock("WallN", new Vector2(w + t * 2f, t), new Vector3(0f, h * 0.5f + t * 0.5f, 0f), wallColor, solid: true);
        CreateBlock("WallS", new Vector2(w + t * 2f, t), new Vector3(0f, -h * 0.5f - t * 0.5f, 0f), wallColor, solid: true);
        CreateBlock("WallW", new Vector2(t, h), new Vector3(-w * 0.5f - t * 0.5f, 0f, 0f), wallColor, solid: true);
        CreateBlock("WallE", new Vector2(t, h), new Vector3(w * 0.5f + t * 0.5f, 0f, 0f), wallColor, solid: true);
    }

    private void CreateBlock(string name, Vector2 size, Vector3 localPos, Color color, int sortingOrder = 0, bool solid = false)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetWhiteSprite();
        sr.color = color;
        sr.sortingOrder = sortingOrder;

        // v0.7.5：围墙补碰撞（此前纯色块无碰撞，玩家穿墙）；size 显式 1×1，随 localScale 与视觉同域
        if (solid)
        {
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
        }
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), tex.width);
        }
        return whiteSprite;
    }
}
