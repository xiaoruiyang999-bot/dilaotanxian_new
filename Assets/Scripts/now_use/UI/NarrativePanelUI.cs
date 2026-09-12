using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// v2.0.7 叙事碎片弹窗（V2 §16）：石板面板展示标题+正文分段，点击/J 键 逐段推进（v2.0.8 用户定案），
/// 队列播完关闭。UI 只呈现与标记已读（MarkRead 在关闭时写入），不承载投放决策
///（何时弹由 PrepRoomManager/RunManager 决定）。J 键推进走 PlayerController.Update 设备直读。
/// </summary>
public static class NarrativePanelUI
{
    private static GameObject canvasGo;
    private static TMP_Text bodyText;
    private static TMP_Text titleText;
    private static Queue<NarrativeService.Fragment> queue;
    private static NarrativeService.Fragment current;
    private static int lineIndex;

    public static bool IsOpen => canvasGo != null;

    /// <summary>投放队列（开场连播或单条）；已开时并入队尾。</summary>
    public static void Show(IEnumerable<NarrativeService.Fragment> fragments)
    {
        if (fragments == null) return;
        queue = queue ?? new Queue<NarrativeService.Fragment>();
        foreach (var f in fragments) queue.Enqueue(f);
        if (!IsOpen) OpenNext();
    }

    public static void Close()
    {
        if (canvasGo == null) return;
        Object.Destroy(canvasGo);
        canvasGo = null;
        queue = null;
        current = null;
    }

    /// <summary>点击/J 键：推进正文段落，段落尽则下一篇；整队列播完关闭并标记已读。</summary>
    public static void Advance()
    {
        if (current == null || canvasGo == null) return;
        lineIndex++;
        if (lineIndex < current.Lines.Length)
        {
            bodyText.text = current.Lines[lineIndex];
            return;
        }
        NarrativeService.MarkRead(current.Id);
        OpenNext();
    }

    private static void OpenNext()
    {
        while ((queue?.Count ?? 0) > 0)
        {
            current = queue.Dequeue();
            lineIndex = 0;
            if (current == null) continue;
            EnsureCanvas();
            titleText.text = current.Title;
            bodyText.text = current.Lines[0];
            return;
        }
        Close();   // 队列空：收摊
    }

    private static void EnsureCanvas()
    {
        if (canvasGo != null) return;

        canvasGo = new GameObject("NarrativeCanvas", typeof(Canvas));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;   // 技能树(230)/奖励(240)之上，仅低于过渡黑屏
        PanelSprite.ConfigureCanvasScaler(canvasGo);
        canvasGo.AddComponent<GraphicRaycaster>();

        Image mask = new GameObject("Mask", typeof(Image)).GetComponent<Image>();
        mask.transform.SetParent(canvasGo.transform, false);
        mask.color = new Color(0f, 0f, 0f, 0.65f);
        mask.rectTransform.anchorMin = Vector2.zero;
        mask.rectTransform.anchorMax = Vector2.one;
        mask.rectTransform.offsetMin = mask.rectTransform.offsetMax = Vector2.zero;
        var maskBtn = mask.gameObject.AddComponent<Button>();
        maskBtn.transition = Button.Transition.None;
        maskBtn.onClick.AddListener(Advance);

        var panel = new GameObject("StonePanel", typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        Image img = panel.GetComponent<Image>();
        PanelSprite.ApplyStonePanel(img, new Color(0.05f, 0.05f, 0.06f, 0.97f));
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        img.rectTransform.sizeDelta = new Vector2(900f, 420f);

        titleText = CreateText(panel.transform, "Title", "", 30, TextAlignmentOptions.Center,
            new Color(0.95f, 0.85f, 0.6f));
        titleText.rectTransform.anchoredPosition = new Vector2(0f, 155f);

        bodyText = CreateText(panel.transform, "Body", "", 22, TextAlignmentOptions.Center, Color.white);
        bodyText.rectTransform.sizeDelta = new Vector2(760f, 180f);
        bodyText.rectTransform.anchoredPosition = new Vector2(0f, -10f);

        var hint = CreateText(panel.transform, "Hint", "点击 或 J 键 继续", 15,
            TextAlignmentOptions.Center, new Color(0.8f, 0.78f, 0.7f));
        hint.rectTransform.anchoredPosition = new Vector2(0f, -170f);
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
        text.rectTransform.sizeDelta = new Vector2(500f, 40f);
        return text;
    }
}
