using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 技能树页面（v1.1.47，M4 meta 层 UI）：石板素材整版页面，静态 Open/Close（ClassSelectUI 模式）。
/// 布局：三条支线（力量/迅捷/坚韧）× 四层，线内线性前置，跨线独立——节点=三态石板按钮 +
/// 支线色图标 + 名称/效果/消耗；父子连线按解锁态点亮（金=已通，灰=未通）。
/// 状态：已解锁（暖金 tint + "已解锁"）/ 可解锁（正常石板三态，显示"◆ 魂晶 N"）/
/// 锁定（整体压暗，显示前置名）。点击可解锁节点 → 校验前置+余额 → SkillTreeSave.Unlock +
/// PlayerStats.RefreshSkillTreeBonuses（局内加点即时生效；HP 节点回满血=设计选择：加点视为休整）。
/// 入口：准备室技能石碑（E 交互）+ 暂停菜单"技能树"按钮。Esc 关闭走 PlayerController Cancel 链。
/// 不碰 timeScale：从暂停菜单进入时由暂停面板（sortingOrder 220）继续管停时，本页 230 盖其上。
/// </summary>
public static class SkillTreeUI
{
    private static GameObject canvasGo;
    private static TMP_Text essenceLabel;
    private static TMP_Text hintLabel;
    private static readonly List<GameObject> treeItems = new List<GameObject>();

    private static readonly Color gold = new Color(1f, 0.82f, 0.35f);
    private static readonly Color warmWhite = new Color(1f, 0.95f, 0.8f);
    private static readonly Color dimGray = new Color(0.45f, 0.45f, 0.45f);
    private static readonly Color hintNormal = new Color(0.85f, 0.8f, 0.7f);
    private static readonly Color hintError = new Color(0.95f, 0.4f, 0.35f);

    private static readonly Vector2 NodeSize = new Vector2(280f, 170f);
    private const float NodeGap = 62f;   // 节点水平间距（含连线区）

    public static bool IsOpen => canvasGo != null;

    public static void Open()
    {
        if (canvasGo != null) return;
        EnsureEventSystem();

        canvasGo = new GameObject("SkillTreeCanvas", typeof(Canvas));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 230;   // 高于 PausePanel(220)：可从暂停菜单直接进入
        PanelSprite.ConfigureCanvasScaler(canvasGo);
        canvasGo.AddComponent<GraphicRaycaster>();   // v1.1.12：无它按钮全灭而 Esc 正常

        // 全屏暗化 Mask（吃掉背景点击，不做按钮——关闭走 Esc/返回键）
        Image mask = new GameObject("Mask", typeof(Image)).GetComponent<Image>();
        mask.transform.SetParent(canvasGo.transform, false);
        mask.color = new Color(0f, 0f, 0f, 0.6f);
        StretchFill(mask.rectTransform);

        // 石板主面板
        var panel = new GameObject("StonePanel", typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        Image panelImage = panel.GetComponent<Image>();
        PanelSprite.ApplyStonePanel(panelImage, new Color(0.07f, 0.06f, 0.05f, 0.97f));
        PlaceUI(panelImage.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560f, 860f));

        // 标题 + 魂晶余额
        CreateText(panel.transform, "Title", "技 能 树", 40, TextAlignmentOptions.Center, warmWhite)
            .rectTransform.anchoredPosition = new Vector2(0f, 380f);
        essenceLabel = CreateText(panel.transform, "Essence", "", 26, TextAlignmentOptions.Center, gold);
        essenceLabel.rectTransform.anchoredPosition = new Vector2(0f, 328f);

        // 底部：提示行 + 返回按钮
        hintLabel = CreateText(panel.transform, "Hint", "消耗魂晶解锁永久强化 · 击杀敌人积累魂晶（死亡结算入账）",
            18, TextAlignmentOptions.Center, hintNormal);
        hintLabel.rectTransform.sizeDelta = new Vector2(900f, 30f);
        hintLabel.rectTransform.anchoredPosition = new Vector2(-130f, -372f);

        var closeBtn = CreateStoneButton(panel.transform, "返 回", new Vector2(560f, -370f), new Vector2(260f, 64f));
        closeBtn.onClick.AddListener(Close);

        // v1.1.51：右上角叉除键（同 返回，关技能树页面）
        UIHelper.CreateCloseButton(panel.transform, Close);

        Rebuild();
    }

