using UnityEngine;

/// <summary>
/// 准备场景管理器（v0.6.2 阶段 C，计划书 R4：独立准备场景）。
/// Start 时运行时代码构建：准备房间地板与围墙（多色块）→ 三展台（PrepRoomPlacer）
/// → 进入地牢的传送门（PrepPortalInteractable）→ 玩家摆到展台下方出生位。
/// 准备场景专属武器规则：PlayerWeaponHolder.storeOldWeaponInSatchel = false（v0.7.2 字段换代），
/// 订阅 OnWeaponChanged —— 换武器时旧初始武器自动归位原展台（不掉落、不入武器背包）。
/// 死亡回来时（RunStateCarrier 有上次职业）按上次职业立即刷新武器展台。
/// v0.7.2：地面运行时摆放 4 个测试消耗品 ItemPickup（Consumable_Test，仅本版链路验证用，
/// v0.7.3 建正式三包后删除，见 v0.7.2_正式计划 §七）。
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

        // v0.7.2 测试投放：地面 4 个测试消耗品（仅本版链路验证，v0.7.3 删除）
        SpawnTestItems();

        // 死亡回来：职业保留，按上次职业立即摆好武器展台（武器需重新拾取）
        ClassData last = RunStateCarrier.Ensure().LastChosenClass;
        if (last != null)
            PrepRoomPlacer.RefreshWeapons(last);

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

    // ========== v0.7.2 测试道具投放（v0.7.3 建正式三包后删除本块与 Consumable_Test 资产） ==========

    /// <summary>地面横排 4 个测试消耗品（出生位前方），加载走 ClassCatalog 同款编辑器路径分支。</summary>
    private void SpawnTestItems()
    {
        ConsumableData testItem = LoadTestConsumable();
        if (testItem == null) return;

        for (int i = 0; i < 4; i++)
            ItemPickup.Spawn(testItem, playerSpawn + new Vector3((i - 1.5f) * 1.2f, -1.2f, 0f));
    }

    private static ConsumableData LoadTestConsumable()
    {
#if UNITY_EDITOR
        ConsumableData data = UnityEditor.AssetDatabase.LoadAssetAtPath<ConsumableData>(
            "Assets/Data/Item/Consumable_Test.asset");
        if (data == null)
            Debug.LogWarning("[Item] 测试消耗品 Consumable_Test.asset 未找到，跳过测试投放。");
        return data;
#else
        return Resources.Load<ConsumableData>("Item/Consumable_Test");
#endif
    }

    // ========== 房间视觉（程序员美术多色块） ==========

    private void BuildRoom()
    {
        CreateBlock("Floor", roomSize, Vector3.zero, floorColor, -1);

        float w = roomSize.x, h = roomSize.y;
        const float t = 0.5f;   // 墙厚
        CreateBlock("WallN", new Vector2(w + t * 2f, t), new Vector3(0f, h * 0.5f + t * 0.5f, 0f), wallColor);
        CreateBlock("WallS", new Vector2(w + t * 2f, t), new Vector3(0f, -h * 0.5f - t * 0.5f, 0f), wallColor);
        CreateBlock("WallW", new Vector2(t, h), new Vector3(-w * 0.5f - t * 0.5f, 0f, 0f), wallColor);
        CreateBlock("WallE", new Vector2(t, h), new Vector3(w * 0.5f + t * 0.5f, 0f, 0f), wallColor);
    }

    private void CreateBlock(string name, Vector2 size, Vector3 localPos, Color color, int sortingOrder = 0)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetWhiteSprite();
        sr.color = color;
        sr.sortingOrder = sortingOrder;
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
