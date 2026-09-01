using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 角色选择界面（v1.0.8）：选择角色外形——战士 / 狼人。
/// 与职业选择（ClassSelectUI：战士/弓手/法师=数值与武器）是两个正交维度：
/// 角色=纯视觉外形（FrameAnimator 帧组），职业=数值/武器/技能，任意组合互不冲突。
/// 流程：首次进入准备场景自动弹出 → 点角色按钮（高亮）→ 确认 → 写入 RunStateCarrier.ChosenCharacter
/// （死亡保留）→ 若尚未选职业则自动接续弹出职业选择页。Esc 关闭（未确认不生效）。
/// 打开期间 PlayerController 查询 IsOpen 屏蔽攻击/技能/交互输入。
/// 【美术资产缺失】面板为纯色块+内置字体占位；待补：角色立绘/按钮框图/标题字效。
/// </summary>
public class CharacterSelectUI : MonoBehaviour
{
    /// <summary>UI 是否打开（PlayerController 据此屏蔽攻击/技能/交互输入）。</summary>
    public static bool IsOpen { get; private set; }

    private static CharacterSelectUI instance;

    private GameObject canvasGo;
    private Image warriorFrame, werewolfFrame;
    private CharacterSkin selected;
    private bool confirming;

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
        selected = RunStateCarrier.Ensure().ChosenCharacter;   // 默认当前外形（可改选）
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
        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(canvasGo.transform, false);
        Image panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.85f);
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(600f, 500f);

        Label(panelRect, "选择你的角色", 30, Color.white, new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(500f, 40f));
        Label(panelRect, "外形与职业独立：任意角色可选任意职业与武器", 14, new Color(0.75f, 0.73f, 0.65f),
            new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(560f, 24f));

        warriorFrame = BuildCharacterButton(panelRect, "Btn_Character_Warrior", "战 士",
            "经典体型 · 均衡手感", WarriorTint, new Vector2(0f, 60f));
        werewolfFrame = BuildCharacterButton(panelRect, "Btn_Character_Werewolf", "狼 人",
            "狼形大体型 · 纯视觉外形（数值不变）", WerewolfTint, new Vector2(0f, -50f));

        BuildConfirmButton(panelRect, new Vector2(0f, -170f));

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
        frameRect.sizeDelta = new Vector2(420f, 90f);

        GameObject bg = new GameObject("Bg", typeof(RectTransform));
        bg.transform.SetParent(frame.transform, false);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.14f, 0.95f);
        RectTransform bgRect = (RectTransform)bg.transform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = new Vector2(3f, 3f);
        bgRect.offsetMax = new Vector2(-3f, -3f);

        Label(bgRect, title, 24, Color.white, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(400f, 34f));
        Label(bgRect, desc, 14, new Color(0.8f, 0.78f, 0.7f), new Vector2(0.5f, 0f), new Vector2(0f, -16f), new Vector2(400f, 22f));

        Button btn = frame.AddComponent<Button>();
        btn.targetGraphic = frameImg;
        btn.onClick.AddListener(() =>
        {
            selected = name.Contains("Werewolf") ? CharacterSkin.Werewolf : CharacterSkin.Warrior;
            RefreshHighlights();
        });
        return frameImg;
    }

    private void BuildConfirmButton(Transform parent, Vector2 pos)
    {
        GameObject go = new GameObject("Btn_Confirm", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.45f, 0.25f, 0.95f);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(220f, 46f);
        Label(rect, "确  认", 20, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 40f));

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(Confirm);
    }

    private void Confirm()
    {
        if (confirming) return;
        confirming = true;

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
                    WerewolfTransformation.EnsureOn(pc.gameObject);
                else
                {
                    WerewolfTransformation old = pc.GetComponent<WerewolfTransformation>();
                    if (old != null) Destroy(old);   // OnDestroy 复位判定缩放/数值/血条
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