    public static void Close()
    {
        if (canvasGo == null) return;
        Object.Destroy(canvasGo);
        canvasGo = null;
        essenceLabel = null;
        hintLabel = null;
        treeItems.Clear();
    }

    /// <summary>整树重建（解锁操作低频，直接清空重画最稳）。</summary>
    private static void Rebuild()
    {
        foreach (GameObject go in treeItems) if (go != null) Object.Destroy(go);
        treeItems.Clear();
        if (canvasGo == null) return;

        essenceLabel.text = $"魂晶  ◆ {SkillTreeSave.Essence}";

        var unlocked = SkillTreeSave.UnlockedIds;
        var panel = canvasGo.transform.Find("StonePanel");

        // 三支线行（垂直间隔 240）
        for (int branch = 0; branch < SkillTreeDef.BranchCount; branch++)
        {
            float rowY = 190f - branch * 240f;
            var branchName = CreateText(panel, $"Branch{branch}", SkillTreeDef.BranchNames[branch],
                28, TextAlignmentOptions.Center, SkillTreeDef.BranchColors[branch]);
            branchName.rectTransform.anchoredPosition = new Vector2(-675f, rowY);

            for (int tier = 0; tier < SkillTreeDef.TierCount; tier++)
            {
                var node = FindNode(branch, tier);
                if (node == null) continue;
                float x = -490f + tier * (NodeSize.x + NodeGap);
                CreateNode(panel, node, new Vector2(x, rowY), unlocked);

                // 连线：指向下一层（画在本层节点右侧；z 序在节点之下——先入列）
                if (tier < SkillTreeDef.TierCount - 1)
                {
                    var next = FindNode(branch, tier + 1);
                    bool lit = SkillTreeSave.IsUnlocked(node.id) && next != null && SkillTreeSave.IsUnlocked(next.id);
                    CreateLink(panel, new Vector2(x + NodeSize.x / 2f + NodeGap / 2f, rowY),
                        lit, SkillTreeDef.BranchColors[branch]);
                }
            }
        }
    }

    private static SkillTreeNodeDef FindNode(int branch, int tier)
    {
        foreach (var n in SkillTreeDef.Nodes)
            if (n.branch == branch && n.tier == tier) return n;
        return null;
    }

    private static void CreateNode(Transform panel, SkillTreeNodeDef node, Vector2 pos, IReadOnlyList<int> unlocked)
    {
        bool isUnlocked = SkillTreeSave.IsUnlocked(node.id);
        bool canUnlock = !isUnlocked && SkillTreeDef.RequirementMet(node, unlocked);

        var go = new GameObject($"Node_{node.id}_{node.name}", typeof(Image));
        go.transform.SetParent(panel, false);
        Image img = go.GetComponent<Image>();
        // 底图=按钮常态石板切片（ApplyStoneButton 需配对 Button 且会重置 color，此处手动挂底，
        // 素材缺失回退纯色；三态色直接染在 img.color 上，hover 反馈由 Button 默认 ColorTint 叠加）
        img.sprite = PanelSprite.BtnNormal;
        if (img.sprite != null) img.type = Image.Type.Sliced;
        img.color = isUnlocked ? warmWhite : (canUnlock ? Color.white : dimGray);
        PlaceUI(img.rectTransform, new Vector2(0.5f, 0.5f), pos, NodeSize);

        // 只有可解锁节点可交互（锁定纯图无 hover=不可用语义；已解锁无需再点）。
        // v1.1.47.1 三态石板全量接入：Button SpriteSwap 换 normal/hover/pressed 三图
        //（ApplyStoneButton 配对使用，img.color=white 让三图明暗原样呈现）
        if (canUnlock)
        {
            Button btn = go.AddComponent<Button>();
            PanelSprite.ApplyStoneButton(btn, img, dimGray);
            btn.onClick.AddListener(() => OnNodeClick(node));
        }

        // 支线色图标块
        var icon = new GameObject("Icon", typeof(Image));
        icon.transform.SetParent(go.transform, false);
        var iconImg = icon.GetComponent<Image>();
        iconImg.raycastTarget = false;   // 图标不得拦截节点点击
        iconImg.color = isUnlocked || canUnlock
            ? SkillTreeDef.BranchColors[node.branch] : dimGray;
        PlaceUI(iconImg.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, 56f), new Vector2(42f, 42f));

