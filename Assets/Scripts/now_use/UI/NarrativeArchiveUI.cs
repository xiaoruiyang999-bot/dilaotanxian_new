using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// v2.0.7 第二批 守灯厅档案柜（V2 §2.4/§16）：已读叙事碎片回看。左列标题（未读显示 ???）、
/// 右侧正文；Esc/关闭按钮退出。数据全读 NarrativeService（已读持久），UI 不改状态。
/// 由守灯厅 ArchiveCabinet 交互物打开。
/// </summary>
public static class NarrativeArchiveUI
{
    private static GameObject canvasGo;
    private static TMP_Text bodyText;
    private static TMP_Text titleText;
    private static readonly List<GameObject> rows = new List<GameObject>();

    public static bool IsOpen => canvasGo != null;

    public static void Open()
    {
        if (IsOpen) return;

        canvasGo = new GameObject("ArchiveCanvas", typeof(Canvas));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 245;
        PanelSprite.ConfigureCanvasScaler(canvasGo);
        canvasGo.AddComponent<GraphicRaycaster>();

        Image mask = new GameObject("Mask", typeof(Image)).GetComponent<Image>();
        mask.transform.SetParent(canvasGo.transform, false);
        mask.color = new Color(0f, 0f, 0f, 0.6f);
        mask.rectTransform.anchorMin = Vector2.zero;
        mask.rectTransform.anchorMax = Vector2.one;
        mask.rectTransform.offsetMin = mask.rectTransform.offsetMax = Vector2.zero;

        var panel = new GameObject("StonePanel", typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        Image img = panel.GetComponent<Image>();
        PanelSprite.ApplyStonePanel(img, new Color(0.05f, 0.05f, 0.06f, 0.97f));
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        img.rectTransform.sizeDelta = new Vector2(1100f, 520f);

        var header = CreateText(panel.transform, "Header", "碎 片 档 案", 30, TextAlignmentOptions.Center,
            new Color(0.95f, 0.85f, 0.6f));
        header.rectTransform.anchoredPosition = new Vector2(0f, 215f);

        var listGo = new GameObject("ListRoot", typeof(RectTransform));
        listGo.transform.SetParent(panel.transform, false);
        var listRt = listGo.GetComponent<RectTransform>();
        listRt.anchorMin = new Vector2(0f, 0f);
        listRt.anchorMax = new Vector2(0.38f, 1f);
        listRt.offsetMin = new Vector2(30f, 30f);
        listRt.offsetMax = new Vector2(-10f, -70f);

        titleText = CreateText(panel.transform, "DetailTitle", "选择左侧条目", 24,
            TextAlignmentOptions.Center, new Color(0.95f, 0.85f, 0.6f));
        titleText.rectTransform.anchorMin = new Vector2(0.38f, 1f);
        titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleText.rectTransform.offsetMin = new Vector2(10f, -100f);
        titleText.rectTransform.offsetMax = new Vector2(-30f, -60f);

        bodyText = CreateText(panel.transform, "DetailBody", "", 20, TextAlignmentOptions.TopLeft, Color.white);
        bodyText.rectTransform.anchorMin = new Vector2(0.38f, 0f);
        bodyText.rectTransform.anchorMax = new Vector2(1f, 1f);
        bodyText.rectTransform.offsetMin = new Vector2(10f, 70f);
        bodyText.rectTransform.offsetMax = new Vector2(-30f, -110f);

        var closeBtn = CreateStoneButton(panel.transform, "关 闭", new Vector2(430f, -215f), new Vector2(160f, 48f));
        closeBtn.onClick.AddListener(Close);

        var hint = CreateText(panel.transform, "Hint", "未读碎片以 ??? 封存", 14,
            TextAlignmentOptions.BottomLeft, new Color(0.7f, 0.68f, 0.62f));
        hint.rectTransform.anchorMin = new Vector2(0f, 0f);
        hint.rectTransform.offsetMin = new Vector2(30f, 14f);
        hint.rectTransform.sizeDelta = new Vector2(400f, 24f);

        BuildList(listGo.transform);
    }

    public static void Close()
    {
        if (canvasGo == null) return;
        Object.Destroy(canvasGo);
        canvasGo = null;
        rows.Clear();
    }

    private static void BuildList(Transform listRoot)
    {
        foreach (GameObject go in rows) if (go != null) Object.Destroy(go);
        rows.Clear();

        HashSet<string> read = NarrativeService.ReadIds();
        float y = -18f;
        foreach (NarrativeService.Fragment f in NarrativeService.Library)
        {
            bool isRead = read.Contains(f.Id);
            var rowGo = new GameObject($"Row_{f.Id}", typeof(Image));
            rowGo.transform.SetParent(listRoot, false);
            Image rowImg = rowGo.GetComponent<Image>();
            rowImg.sprite = PanelSprite.BtnNormal;
            if (rowImg.sprite != null) rowImg.sprite = null;   // 列表行用纯色，避免按钮切片过宽
            rowImg.color = isRead ? new Color(0.16f, 0.16f, 0.18f, 0.9f) : new Color(0.1f, 0.1f, 0.11f, 0.8f);
            var rt = rowGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(370f, 52f);

            var label = CreateText(rowGo.transform, "Label", isRead ? f.Title : "？？？", 19,
                TextAlignmentOptions.Left, isRead ? Color.white : new Color(0.45f, 0.45f, 0.45f));
            label.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            label.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            label.rectTransform.anchoredPosition = new Vector2(16f, 0f);
            label.rectTransform.sizeDelta = new Vector2(340f, 30f);

            if (isRead)
            {
                NarrativeService.Fragment captured = f;
                var btn = rowGo.AddComponent<Button>();
                btn.targetGraphic = rowImg;
                btn.onClick.AddListener(() =>
                {
                    titleText.text = captured.Title;
                    bodyText.text = string.Join("\n\n", captured.Lines);
                });
            }

            rows.Add(rowGo);
            y -= 60f;
        }
    }

    private static Button CreateStoneButton(Transform parent, string label, Vector2 pos, Vector2 size)
    {
        var go = new GameObject($"Btn_{label}", typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        PanelSprite.ApplyStoneButton(null, img, new Color(0.35f, 0.3f, 0.25f, 0.95f));
        PlaceUI(img.rectTransform, pos, size);
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var text = CreateText(go.transform, "Label", label, 22, TextAlignmentOptions.Center, Color.white);
        text.rectTransform.sizeDelta = size;
        return btn;
    }

    private static void PlaceUI(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
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
        text.rectTransform.sizeDelta = new Vector2(300f, 40f);
        return text;
    }
}
