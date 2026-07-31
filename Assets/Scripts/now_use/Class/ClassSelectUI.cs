using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 职业选择界面（v0.6.2 阶段 B，计划书 4.5 / 美术清单第七节）。
/// 屏幕空间 Overlay + TMP，全部运行时代码构建（纯色块 + 文字，无图片资源）。
/// 流程：E 交互职业选择台 → Open() → 点职业按钮（职业色边框高亮，其余熄灭）
/// → 点确认（边框 DOTween 闪烁 ≈0.3s）→ 关闭 → ApplyClass → 刷新两个武器展台。
/// 默认高亮 RunStateCarrier.LastChosenClass（死亡重开默认上次职业）；Esc 关闭（未确认不生效）。
/// 打开期间 PlayerController 查询 IsOpen 屏蔽 Attack/Skill/Interact 分发（点击按钮不触发攻击）。
/// </summary>
public class ClassSelectUI : MonoBehaviour
{
    /// <summary>UI 是否打开（PlayerController 据此屏蔽攻击/技能输入）。</summary>
    public static bool IsOpen { get; private set; }

    private static ClassSelectUI instance;

    private GameObject canvasGo;
    private readonly List<ClassButton> buttons = new List<ClassButton>();
    private Image confirmFrame;
    private ClassData selected;
    private bool confirming;

    private struct ClassButton
    {
        public ClassData data;
        public Image frame;
    }

    // ========== 静态入口 ==========

    public static void Open()
    {
        if (instance == null)
        {
            GameObject go = new GameObject("ClassSelectUI");
            instance = go.AddComponent<ClassSelectUI>();
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

        // 默认上次职业（死亡重开预置高亮），可改选或直接确认
        selected = RunStateCarrier.Ensure().LastChosenClass;

        RefreshHighlights();
        confirming = false;
        canvasGo.SetActive(true);
        IsOpen = true;
    }

    private void Hide()
    {
        canvasGo.SetActive(false);
        IsOpen = false;
        confirming = false;
    }

    void OnDestroy()
    {
        // 静态状态不残留：场景切换销毁实例后，IsOpen 必须复位（否则新场景输入被持续屏蔽）
        if (instance == this) instance = null;
        IsOpen = false;
    }

    // ========== 交互 ==========

    private void Select(ClassData data)
    {
        if (confirming) return;
        selected = data;
        RefreshHighlights();
    }

    private void RefreshHighlights()
    {
        foreach (ClassButton b in buttons)
        {
            if (b.frame == null || b.data == null) continue;
            Color c = b.data.ClassColor;
            // 选中：职业色边框高亮；未选中：熄灭（低透明度）
            b.frame.color = b.data == selected
                ? new Color(c.r, c.g, c.b, 1f)
                : new Color(c.r, c.g, c.b, 0.15f);
        }
    }

    private void Confirm()
    {
        if (confirming || selected == null) return;
        confirming = true;

        // 确认按钮边框短暂高亮闪烁 ≈0.3s（SetLink 规范）后结算
        if (confirmFrame != null)
        {
            confirmFrame.DOFade(0.2f, 0.15f)
                .SetLoops(2, LoopType.Yoyo)
                .SetLink(canvasGo)
                .OnComplete(ApplyAndClose);
        }
        else
        {
            ApplyAndClose();
        }
    }

    private void ApplyAndClose()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null && selected != null)
        {
            player.GetStats().ApplyClass(selected);
            Debug.Log($"[Class] 已选择职业：{selected.DisplayName}");
        }

        RunStateCarrier.Ensure().SetClass(selected);   // 跨场景载体（死亡保留，进地牢锁定）

