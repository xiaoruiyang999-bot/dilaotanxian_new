using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家固定 UI（屏幕左下角）。
/// 负责显示玩家 HP 与护甲，并监听 Health / PlayerStats 的事件实时刷新。
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
        Debug.Log("[PlayerUI] Start");

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

        Debug.Log($"[PlayerUI] PlayerController={player.name}, Health={health}, PlayerStats={stats}");
        Debug.Log($"[PlayerUI] hpBar={hpBar}, armorBar={armorBar}");

        health.OnHealthChanged += OnHealthChanged;
        stats.OnStatsChanged += OnStatsChanged;

        // 立即刷新一次，避免事件触发顺序问题导致初始显示不正确
        OnHealthChanged(health.CurrentHealth, health.MaxHealth);
        OnStatsChanged();

        Debug.Log("[PlayerUI] Initialization Success");
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

    private void OnHealthChanged(float current, float max)
    {
        if (hpBar == null) return;

        hpBar.fillAmount = max > 0 ? current / max : 0f;
    }

    private void OnStatsChanged()
    {
        if (stats == null) return;

        if (armorBar == null) return;

        armorBar.fillAmount = stats.MaxArmor > 0 ? stats.CurrentArmor / stats.MaxArmor : 0f;
    }
}
