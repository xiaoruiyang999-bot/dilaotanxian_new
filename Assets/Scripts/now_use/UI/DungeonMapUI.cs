using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// v2.0.5 第一批：DAG 大地图（只读预览，V2 §16.2）。列式石板节点图：
/// 列自左向右、行垂直错位；连线按 Next/Prev 画；节点状态三档——当前（金环）/已访问（暗实心）/
/// 已揭示（类型色）/未揭示（灰暗 + ？）。本批只读（Tab 开关），节点选择与单房过渡属第二批。
/// 状态与类型色合同集中此处，选择版复用同一渲染。
/// </summary>
public static class DungeonMapUI
{
    private static GameObject canvasGo;
    private static readonly List<GameObject> items = new List<GameObject>();
    private static System.Action<int> pickCallback;   // v2.0.5 交互模式：选择回调（null = 只读预览）

    /// <summary>当前层的图（地牢生成时由 DungeonManager 写入；null 时 Tab 不响应）。</summary>
    public static DungeonGraphData CurrentGraph { get; set; }
    public static int CurrentNodeId { get; set; } = -1;

    private static readonly Color gold = new Color(1f, 0.82f, 0.35f);
    private static readonly Color visitedGray = new Color(0.45f, 0.45f, 0.45f);
    private static readonly Color undiscovered = new Color(0.3f, 0.3f, 0.3f, 0.8f);
    private static readonly Dictionary<NodeType, Color> typeColors = new Dictionary<NodeType, Color>
    {
        { NodeType.Combat,   new Color(0.75f, 0.45f, 0.4f) },
        { NodeType.Elite,    new Color(0.9f,  0.35f, 0.25f) },
        { NodeType.Treasure, new Color(0.9f,  0.75f, 0.3f) },
        { NodeType.Shop,     new Color(0.4f,  0.7f,  0.9f) },
        { NodeType.Event,    new Color(0.7f,  0.5f,  0.85f) },
        { NodeType.Recovery, new Color(0.4f,  0.85f, 0.5f) },
        { NodeType.Boss,     new Color(0.9f,  0.25f, 0.3f) },
        { NodeType.Start,    new Color(0.5f,  0.8f,  0.5f) },
    };
    private static readonly Dictionary<NodeType, string> typeGlyphs = new Dictionary<NodeType, string>
    {
        { NodeType.Combat, "战" }, { NodeType.Elite, "精" }, { NodeType.Treasure, "宝" },
        { NodeType.Shop, "店" }, { NodeType.Event, "?" }, { NodeType.Recovery, "愈" },
        { NodeType.Boss, "王" }, { NodeType.Start, "起" },
    };

    public static bool IsOpen => canvasGo != null;

    public static void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    /// <summary>交互选择模式（v2.0.5 第二批）：可选节点（当前节点的 Next）金色可点，其余只读。</summary>
    public static void OpenInteractive(DungeonGraphData graph, int currentNodeId, System.Action<int> onPick)
    {
        CurrentGraph = graph;
        CurrentNodeId = currentNodeId;
        pickCallback = onPick;
        Open();
    }