        var nameText = CreateText(go.transform, "Name", node.name, 22, TextAlignmentOptions.Center,
            isUnlocked || canUnlock ? Color.white : dimGray);
        nameText.rectTransform.anchoredPosition = new Vector2(0f, 16f);

        var effectText = CreateText(go.transform, "Effect", SkillTreeDef.Describe(node), 19,
            TextAlignmentOptions.Center, isUnlocked || canUnlock ? SkillTreeDef.BranchColors[node.branch] : dimGray);
        effectText.rectTransform.anchoredPosition = new Vector2(0f, -18f);

        string status = isUnlocked ? "已解锁" : (canUnlock ? $"◆ {node.cost}" : $"需 {SkillTreeDef.Find(node.requiresId)?.name}");
        var statusText = CreateText(go.transform, "Status", status, 18, TextAlignmentOptions.Center,
            isUnlocked ? gold : (canUnlock ? gold : dimGray));
        statusText.rectTransform.anchoredPosition = new Vector2(0f, -54f);

        treeItems.Add(go);
    }

    private static void CreateLink(Transform panel, Vector2 center, bool lit, Color branchColor)
    {
        var go = new GameObject("Link", typeof(Image));
        go.transform.SetParent(panel, false);
        var img = go.GetComponent<Image>();
        // 点亮=金；未点亮=支线色暗阶（低饱和底不抢节点）
        img.color = lit ? gold
            : new Color(branchColor.r * 0.35f, branchColor.g * 0.35f, branchColor.b * 0.35f, 0.9f);
        PlaceUI(img.rectTransform, new Vector2(0.5f, 0.5f), center, new Vector2(NodeGap, 10f));
        go.transform.SetAsFirstSibling();   // 线在所有节点之下
        treeItems.Add(go);
    }

    private static void OnNodeClick(SkillTreeNodeDef node)
    {
        if (canvasGo == null) return;
        var unlocked = SkillTreeSave.UnlockedIds;

        if (SkillTreeSave.IsUnlocked(node.id)) { ShowHint($"{node.name} 已解锁", hintNormal); return; }
        if (!SkillTreeDef.RequirementMet(node, unlocked))
        {
            ShowHint($"需先解锁：{SkillTreeDef.Find(node.requiresId)?.name}", hintError);
            return;
        }
        if (SkillTreeSave.Essence < node.cost) { ShowHint("魂晶不足——进入地牢击杀敌人积累", hintError); return; }

        if (!SkillTreeSave.TrySpendEssence(node.cost)) return;
        SkillTreeSave.Unlock(node.id);

        // 局内即时生效（准备室/地牢都有玩家实例；不在场也不影响——下局 ApplyClass 聚合）
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        var stats = p != null ? p.GetComponent<PlayerStats>() : null;
        if (stats != null) stats.RefreshSkillTreeBonuses();

        ShowHint($"已解锁：{node.name}（{SkillTreeDef.Describe(node)}）", gold);
        Rebuild();
    }

    private static void ShowHint(string text, Color color)
    {
        if (hintLabel == null) return;
        hintLabel.text = text;
        hintLabel.color = color;
    }

    // ---------- 石板 UI 工具（照 ClassSelectUI/PausePanel 已验证模式） ----------

    private static Button CreateStoneButton(Transform parent, string label, Vector2 pos, Vector2 size)
    {
        var go = new GameObject($"Btn_{label}", typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        // v1.1.47.1 返回按钮同样走三态石板（SpriteSwap normal/hover/pressed）
        Button btn = go.AddComponent<Button>();
        PanelSprite.ApplyStoneButton(btn, img, new Color(0.35f, 0.3f, 0.25f, 0.95f));
        PlaceUI(img.rectTransform, new Vector2(0.5f, 0.5f), pos, size);

        btn.targetGraphic = img;
        var text = CreateText(go.transform, "Label", label, 26, TextAlignmentOptions.Center, warmWhite);
        text.rectTransform.sizeDelta = size;
        return btn;
    }

    /// <summary>TMP 文本（v1.0.8 规范：无参 GO + 单次 AddComponent + 先 text 后 font）。
    /// raycastTarget=false：节点上的文字/图标不得拦截 Button 点击。</summary>
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
        text.rectTransform.sizeDelta = new Vector2(400f, 40f);
        return text;
    }

    private static void PlaceUI(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void StretchFill(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.one * -inset;
        rt.offsetMax = Vector2.one * inset;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        Object.DontDestroyOnLoad(es);
        es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
    }
}
