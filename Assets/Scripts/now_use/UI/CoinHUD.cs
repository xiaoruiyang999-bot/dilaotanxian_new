using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 金币 HUD（M2·v0.7.0）：屏幕左下角（PlayerStatsPanel 上方）的金币图标 + 数字。
/// 纯代码 UI（风格同 MinimapController / AudioManager 挂载模式）：场景空对象挂本组件，
/// Awake 自建 Overlay Canvas，订阅 PlayerStats.OnCoinsChanged 实时刷新。
/// </summary>
public class CoinHUD : MonoBehaviour
{
    [Tooltip("留空自动按 Player tag 查找")]
    [SerializeField] private PlayerStats stats;
    [Tooltip("面板左下角锚点偏移（x, y）")]
    [SerializeField] private Vector2 anchoredPosition = new Vector2(18f, 96f);

    private Text coinText;

    void Awake()
    {
        if (stats == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) stats = p.GetComponent<PlayerStats>();
        }
        if (stats == null)
        {
            Debug.LogWarning("[CoinHUD] 未找到 PlayerStats，金币显示停用。");
            enabled = false;
            return;
        }
        BuildUI();
    }

    void OnEnable()
    {
        if (stats != null)
        {
            stats.OnCoinsChanged += OnCoinsChanged;
            OnCoinsChanged(stats.Coins);   // 订阅后立即刷一次，避免时序空窗
        }
    }

    void OnDisable()
    {
        if (stats != null) stats.OnCoinsChanged -= OnCoinsChanged;
    }

    private void OnCoinsChanged(int current) => coinText.text = current.ToString();

    private void BuildUI()
    {
        var canvasGo = new GameObject("CoinHUDCanvas", typeof(Canvas));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;   // 小地图(100)之下，游戏世界之上

        // 金币图标：程序生成的金色小圆（与 CoinDrop 掉落物同款视觉）
        Image icon = CreateImage("CoinIcon", canvasGo.transform, CoinDrop.CoinColor);
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = Vector2.zero;
        icon.rectTransform.pivot = Vector2.zero;
        icon.rectTransform.anchoredPosition = anchoredPosition;
        icon.rectTransform.sizeDelta = Vector2.one * 16f;
        icon.sprite = CoinDrop.CoinSprite;

        // 数字
        var textGo = new GameObject("CoinText", typeof(Text));
        textGo.transform.SetParent(canvasGo.transform, false);
        coinText = textGo.GetComponent<Text>();
        coinText.font = MinimapController.BuiltinFont;   // 复用小地图的内置字体缓存（同风格）
        coinText.fontSize = 16;
        coinText.fontStyle = FontStyle.Bold;
        coinText.color = new Color(1f, 0.85f, 0.35f);
        coinText.alignment = TextAnchor.MiddleLeft;
        coinText.raycastTarget = false;
        coinText.rectTransform.anchorMin = coinText.rectTransform.anchorMax = Vector2.zero;
        coinText.rectTransform.pivot = Vector2.zero;
        coinText.rectTransform.anchoredPosition = anchoredPosition + new Vector2(22f, 8f);
        coinText.rectTransform.sizeDelta = new Vector2(80f, 18f);
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
}
