using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 弹药面板（v0.6.3）：武器色块 + "武器名  x/y" + 换弹进度细条。
/// 挂 Canvas，全部视觉运行时构建（创建后不再碰既有 UI 的 Transform，自检清单 #15），
/// 订阅 PlayerCombat 的弹药/换弹/武器展示事件实时刷新。
/// 默认近战 / 无弹夹武器（max = 0）时整面板隐藏。
/// </summary>
public class AmmoUI : MonoBehaviour
{
    [Header("布局")]
    [Tooltip("面板相对参考位置（PlayerStatsPanel 上方）的微调偏移")]
    [SerializeField] private Vector2 panelOffset = new Vector2(0f, 6f);

    [Header("角色引用")]
    [Tooltip("玩家战斗组件。为空时自动 FindAnyObjectByType 查找。")]
    [SerializeField] private PlayerCombat combat;

    private GameObject panel;
    private Image colorBlock;
    private TMP_Text label;
    private GameObject reloadBar;
    private Image reloadFill;

    private string weaponName;
    private int ammoCurrent;
    private int ammoMax;

    void Start()
    {
        // 引用恢复（PlayerUI 同款风格）：Inspector 未配置时自动查找
        if (combat == null)
            combat = FindAnyObjectByType<PlayerCombat>();
        if (combat == null)
        {
            Debug.LogWarning("[AmmoUI] 未找到 PlayerCombat，弹药面板不会更新。", this);
            return;
        }

        BuildPanel();

        combat.OnAmmoChanged += HandleAmmoChanged;
        combat.OnReloadProgress += HandleReloadProgress;
        combat.OnWeaponDisplayChanged += HandleWeaponDisplayChanged;
    }

    void OnDestroy()
    {
        if (combat == null) return;
        combat.OnAmmoChanged -= HandleAmmoChanged;
        combat.OnReloadProgress -= HandleReloadProgress;
        combat.OnWeaponDisplayChanged -= HandleWeaponDisplayChanged;
    }

    // ========== 事件响应 ==========

    private void HandleWeaponDisplayChanged(string name, Color color)
    {
        weaponName = name;
        if (colorBlock != null)
            colorBlock.color = color;
        Refresh();
    }

    private void HandleAmmoChanged(int current, int max)
    {
        ammoCurrent = current;
        ammoMax = max;
        Refresh();
    }

    private void HandleReloadProgress(float remaining, float total)
    {
        if (reloadBar == null || reloadFill == null) return;

        bool reloading = remaining > 0f && total > 0f;
        reloadBar.SetActive(reloading);   // 非换弹隐藏（含结束时的 (0,total)）
        if (reloading)
            reloadFill.fillAmount = 1f - remaining / total;
    }

    /// <summary>无武器名（默认近战/未装备）或无弹夹（max &lt;= 0）→ 整面板隐藏。</summary>
    private void Refresh()
    {
        if (panel == null) return;

        bool visible = !string.IsNullOrEmpty(weaponName) && ammoMax > 0;
        panel.SetActive(visible);
        if (visible && label != null)
            label.text = $"{weaponName}  {ammoCurrent}/{ammoMax}";
    }

    // ========== 运行时构建 ==========

    private void BuildPanel()
    {
        const float panelWidth = 200f;
        const float panelHeight = 40f;

        panel = new GameObject("AmmoPanel");
        panel.transform.SetParent(transform, false);
        RectTransform rect = panel.AddComponent<RectTransform>();

        // 定位在屏幕左下 PlayerStatsPanel 上方；不存在则左下兜底。只读其位置，不改其 Transform。
        RectTransform statsPanel = transform.Find("PlayerStatsPanel") as RectTransform;
        if (statsPanel != null)
        {
            rect.anchorMin = statsPanel.anchorMin;
            rect.anchorMax = statsPanel.anchorMax;
            rect.pivot = statsPanel.pivot;
            rect.sizeDelta = new Vector2(panelWidth, panelHeight);
            rect.anchoredPosition = statsPanel.anchoredPosition
                + new Vector2(0f, statsPanel.sizeDelta.y * 0.5f + panelHeight * 0.5f)
                + panelOffset;
        }
        else
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(panelWidth, panelHeight);
            rect.anchoredPosition = new Vector2(20f, 120f) + panelOffset;
        }

        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.14f, 0.85f);   // 深色半透明底

        // 武器色块（OnWeaponDisplayChanged 的染色）
        GameObject block = CreateUIObject("ColorBlock", panel.transform);
        colorBlock = block.AddComponent<Image>();
        colorBlock.color = Color.white;
        RectTransform blockRect = (RectTransform)block.transform;
        blockRect.anchorMin = new Vector2(0f, 0.5f);
        blockRect.anchorMax = new Vector2(0f, 0.5f);
        blockRect.pivot = new Vector2(0f, 0.5f);
        blockRect.sizeDelta = new Vector2(16f, 16f);
        blockRect.anchoredPosition = new Vector2(8f, 3f);

        // 文字 "武器名  x/y"
        GameObject textGo = CreateUIObject("Label", panel.transform);
        label = textGo.AddComponent<TextMeshProUGUI>();
        label.font = TMPFontProvider.Font;
        label.fontSize = 14;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = Color.white;
        RectTransform textRect = (RectTransform)textGo.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(30f, 8f);
        textRect.offsetMax = new Vector2(-6f, -8f);

        // 换弹进度细条（Image Filled，OnReloadProgress 驱动，非换弹隐藏）
        reloadBar = CreateUIObject("ReloadBar", panel.transform);
        Image reloadBg = reloadBar.AddComponent<Image>();
        reloadBg.color = new Color(1f, 1f, 1f, 0.15f);
        RectTransform reloadRect = (RectTransform)reloadBar.transform;
        reloadRect.anchorMin = new Vector2(0f, 0f);
        reloadRect.anchorMax = new Vector2(1f, 0f);
        reloadRect.pivot = new Vector2(0.5f, 0f);
        reloadRect.sizeDelta = new Vector2(-12f, 5f);
        reloadRect.anchoredPosition = new Vector2(0f, 4f);

        GameObject fillGo = CreateUIObject("Fill", reloadBar.transform);
        reloadFill = fillGo.AddComponent<Image>();
        reloadFill.color = new Color(0.9569f, 0.8157f, 0.2471f);   // #F4D03F（与体力条同色）
        reloadFill.type = Image.Type.Filled;
        reloadFill.fillMethod = Image.FillMethod.Horizontal;
        reloadFill.fillAmount = 0f;
        StretchFill((RectTransform)fillGo.transform, 0f);

        reloadBar.SetActive(false);
        panel.SetActive(false);   // 初始隐藏，等武器事件驱动
    }

    // ========== UI 工具 ==========

    private GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private void StretchFill(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }
}
