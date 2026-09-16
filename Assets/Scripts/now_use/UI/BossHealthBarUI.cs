using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// v2.0.10 Boss 血条 UI(屏幕顶部):
/// 进入 Boss 房(Room.OnRoomEntered + 房内有 BossPhaseController)时显示;
/// 持续订阅 EnemyHealth.OnHealthChanged 实时刷新;Boss 死亡或离开房间时隐藏。
/// 石板面板视觉(PanelSprite)+ 阶段变色(P1 白/P2 红/P3 赤红由 BossPhaseController.OnPhaseEntered)。
/// 静态类(项目 UI 惯例):自动由 Room.OnRoomEntered 触发创建,无需手动挂载。
/// </summary>
public static class BossHealthBarUI
{
    private static GameObject canvasGo;
    private static Image fillImage;
    private static TMP_Text nameText;
    private static TMP_Text phaseText;
    private static EnemyHealth trackedHealth;
    private static Room trackedRoom;
    private static System.Action<float, float> healthHandler;
    private static System.Action<Room> clearedHandler;
    private static System.Action<int> phaseHandler;

    private static readonly Color Phase1Color = new Color(0.85f, 0.72f, 0.35f);   // 金(守门)
    private static readonly Color Phase2Color = new Color(0.9f, 0.35f, 0.25f);    // 红(追猎)

    public static bool IsOpen => canvasGo != null;

    /// <summary>Room.OnRoomEntered 触发:查找房内 Boss → 显示血条并开始追踪。</summary>
    public static void TryShowForRoom(Room room)
    {
        if (room == null) return;

        // 找房内 Boss(挂 BossPhaseController 的敌人)
        BossPhaseController boss = null;
        foreach (var phase in Object.FindObjectsByType<BossPhaseController>(FindObjectsInactive.Exclude))
        {
            if (phase != null && room.ContentRoot != null
                && phase.transform.IsChildOf(room.ContentRoot))
            {
                boss = phase;
                break;
            }
        }
        if (boss == null) return;

        var health = boss.GetComponent<EnemyHealth>();
        if (health == null) return;

        Unsubscribe();   // 防重复(切 Boss 房时先清旧追踪)
        trackedHealth = health;
        trackedRoom = room;
        Build();
        Show();

        healthHandler = (cur, max) => UpdateFill(cur, max);
        health.OnHealthChanged += healthHandler;

        clearedHandler = _ => Hide();
        room.OnRoomCleared += clearedHandler;

        phaseHandler = phaseIdx =>
        {
            if (phaseText != null)
                phaseText.text = phaseIdx >= 2 ? "Phase II" : "Phase I";
            if (fillImage != null)
                fillImage.color = phaseIdx >= 2 ? Phase2Color : Phase1Color;
        };
        boss.OnPhaseEntered += phaseHandler;
        phaseHandler?.Invoke(1);   // 初始 P1

        // 初始一刷
        UpdateFill(health.CurrentHealth, health.MaxHealth);
    }

    public static void Hide()
    {
        Unsubscribe();
        if (canvasGo != null)
        {
            Object.Destroy(canvasGo);
            canvasGo = null;
            fillImage = null;
            nameText = null;
            phaseText = null;
        }
    }

    private static void Unsubscribe()
    {
        if (trackedHealth != null && healthHandler != null)
            trackedHealth.OnHealthChanged -= healthHandler;
        if (trackedRoom != null && clearedHandler != null)
            trackedRoom.OnRoomCleared -= clearedHandler;
        // PhaseController 的退订(FindObjectsByType 每次调太贵——由销毁/Hide 自然回收)
        trackedHealth = null;
        trackedRoom = null;
        healthHandler = null;
        clearedHandler = null;
        phaseHandler = null;
    }

    private static void UpdateFill(float current, float max)
    {
        if (fillImage == null || max <= 0f) return;
        fillImage.fillAmount = Mathf.Clamp01(current / max);

        if (nameText != null)
            nameText.text = $"失落守门人·格兰  {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";

        if (current <= 0f) Hide();   // Boss 死亡 → 隐藏
    }

    // ---------- UI 构建(石板面板,项目 UI 惯例) ----------

    private static void Build()
    {
        if (canvasGo != null) return;

        canvasGo = new GameObject("BossHealthBarCanvas", typeof(Canvas));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 205;   // PausePanel(220)之下,HUD 之上
        PanelSprite.ConfigureCanvasScaler(canvasGo);

        // 外框(石板窄条,顶部居中)
        var frame = new GameObject("Frame", typeof(Image));
        frame.transform.SetParent(canvasGo.transform, false);
        Image frameImg = frame.GetComponent<Image>();
        PanelSprite.ApplyStonePanel(frameImg, new Color(0.05f, 0.05f, 0.06f, 0.92f));
        frameImg.rectTransform.anchorMin = frameImg.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        frameImg.rectTransform.anchoredPosition = new Vector2(0f, -40f);
        frameImg.rectTransform.sizeDelta = new Vector2(680f, 54f);

        // 血条底(深色轨道)
        var track = new GameObject("Track", typeof(Image));
        track.transform.SetParent(frame.transform, false);
        Image trackImg = track.GetComponent<Image>();
        trackImg.color = new Color(0.12f, 0.10f, 0.08f, 0.95f);
        trackImg.rectTransform.anchorMin = trackImg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        trackImg.rectTransform.anchoredPosition = new Vector2(0f, -2f);
        trackImg.rectTransform.sizeDelta = new Vector2(640f, 26f);

        // 血条填充(Filled 类型,fillAmount 控制)
        var fill = new GameObject("Fill", typeof(Image));
        fill.transform.SetParent(track.transform, false);
        fillImage = fill.GetComponent<Image>();
        fillImage.color = Phase1Color;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 1f;
        fillImage.rectTransform.anchorMin = fillImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        fillImage.rectTransform.anchoredPosition = Vector2.zero;
        fillImage.rectTransform.sizeDelta = new Vector2(640f, 26f);

        // Boss 名字(血条上方)
        nameText = CreateText(frame.transform, "Name", "失落守门人·格兰", 18,
            TextAlignmentOptions.Center, new Color(0.95f, 0.85f, 0.6f));
        nameText.rectTransform.anchorMin = nameText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        nameText.rectTransform.anchoredPosition = new Vector2(0f, -14f);

        // 阶段标签(血条右端)
        phaseText = CreateText(frame.transform, "Phase", "Phase I", 13,
            TextAlignmentOptions.Right, new Color(0.7f, 0.68f, 0.62f));
        phaseText.rectTransform.anchorMin = phaseText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        phaseText.rectTransform.anchoredPosition = new Vector2(-14f, -2f);
    }

    private static void Show()
    {
        if (canvasGo != null) canvasGo.SetActive(true);
    }

    private static TMP_Text CreateText(Transform parent, string name, string content, int fontSize,
        TextAlignmentOptions alignment, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.font = TMPFontProvider.Font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.rectTransform.sizeDelta = new Vector2(400f, 24f);
        return text;
    }
}
