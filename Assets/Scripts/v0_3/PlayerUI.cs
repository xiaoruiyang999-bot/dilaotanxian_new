using UnityEngine;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
    [Header("UI引用")]
    [SerializeField] private Image hpBar;
    [SerializeField] private Image armorBar;

    [Header("角色引用")]
    [SerializeField] private PlayerController player;

    private Health health;
    private PlayerStats stats;

    void Start()
    {
        if (player == null) { Debug.LogWarning("[PlayerUI] PlayerController未设置"); return; }

        health = player.GetHealth();
        stats = player.GetStats();

        if (health != null)
        {
            health.OnHealthChanged += OnHealthChanged;
            OnHealthChanged(health.CurrentHealth, health.MaxHealth);
        }

        if (stats != null)
        {
            stats.OnStatsChanged += OnStatsChanged;
            OnStatsChanged();
        }
    }

    void OnDestroy()
    {
        if (health != null) health.OnHealthChanged -= OnHealthChanged;
        if (stats != null) stats.OnStatsChanged -= OnStatsChanged;
    }

    private void OnHealthChanged(float current, float max)
    {
        if (hpBar != null) hpBar.fillAmount = max > 0 ? current / max : 0f;
    }

    private void OnStatsChanged()
    {
        if (armorBar != null && stats != null)
            armorBar.fillAmount = stats.MaxArmor > 0 ? stats.CurrentArmor / stats.MaxArmor : 0f;
    }
}
