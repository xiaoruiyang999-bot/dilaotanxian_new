using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 角色选择界面（v1.0.8）：选择角色外形——战士 / 狼人。
/// 与职业选择（ClassSelectUI：战士/弓手/法师=数值与武器）是两个正交维度：
/// 角色=纯视觉外形（FrameAnimator 帧组），职业=数值/武器/技能，任意组合互不冲突。
/// 流程：首次进入准备场景自动弹出 → **点颜色卡=选中（高亮预览），点"选 择"键=确定**（v1.1.31 修正语义）
/// → 写入 RunStateCarrier.ChosenCharacter（死亡保留）→ 若尚未选职业则自动接续弹出职业选择页。
/// Esc 关闭（不选择不生效）。
/// 打开期间 PlayerController 查询 IsOpen 屏蔽攻击/技能/交互输入。
/// v1.1.6：主面板接入石板 9-Slice 母版（与 ClassSelectUI 同款，PanelSprite 统一入口，素材缺失回退纯色）；
/// 新角色行：Build 里向 panelRect 追加 BuildCharacterButton 即可。待补：角色立绘/标题字效。
/// </summary>
public class CharacterSelectUI : MonoBehaviour
{
    /// <summary>UI 是否打开（PlayerController 据此屏蔽攻击/技能/交互输入）。</summary>
    public static bool IsOpen { get; private set; }

    private static CharacterSelectUI instance;

    private GameObject canvasGo;
    private Image warriorFrame, werewolfFrame;
    private CharacterSkin selected;

    private static readonly Color WarriorTint = new Color(0.30f, 0.55f, 0.95f);   // 战士：蓝
    private static readonly Color WerewolfTint = new Color(0.35f, 0.85f, 0.45f);  // 狼人：绿
    private static readonly Color DimColor = new Color(1f, 1f, 1f, 0.15f);

    // ========== 静态入口 ==========

    public static void Open()
    {
        if (instance == null)
        {
            GameObject go = new GameObject("CharacterSelectUI");
            instance = go.AddComponent<CharacterSelectUI>();
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
        selected = RunStateCarrier.Ensure().ChosenCharacter;   // 默认当前外形（高亮指示）
        RefreshHighlights();
        canvasGo.SetActive(true);
        IsOpen = true;
    }

    private void Hide()
    {
        canvasGo.SetActive(false);
        IsOpen = false;
    }

    void OnDestroy()
    {
        // 静态状态不残留（同 ClassSelectUI 规范）
        if (instance == this) instance = null;
        IsOpen = false;
    }

    // ========== 构建（程序员美术占位） ==========

    private void Build()
    {
        canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 205;   // 略高于 ClassSelectUI(200)：角色页在前、职业页在后接续弹出
        PanelSprite.ConfigureCanvasScaler(canvasGo);
        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(canvasGo.transform, false);
        Image panelImg = panel.AddComponent<Image>();
        PanelSprite.ApplyStonePanel(panelImg, new Color(0f, 0f, 0f, 0.85f));   // v1.1.6 石板 9-Slice 母版
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(900f, 555f);   // 接近原图 594:366，避免整体比例失真

        // 标题/副标题整体下移 26px 让出 49px 顶部砖框（v1.1.6）
        Label(panelRect, "选择你的角色", 30, Color.white, new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(600f, 44f));
        Label(panelRect, "外形与职业独立：任意角色可选任意职业与武器", 14, new Color(0.75f, 0.73f, 0.65f),
            new Vector2(0.5f, 1f), new Vector2(0f, -114f), new Vector2(700f, 24f));

        warriorFrame = BuildCharacterButton(panelRect, "Btn_Character_Warrior", "战 士",
            "经典体型 · 均衡手感", WarriorTint, new Vector2(-190f, -30f));
        werewolfFrame = BuildCharacterButton(panelRect, "Btn_Character_Werewolf", "狼 人",
            "狼形大体型 · 纯视觉外形（数值不变）", WerewolfTint, new Vector2(190f, -30f));

        // v1.1.30：确认键移除——点击卡片/选择键即选中即确定

        canvasGo.SetActive(false);
    }

    private Image BuildCharacterButton(Transform parent, string name, string title, string desc, Color tint, Vector2 pos)
    {
        GameObject frame = new GameObject(name, typeof(RectTransform));
        frame.transform.SetParent(parent, false);
        Image frameImg = frame.AddComponent<Image>();
        frameImg.color = DimColor;
        RectTransform frameRect = (RectTransform)frame.transform;
        frameRect.anchorMin = frameRect.anchorMax = frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.anchoredPosition = pos;
        frameRect.sizeDelta = new Vector2(340f, 300f);

        GameObject bg = new GameObject("Bg", typeof(RectTransform));
        bg.transform.SetParent(frame.transform, false);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.035f, 0.04f, 0.045f, 0.82f);
        RectTransform bgRect = (RectTransform)bg.transform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = new Vector2(3f, 3f);
        bgRect.offsetMax = new Vector2(-3f, -3f);

        Label(bgRect, "[角色立绘待补]", 17, tint, new Vector2(0.5f, 0.5f), new Vector2(0f, 58f), new Vector2(280f, 112f));
        Label(bgRect, title, 26, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(300f, 38f));
        Label(bgRect, desc, 14, new Color(0.8f, 0.78f, 0.7f), new Vector2(0.5f, 0.5f), new Vector2(0f, -68f), new Vector2(300f, 42f));

        GameObject selectGo = new GameObject("Select", typeof(RectTransform));
        selectGo.transform.SetParent(frame.transform, false);
        Image selectImg = selectGo.AddComponent<Image>();
        RectTransform selectRect = (RectTransform)selectGo.transform;
        selectRect.anchorMin = selectRect.anchorMax = selectRect.pivot = new Vector2(0.5f, 0f);
        selectRect.anchoredPosition = new Vector2(0f, 18f);
        selectRect.sizeDelta = new Vector2(280f, 44f);   // 横向素材只做横条，不再拉成卡片
        Label(selectRect, "选 择", 17, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 30f));
        Button btn = selectGo.AddComponent<Button>();
        PanelSprite.ApplyStoneButton(btn, selectImg, new Color(0.12f, 0.12f, 0.14f, 0.95f));
        btn.onClick.AddListener(() => Pick(name));   // "选 择"键 = 确定（应用并关闭）

