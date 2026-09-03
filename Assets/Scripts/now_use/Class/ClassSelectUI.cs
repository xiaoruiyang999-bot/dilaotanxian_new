using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 职业选择界面（v0.6.2 阶段 B，计划书 4.5 / 美术清单第七节）。
/// 屏幕空间 Overlay + TMP，v1.1.9 起使用石板大面板 + 三列职业卡的完整新布局。
/// 流程：E 交互职业选择台 → Open() → 点职业按钮（职业色边框高亮，其余熄灭）
/// → 点确认（边框 DOTween 闪烁 ≈0.3s）→ 关闭 → ApplyClass → 刷新两个武器展台。
/// 默认高亮 RunStateCarrier.LastChosenClass（死亡重开默认上次职业）；Esc 关闭（未确认不生效）。
/// 打开期间 PlayerController 查询 IsOpen 屏蔽 Attack/Skill/Interact 分发（点击按钮不触发攻击）。
/// 新增职业行：Build 里向 panel.transform 追加 BuildClassButton 即可，样式自动继承母版。
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
        PanelSprite.ConfigureCanvasScaler(canvasGo);
        canvasGo.AddComponent<GraphicRaycaster>();

        // v1.1.9：完整置换旧竖排菜单，改为大石板上的三列职业卡。
        GameObject panel = CreateUIObject("Panel", canvasGo.transform);
        Image panelImg = panel.AddComponent<Image>();
        PanelSprite.ApplyStonePanel(panelImg, new Color(0f, 0f, 0f, 0.82f));
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(1040f, 640f);   // 接近石板原图比例，避免整体压扁/拉高

        // 标题（28pt，面板顶部居中；-68 = 让出 49px 顶部砖框，字面不压框）
        TMP_Text title = CreateText(panel.transform, "Title", "选择你的职业", 28,
            TextAlignmentOptions.Center, Color.white);
        PlaceUI((RectTransform)title.transform, new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(700f, 46f));

        TMP_Text subtitle = CreateText(panel.transform, "Subtitle", "选择职业属性，武器将在准备房间中单独选择", 16,
            TextAlignmentOptions.Center, new Color(0.78f, 0.75f, 0.66f));
        PlaceUI((RectTransform)subtitle.transform, new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(760f, 30f));

        // 三列职业卡。暂无职业立绘/图标，卡内用明确文字挂点占位。
        var classes = ClassCatalog.All;
        for (int i = 0; i < 3 && i < classes.Count; i++)
        {
            ClassData data = classes[i];
            if (data == null) continue;
            buttons.Add(BuildClassButton(panel.transform, data, BuildStatLine(data),
                new Vector2(-320f + i * 320f, -4f)));
        }

        // 确认按钮（面板中间偏下，20pt，与最低职业按钮不重叠）
        BuildConfirmButton(panel.transform, new Vector2(0f, -252f));

        canvasGo.SetActive(false);
    }

    /// <summary>六维数值行（v0.7.0，决策 6）：HP/护甲/攻击/魔力/暴击率%/暴击伤害倍率。</summary>
    private static string BuildStatLine(ClassData d)
    {
        return $"HP  {d.MaxHP:0}    护甲  {d.MaxArmor:0}\n攻击  {d.Attack:0}    魔力  {d.MaxMana:0}\n暴击  {d.CritRate:P0}    暴伤  ×{d.CritDamage:0.##}";
    }

    private ClassButton BuildClassButton(Transform parent, ClassData data, string statLine, Vector2 pos)
    {
        // 边框（职业色，选中高亮）
        GameObject frame = CreateUIObject($"Btn_{data.ClassType}", parent);
        Image frameImg = frame.AddComponent<Image>();
        frameImg.color = new Color(data.ClassColor.r, data.ClassColor.g, data.ClassColor.b, 0.15f);
        RectTransform frameRect = (RectTransform)frame.transform;
        PlaceUI(frameRect, new Vector2(0.5f, 0.5f), pos, new Vector2(286f, 330f));

        // 底（v1.1.7 三态石板按钮：hover/pressed 自动换图；素材缺失回退半透明深色）
        GameObject bg = CreateUIObject("Bg", frame.transform);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.035f, 0.04f, 0.045f, 0.82f);
        StretchFill((RectTransform)bg.transform, 3f);

        // 美术挂点：得到职业立绘后，将此 Placeholder 换为 Image 即可。
        TMP_Text art = CreateText(frame.transform, "ArtPlaceholder", "[职业立绘待补]", 16,
            TextAlignmentOptions.Center, new Color(data.ClassColor.r, data.ClassColor.g, data.ClassColor.b, 0.85f));
        PlaceUI((RectTransform)art.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 78f), new Vector2(230f, 88f));

        // 职业名（22pt）
        TMP_Text name = CreateText(frame.transform, "Name", data.DisplayName, 22,
            TextAlignmentOptions.MidlineLeft, Color.white);
        PlaceUI((RectTransform)name.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 25f), new Vector2(240f, 36f));

        // 六维数值行（小字灰，v0.7.0）
        TMP_Text feat = CreateText(frame.transform, "StatLine", statLine, 14,
            TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.68f));
        PlaceUI((RectTransform)feat.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -48f), new Vector2(246f, 86f));

        // 横向三态图只用于卡片底部操作条，卡片背景本身不拉伸按钮素材。
        GameObject selectGo = CreateUIObject("Select", frame.transform);
        Image selectImg = selectGo.AddComponent<Image>();
        PlaceUI((RectTransform)selectGo.transform, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(238f, 42f));
        Button btn = selectGo.AddComponent<Button>();
        PanelSprite.ApplyStoneButton(btn, selectImg, new Color(0.12f, 0.12f, 0.14f, 0.95f));
        TMP_Text selectLabel = CreateText(selectGo.transform, "Label", "选 择", 16, TextAlignmentOptions.Center, Color.white);
        StretchFill((RectTransform)selectLabel.transform, 0f);
        btn.onClick.AddListener(() => Select(data));

        return new ClassButton { data = data, frame = frameImg };
    }

    private void BuildConfirmButton(Transform parent, Vector2 pos)
    {
        GameObject frame = CreateUIObject("Btn_Confirm", parent);
        confirmFrame = frame.AddComponent<Image>();
        confirmFrame.color = new Color(0.9451f, 0.7686f, 0.0588f, 0.7f);   // 金色边框
        RectTransform frameRect = (RectTransform)frame.transform;
        PlaceUI(frameRect, new Vector2(0.5f, 0.5f), pos, new Vector2(300f, 46f));

        GameObject bg = CreateUIObject("Bg", frame.transform);
        Image bgImg = bg.AddComponent<Image>();
        StretchFill((RectTransform)bg.transform, 3f);

        TMP_Text label = CreateText(frame.transform, "Label", "确 认", 20,
            TextAlignmentOptions.Center, Color.white);
        StretchFill((RectTransform)label.transform, 0f);

        Button btn = frame.AddComponent<Button>();
        PanelSprite.ApplyStoneButton(btn, bgImg, new Color(0f, 0f, 0f, 0.5f));   // v1.1.7 三态石板
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
