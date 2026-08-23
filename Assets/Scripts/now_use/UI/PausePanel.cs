using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 暂停菜单（M5·v1.0.0）：Esc 打开——继续 / 重开本局 / SFX与BGM 音量滑条（PlayerPrefs 持久化）。
/// 时停与升级面板同机制（HitStop.SuppressByUI 协调）。挂载同 MinimapSystem 模式（PauseSystem）。
/// </summary>
public class PausePanel : MonoBehaviour
{
    public static PausePanel Instance { get; private set; }

    private GameObject panelRoot;
    private PlayerInput playerInput;
    private bool subscribed;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (playerInput == null && !subscribed)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                playerInput = p.GetComponent<PlayerInput>();
                if (playerInput != null)
                {
                    playerInput.onActionTriggered += OnAction;
                    subscribed = true;
                }
            }
        }
    }

    private void OnAction(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
    {
        if (ctx.action?.name == "Pause" && ctx.performed) Toggle();
    }

    void OnDisable()
    {
        if (playerInput != null && subscribed)
        {
            playerInput.onActionTriggered -= OnAction;
            subscribed = false;
        }
    }

    private void Toggle()
    {
        if (panelRoot != null) Close();
        else Open();
    }

    private void Open()
    {
        // 升级/商店面板开着时不抢暂停（同属时停 UI）
        if (Time.timeScale == 0f) return;

        Time.timeScale = 0f;
        HitStop.SuppressByUI = true;

        var canvasGo = new GameObject("PauseCanvas", typeof(Canvas));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 220;
        panelRoot = canvasGo;

        Image mask = new GameObject("Mask", typeof(Image)).GetComponent<Image>();
        mask.transform.SetParent(canvasGo.transform, false);
        mask.color = new Color(0f, 0f, 0f, 0.55f);
        mask.rectTransform.anchorMin = Vector2.zero;
        mask.rectTransform.anchorMax = Vector2.one;
        mask.rectTransform.offsetMin = mask.rectTransform.offsetMax = Vector2.zero;

        Label(canvasGo.transform, "已暂停", 34, Color.white, new Vector2(0.5f, 0.78f), new Vector2(400f, 46f));

        // 音量滑条 ×2
        CreateSlider(canvasGo.transform, "SFX 音量", "sfx_volume", 0.5f,
            AudioManager.SetSfxVolume, new Vector2(0.5f, 0.58f));
        CreateSlider(canvasGo.transform, "BGM 音量", "bgm_volume", 0.5f,
            AudioManager.SetBgmVolume, new Vector2(0.5f, 0.48f));

        // 继续 / 重开
        CreateButton(canvasGo.transform, "继续游戏", new Vector2(0.5f, 0.34f), Close);
        CreateButton(canvasGo.transform, "重开本局（魂照常结算）", new Vector2(0.5f, 0.24f), RestartRun);
    }

    private void Close()
    {
        if (panelRoot != null) Destroy(panelRoot);
        panelRoot = null;
        HitStop.SuppressByUI = false;
        Time.timeScale = 1f;
    }

    private void RestartRun()
    {
        Close();
        var run = FindAnyObjectByType<RunManager>();
        if (run != null) run.DebugRestartFromPause();
        Debug.Log("[Pause] 手动重开本局");
    }

    private void CreateSlider(Transform parent, string title, string prefsKey, float defaultVal,
        System.Action<float> onChanged, Vector2 anchor)
    {
        Label(parent, title, 16, new Color(0.85f, 0.83f, 0.75f), anchor + new Vector2(-0.19f, 0f), new Vector2(120f, 22f));
        var sliderGo = new GameObject($"Slider_{prefsKey}", typeof(Slider));
        sliderGo.transform.SetParent(parent, false);
        var slider = sliderGo.GetComponent<Slider>();

        // 滑条视觉：轨道 + 填充 + 手柄（纯代码拼装）
        var rt = sliderGo.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor + new Vector2(0.08f, 0f);
        rt.sizeDelta = new Vector2(260f, 20f);

        var track = new GameObject("Background", typeof(Image)).GetComponent<Image>();
        track.transform.SetParent(sliderGo.transform, false);
        track.color = new Color(0.25f, 0.23f, 0.18f);
        track.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        track.rectTransform.sizeDelta = new Vector2(0f, 6f);

        var fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGo.transform, false);
        fillArea.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0.5f);
        fillArea.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.5f);
        fillArea.GetComponent<RectTransform>().sizeDelta = new Vector2(-16f, 6f);
        var fill = new GameObject("Fill", typeof(Image)).GetComponent<Image>();
        fill.transform.SetParent(fillArea.transform, false);
        fill.color = new Color(1f, 0.82f, 0.25f);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);   // fillRect 由 Slider 驱动
        slider.fillRect = fill.rectTransform;

        var handleArea = new GameObject("HandleArea", typeof(RectTransform));
        handleArea.transform.SetParent(sliderGo.transform, false);
        handleArea.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        handleArea.GetComponent<RectTransform>().anchorMax = Vector2.one;
        handleArea.GetComponent<RectTransform>().sizeDelta = new Vector2(-12f, 0f);
        var handle = new GameObject("Handle", typeof(Image)).GetComponent<Image>();
        handle.transform.SetParent(handleArea.transform, false);
        handle.color = Color.white;
        handle.rectTransform.sizeDelta = new Vector2(14f, 18f);
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = PlayerPrefs.GetFloat(prefsKey, defaultVal);
        slider.onValueChanged.AddListener(v =>
        {
            PlayerPrefs.SetFloat(prefsKey, v);
            onChanged?.Invoke(v);
        });
        onChanged?.Invoke(slider.value);   // 打开面板即应用一次（保证当前生效）
    }

    private void CreateButton(Transform parent, string text, Vector2 anchor, System.Action onClick)
    {
        var btnGo = new GameObject($"Btn_{text}", typeof(Image), typeof(Button));
        btnGo.transform.SetParent(parent, false);
        var btn = btnGo.GetComponent<Button>();
        Image img = btnGo.GetComponent<Image>();
        img.color = new Color(0.2f, 0.18f, 0.14f, 0.95f);
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = anchor;
        img.rectTransform.sizeDelta = new Vector2(260f, 42f);
        Label(img.rectTransform, text, 17, Color.white, new Vector2(0.5f, 0.5f), new Vector2(240f, 30f));
        btn.onClick.AddListener(() => onClick?.Invoke());
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
