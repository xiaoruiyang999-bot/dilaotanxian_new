using UnityEngine;

/// <summary>
/// v2.0.6 第三批 商店货架（V2 §13.3）：E 交互购买——星蓝币扣款（PlayerStats.TrySpendCoins）、
/// 余额不足走 PlayerInteractor 临时提示（不吞输入）；每种限购一件（售罄压暗）。
/// 挂 Room.ContentRoot 随房销毁；商品数据由 ShopService 提供（UI/交互层不定价）。
/// </summary>
public class ShopStall : Interactable
{
    private ShopService.Goods goods;
    private PlayerStats cachedStats;
    private bool soldOut;

    public static ShopStall Create(Transform parent, Vector3 position, ShopService.Goods item)
    {
        GameObject go = new GameObject($"ShopStall_{item.Kind}");
        go.transform.SetParent(parent, true);
        go.transform.position = position;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        ShopStall stall = go.AddComponent<ShopStall>();
        col.isTrigger = true;
        col.size = new Vector2(0.9f, 0.9f);
        stall.goods = item;
        stall.BuildVisual();
        return stall;
    }

    /// <summary>E 购买：扣星蓝币 → 应用效果；余额不足提示不拦截。</summary>
    public override void Interact(Collider2D player)
    {
        if (soldOut) return;
        if (cachedStats == null && player != null)
            cachedStats = player.GetComponentInParent<PlayerStats>();
        if (cachedStats == null) return;

        if (!cachedStats.TrySpendCoins(goods.Price))
        {
            if (player != null && player.TryGetComponent(out PlayerInteractor interactor))
                interactor.ShowTemporaryHint($"星蓝币不足（需要 {goods.Price}）");
            return;
        }

        ApplyGoods(cachedStats);
        soldOut = true;
        SetConsumedVisual();
        Debug.Log($"[Shop] 售出 {goods.Title}（-{goods.Price} 星蓝币）");
    }

    private void ApplyGoods(PlayerStats stats)
    {
        Health health = stats.GetComponent<Health>();
        switch (goods.Kind)
        {
            case ShopService.GoodsKind.HealPotion:
                health?.Heal(goods.Value);
                break;
            case ShopService.GoodsKind.ArmorKit:
                stats.ModifyArmor(goods.Value);
                break;
            case ShopService.GoodsKind.AttackCharm:
                stats.PermDamageMult += goods.Value;   // 本局累计
                break;
        }
    }

    protected override void ApplyEffect(Collider2D player) { }   // 覆盖 Interact，不走一次性消耗

    private void BuildVisual()
    {
        // 货架底座 + 商品色块 + 价格牌（世界空间 TMP 简化为色块；正式素材到位后替换）
        var baseGo = new GameObject("StallBase");
        baseGo.transform.SetParent(transform, false);
        baseGo.transform.localPosition = Vector3.zero;
        var sr = baseGo.AddComponent<SpriteRenderer>();
        sr.sprite = CreateWhiteSprite();
        sr.color = new Color(0.3f, 0.3f, 0.32f);
        sr.sortingOrder = 2;
        baseGo.transform.localScale = new Vector3(1.1f, 0.8f, 1f);

        var goodsGo = new GameObject("GoodsMark");
        goodsGo.transform.SetParent(transform, false);
        goodsGo.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        var gsr = goodsGo.AddComponent<SpriteRenderer>();
        gsr.sprite = CreateWhiteSprite();
        gsr.color = goods.Color;
        gsr.sortingOrder = 3;
        goodsGo.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
    }

    private static Sprite whiteSprite;
    private static Sprite CreateWhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), tex.width);
            whiteSprite.name = "RT_whiteSprite";   // v1.1.40：运行时精灵必须命名
        }
        return whiteSprite;
    }
}
