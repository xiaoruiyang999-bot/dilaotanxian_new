using UnityEngine;

/// <summary>
/// 商店商品（M2·v0.7.1）：walk-over 购买——走上基座时若金币够则自动购买并压暗售罄；
/// 不够则不消耗（可再次走上来买），价格常显在基座上方。
/// 同一 Shop.prefab 被 InteractableTable_Shop 以 Row 布局生成 3 个实例，
/// 商品按静态轮转分配（第1个治疗/第2个护甲/第3个升级券），同层货架固定可复现。
/// 视觉纯代码自建（金色基座 + 名称/价格 TextMesh），prefab 只需空 GO + 本组件 + trigger Collider。
/// </summary>
public class ShopInteractable : Interactable
{
    public enum GoodType { Heal, Armor, Upgrade }

    [Header("商品（留 None=按实例轮转自动分配）")]
    [SerializeField] private GoodType good = GoodType.Heal;
    [SerializeField] private bool autoAssign = true;

    [Header("定价")]
    [SerializeField] private int healPrice = 8;
    [SerializeField] private int armorPrice = 10;
    [SerializeField] private int upgradePrice = 15;

    private static int spawnCounter;   // 每层商品轮转（InteractableSpawner 重建时重置意义不大，取模循环即可）
    private GoodType assigned;
    private int price;
    private string label;

    protected override void Awake()
    {
        base.Awake();
        if (autoAssign)
        {
            GoodType[] order = { GoodType.Heal, GoodType.Armor, GoodType.Upgrade };
            assigned = order[Mathf.Abs(spawnCounter) % order.Length];
            spawnCounter++;
        }
        else assigned = good;

        switch (assigned)
        {
            case GoodType.Heal: price = healPrice; label = "治疗+2"; break;
            case GoodType.Armor: price = armorPrice; label = "护甲+2"; break;
            case GoodType.Upgrade: price = upgradePrice; label = "强化三选一"; break;
        }
        BuildVisual();
    }

    /// <summary>代码自建视觉：金色基座方块 + 名称/价格文字（prefab 无需任何资产引用）。</summary>
    private void BuildVisual()
    {
        if (visual == null)
        {
            var srGo = new GameObject("Base");
            srGo.transform.SetParent(transform, false);
            visual = srGo.AddComponent<SpriteRenderer>();
            visual.sprite = CoinDrop.CoinSprite;             // 复用金币圆（程序生成，零资产）
            visual.color = new Color(1f, 0.78f, 0.2f);
            srGo.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
            srGo.transform.position = transform.position;
        }

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(transform, false);
        labelGo.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        var tm = labelGo.AddComponent<TextMesh>();
        tm.text = label + " " + price + "币";
        tm.fontSize = 14;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = new Color(1f, 0.85f, 0.35f);
        var mr = labelGo.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 6;                  // 狼人(2)武器(3)金币(4)之上，血条(10)之下
    }

    protected override void OnConsumed(Collider2D player)
    {
        if (!player.TryGetComponent(out PlayerStats stats)) return;

        if (!stats.TrySpendCoins(price))
        {
            // 余额不足：恢复可触发（基类 consumed 已置 true），走开再回来可重试
            consumed = false;
            AudioManager.PlaySFX("door");   // 低沉提示音（未配静默）
            Debug.Log($"[Shop] 金币不足（{stats.Coins}/{price}）");
            return;
        }

        switch (assigned)
        {
            case GoodType.Heal:
                if (player.TryGetComponent(out Health hp)) hp.Heal(20f);
                break;
            case GoodType.Armor:
                stats.AddMaxArmor(0f);   // 不加上限，直接补 2 点当前护甲（商店是即時补给）
                stats.ModifyArmor(20f);
                break;
            case GoodType.Upgrade:
                UpgradePanel.Show();
                break;
        }
        AudioManager.PlaySFX("chest");
        SetConsumedVisual();
        Debug.Log($"[Shop] 购买 {label}（-{price} 币，余 {stats.Coins}）");
    }

    protected override void ApplyEffect(Collider2D player)
    {
        // 结算在 OnConsumed 内完成（需要"不足可重试"，不走基类一次性流程）
    }
}
