using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 金币 HUD（v1.1.3 自 MCP 分支还原，M2·v0.7.0 原型）：屏幕左下角属性面板上方的金币图标 + 数字。
/// 与 MCP 版的差异（适配主分支）：
/// - 文本从 Legacy UGUI Text 迁移 TMP（先 text 后 font=TMPFontProvider.Font，ClassSelectUI.CreateText 模式）；
/// - RuntimeInitializeOnLoadMethod 自装隐藏常驻对象（零场景手术，R1/R4 无关）；
/// - 玩家实例跨场景更换（死亡重开/进地牢均为新实例）：0.5s 轮询重绑，事件订阅随实例配对退订。
/// </summary>
public class CoinHUD : MonoBehaviour
{
    private const float RebindInterval = 0.5f;
    private const float IconSize = 16f;

    private static readonly Vector2 AnchorPos = new Vector2(18f, 96f);   // 左下角，属性面板上方

    private TMP_Text coinText;
    private PlayerStats boundStats;
    private float rebindTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("CoinHUD(Runtime)");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        go.AddComponent<CoinHUD>();
    }

    void Awake()
    {
        var canvasGo = new GameObject("CoinHUDCanvas");
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;   // 小地图(100)之下，游戏世界之上
        canvasGo.AddComponent<GraphicRaycaster>().enabled = false;   // 纯显示，不参与点击

        // 金币图标：程序生成的金色小圆（与 CoinDrop 掉落物同款视觉）
        var iconGo = new GameObject("CoinIcon");
        iconGo.transform.SetParent(canvasGo.transform, false);
        Image icon = iconGo.AddComponent<Image>();
        icon.sprite = CoinDrop.CoinSprite;
        icon.color = CoinDrop.CoinColor;
        icon.raycastTarget = false;
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = Vector2.zero;
        icon.rectTransform.pivot = Vector2.zero;
        icon.rectTransform.anchoredPosition = AnchorPos;
        icon.rectTransform.sizeDelta = Vector2.one * IconSize;

        // 数字（TMP：无参 GameObject + 单次 AddComponent + 先 text 后 font，禁构造参数塞组件）
        var textGo = new GameObject("CoinText");
        textGo.transform.SetParent(canvasGo.transform, false);
        coinText = textGo.AddComponent<TextMeshProUGUI>();
        coinText.text = "0";
        coinText.font = TMPFontProvider.Font;
        coinText.fontSize = 16;
        coinText.fontStyle = FontStyles.Bold;
        coinText.color = new Color(1f, 0.85f, 0.35f);
        coinText.alignment = TextAlignmentOptions.MidlineLeft;
        coinText.raycastTarget = false;
        coinText.rectTransform.anchorMin = coinText.rectTransform.anchorMax = Vector2.zero;
        coinText.rectTransform.pivot = Vector2.zero;
        coinText.rectTransform.anchoredPosition = AnchorPos + new Vector2(IconSize + 6f, IconSize * 0.5f - 9f);
        coinText.rectTransform.sizeDelta = new Vector2(90f, 18f);
    }

    void Update()
    {
        rebindTimer -= Time.unscaledDeltaTime;
        if (rebindTimer > 0f) return;
        rebindTimer = RebindInterval;

        // 场景切换/死亡重开后玩家为新实例：按 tag 重找，实例变化时换绑
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        PlayerStats stats = p != null ? p.GetComponent<PlayerStats>() : null;
        if (stats == boundStats) return;

        if (boundStats != null) boundStats.OnCoinsChanged -= OnCoinsChanged;
        boundStats = stats;
        if (boundStats != null)
        {
            boundStats.OnCoinsChanged += OnCoinsChanged;
            OnCoinsChanged(boundStats.Coins);   // 换绑后立即刷一次，避免时序空窗
        }
        else
        {
            coinText.text = "0";
        }
    }

    void OnDestroy()
    {
        if (boundStats != null) boundStats.OnCoinsChanged -= OnCoinsChanged;
    }

    private void OnCoinsChanged(int current) => coinText.text = current.ToString();
}
