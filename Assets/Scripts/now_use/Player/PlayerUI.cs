using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家固定 UI（屏幕左下角）。
/// 负责显示玩家 HP、护甲与体力（v0.6.0），并监听 Health / PlayerStats 的事件实时刷新。
///
/// 设计要点：
/// 1. Inspector 手动配置优先，符合 Unity 常规工作流。
/// 2. 当 Inspector 引用丢失时，Awake() 会自动恢复引用，避免重新挂载脚本、
///    重新打开场景或版本控制合并后导致引用断裂。
/// 3. 所有关键引用都有空值检查，运行时不应出现 NullReferenceException。
/// </summary>
public class PlayerUI : MonoBehaviour
{
    [Header("UI 引用")]
    [Tooltip("HP 条 Image（Filled）。为空时自动查找 Canvas/PlayerStatsPanel/HPBar。")]
    [SerializeField] private Image hpBar;

    [Tooltip("护甲条 Image（Filled）。为空时自动查找 Canvas/PlayerStatsPanel/ArmorBar。")]
    [SerializeField] private Image armorBar;

    [Tooltip("体力条 Image（Filled，v0.6.0）。为空时自动查找 Canvas/PlayerStatsPanel/StaminaBar；场景里没有则运行时克隆护甲条自动创建（HP 条下方）。")]
    [SerializeField] private Image staminaBar;

    [Header("角色引用")]
    [Tooltip("玩家控制器。为空时优先使用 FindAnyObjectByType，其次按 Tag 'Player' 查找。")]
    [SerializeField] private PlayerController player;

    private Health health;
    private PlayerStats stats;

    /// <summary>
    /// 在 Start 之前完成引用恢复，确保无论脚本执行顺序如何，Start() 里都能拿到有效引用。
    /// </summary>
    void Awake()
    {
        RecoverReferences();
    }

    void Start()
    {
        if (player == null)
        {
            Debug.LogError("[PlayerUI] PlayerController not found. UI will not update.");
            return;
        }

        health = player.GetHealth();
        stats = player.GetStats();

        if (health == null)
        {
            Debug.LogError("[PlayerUI] Health not found.");
            return;
        }

        if (stats == null)
        {
            Debug.LogError("[PlayerUI] PlayerStats not found.");
            return;
        }

        health.OnHealthChanged += OnHealthChanged;
        stats.OnStatsChanged += OnStatsChanged;

        // 体力条位置统一强制到 HP 条正下方（无论来自场景 YAML 还是运行时克隆）
        RepositionStaminaBar();

        // 立即刷新一次，避免事件触发顺序问题导致初始显示不正确
        OnHealthChanged(health.CurrentHealth, health.MaxHealth);
        OnStatsChanged();
    }

    void OnDestroy()
    {
        if (health != null) health.OnHealthChanged -= OnHealthChanged;
        if (stats != null) stats.OnStatsChanged -= OnStatsChanged;
    }

    /// <summary>
    /// 自动恢复关键引用。Inspector 已配置的值不会被覆盖。
    /// </summary>
    private void RecoverReferences()
    {
        RecoverPlayerController();
        RecoverHPBar();
        RecoverArmorBar();
        RecoverStaminaBar();
    }

    private void RecoverPlayerController()
    {
        if (player != null) return;

        // 优先使用新版的类型搜索 API（Unity 2021.2+）
        player = FindAnyObjectByType<PlayerController>();

        // 回退方案：按 Tag 查找（兼容旧版本或特殊场景结构）
        if (player == null)
        {
            GameObject playerGo = GameObject.FindWithTag("Player");
            if (playerGo != null)
            {
                player = playerGo.GetComponent<PlayerController>();
            }
        }

        if (player == null)
        {
            Debug.LogError("[PlayerUI] PlayerController not found.");
        }
    }

