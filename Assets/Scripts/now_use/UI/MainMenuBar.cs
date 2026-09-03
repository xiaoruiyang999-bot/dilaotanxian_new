using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 主界面横幅（v1.1.8）：石板横条（Resources/UI/MainBar 十帧序列，1242×170）
/// + 右侧取消按钮（三态石板）。点击取消 → 播放完整 Disabled 序列（暗→亮闪→碎裂，帧 001→010）
/// → 完成后收起。Open 时静止显示亮态帧（004）。
/// 独立入口 MainMenuBar.Open()/Close()；编辑器内 F8 开关预览（运行时自装、零场景手术）。
/// 时间走 unscaledDeltaTime，暂停期间照播（UIFramePlayer 契约）。
/// </summary>
public class MainMenuBar : MonoBehaviour
{
    private const string FrameDir = "UI/MainBar";
    private const float FramesPerSecond = 12f;
    private const int IdleFrameIndex = 3;    // 亮态静止帧（004：接近峰值亮度）

    /// <summary>UI 是否打开。</summary>
    public static bool IsOpen { get; private set; }

    private static MainMenuBar instance;

    private GameObject canvasGo;
    private UIFramePlayer player;
    private Sprite[] frames;
    private Button cancelButton;

    // ========== 静态入口 ==========

    public static void Open()
    {
        if (instance == null)
        {
            var go = new GameObject("MainMenuBar");
            instance = go.AddComponent<MainMenuBar>();
            instance.Build();
        }
        instance.Show();
    }

    public static void Close()
    {
        if (instance != null) instance.Hide();
    }

    // ========== 显示 / 隐藏 ==========

    private void Show()
    {
        EnsureEventSystem();
        canvasGo.SetActive(true);
        IsOpen = true;
        if (player != null)
        {
            player.Stop();
            player.ShowFrame(IdleFrameIndex);   // 亮态静止
        }
        if (cancelButton != null) cancelButton.interactable = true;
    }

    private void Hide()
    {
        canvasGo.SetActive(false);
        IsOpen = false;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        IsOpen = false;
    }

#if UNITY_EDITOR
    void Update()
    {
        // 编辑器预览开关（F8 与既有键位/F9 调试键无冲突；正式入口走 Open()）
        if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
        {
            if (IsOpen) Hide();
            else Show();
        }
    }
#endif

    // ========== 取消：播放 Disabled 序列后收起 ==========

    private void OnCancelClicked()
    {
        if (player == null || frames == null || frames.Length == 0) return;
        if (player.IsPlaying) return;                 // 播放互斥（连点防抖）
        cancelButton.interactable = false;            // 播放期间禁用按钮

        // 完整序列：暗起 → 亮闪（001-005）→ 碎裂禁用（006-010）→ 收起
        player.Play(0, frames.Length - 1, Hide);
    }

    // ========== UI 构建 ==========

    private void Build()
    {
        frames = UIFramePlayer.LoadFrames(FrameDir);

        canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 190;   // 选择菜单(200/205)与暂停(220)之下
        PanelSprite.ConfigureCanvasScaler(canvasGo);
        canvasGo.AddComponent<GraphicRaycaster>();

        // 横幅条：底部居中，原生像素尺寸（PPU=1 → 1242×170）；帧缺失回退深色条占位
        var barGo = new GameObject("Bar");
        barGo.transform.SetParent(canvasGo.transform, false);
        Image barImg = barGo.AddComponent<Image>();
        RectTransform barRect = (RectTransform)barGo.transform;
        barRect.anchorMin = barRect.anchorMax = barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = new Vector2(0f, 60f);
        barRect.sizeDelta = new Vector2(1242f, 170f);
        if (frames != null && frames.Length > 0)
        {
            player = barGo.AddComponent<UIFramePlayer>();
            player.fps = FramesPerSecond;
            player.Setup(frames, barImg, IdleFrameIndex);
        }
        else
        {
            barImg.color = new Color(0.08f, 0.08f, 0.1f, 0.9f);
            Debug.LogWarning("[MainMenuBar] 帧资产缺失（Resources/UI/MainBar），横幅为纯色占位。");
        }

        // 取消按钮：横幅右端（三态石板 + TMP 文本）
        var btnGo = new GameObject("Btn_Cancel");
        btnGo.transform.SetParent(barGo.transform, false);
        Image btnImg = btnGo.AddComponent<Image>();
        RectTransform btnRect = (RectTransform)btnGo.transform;
        btnRect.anchorMin = btnRect.anchorMax = btnRect.pivot = new Vector2(1f, 0.5f);
        btnRect.anchoredPosition = new Vector2(-70f, 0f);
        btnRect.sizeDelta = new Vector2(220f, 64f);

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(btnGo.transform, false);
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = "取 消";
        label.font = TMPFontProvider.Font;   // 先 text 后 font（TMP 规范）
        label.fontSize = 26;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        RectTransform labelRect = (RectTransform)labelGo.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

        cancelButton = btnGo.AddComponent<Button>();
        PanelSprite.ApplyStoneButton(cancelButton, btnImg, new Color(0f, 0f, 0f, 0.6f));
        cancelButton.onClick.AddListener(OnCancelClicked);

        canvasGo.SetActive(false);
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }
}
