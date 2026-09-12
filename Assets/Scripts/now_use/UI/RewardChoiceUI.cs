using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// v2.0.6 第二批 奖励三选一（V2 §8.4）：石板三卡界面。UI 只呈现与回调选中项，
/// 应用效果由调用方（DungeonNodeRunner）执行——UI 不写数值（分层红线）。
/// 调用链：战斗/精英节点完成 → Show → 玩家点选 → onPicked → 关闭后 Runner 再弹 DAG 路线选择。
/// </summary>
public static class RewardChoiceUI
{
    private static GameObject canvasGo;
    public static bool IsOpen => canvasGo != null;

    public static void Show(List<NodeRewardService.RewardOption> options,
        System.Action<NodeRewardService.RewardOption> onPicked)
    {
        if (IsOpen || options == null || options.Count == 0) return;

        canvasGo = new GameObject("RewardChoiceCanvas", typeof(Canvas));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 240;   // 地图选择(225)之上
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
        PanelSprite.ApplyStonePanel(img, new Color(0.06f, 0.06f, 0.07f, 0.96f));
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        img.rectTransform.sizeDelta = new Vector2(1100f, 460f);

        var title = CreateText(panel.transform, "Title", "选 择 奖 励", 34, TextAlignmentOptions.Center, Color.white);
        title.rectTransform.anchoredPosition = new Vector2(0f, 175f);

        // 三卡横排
        float cardW = 300f, gap = 40f;
        for (int i = 0; i < options.Count; i++)
        {
            NodeRewardService.RewardOption opt = options[i];
            float x = (i - (options.Count - 1) * 0.5f) * (cardW + gap);
            CreateCard(panel.transform, opt, new Vector2(x, -40f), () =>
            {
                Close();
                onPicked?.Invoke(opt);
            });
        }

        var hint = CreateText(panel.transform, "Hint", "点击一项带走 · 关闭即视为放弃其余",
            15, TextAlignmentOptions.Center, new Color(0.8f, 0.78f, 0.7f));
        hint.rectTransform.anchoredPosition = new Vector2(0f, -185f);
    }

    public static void Close()
    {
        if (canvasGo == null) return;
        Object.Destroy(canvasGo);
        canvasGo = null;
    }

    private static void CreateCard(Transform panel, NodeRewardService.RewardOption opt, Vector2 pos,
        System.Action onClick)
    {
        var go = new GameObject($"Card_{opt.Kind}", typeof(Image));
        go.transform.SetParent(panel, false);
        Image img = go.GetComponent<Image>();
        img.sprite = PanelSprite.BtnNormal;
        if (img.sprite != null) img.type = Image.Type.Sliced;
        img.color = Color.white;
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        img.rectTransform.anchoredPosition = pos;
        img.rectTransform.sizeDelta = new Vector2(300f, 260f);

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());

        // 类型色条（顶部）
        var band = new GameObject("Band", typeof(Image));
        band.transform.SetParent(go.transform, false);
        Image bandImg = band.GetComponent<Image>();
        bandImg.color = opt.Color;
        bandImg.raycastTarget = false;
        bandImg.rectTransform.anchorMin = new Vector2(0f, 1f);
        bandImg.rectTransform.anchorMax = new Vector2(1f, 1f);
        bandImg.rectTransform.anchoredPosition = new Vector2(0f, -14f);
        bandImg.rectTransform.sizeDelta = new Vector2(0f, 28f);

        var nameText = CreateText(go.transform, "Name", opt.Title, 26, TextAlignmentOptions.Center, Color.white);
        nameText.rectTransform.anchoredPosition = new Vector2(0f, 55f);
        var descText = CreateText(go.transform, "Desc", opt.Description, 20, TextAlignmentOptions.Center, opt.Color);
        descText.rectTransform.sizeDelta = new Vector2(270f, 60f);
        descText.rectTransform.anchoredPosition = new Vector2(0f, -25f);
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
        text.rectTransform.sizeDelta = new Vector2(400f, 40f);
        return text;
    }
}
