using UnityEngine;

/// <summary>
/// 法力瓶拾取物（v0.6.3 掉落闭环）：宝箱掉落 / 商店补给的 +法力 道具。
/// 实现 IPickupable（与 HealPickup 同模式）：走近成为候选 → 按 E 拾取 → 回蓝并销毁。
/// 视觉运行时构建：瓶身方块（#3498DB）+ 瓶颈窄条 + 浅蓝液面（#85C1E9），无需 prefab；
/// 宝箱（ManaBottlePickup.Spawn）与商店补给两处复用。
/// </summary>
public class ManaBottlePickup : MonoBehaviour, IPickupable
{
    private static readonly Color BottleColor = new Color(0.204f, 0.596f, 0.859f);   // #3498DB 法力蓝
    private static readonly Color LiquidColor = new Color(0.522f, 0.757f, 0.914f);   // #85C1E9 浅蓝

    private static Sprite whiteSprite;   // 局部缓存的白图方块 sprite

    [SerializeField] private float manaAmount = 40f;

    public string DisplayName => $"法力瓶 +{manaAmount}法力";

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

    /// <summary>运行时构建一瓶法力瓶（宝箱掉落用，amount 默认 40）。</summary>
    public static ManaBottlePickup Spawn(Vector3 pos, float amount = 40f)
    {
        GameObject go = new GameObject("ManaBottlePickup");
        go.transform.position = pos;

        // 瓶身 0.25×0.3
        CreateBlock(go.transform, "Body", Vector3.zero,
            new Vector3(0.25f, 0.3f, 1f), BottleColor, 1);
        // 瓶颈窄条
        CreateBlock(go.transform, "Neck", new Vector3(0f, 0.19f, 0f),
            new Vector3(0.1f, 0.08f, 1f), BottleColor, 1);
        // 浅蓝液面小块（瓶身上沿内侧）
        CreateBlock(go.transform, "Liquid", new Vector3(0f, 0.08f, 0f),
            new Vector3(0.19f, 0.08f, 1f), LiquidColor, 2);

        ManaBottlePickup pickup = go.AddComponent<ManaBottlePickup>();
        pickup.manaAmount = amount;
        return pickup;
    }

    public void OnPickedUp(GameObject player)
    {
        if (player != null)
            player.GetComponent<PlayerStats>()?.AddMana(manaAmount);
        Debug.Log($"[Dungeon] 拾取{DisplayName}");
        Destroy(gameObject);
    }

    /// <summary>创建染色方块部件（白图 sprite 染色，与 v0.6.2 展台同款做法）。</summary>
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
