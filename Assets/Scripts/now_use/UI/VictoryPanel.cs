using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通关结算面板（M3·v0.8.1）：第 9 层 Boss 清空后弹出——"通关！"+ 本局统计（层数/金币/用时）
/// + "进入无尽模式"按钮（点击继续下一层，难度照常按层增长）。纯代码 UI（复用升级面板风格）。
/// 死亡重开 = 新的一局（回第 1 层普通模式），RunManager 调用 Show。
/// </summary>
public class VictoryPanel : MonoBehaviour
{
    public static VictoryPanel Instance { get; private set; }

    private GameObject panelRoot;
    private System.Action onContinue;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void Show(int floor, int coins, float runSeconds, System.Action continueAction)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[Victory] 面板未挂载，直接继续。");
            continueAction?.Invoke();
            return;
        }
        Instance.Build(floor, coins, runSeconds, continueAction);
    }

    private void Build(int floor, int coins, float runSeconds, System.Action continueAction)
    {
        if (panelRoot != null) return;
        onContinue = continueAction;
        Time.timeScale = 0f;
        HitStop.SuppressByUI = true;

        var canvasGo = new GameObject("VictoryCanvas", typeof(Canvas));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 210;
        panelRoot = canvasGo;

        Image mask = new GameObject("Mask", typeof(Image)).GetComponent<Image>();
        mask.transform.SetParent(canvasGo.transform, false);
        mask.color = new Color(0f, 0f, 0f, 0.7f);
        mask.rectTransform.anchorMin = Vector2.zero;
        mask.rectTransform.anchorMax = Vector2.one;
        mask.rectTransform.offsetMin = mask.rectTransform.offsetMax = Vector2.zero;

        int minutes = (int)(runSeconds / 60f);
        int seconds = (int)(runSeconds % 60f);
        Label(canvasGo.transform, "通 关 ！", 44, new Color(1f, 0.85f, 0.35f), new Vector2(0.5f, 0.66f), new Vector2(600f, 60f));
        Label(canvasGo.transform, $"第 {floor} 层守关成功 · 金币 {coins} · 用时 {minutes}分{seconds:00}秒",
            20, Color.white, new Vector2(0.5f, 0.52f), new Vector2(800f, 32f));
        Label(canvasGo.transform, "无尽模式已解锁——敌人将随层数继续增强",
            16, new Color(0.8f, 0.78f, 0.7f), new Vector2(0.5f, 0.44f), new Vector2(700f, 26f));

        var btnGo = new GameObject("ContinueBtn", typeof(Image), typeof(Button));
        btnGo.transform.SetParent(canvasGo.transform, false);
        var btn = btnGo.GetComponent<Button>();
        Image card = btnGo.GetComponent<Image>();
        card.color = new Color(0.35f, 0.3f, 0.18f, 1f);
        card.rectTransform.anchorMin = card.rectTransform.anchorMax = new Vector2(0.5f, 0.3f);
        card.rectTransform.sizeDelta = new Vector2(240f, 54f);
        Label(card.rectTransform, "进入无尽模式", 20, Color.white, new Vector2(0.5f, 0.5f), new Vector2(220f, 32f));
        btn.onClick.AddListener(OnContinue);
    }

    private void OnContinue()
    {
        Destroy(panelRoot);
        panelRoot = null;
        HitStop.SuppressByUI = false;
        Time.timeScale = 1f;
        onContinue?.Invoke();
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