        PrepRoomPlacer.RefreshWeapons(selected);
        Hide();
    }

    // ========== UI 构建 ==========

    private void Build()
    {
        canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        canvasGo.AddComponent<GraphicRaycaster>();

        // 主面板：深色半透明矩形底（600×500 居中）
        GameObject panel = CreateUIObject("Panel", canvasGo.transform);
        Image panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.82f);
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(600f, 500f);

        // 标题（28pt，面板顶部居中）
        TMP_Text title = CreateText(panel.transform, "Title", "选择你的职业", 28,
            TextAlignmentOptions.Center, Color.white);
        PlaceUI((RectTransform)title.transform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(400f, 40f));

        // 三个职业按钮（22pt，各带职业色方块 + 六维数值行（v0.7.0））：
        // 面板内纵向等距排列（中心锚点，y = +120 / +30 / -60，间距 90，不溢出面板）
        var classes = ClassCatalog.All;
        for (int i = 0; i < 3 && i < classes.Count; i++)
        {
            ClassData data = classes[i];
            if (data == null) continue;
            buttons.Add(BuildClassButton(panel.transform, data, BuildStatLine(data),
                new Vector2(0f, 120f - i * 90f)));
        }

        // 确认按钮（面板中间偏下，20pt，与最低职业按钮不重叠）
        BuildConfirmButton(panel.transform, new Vector2(0f, -160f));

        canvasGo.SetActive(false);
    }

    /// <summary>六维数值行（v0.7.0，决策 6）：HP/护甲/攻击/魔力/暴击率%/暴击伤害倍率。</summary>
    private static string BuildStatLine(ClassData d)
    {
        return $"HP {d.MaxHP:0}  护甲 {d.MaxArmor:0}  攻击 {d.Attack:0}  魔力 {d.MaxMana:0}  暴击 {d.CritRate:P0}  暴伤 ×{d.CritDamage:0.##}";
    }

    private ClassButton BuildClassButton(Transform parent, ClassData data, string statLine, Vector2 pos)
    {
        // 边框（职业色，选中高亮）
        GameObject frame = CreateUIObject($"Btn_{data.ClassType}", parent);
        Image frameImg = frame.AddComponent<Image>();
        frameImg.color = new Color(data.ClassColor.r, data.ClassColor.g, data.ClassColor.b, 0.15f);
        RectTransform frameRect = (RectTransform)frame.transform;
        PlaceUI(frameRect, new Vector2(0.5f, 0.5f), pos, new Vector2(420f, 72f));

        // 底（内缩 3px 深色）
        GameObject bg = CreateUIObject("Bg", frame.transform);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.14f, 0.95f);
        StretchFill((RectTransform)bg.transform, 3f);

        // 职业色方块图标
        GameObject icon = CreateUIObject("Icon", frame.transform);
        Image iconImg = icon.AddComponent<Image>();
        iconImg.color = data.ClassColor;
        PlaceUI((RectTransform)icon.transform, new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(26f, 26f));

        // 职业名（22pt）
        TMP_Text name = CreateText(frame.transform, "Name", data.DisplayName, 22,
            TextAlignmentOptions.MidlineLeft, Color.white);
        PlaceUI((RectTransform)name.transform, new Vector2(0f, 0.5f), new Vector2(78f, 12f), new Vector2(300f, 30f));

        // 六维数值行（小字灰，v0.7.0）
        TMP_Text feat = CreateText(frame.transform, "StatLine", statLine, 12,
            TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.6f));
        PlaceUI((RectTransform)feat.transform, new Vector2(0f, 0.5f), new Vector2(78f, -16f), new Vector2(336f, 20f));

        // 点击 → 选中
        Button btn = frame.AddComponent<Button>();
        btn.targetGraphic = bgImg;
        btn.onClick.AddListener(() => Select(data));

        return new ClassButton { data = data, frame = frameImg };
    }

    private void BuildConfirmButton(Transform parent, Vector2 pos)
    {
        GameObject frame = CreateUIObject("Btn_Confirm", parent);
        confirmFrame = frame.AddComponent<Image>();
        confirmFrame.color = new Color(0.9451f, 0.7686f, 0.0588f, 0.7f);   // 金色边框
        RectTransform frameRect = (RectTransform)frame.transform;
        PlaceUI(frameRect, new Vector2(0.5f, 0.5f), pos, new Vector2(180f, 46f));

        GameObject bg = CreateUIObject("Bg", frame.transform);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.14f, 0.95f);
        StretchFill((RectTransform)bg.transform, 3f);

        TMP_Text label = CreateText(frame.transform, "Label", "确 认", 20,
            TextAlignmentOptions.Center, Color.white);
        StretchFill((RectTransform)label.transform, 0f);

        Button btn = frame.AddComponent<Button>();
        btn.targetGraphic = bgImg;
        btn.onClick.AddListener(Confirm);
    }

    // ========== UI 工具 ==========

    private GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private TMP_Text CreateText(Transform parent, string name, string content, int fontSize,
        TextAlignmentOptions alignment, Color color)
    {
        GameObject go = CreateUIObject(name, parent);
        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.font = TMPFontProvider.Font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        return text;
    }

    private void PlaceUI(RectTransform rect, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
    }

    private void StretchFill(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    /// <summary>场景无 EventSystem 时运行时补齐（UI 点击必需）。</summary>
    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        Debug.Log("[Class] 场景无 EventSystem，已运行时补齐（InputSystemUIInputModule）。");
    }
}
