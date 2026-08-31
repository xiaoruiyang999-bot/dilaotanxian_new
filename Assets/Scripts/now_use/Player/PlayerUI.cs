using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家固定 UI（屏幕左下角）。
/// 负责显示玩家 HP、护甲与法力（v0.6.2），并监听 Health / PlayerStats 的事件实时刷新。
/// v0.7.0：体力条下线——代码不再创建/更新体力条；场景 YAML 里残留的 StaminaBar 对象运行时隐藏
/// （布局归场景编辑，代码不删场景对象）。
///
/// 设计要点：
/// 1. Inspector 手动配置优先，符合 Unity 常规工作流。
/// 2. 当 Inspector 引用丢失时，Awake() 会自动恢复引用，避免重新挂载脚本、
///    重新打开场景或版本控制合并后导致引用断裂。
/// 3. 所有关键引用都有空值检查，运行时不应出现 NullReferenceException。
/// 4. 布局归场景编辑：代码只在场景缺失条时兜底创建（创建时给整齐默认布局），
///    创建后永不改任何已存在条的 Transform（自检清单 #15）。
/// </summary>
public class PlayerUI : MonoBehaviour
{
    [Header("UI 引用")]
    [Tooltip("HP 条 Image（Filled）。为空时自动查找 Canvas/PlayerStatsPanel/HPBar。")]
    [SerializeField] private Image hpBar;

    [Tooltip("护甲条 Image（Filled）。为空时自动查找 Canvas/PlayerStatsPanel/ArmorBar。")]
    [SerializeField] private Image armorBar;

    [Tooltip("法力条 Image（Filled，v0.6.2）。为空时自动查找 Canvas/PlayerStatsPanel/ManaBar；场景里没有则运行时克隆护甲条自动创建（护甲条下方）。")]
    [SerializeField] private Image manaBar;

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
        HideLegacyStaminaBar();
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
        RecoverManaBar();
    }

    /// <summary>
    /// v0.7.0：体力条已下线。场景 YAML 里残留的 StaminaBar 对象运行时隐藏
    /// （代码不删场景对象，用户可在编辑器方便时手动删除，见 v0.7.0 计划书 §五）。
    /// </summary>
    private void HideLegacyStaminaBar()
    {
        Transform t = transform.Find("PlayerStatsPanel/StaminaBar");
        if (t != null)
            t.gameObject.SetActive(false);
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

    private void RecoverManaBar()
    {
        if (manaBar != null) return;

        Transform manaTransform = transform.Find("PlayerStatsPanel/ManaBar");
        if (manaTransform != null)
        {
            manaBar = manaTransform.GetComponent<Image>();
        }

        // 场景里没有 ManaBar 时，运行时兜底创建一条（默认整齐布局）
        if (manaBar == null)
        {
            manaBar = CreateBarBelow(armorBar, armorBar,
                "ManaBar", new Color(0.2039f, 0.5961f, 0.8588f));   // #3498DB
        }
    }

    /// <summary>
    /// 运行时兜底创建状态条：克隆模板条，创建时给整齐的默认布局
    /// （与上方条同宽同锚点、左缘对齐、间距 2、5px 窄条）。
    /// 创建后不再触碰其 Transform——布局归场景编辑，代码永不改已存在条的位置（自检清单 #15）。
    /// </summary>
    private Image CreateBarBelow(Image templateBar, Image aboveBar, string name, Color color)
    {
        if (templateBar == null || aboveBar == null) return null;

        GameObject go = Instantiate(templateBar.gameObject, templateBar.transform.parent);
        go.name = name;

        Image img = go.GetComponent<Image>();
        img.color = color;
        img.fillAmount = 1f;

        RectTransform rect = (RectTransform)go.transform;
        RectTransform aboveRect = (RectTransform)aboveBar.transform;
        const float height = 5f;
        const float spacing = 2f;
        rect.anchorMin = aboveRect.anchorMin;
        rect.anchorMax = aboveRect.anchorMax;
        rect.pivot = aboveRect.pivot;
        rect.sizeDelta = new Vector2(aboveRect.sizeDelta.x, height);
        rect.anchoredPosition = new Vector2(
            aboveRect.anchoredPosition.x,
            aboveRect.anchoredPosition.y - aboveRect.sizeDelta.y * 0.5f - spacing - height * 0.5f);

        return img;
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

        if (manaBar != null)
            manaBar.fillAmount = stats.MaxMana > 0 ? stats.CurrentMana / stats.MaxMana : 0f;
    }
}
