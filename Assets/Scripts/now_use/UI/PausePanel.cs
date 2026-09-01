using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 暂停菜单（M5·v1.0.0 → v1.0.4 重建）：Esc 打开——继续 / SFX与BGM 音量滑条（PlayerPrefs 持久化）/ 重开本局。
/// v1.0.4 变更：输入资产已无 "Pause" 动作（v0.7.5），改听 "Cancel"（Esc）——
/// 职业选择 UI 打开时让位（ClassSelectUI 消费 Esc 关自身），玩家死亡流程中不暂停
/// （重开协程的 WaitForSeconds 受 timeScale 影响会被卡死）。
/// 时停与 HitStop.SuppressByUI 协调；重开 = 回准备场景（与 RunManager 死亡重开同链路：清武器 + 关静态 UI）。
/// 挂载模式同 MinimapSystem：场景空对象 PauseSystem 挂本组件，UI 运行时代码构建。
/// </summary>
public class PausePanel : MonoBehaviour
{
    public static PausePanel Instance { get; private set; }

    [Tooltip("重开本局加载的准备场景名（与 RunManager.prepSceneName 一致，需在 Build Settings 中）")]
    [SerializeField] private string prepSceneName = "v0_7_PrepRoom";

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
        Unsubscribe();
    }

    void Update()
    {
        if (subscribed) return;

        if (playerInput == null)
        {
            // 玩家可能晚于本组件激活（运行时 AddComponent / 场景顺序），逐帧惰性查找
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            playerInput = p != null ? p.GetComponent<PlayerInput>() : null;
            if (playerInput == null) return;
        }

        playerInput.onActionTriggered += OnAction;
        subscribed = true;
    }

    void OnDisable()
    {
        // 订阅/退订配对：禁用时退订，重新启用后由 Update 补订（旧版此处退订后不再补订，属隐患已修）
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (playerInput != null && subscribed)
        {
            playerInput.onActionTriggered -= OnAction;
            subscribed = false;
        }
    }

    private void OnAction(InputAction.CallbackContext ctx)
    {
        if (ctx.action?.name == "Cancel" && ctx.performed) Toggle();
    }

    private void Toggle()
    {
        if (panelRoot != null) { Close(); return; }

        // 让位规则：选择类 UI 打开时 Esc 归它；死亡流程中不暂停
        if (ClassSelectUI.IsOpen || CharacterSelectUI.IsOpen) return;
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null && p.TryGetComponent(out Health h) && h.IsDead) return;

        Open();
    }

    private void Open()
    {
        // 命中停帧（timeScale 短暂为 0）期间不抢开，避免 Close 时把停帧时间轴强行拉回
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

        // 音量滑条 ×2（AudioManager.M5 契约：SetSfxVolume/SetBgmVolume + PlayerPrefs 键名）
        CreateSlider(canvasGo.transform, "SFX 音量", "sfx_volume", 0.5f,
            AudioManager.SetSfxVolume, new Vector2(0.5f, 0.58f));
        CreateSlider(canvasGo.transform, "BGM 音量", "bgm_volume", 0.5f,
            AudioManager.SetBgmVolume, new Vector2(0.5f, 0.48f));

        // 继续 / 重开
        CreateButton(canvasGo.transform, "继续游戏", new Vector2(0.5f, 0.34f), Close);
        CreateButton(canvasGo.transform, "重开本局（回准备房间）", new Vector2(0.5f, 0.24f), RestartRun);
    }

    private void Close()
    {
        if (panelRoot != null) Destroy(panelRoot);
        panelRoot = null;
        HitStop.SuppressByUI = false;
        Time.timeScale = 1f;
    }

    /// <summary>重开本局：与 RunManager 死亡重开同链路——清武器（不保留）+ 关静态 UI + 回准备场景。</summary>
    private void RestartRun()
    {
        Close();
        RunStateCarrier.Ensure().ClearWeapon();
        ClassSelectUI.Close();
        Debug.Log("[Pause] 手动重开本局：回准备场景");
        SceneManager.LoadScene(prepSceneName);
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
        img.rectTransform.sizeDelta = new Vector2(300f, 42f);
        Label(img.rectTransform, text, 17, Color.white, new Vector2(0.5f, 0.5f), new Vector2(280f, 30f));
        btn.onClick.AddListener(() => onClick?.Invoke());
    }

    private static void Label(Transform parent, string text, int size, Color color, Vector2 anchor, Vector2 sizeDelta)
    {
        // v1.0.8：照 ClassSelectUI.CreateText 已验证模式——无参 GO + 单次 AddComponent + 先 text 后 font
        //（组件进 GameObject 构造参数会产生双 TMP 组件并触发 TMP 内部 NRE）
        GameObject go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.font = TMPFontProvider.Font;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        t.rectTransform.anchorMin = t.rectTransform.anchorMax = anchor;
        t.rectTransform.anchoredPosition = Vector2.zero;
        t.rectTransform.sizeDelta = sizeDelta;
    }
}