    private void RecoverHPBar()
    {
        if (hpBar != null) return;

        // PlayerUI 挂载在 Canvas 上，直接按层级向下查找
        Transform hpTransform = transform.Find("PlayerStatsPanel/HPBar");
        if (hpTransform != null)
        {
            hpBar = hpTransform.GetComponent<Image>();
        }

        if (hpBar == null)
        {
            Debug.LogError("[PlayerUI] HPBar Image not found.");
        }
    }

    private void RecoverArmorBar()
    {
        if (armorBar != null) return;

        Transform armorTransform = transform.Find("PlayerStatsPanel/ArmorBar");
        if (armorTransform != null)
        {
            armorBar = armorTransform.GetComponent<Image>();
        }

        if (armorBar == null)
        {
            Debug.LogError("[PlayerUI] ArmorBar Image not found.");
        }
    }

    private void RecoverStaminaBar()
    {
        if (staminaBar != null) return;

        Transform staminaTransform = transform.Find("PlayerStatsPanel/StaminaBar");
        if (staminaTransform != null)
        {
            staminaBar = staminaTransform.GetComponent<Image>();
        }

        // 场景里没有 StaminaBar 时（v0_5 及更早场景），运行时按现有条样式自动创建一个，
        // 不依赖场景 YAML，保证任何场景打开都有体力条
        if (staminaBar == null)
        {
            staminaBar = CreateStaminaBarAtRuntime();
        }
    }

    /// <summary>
    /// 运行时创建体力条：克隆护甲条（同为 Filled 横条），改黄色、改窄。
    /// 位置统一由 RepositionStaminaBar() 在 Start 阶段强制设置，此处不摆位置。
    /// </summary>
    private Image CreateStaminaBarAtRuntime()
    {
        if (armorBar == null || hpBar == null) return null;

        GameObject go = Instantiate(armorBar.gameObject, armorBar.transform.parent);
        go.name = "StaminaBar";

        Image img = go.GetComponent<Image>();
        img.color = new Color(0.9569f, 0.8157f, 0.2471f);   // #F4D03F
        img.fillAmount = 1f;

        RectTransform rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, 5f);   // 窄条，比护甲条（10）更窄

        return img;
    }

    /// <summary>
    /// 无论体力条来自场景 YAML 还是运行时创建，统一强制摆到 HP 条正下方。
    /// 间距参考护甲条与 HP 条的间距（5px）；HP 条下方空间不足时收紧到面板底边内。
    /// </summary>
    private void RepositionStaminaBar()
    {
        if (staminaBar == null || hpBar == null) return;

        RectTransform rect = (RectTransform)staminaBar.transform;
        RectTransform hpRect = (RectTransform)hpBar.transform;

        // 与 HP 条同一参考系，避免来源不同（场景/克隆）导致锚点不一致
        rect.anchorMin = hpRect.anchorMin;
        rect.anchorMax = hpRect.anchorMax;
        rect.pivot = hpRect.pivot;

        const float spacing = 5f;   // 与护甲条/HP 条间距一致
        float height = rect.sizeDelta.y;
        float y = hpRect.anchoredPosition.y - hpRect.sizeDelta.y * 0.5f - spacing - height * 0.5f;

        // 不超出面板底边（子条锚点在面板纵向中心，底边 anchored y = -面板高/2）
        RectTransform panel = hpRect.parent as RectTransform;
        float panelBottom = panel != null ? -panel.sizeDelta.y * 0.5f : -25f;
        y = Mathf.Max(y, panelBottom + height * 0.5f);

        rect.anchoredPosition = new Vector2(hpRect.anchoredPosition.x, y);
    }

    private void OnHealthChanged(float current, float max)
    {
        if (hpBar == null) return;

        hpBar.fillAmount = max > 0 ? current / max : 0f;
    }

    private void OnStatsChanged()
    {
        if (stats == null) return;

        if (armorBar != null)
            armorBar.fillAmount = stats.MaxArmor > 0 ? stats.CurrentArmor / stats.MaxArmor : 0f;

        if (staminaBar != null)
            staminaBar.fillAmount = stats.MaxStamina > 0 ? stats.CurrentStamina / stats.MaxStamina : 0f;
    }
}