    public static void Open()
    {
        if (IsOpen || CurrentGraph == null) return;

        canvasGo = new GameObject("DungeonMapCanvas", typeof(Canvas));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 225;   // PausePanel(220) 之下、技能树(230) 之下
        PanelSprite.ConfigureCanvasScaler(canvasGo);
        canvasGo.AddComponent<GraphicRaycaster>();

        Image mask = new GameObject("Mask", typeof(Image)).GetComponent<Image>();
        mask.transform.SetParent(canvasGo.transform, false);
        mask.color = new Color(0f, 0f, 0f, 0.55f);
        mask.rectTransform.anchorMin = Vector2.zero;
        mask.rectTransform.anchorMax = Vector2.one;
        mask.rectTransform.offsetMin = mask.rectTransform.offsetMax = Vector2.zero;

        var panel = new GameObject("StonePanel", typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        Image img = panel.GetComponent<Image>();
        PanelSprite.ApplyStonePanel(img, new Color(0.06f, 0.06f, 0.07f, 0.96f));
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        img.rectTransform.sizeDelta = new Vector2(1400f, 560f);

        var title = CreateText(panel.transform, "Title", "路 线 图", 34, TextAlignmentOptions.Center, Color.white);
        title.rectTransform.anchoredPosition = new Vector2(0f, 235f);
        string hint_text = pickCallback != null ? "选择下一个节点（金色可点）· Esc 取消" : "Tab 关闭";
        var hint = CreateText(panel.transform, "Hint", hint_text,
            16, TextAlignmentOptions.Center, new Color(0.8f, 0.78f, 0.7f));
        hint.rectTransform.anchoredPosition = new Vector2(0f, -235f);

        Rebuild(panel.transform);
    }

    public static void Close()
    {
        if (canvasGo == null) return;
        Object.Destroy(canvasGo);
        canvasGo = null;
        items.Clear();
        pickCallback = null;
    }

    private static void Rebuild(Transform panel)
    {
        foreach (GameObject go in items) if (go != null) Object.Destroy(go);
        items.Clear();
        DungeonGraphData graph = CurrentGraph;

        int maxColumn = 0;
        foreach (DungeonGraphNode n in graph.Nodes) maxColumn = Mathf.Max(maxColumn, n.Column);
        float colStep = 1250f / Mathf.Max(1, maxColumn);

        // 连线（画在节点之下）
        foreach (DungeonGraphNode n in graph.Nodes)
            foreach (int nextId in n.NextNodeIds)
            {
                DungeonGraphNode t = graph.Get(nextId);
                if (t == null) continue;
                Vector2 a = NodePos(n, maxColumn), b = NodePos(t, maxColumn);
                CreateLink(panel, (a + b) * 0.5f, Vector2.Distance(a, b),
                    n.Completed && t.Discovered ? gold : new Color(0.35f, 0.35f, 0.35f, 0.7f),
                    Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            }

        DungeonGraphNode currentNode = graph.Get(CurrentNodeId);
        foreach (DungeonGraphNode n in graph.Nodes)
            CreateNode(panel, n, NodePos(n, maxColumn), currentNode);
    }

    private static Vector2 NodePos(DungeonGraphNode n, int maxColumn)
    {
        float colStep = 1250f / Mathf.Max(1, maxColumn);
        float x = -625f + n.Column * colStep;
        float y = (n.Row == 0 ? 1 : -1) * 70f + (n.Column % 2 == 0 ? 0f : 35f);   // 错位排布
        return new Vector2(x, y);
    }

    private static void CreateNode(Transform panel, DungeonGraphNode n, Vector2 pos, DungeonGraphNode currentNode)
    {
        bool isCurrent = n.NodeId == CurrentNodeId;
        bool revealed = n.Discovered;
        // v2.0.5 交互模式：当前节点的直接后继为"可选"
        bool selectable = pickCallback != null && currentNode != null
            && currentNode.NextNodeIds.Contains(n.NodeId);

        var go = new GameObject($"Node_{n.NodeId}_{n.Type}", typeof(Image));
        go.transform.SetParent(panel, false);
        Image img = go.GetComponent<Image>();
        img.sprite = PanelSprite.BtnNormal;
        if (img.sprite != null) img.type = Image.Type.Sliced;
        img.color = !revealed ? undiscovered
            : n.Completed ? visitedGray
            : typeColors[n.Type];
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        img.rectTransform.anchoredPosition = pos;
        img.rectTransform.sizeDelta = new Vector2(74f, 74f);

        string glyph = revealed ? typeGlyphs[n.Type] : "?";
        var text = CreateText(go.transform, "Glyph", glyph, 26, TextAlignmentOptions.Center,
            n.Completed ? new Color(0.7f, 0.7f, 0.7f) : Color.white);
        text.rectTransform.anchoredPosition = Vector2.zero;

        if (selectable)
        {
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            int picked = n.NodeId;
            btn.onClick.AddListener(() =>
            {
                var cb = pickCallback;
                Close();
                cb?.Invoke(picked);
            });

            var border = new GameObject("SelectBorder", typeof(Image));
            border.transform.SetParent(go.transform, false);
            Image borderImg = border.GetComponent<Image>();
            borderImg.color = gold;
            borderImg.rectTransform.anchorMin = borderImg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            borderImg.rectTransform.anchoredPosition = Vector2.zero;
            borderImg.rectTransform.sizeDelta = new Vector2(84f, 84f);
            border.transform.SetAsFirstSibling();
        }

        if (isCurrent)
        {
            var ring = new GameObject("CurrentRing", typeof(Image));
            ring.transform.SetParent(go.transform, false);
            Image ringImg = ring.GetComponent<Image>();
            ringImg.color = gold;
            ringImg.rectTransform.anchorMin = ringImg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ringImg.rectTransform.anchoredPosition = Vector2.zero;
            ringImg.rectTransform.sizeDelta = new Vector2(88f, 88f);
            // 环在下、节点盖上：用同尺寸空心效果（石板风格简化：金色描边方块垫底）
            ring.transform.SetAsFirstSibling();
        }
        items.Add(go);
    }

    private static void CreateLink(Transform panel, Vector2 center, float length, Color color, float angleDeg)
    {
        var go = new GameObject("Link", typeof(Image));
        go.transform.SetParent(panel, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        img.rectTransform.anchoredPosition = center;
        img.rectTransform.sizeDelta = new Vector2(length, 6f);
        img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
        go.transform.SetAsFirstSibling();
        items.Add(go);
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
        text.rectTransform.sizeDelta = new Vector2(200f, 40f);
        return text;
    }
}
