using UnityEngine;
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
/// v1.2.0：Label/CreateMenuButton 迁移至 UIHelper 消除重复代码。
/// </summary>
public class PausePanel : MonoBehaviour
{
    public static PausePanel Instance { get; private set; }

    [Tooltip("重开本局加载的准备场景名（与 RunManager.prepSceneName 一致，需在 Build Settings 中）")]
    [SerializeField] private string prepSceneName = "v0_7_PrepRoom";

    private GameObject panelRoot;
    private GameObject settingsRoot;
    private GameObject settingsMainRoot, audioRoot, controlsRoot;
    private RectTransform menuButtonsRoot;
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
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            playerInput = p != null ? p.GetComponent<PlayerInput>() : null;
            if (playerInput == null) return;
        }

        playerInput.onActionTriggered += OnAction;
        subscribed = true;
    }

    void OnDisable() => Unsubscribe();

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
        if (SkillTreeUI.IsOpen) return;
        if (panelRoot != null) { Close(); return; }

        if (ClassSelectUI.IsOpen || CharacterSelectUI.IsOpen) return;
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null && p.TryGetComponent(out Health h) && h.IsDead) return;

        Open();
    }

    private void Open()
    {
        if (Time.timeScale == 0f) return;

        Time.timeScale = 0f;
        HitStop.SuppressByUI = true;

        var canvasGo = new GameObject("PauseCanvas", typeof(Canvas));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 220;
        PanelSprite.ConfigureCanvasScaler(canvasGo);
        canvasGo.AddComponent<GraphicRaycaster>();
        panelRoot = canvasGo;

        UIHelper.CreateFullscreenMask(canvasGo.transform, new Color(0f, 0f, 0f, 0.55f));

        Image panel = UIHelper.CreateStonePanel(canvasGo.transform, new Vector2(1100f, 650f), new Color(0.06f, 0.06f, 0.07f, 0.96f));

        UIHelper.CreateLabel(panel.transform, "已暂停", 34, Color.white, new Vector2(0.5f, 1f), new Vector2(440f, 46f), new Vector2(0f, -72f));

        // v1.1.48：楼层唯一真源是 RunManager.FloorNumber。每次打开暂停面板重新读取，
        // 因此跨层后无需维护第二份 UI 状态；准备大厅没有 RunManager 时不显示计数。
        RunManager runManager = FindAnyObjectByType<RunManager>();
        if (runManager != null)
        {
            var floorLabel = UIHelper.CreateLabel(panel.transform,
                $"当前层数：第 {runManager.FloorNumber} 层", 26,
                new Color(0.92f, 0.86f, 0.70f), new Vector2(0f, 1f),
                new Vector2(300f, 46f), new Vector2(190f, -72f), "FloorCounter");
            floorLabel.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
        }

        GameObject buttons = new GameObject("MenuButtons", typeof(RectTransform));
        buttons.transform.SetParent(panel.transform, false);
        menuButtonsRoot = (RectTransform)buttons.transform;
        menuButtonsRoot.anchorMin = menuButtonsRoot.anchorMax = menuButtonsRoot.pivot = new Vector2(0.5f, 0.5f);
        menuButtonsRoot.sizeDelta = new Vector2(540f, 430f);
        menuButtonsRoot.anchoredPosition = new Vector2(0f, -25f);

        // v1.1.47.2：恢复原三键格式（用户要求）——技能树入口只在准备室技能石碑（局外系统，E 交互）
        UIHelper.CreateMenuButton(buttons.transform, "button_continue_game", "继续游戏", new Vector2(0f, 140f), ContinueGame);
        UIHelper.CreateMenuButton(buttons.transform, "button_settings", "设置", Vector2.zero, ToggleSettings);
        UIHelper.CreateMenuButton(buttons.transform, "button_back_lobby", "返回大厅", new Vector2(0f, -140f), RestartRun);

        BuildSettingsPanel(panel.transform);
    }

    private void ContinueGame()
    {
        bool hadPanel = panelRoot != null;
        Close();
        Debug.Log($"[Pause] 继续游戏：面板{(hadPanel ? "已销毁" : "本就不存在")}，timeScale={Time.timeScale}，SuppressByUI={HitStop.SuppressByUI}");
    }

    private void Close()
    {
        if (panelRoot != null)
        {
            Destroy(panelRoot);
            panelRoot = null;
        }
        settingsRoot = null;
        settingsMainRoot = null;
        audioRoot = null;
        controlsRoot = null;
        menuButtonsRoot = null;

        HitStop.SuppressByUI = false;
        Time.timeScale = 1f;
    }

    private void ToggleSettings()
    {
        if (settingsRoot == null || menuButtonsRoot == null) return;
        bool show = !settingsRoot.activeSelf;
        settingsRoot.SetActive(show);
        menuButtonsRoot.gameObject.SetActive(!show);
        if (show) ShowSettingsPage(SettingsPage.Main);
    }

    // ---------- 设置两级导航（v1.1.16） ----------

    private enum SettingsPage { Main, Audio, Controls }

    private void ShowSettingsPage(SettingsPage page)
    {
        if (settingsMainRoot != null) settingsMainRoot.SetActive(page == SettingsPage.Main);
        if (audioRoot != null) audioRoot.SetActive(page == SettingsPage.Audio);
        if (controlsRoot != null) controlsRoot.SetActive(page == SettingsPage.Controls);
    }

    private void BuildSettingsPanel(Transform parent)
    {
        settingsRoot = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image));
        settingsRoot.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)settingsRoot.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -25f);
        rect.sizeDelta = new Vector2(560f, 430f);
        settingsRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        settingsMainRoot = new GameObject("SettingsMain", typeof(RectTransform));
        settingsMainRoot.transform.SetParent(rect, false);
        var mr = (RectTransform)settingsMainRoot.transform;
        mr.anchorMin = mr.anchorMax = mr.pivot = new Vector2(0.5f, 0.5f);
        mr.sizeDelta = new Vector2(560f, 430f);
        UIHelper.CreateMenuButton(mr, "button_audio_settings", "音量设置", new Vector2(0f, 120f),
            () => ShowSettingsPage(SettingsPage.Audio), new Vector2(520f, 110f));
        UIHelper.CreateMenuButton(mr, "button_controls_settings", "操作设置", new Vector2(0f, -10f),
            () => ShowSettingsPage(SettingsPage.Controls), new Vector2(520f, 110f));
        BuildBackButton(mr, -170f, ToggleSettings);

        audioRoot = BuildAudioTab(rect);
        controlsRoot = BuildControlsTab(rect);
        ShowSettingsPage(SettingsPage.Main);

        settingsRoot.SetActive(false);
    }

    private void BuildBackButton(Transform parent, float anchorY, System.Action onClick = null)
    {
        var go = new GameObject("Btn_Back", typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        var r = img.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(0f, anchorY);
        r.sizeDelta = new Vector2(150f, 36f);
        UIHelper.CreateLabel(r, "返 回", 18, Color.white, new Vector2(0.5f, 0.5f), new Vector2(140f, 30f));
        var btn = go.GetComponent<Button>();
        PanelSprite.ApplyStoneButton(btn, img, new Color(0.2f, 0.18f, 0.14f, 0.95f));
        btn.onClick.AddListener(() => (onClick ?? ShowToMain)());
    }

    private void ShowToMain() => ShowSettingsPage(SettingsPage.Main);

    private GameObject BuildAudioTab(Transform parent)
    {
        var root = new GameObject("AudioTab", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)root.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        UIHelper.CreateLabel(rt, "音频设置", 24, Color.white, new Vector2(0.5f, 0.80f), new Vector2(300f, 34f));
        CreateSlider(rt, "SFX 音量", "sfx_volume", 0.5f,
            AudioManager.SetSfxVolume, new Vector2(0.5f, 0.55f));
        CreateSlider(rt, "BGM 音量", "bgm_volume", 0.5f,
            AudioManager.SetBgmVolume, new Vector2(0.5f, 0.38f));
        BuildBackButton(rt, -160f);
        return root;
    }

    private GameObject BuildControlsTab(Transform parent)
    {
        var root = new GameObject("ControlsTab", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)root.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        UIHelper.CreateLabel(rt, "控制设置", 24, Color.white, new Vector2(0.5f, 0.80f), new Vector2(300f, 34f));

        string[] rows =
        {
            "移动   WASD", "攻击   鼠标左键", "交互 / 拾取   E", "小技能   F",
            "大招   Q", "武器技能   R", "使用道具   C", "兽化变身（狼人）   T", "暂停 / 关闭界面   Esc",
        };
        for (int i = 0; i < rows.Length; i++)
        {
            UIHelper.CreateLabel(rt, rows[i], 15, new Color(0.85f, 0.83f, 0.75f),
                new Vector2(0.5f, 0.64f - i * 0.056f), new Vector2(330f, 24f));
        }
        BuildBackButton(rt, -160f);
        return root;
    }

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
        UIHelper.CreateLabel(parent, title, 16, new Color(0.85f, 0.83f, 0.75f), anchor + new Vector2(-0.19f, 0f), new Vector2(120f, 22f));
        var sliderGo = new GameObject($"Slider_{prefsKey}", typeof(Slider));
        sliderGo.transform.SetParent(parent, false);
        var slider = sliderGo.GetComponent<Slider>();

        var rt = sliderGo.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor + new Vector2(0.08f, 0f);
        rt.sizeDelta = new Vector2(260f, 44f);

        if (SliderSprites.TrackEmpty != null)
            BuildArtSlider(slider);
        else
            BuildPlainSlider(slider);

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = PlayerPrefs.GetFloat(prefsKey, defaultVal);
        slider.onValueChanged.AddListener(v =>
        {
            PlayerPrefs.SetFloat(prefsKey, v);
            onChanged?.Invoke(v);
        });
        onChanged?.Invoke(slider.value);
    }

    private static void BuildArtSlider(Slider slider)
    {
        var parent = slider.transform;

        var trackGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        trackGo.transform.SetParent(parent, false);
        Image track = trackGo.GetComponent<Image>();
        track.sprite = SliderSprites.TrackEmpty;
        track.raycastTarget = true;
        var tr = track.rectTransform;
        tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 0.5f);
        tr.sizeDelta = new Vector2(260f, 32f);

        var fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(parent, false);
        var fa = (RectTransform)fillArea.transform;
        fa.anchorMin = new Vector2(0f, 0.5f);
        fa.anchorMax = new Vector2(1f, 0.5f);
        fa.offsetMin = new Vector2(10f, -8f);
        fa.offsetMax = new Vector2(-10f, 8f);

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(fillArea.transform, false);
        Image fill = fillGo.GetComponent<Image>();
        fill.sprite = SliderSprites.Fill;
        fill.raycastTarget = false;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        slider.fillRect = fill.rectTransform;

        var handleArea = new GameObject("HandleSlideArea", typeof(RectTransform));
        handleArea.transform.SetParent(parent, false);
        var ha = (RectTransform)handleArea.transform;
        ha.anchorMin = Vector2.zero;
        ha.anchorMax = Vector2.one;
        ha.offsetMin = new Vector2(20f, 2f);
        ha.offsetMax = new Vector2(-20f, -2f);

        var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGo.transform.SetParent(handleArea.transform, false);
        Image handle = handleGo.GetComponent<Image>();
        handle.sprite = SliderSprites.KnobDefault;
        handle.preserveAspect = true;
        var hr = handle.rectTransform;
        hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(0.5f, 0.5f);
        hr.sizeDelta = new Vector2(40f, 40f);
        handleGo.AddComponent<SliderHandleState>().SetupGlow(slider.transform, new Vector2(56f, 56f));

        slider.handleRect = hr;
        slider.targetGraphic = handle;
        slider.transition = Selectable.Transition.None;
    }

    private static void BuildPlainSlider(Slider slider)
    {
        var parent = slider.transform;

        var bg = new GameObject("Background", typeof(Image));
        bg.transform.SetParent(parent, false);
        bg.GetComponent<Image>().color = new Color(0.15f, 0.12f, 0.1f, 1f);
        var bgRect = (RectTransform)bg.transform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(parent, false);
        var fillRect = (RectTransform)fillArea.transform;
        fillRect.anchorMin = new Vector2(0f, 0.25f);
        fillRect.anchorMax = new Vector2(1f, 0.75f);
        fillRect.sizeDelta = Vector2.zero;

        var fill = new GameObject("Fill", typeof(Image)).GetComponent<Image>();   // 编译修复：取 Image（color/rectTransform 在组件上，与下方 handle 同款）
        fill.transform.SetParent(fillArea.transform, false);
        fill.color = new Color(1f, 0.82f, 0.25f);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        slider.fillRect = fill.rectTransform;

        var handleArea = new GameObject("HandleArea", typeof(RectTransform));
        handleArea.transform.SetParent(parent, false);
        handleArea.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        handleArea.GetComponent<RectTransform>().anchorMax = Vector2.one;
        handleArea.GetComponent<RectTransform>().sizeDelta = new Vector2(-12f, 0f);
        var handle = new GameObject("Handle", typeof(Image)).GetComponent<Image>();
        handle.transform.SetParent(handleArea.transform, false);
        handle.color = Color.white;
        handle.rectTransform.sizeDelta = new Vector2(14f, 18f);
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
    }
}
