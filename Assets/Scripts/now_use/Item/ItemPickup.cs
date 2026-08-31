using UnityEngine;

/// <summary>
/// 消耗品拾取物（v0.7.2，实现 v0.6.1 IPickupable，与 HealPickup 同模式）。
/// E 拾取 → ItemInventory.Add 分流；背包满 → 提示"背包已满"且不消耗拾取物（留在原地）。
/// 视觉：ConsumableData.icon 非空 → 直接显示该 sprite（无染色，缩放到约 0.45 世界单位，v0.7.5 换美术留口）；
/// 空 → 色块占位：道具色（ConsumableData.iconColor）方块 + 顶部高光条。
/// </summary>
public class ItemPickup : MonoBehaviour, IPickupable
{
    private static Sprite whiteSprite;   // 局部缓存的白图方块 sprite

    [SerializeField] private ConsumableData itemData;

    public string DisplayName
    {
        get
        {
            if (itemData == null) return "未知道具";
            return $"{itemData.DisplayName} +{itemData.Value}{EffectSuffix(itemData.EffectType)}";
        }
    }

    private void Awake()
    {
        // 与 HealPickup 同模式：运行时构建无碰撞体，自动补触发器供 PlayerInteractor 探测
        if (GetComponent<Collider2D>() == null)
        {
            CircleCollider2D col = gameObject.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;
        }
    }

    /// <summary>设置道具数据（运行时 Spawn 构建用）。</summary>
    public void Init(ConsumableData data)
    {
        itemData = data;
    }

    /// <summary>运行时构建一个消耗品拾取物（准备房间投放 / 商店陈列 / 宝箱奖励池用）。</summary>
    public static ItemPickup Spawn(ConsumableData data, Vector3 pos)
    {
        if (data == null) return null;

        GameObject go = new GameObject($"ItemPickup_{data.DisplayName}");
        go.transform.position = pos;

        if (data.Icon != null)
        {
            // 正式图标：无染色单 sprite，缩放到与色块相当的世界尺寸（约 0.45），层级 1 不超玩家层
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = data.Icon;
            sr.sortingOrder = 1;
            float maxSide = Mathf.Max(data.Icon.bounds.size.x, data.Icon.bounds.size.y);
            if (maxSide > 0f)
                go.transform.localScale = Vector3.one * (0.45f / maxSide);
        }
        else
        {
            Color c = data.IconColor;
            // 主体方块 0.3×0.3 + 顶部高光条（交互物层级 1~2，不超玩家层）
            CreateBlock(go.transform, "Body", Vector3.zero, new Vector3(0.3f, 0.3f, 1f), c, 1);
            CreateBlock(go.transform, "Highlight", new Vector3(0f, 0.1f, 0f),
                new Vector3(0.18f, 0.06f, 1f), Color.Lerp(c, Color.white, 0.5f), 2);
        }

        ItemPickup pickup = go.AddComponent<ItemPickup>();
        pickup.Init(data);
        return pickup;
    }

    public void OnPickedUp(GameObject player)
    {
        if (itemData == null || player == null) return;

        ItemInventory inventory = player.GetComponent<ItemInventory>();
        if (inventory == null)
            inventory = player.AddComponent<ItemInventory>();

        // 背包满：提示且不消耗拾取物（计划书 §一.2 满拒）
        if (!inventory.Add(itemData))
        {
            if (player.TryGetComponent(out PlayerInteractor interactor))
                interactor.ShowTemporaryHint("背包已满");
            return;
        }

        Debug.Log($"[Item] 拾取道具：{DisplayName}");
        Destroy(gameObject);
    }

    private static string EffectSuffix(ConsumableEffectType type)
    {
        switch (type)
        {
            case ConsumableEffectType.HP: return "HP";
            case ConsumableEffectType.Armor: return "护甲";
            default: return "法力";
        }
    }

    /// <summary>创建染色方块部件（白图 sprite 染色，色块占位视觉用）。</summary>
    private static void CreateBlock(Transform parent, string name, Vector3 localPos,
        Vector3 scale, Color color, int sortingOrder)
    {
        GameObject block = new GameObject(name);
        block.transform.SetParent(parent, false);
        block.transform.localPosition = localPos;
        block.transform.localScale = scale;
        SpriteRenderer sr = block.AddComponent<SpriteRenderer>();
        sr.sprite = GetWhiteSprite();
        sr.color = color;
        sr.sortingOrder = sortingOrder;
    }

    /// <summary>生成/返回缓存的白图方块 sprite（Texture2D.whiteTexture）。</summary>
    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;
        Texture2D tex = Texture2D.whiteTexture;
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), tex.width);
        return whiteSprite;
    }
}
