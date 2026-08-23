using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Meta 进度管理器（M4·v0.9.0）：魂货币 + 永久升级树 + 存档单例。
/// - 死亡/通关时结算魂（层数 ×10 + 通关 +100）入档
/// - U 键打开魂商店（时停面板）：3 个永久节点，购买写入存档
/// - 开局注入（RunManager 调用 ApplyOwnedUpgrades）：强健+20HP / 富有+30币 / 老练+10%伤
/// 挂载同 MinimapSystem 模式（场景 MetaSystem 空对象）。
/// </summary>
public class MetaManager : MonoBehaviour
{
    public static MetaManager Instance { get; private set; }
    public static SaveSystem.SaveData Data { get; private set; }

    [Header("键位")]
    [SerializeField] private string shopKeyAction = "MetaShop";

    private PlayerInput playerInput;
    private bool subscribed;
    private GameObject shopRoot;

    // 永久升级定义（id → 名称/价格/效果）。数值参考数值书 §6.2 刻印梯度。
    private static readonly (string id, string title, string desc, int cost)[] UpgradeDefs =
    {
        ("tough", "强健之魂", "初始最大生命 +20（每局）", 100),
        ("rich", "富有之魂", "开局金币 +30（每局）", 80),
        ("master", "老练之魂", "永久伤害 +10%（每局）", 150),
    };

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Data = SaveSystem.Load();
        Debug.Log($"[Meta] 存档载入：魂 {Data.souls} | 最深 {Data.bestFloor} 层 | 升级 {Data.ownedUpgrades.Length} 项");
    }

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerInput = p.GetComponent<PlayerInput>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (playerInput == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                playerInput = p.GetComponent<PlayerInput>();
                if (playerInput != null && !subscribed)
                {
                    playerInput.onActionTriggered += OnActionTriggered;
                    subscribed = true;
                }
            }
        }
    }

    private void OnActionTriggered(InputAction.CallbackContext ctx)
    {
        if (ctx.action?.name == shopKeyAction && ctx.performed) ToggleShop();
    }

    void OnDisable()
    {
        if (playerInput != null && subscribed)
        {
            playerInput.onActionTriggered -= OnActionTriggered;
            subscribed = false;
        }
    }

    /// <summary>死亡/通关结算：魂入账 + 最深层数 + 总局数，写档。</summary>
    public static int SettleRun(int reachedFloor, bool victory)
    {
        int earned = reachedFloor * 10 + (victory ? 100 : 0);
        Data.souls += earned;
        Data.bestFloor = Mathf.Max(Data.bestFloor, reachedFloor);
        Data.totalRuns++;
        SaveSystem.Save(Data);
        Debug.Log($"[Meta] 结算：到达 {reachedFloor} 层 → 魂 +{earned}（总计 {Data.souls}）");
        return earned;
    }

    public static bool Owns(string id) =>
        Data.ownedUpgrades != null && System.Array.IndexOf(Data.ownedUpgrades, id) >= 0;

    /// <summary>开局把已购升级注入本局玩家（RunManager.InitDelayed 调）。</summary>
    public static void ApplyOwnedUpgrades(Health health, PlayerStats stats)
    {
        if (health != null && Owns("tough")) health.AddMaxHealth(20f, true);
        if (stats != null && Owns("rich")) stats.AddCoins(30);
        if (stats != null && Owns("master")) stats.PermanentDamageMult = 1.1f;
    }

    // ---------- 魂商店面板（U 键） ----------

    private void ToggleShop()
    {
        if (shopRoot != null) { CloseShop(); return; }
        BuildShop();
    }

    private void BuildShop()
    {
        Time.timeScale = 0f;
        HitStop.SuppressByUI = true;

        var canvasGo = new GameObject("MetaShopCanvas", typeof(Canvas));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 205;
        shopRoot = canvasGo;

        Image mask = new GameObject("Mask", typeof(Image)).GetComponent<Image>();
        mask.transform.SetParent(canvasGo.transform, false);
        mask.color = new Color(0f, 0f, 0f, 0.6f);
        mask.rectTransform.anchorMin = Vector2.zero;
        mask.rectTransform.anchorMax = Vector2.one;
        mask.rectTransform.offsetMin = mask.rectTransform.offsetMax = Vector2.zero;

        Label(canvasGo.transform, $"魂之商店（当前：{Data.souls} 魂）", 26, new Color(0.7f, 0.55f, 1f),
            new Vector2(0.5f, 0.75f), new Vector2(500f, 40f));
        Label(canvasGo.transform, "购买为永久生效（每局开局注入）· 再按 U 关闭", 14, new Color(0.8f, 0.78f, 0.7f),
            new Vector2(0.5f, 0.68f), new Vector2(600f, 24f));

        for (int i = 0; i < UpgradeDefs.Length; i++)
        {
            var def = UpgradeDefs[i];
            bool owned = Owns(def.id);
            float x = (i - 1) * 280f;

            var btnGo = new GameObject($"Upg_{def.id}", typeof(Image), typeof(Button));
            btnGo.transform.SetParent(canvasGo.transform, false);
            var btn = btnGo.GetComponent<Button>();
            Image card = btnGo.GetComponent<Image>();
            card.color = owned ? new Color(0.15f, 0.3f, 0.15f, 0.95f) : new Color(0.18f, 0.16f, 0.12f, 0.95f);
            card.rectTransform.anchorMin = card.rectTransform.anchorMax = new Vector2(0.5f, 0.42f);
            card.rectTransform.anchoredPosition = new Vector2(x, 0f);
            card.rectTransform.sizeDelta = new Vector2(240f, 170f);

            Label(card.rectTransform, def.title, 20, Color.white, new Vector2(0.5f, 0.72f), new Vector2(200f, 28f));
            Label(card.rectTransform, def.desc, 14, new Color(0.8f, 0.78f, 0.7f), new Vector2(0.5f, 0.45f), new Vector2(210f, 44f));
            Label(card.rectTransform, owned ? "已拥有" : $"{def.cost} 魂",
                16, owned ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.85f, 0.35f),
                new Vector2(0.5f, 0.18f), new Vector2(180f, 24f));

            var colors = btn.colors;
            colors.highlightedColor = new Color(0.35f, 0.3f, 0.18f, 1f);
            btn.colors = colors;

            string id = def.id;
            btn.onClick.AddListener(() => TryBuy(id));
        }
    }

    private void TryBuy(string id)
    {
        var def = System.Array.Find(UpgradeDefs, d => d.id == id);
        if (Owns(id)) return;
        if (Data.souls < def.cost)
        {
            Debug.Log($"[Meta] 魂不足（{Data.souls}/{def.cost}）");
            return;
        }
        Data.souls -= def.cost;
        var list = new List<string>(Data.ownedUpgrades ?? System.Array.Empty<string>()) { id };
        Data.ownedUpgrades = list.ToArray();
        SaveSystem.Save(Data);
        Debug.Log($"[Meta] 已购永久升级：{def.title}（-{def.cost} 魂，余 {Data.souls}）");
        CloseShop();   // 重建面板刷新显示
        BuildShop();
    }

    private void CloseShop()
    {
        if (shopRoot != null) Destroy(shopRoot);
        shopRoot = null;
        HitStop.SuppressByUI = false;
        Time.timeScale = 1f;
    }

    private static Text Label(Transform parent, string text, int size, Color color, Vector2 anchor, Vector2 sizeDelta)
    {
        var go = new GameObject("Label", typeof(Text));
        go.transform.SetParent(parent, false);
        Text t = go.GetComponent<Text>();
        t.font = MinimapController.BuiltinFont;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.text = text;
        t.raycastTarget = false;
        t.rectTransform.anchorMin = t.rectTransform.anchorMax = anchor;
        t.rectTransform.anchoredPosition = Vector2.zero;
        t.rectTransform.sizeDelta = sizeDelta;
        return t;
    }
}
