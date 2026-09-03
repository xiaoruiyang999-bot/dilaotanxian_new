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
    private GameObject settingsRoot;
    private GameObject settingsMainRoot, audioRoot, controlsRoot;   // v1.1.16 两级导航：设置主页/音量页/操作页
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
        PanelSprite.ConfigureCanvasScaler(canvasGo);
        // v1.1.12：缺 GraphicRaycaster = 整个画布收不到指针事件（按钮全灭、Esc 走 PlayerInput 不受影响）
        canvasGo.AddComponent<GraphicRaycaster>();
        panelRoot = canvasGo;

        Image mask = new GameObject("Mask", typeof(Image)).GetComponent<Image>();
        mask.transform.SetParent(canvasGo.transform, false);
        mask.color = new Color(0f, 0f, 0f, 0.55f);
        mask.rectTransform.anchorMin = Vector2.zero;
        mask.rectTransform.anchorMax = Vector2.one;
        mask.rectTransform.offsetMin = mask.rectTransform.offsetMax = Vector2.zero;

        var panel = new GameObject("StonePanel", typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        Image panelImage = panel.GetComponent<Image>();
        PanelSprite.ApplyStonePanel(panelImage, new Color(0.06f, 0.06f, 0.07f, 0.96f));
        panelImage.rectTransform.anchorMin = panelImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        panelImage.rectTransform.sizeDelta = new Vector2(1100f, 650f);

        Label(panel.transform, "已暂停", 34, Color.white, new Vector2(0.5f, 1f), new Vector2(440f, 46f), new Vector2(0f, -72f));

        // 三张完整功能图：继续 / 设置 / 返回大厅。按钮列初始居中，设置展开时平移到左侧。
        GameObject buttons = new GameObject("MenuButtons", typeof(RectTransform));
        buttons.transform.SetParent(panel.transform, false);
        menuButtonsRoot = (RectTransform)buttons.transform;
        menuButtonsRoot.anchorMin = menuButtonsRoot.anchorMax = menuButtonsRoot.pivot = new Vector2(0.5f, 0.5f);
        menuButtonsRoot.sizeDelta = new Vector2(540f, 430f);
        menuButtonsRoot.anchoredPosition = new Vector2(0f, -25f);

        CreateMenuButton(buttons.transform, "button_continue_game", "继续游戏", new Vector2(0f, 140f), ContinueGame);
        CreateMenuButton(buttons.transform, "button_settings", "设置", Vector2.zero, ToggleSettings);
        CreateMenuButton(buttons.transform, "button_back_lobby", "返回大厅", new Vector2(0f, -140f), RestartRun);

        BuildSettingsPanel(panel.transform);
    }

    /// <summary>
    /// 继续游戏（v1.1.11 重写）：关闭暂停面板并完整恢复游戏时间轴。
    /// 独立方法而非直接绑 Close——便于运行时日志验证接线与状态复位。
    /// </summary>
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
        // 子面板/按钮列随 panelRoot 一并销毁：引用同步置空，防 ToggleSettings 等操作悬挂对象
        settingsRoot = null;
        settingsMainRoot = null;
        audioRoot = null;
        controlsRoot = null;
        menuButtonsRoot = null;

        HitStop.SuppressByUI = false;
        // 时间轴强制复位：无论时停协程处于何种状态，继续游戏必须恢复 1
        //（防暂停期间积累的边界状态把 timeScale 卡在 0——"面板已关但游戏仍冻结"即此症）
        Time.timeScale = 1f;
    }

    private void ToggleSettings()
    {
        if (settingsRoot == null || menuButtonsRoot == null) return;
        bool show = !settingsRoot.activeSelf;
        settingsRoot.SetActive(show);
        // v1.1.18：进设置 = 左侧三个暂停主键整体隐藏（原为平移让位），设置面板居中独占。
        // 退出设置由设置主页"返 回"键触发（ToggleSettings 自身）——"设置"按钮已随左列隐藏
        menuButtonsRoot.gameObject.SetActive(!show);
        if (show) ShowSettingsPage(SettingsPage.Main);   // 每次进入回到设置主页
    }

    // ---------- 设置两级导航（v1.1.16：主页两按钮 → 音量/操作子页） ----------

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
        rect.anchoredPosition = new Vector2(0f, -25f);   // v1.1.18：左列隐藏后设置面板居中独占
        rect.sizeDelta = new Vector2(560f, 430f);   // v1.1.19：布容器放大承载 520 宽按键
        // v1.1.19：移除深色底板（v1.1.14 加的半透明矩形——按钮原图 93% 不透明满幅石条，无需垫底；
        // 去掉后内容直接浮在石板主面板上）。保留零透明 Image 作点击阻挡层。
        settingsRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        // 设置主页（第一级）：音量设置 / 操作设置 两个大功能键
        // v1.1.19：尺寸对齐暂停主键 520×110（原图 4.63:1 等比），无底板后直接浮于石板面板
        settingsMainRoot = new GameObject("SettingsMain", typeof(RectTransform));
        settingsMainRoot.transform.SetParent(rect, false);
        var mr = (RectTransform)settingsMainRoot.transform;
        mr.anchorMin = mr.anchorMax = mr.pivot = new Vector2(0.5f, 0.5f);
        mr.sizeDelta = new Vector2(560f, 430f);
        CreateMenuButton(mr, "button_audio_settings", "音量设置", new Vector2(0f, 120f),
            () => ShowSettingsPage(SettingsPage.Audio), new Vector2(520f, 110f));
        CreateMenuButton(mr, "button_controls_settings", "操作设置", new Vector2(0f, -10f),
            () => ShowSettingsPage(SettingsPage.Controls), new Vector2(520f, 110f));
        BuildBackButton(mr, -170f, ToggleSettings);   // 返回 = 收起设置、恢复左侧三键

        // 子页（第二级）：音量滑钮 / 键位速查，各带返回键
        audioRoot = BuildAudioTab(rect);
        controlsRoot = BuildControlsTab(rect);
        ShowSettingsPage(SettingsPage.Main);

        settingsRoot.SetActive(false);
    }

    /// <summary>返回键（三态石板小操作条 + TMP 文本，无专属美术）。onClick 缺省回设置主页，可指定其他目标（如回暂停菜单）。</summary>
    private void BuildBackButton(Transform parent, float anchorY, System.Action onClick = null)
    {
        var go = new GameObject("Btn_Back", typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        var r = img.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(0f, anchorY);
        r.sizeDelta = new Vector2(150f, 36f);
        Label(r, "返 回", 18, Color.white, new Vector2(0.5f, 0.5f), new Vector2(140f, 30f));
        var btn = go.GetComponent<Button>();
        PanelSprite.ApplyStoneButton(btn, img, new Color(0.2f, 0.18f, 0.14f, 0.95f));
        btn.onClick.AddListener(() => (onClick ?? ShowToMain)());
    }

    /// <summary>BuildBackButton 缺省目标：回设置主页。</summary>
    private void ShowToMain() => ShowSettingsPage(SettingsPage.Main);

    /// <summary>音频标签页：标题 + 双音量滑条（PlayerPrefs 持久化）+ 收起提示。</summary>
    private GameObject BuildAudioTab(Transform parent)
    {
        var root = new GameObject("AudioTab", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)root.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        Label(rt, "音频设置", 24, Color.white, new Vector2(0.5f, 0.80f), new Vector2(300f, 34f));
        CreateSlider(rt, "SFX 音量", "sfx_volume", 0.5f,
            AudioManager.SetSfxVolume, new Vector2(0.5f, 0.55f));
        CreateSlider(rt, "BGM 音量", "bgm_volume", 0.5f,
            AudioManager.SetBgmVolume, new Vector2(0.5f, 0.38f));
        BuildBackButton(rt, -160f);   // v1.1.16 返回设置主页（v1.1.18 移除"再点设置收起"提示——该键已随左列隐藏）
        return root;
    }

    /// <summary>控制标签页（v1.1.14）：键位速查（只读展示，v0.7.5 键位表）。</summary>
    private GameObject BuildControlsTab(Transform parent)
    {
        var root = new GameObject("ControlsTab", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)root.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        Label(rt, "控制设置", 24, Color.white, new Vector2(0.5f, 0.80f), new Vector2(300f, 34f));

        string[] rows =
        {
            "移动   WASD", "攻击   鼠标左键", "交互 / 拾取   E", "小技能   F",
            "大招   Q", "武器技能   R", "使用道具   C", "兽化变身（狼人）   T", "暂停 / 关闭界面   Esc",
        };
        for (int i = 0; i < rows.Length; i++)
        {
            Label(rt, rows[i], 15, new Color(0.85f, 0.83f, 0.75f),
                new Vector2(0.5f, 0.64f - i * 0.056f), new Vector2(330f, 24f));
        }
        BuildBackButton(rt, -160f);   // v1.1.16 返回设置主页
        return root;
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

        var rt = sliderGo.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor + new Vector2(0.08f, 0f);
        rt.sizeDelta = new Vector2(260f, 44f);   // v1.1.15：容纳 40px 滑钮（原 20 高纯色条退役）

        if (SliderSprites.TrackEmpty != null)
            BuildArtSlider(slider);
        else
            BuildPlainSlider(slider);   // 素材断链兜底（v1.0.x 旧观感）

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

    /// <summary>
    /// 石槽滑条（v1.1.15）：轨道 1206×147 等比 260×32（比例 8.1≈原生 8.2 零失真）；
    /// 滑钮 182×182 与轨道原生配比 1.24 → 40×40；黄条 16px 居中于轨道内槽，随滑钮实时增减
    /// （fillRect 由 Slider 驱动）；滑钮 hover=光晕图/按下=压暗图由 SliderHandleState 换图，
    /// 故 Slider 自身 Transition 关闭防叠色。
    /// </summary>
    private static void BuildArtSlider(Slider slider)
    {
        var parent = slider.transform;

        // 轨道（空槽）：等比 260×32 垂直居中
        var trackGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        trackGo.transform.SetParent(parent, false);
        Image track = trackGo.GetComponent<Image>();
        track.sprite = SliderSprites.TrackEmpty;
        track.raycastTarget = true;   // 点轨道任意处可跳值（Slider 原生行为）
        var tr = track.rectTransform;
        tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 0.5f);
        tr.sizeDelta = new Vector2(260f, 32f);

        // 填充区：轨道内槽（高 16 居中，左右让出端帽）
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
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);   // 宽度由 Slider 随值驱动
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        slider.fillRect = fill.rectTransform;

        // 滑钮区 + 五态滑钮（hover 光晕/按下压暗：SliderHandleState 换图）
        var handleArea = new GameObject("HandleSlideArea", typeof(RectTransform));
        handleArea.transform.SetParent(parent, false);
        var ha = (RectTransform)handleArea.transform;
        ha.anchorMin = Vector2.zero;
        ha.anchorMax = Vector2.one;
        ha.offsetMin = new Vector2(20f, 2f);    // 左右各留半钮宽：钮心覆盖 [20,240]
        ha.offsetMax = new Vector2(-20f, -2f);

        var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGo.transform.SetParent(handleArea.transform, false);
        Image handle = handleGo.GetComponent<Image>();
        handle.sprite = SliderSprites.KnobDefault;
        handle.preserveAspect = true;
        var hr = handle.rectTransform;
        hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(0.5f, 0.5f);
        hr.sizeDelta = new Vector2(40f, 40f);
        // v1.1.16 纯代码光晕（省状态图内存）：悬停=程序生成径向光晕跟随滑钮，按下=色调 0.65
        handleGo.AddComponent<SliderHandleState>().SetupGlow(slider.transform, new Vector2(56f, 56f));

        slider.handleRect = hr;
        slider.targetGraphic = handle;
        slider.transition = Selectable.Transition.None;   // 视觉全权交给 SliderHandleState 换图
    }

    /// <summary>纯色滑条兜底（v1.0.x 旧观感，素材缺失时使用）。</summary>
    private static void BuildPlainSlider(Slider slider)
    {
        var parent = slider.transform;

        var track = new GameObject("Background", typeof(Image)).GetComponent<Image>();
        track.transform.SetParent(parent, false);
        track.color = new Color(0.25f, 0.23f, 0.18f);
        track.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        track.rectTransform.sizeDelta = new Vector2(0f, 6f);

        var fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(parent, false);
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

    private void CreateMenuButton(Transform parent, string spriteName, string fallbackText,
        Vector2 position, System.Action onClick, Vector2? size = null)
    {
        var btnGo = new GameObject($"Btn_{spriteName}", typeof(Image), typeof(Button));
        btnGo.transform.SetParent(parent, false);
        var btn = btnGo.GetComponent<Button>();
        Image img = btnGo.GetComponent<Image>();
        img.sprite = Resources.Load<Sprite>($"UI/PauseMenu/{spriteName}");
        img.preserveAspect = true;
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        img.rectTransform.anchoredPosition = position;
        img.rectTransform.sizeDelta = size ?? new Vector2(520f, 110f);   // 原图约 4.63:1，等比缩小（preserveAspect 保比例）

        if (img.sprite == null)
        {
            PanelSprite.ApplyStoneButton(btn, img, new Color(0.2f, 0.18f, 0.14f, 0.95f));
            Label(img.rectTransform, fallbackText, 20, Color.white, new Vector2(0.5f, 0.5f), new Vector2(460f, 42f));
        }
        else
        {
            // 单图亮度状态：Normal 稍暗 / Hover 原亮度 / Pressed 压暗。
            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
            colors.selectedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.75f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;
        }
        btn.onClick.AddListener(() => onClick?.Invoke());
    }

    private static void Label(Transform parent, string text, int size, Color color, Vector2 anchor, Vector2 sizeDelta, Vector2? position = null)
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
        t.rectTransform.anchoredPosition = position ?? Vector2.zero;
        t.rectTransform.sizeDelta = sizeDelta;
    }
}
