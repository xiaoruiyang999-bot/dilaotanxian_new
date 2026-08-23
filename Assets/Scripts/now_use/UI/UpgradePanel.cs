using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 三选一升级面板（M2·v0.7.1）：timeScale 归零弹出 3 张升级卡，点击应用后恢复。
/// 升级池内置代码（数值可后续搬 SO——v1 六项足够，避免资产创建链）；
/// 触发源：宝箱 roll 出"升级" / 商店购买"升级券"。
/// 挂载同 MinimapSystem 模式（场景空对象），静态 Show 调用。
/// 注意与 HitStop 的 timeScale 冲突已由 HitStop.SuppressByUI 协调。
/// </summary>
public class UpgradePanel : MonoBehaviour
{
    public static UpgradePanel Instance { get; private set; }

    private GameObject panelRoot;
    private PlayerStats stats;
    private Health health;

    private class UpgradeOption
    {
        public string Title;
        public string Desc;
        public System.Action<PlayerStats, Health> Apply;
    }

    private readonly List<UpgradeOption> pool = new List<UpgradeOption>
    {
        new UpgradeOption { Title = "利爪", Desc = "伤害 +20%", Apply = (s, h) => s.AddDamageBonus(0.20f) },
        new UpgradeOption { Title = "迅捷", Desc = "攻击速度 +15%", Apply = (s, h) => s.AddAttackSpeedBonus(0.15f) },
        new UpgradeOption { Title = "疾风", Desc = "移动速度 +10%", Apply = (s, h) => s.AddMoveSpeedBonus(0.10f) },
        new UpgradeOption { Title = "活力", Desc = "最大生命 +20（并治疗 20）", Apply = (s, h) => h.AddMaxHealth(20f, true) },
        new UpgradeOption { Title = "坚甲", Desc = "护甲上限 +20（并补满护甲）", Apply = (s, h) => { s.AddMaxArmor(20f); s.ModifyArmor(99f); } },
        new UpgradeOption { Title = "贪婪", Desc = "立即获得 15 金币", Apply = (s, h) => s.AddCoins(15) },
    };

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            stats = p.GetComponent<PlayerStats>();
            health = p.GetComponent<Health>();
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>弹出三选一（已开着则忽略重复触发）。</summary>
    public static void Show()
    {
        if (Instance == null)
        {
            Debug.LogWarning("[UpgradePanel] 场景中未挂载 UpgradePanel，升级触发被忽略。");
            return;
        }
        if (Instance.panelRoot != null) return;   // 面板已开着
        Instance.BuildPanel();
    }

    private void BuildPanel()
    {
        Time.timeScale = 0f;
        HitStop.SuppressByUI = true;

        // 随机抽 3 个不重复选项
        var options = new List<UpgradeOption>(pool);
        var picked = new List<UpgradeOption>();
        while (picked.Count < 3 && options.Count > 0)
        {
            int i = Random.Range(0, options.Count);
            picked.Add(options[i]);
            options.RemoveAt(i);
        }

        var canvasGo = new GameObject("UpgradeCanvas", typeof(Canvas));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        panelRoot = canvasGo;

        // 全屏半透明遮罩
        Image mask = CreateRect("Mask", canvasGo.transform);
        mask.color = new Color(0f, 0f, 0f, 0.6f);
        mask.rectTransform.anchorMin = Vector2.zero;
        mask.rectTransform.anchorMax = Vector2.one;
        mask.rectTransform.offsetMin = mask.rectTransform.offsetMax = Vector2.zero;

        // 标题
        CreateLabel(canvasGo.transform, "选择一项强化", 26, new Color(1f, 0.85f, 0.35f),
            new Vector2(0.5f, 0.72f), new Vector2(500f, 40f));

        // 3 张卡片
        for (int i = 0; i < picked.Count; i++)
        {
            UpgradeOption opt = picked[i];
            float xOffset = (i - 1) * 280f;

            var btnGo = new GameObject($"Card_{opt.Title}", typeof(Image), typeof(Button));
            btnGo.transform.SetParent(canvasGo.transform, false);
            var btn = btnGo.GetComponent<Button>();
            Image card = btnGo.GetComponent<Image>();
            card.color = new Color(0.18f, 0.16f, 0.12f, 0.95f);
            card.raycastTarget = true;
            card.rectTransform.anchorMin = card.rectTransform.anchorMax = new Vector2(0.5f, 0.42f);
            card.rectTransform.anchoredPosition = new Vector2(xOffset, 0f);
            card.rectTransform.sizeDelta = new Vector2(240f, 150f);

            var colors = btn.colors;
            colors.highlightedColor = new Color(0.35f, 0.3f, 0.18f, 1f);
            colors.pressedColor = new Color(0.25f, 0.22f, 0.12f, 1f);
            btn.colors = colors;

            CreateLabel(card.rectTransform, opt.Title, 22, Color.white,
                new Vector2(0.5f, 0.68f), new Vector2(200f, 30f));
            CreateLabel(card.rectTransform, opt.Desc, 15, new Color(0.8f, 0.78f, 0.7f),
                new Vector2(0.5f, 0.38f), new Vector2(210f, 44f));

            UpgradeOption captured = opt;
            btn.onClick.AddListener(() => ApplyChoice(captured));
        }
    }

    private void ApplyChoice(UpgradeOption opt)
    {
        opt.Apply?.Invoke(stats, health);
        AudioManager.PlaySFX("chest");
        Destroy(panelRoot);
        panelRoot = null;
        HitStop.SuppressByUI = false;
        Time.timeScale = 1f;
        Debug.Log($"[Upgrade] 已选择：{opt.Title}（{opt.Desc}）");
    }

    private static Image CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        return go.GetComponent<Image>();
    }

    private static Text CreateLabel(Transform parent, string text, int size, Color color,
        Vector2 anchor, Vector2 sizeDelta)
    {
        var go = new GameObject("Label", typeof(Text));
        go.transform.SetParent(parent, false);
        Text t = go.GetComponent<Text>();
        t.font = MinimapController.BuiltinFont;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.text = text;
        t.raycastTarget = false;
        t.rectTransform.anchorMin = t.rectTransform.anchorMax = anchor;
        t.rectTransform.anchoredPosition = Vector2.zero;
        t.rectTransform.sizeDelta = sizeDelta;
        return t;
    }
}