        // v1.1.30/31：颜色卡区域 = 仅选中（高亮预览，可反复切换比较，不应用）；
        // 确定功能专属"选 择"键
        Button cardBtn = frame.AddComponent<Button>();
        cardBtn.targetGraphic = frameImg;
        cardBtn.transition = Selectable.Transition.ColorTint;
        var cb = cardBtn.colors;
        cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 0.35f);
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 0.35f);
        cb.fadeDuration = 0.08f;
        cardBtn.colors = cb;
        cardBtn.onClick.AddListener(() =>
        {
            selected = name.Contains("Werewolf") ? CharacterSkin.Werewolf : CharacterSkin.Warrior;
            RefreshHighlights();
        });
        return frameImg;
    }

    /// <summary>选中即确定（v1.1.30）：写载体、即时换形、关闭，未选职业则接续职业页。</summary>
    private void Pick(string cardName)
    {
        selected = cardName.Contains("Werewolf") ? CharacterSkin.Werewolf : CharacterSkin.Warrior;
        RefreshHighlights();

        RunStateCarrier.Ensure().SetCharacter(selected);
        Debug.Log($"[Character] 已选择角色外形：{selected}");

        // v1.0.9 即时换形：确认立刻应用视觉与变身能力（不再等下一次场景加载）
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null)
        {
            FrameAnimator fa = pc.GetComponent<FrameAnimator>();
            if (fa != null)
            {
                fa.SetWerewolfVisual(selected == CharacterSkin.Werewolf);
                if (selected == CharacterSkin.Werewolf)
                {
                    WerewolfTransformation.EnsureOn(pc.gameObject);
                    WerewolfDash.EnsureOn(pc.gameObject);   // v1.1.42 狼人专属冲刺
                }
                else
                {
                    WerewolfTransformation old = pc.GetComponent<WerewolfTransformation>();
                    if (old != null) Destroy(old);   // OnDestroy 复位判定缩放/数值/血条
                    WerewolfDash dash = pc.GetComponent<WerewolfDash>();
                    if (dash != null) Destroy(dash);   // v1.1.42 改选战士：冲刺下线
                }
            }
        }

        Hide();
        if (RunStateCarrier.Ensure().LastChosenClass == null)
            ClassSelectUI.Open();   // 接续：角色定了还没职业 → 直接弹职业选择
    }

    private void RefreshHighlights()
    {
        if (warriorFrame != null)
            warriorFrame.color = selected == CharacterSkin.Warrior ? Fade(WarriorTint, 0.85f) : DimColor;
        if (werewolfFrame != null)
            werewolfFrame.color = selected == CharacterSkin.Werewolf ? Fade(WerewolfTint, 0.85f) : DimColor;
    }

    private static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, a);

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    private static void Label(Transform parent, string text, int size, Color color,
        Vector2 anchor, Vector2 offset, Vector2 sizeDelta)
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
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, anchor.y);
        rect.anchoredPosition = offset;
        rect.sizeDelta = sizeDelta;
    }
}
