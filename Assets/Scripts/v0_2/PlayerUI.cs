using UnityEngine;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
    [Header("UI引用")]
    [SerializeField] private Image hpBar;      // 红色血条Image
    [SerializeField] private Image armorBar;   // 蓝色护甲条Image

    [Header("角色引用")]
    [SerializeField] private WarriorCharacter character;

    void Start()
    {
        // 监听WarriorCharacter的属性变化事件
        if (character != null)
        {
            character.OnStatsChanged += UpdateUI;
            // 初始化显示
            UpdateUI();
        }
        else
        {
            Debug.LogWarning("[PlayerUI] WarriorCharacter reference is missing!");
        }
    }

    void OnDestroy()
    {
        if (character != null)
            character.OnStatsChanged -= UpdateUI;
    }

    private void UpdateUI()
    {
        if (character == null) return;

        // 计算Fill Amount（0~1）
        float hpRatio = character.MaxHP > 0 ? character.CurrentHP / character.MaxHP : 0;
        float armorRatio = character.MaxArmor > 0 ? character.CurrentArmor / character.MaxArmor : 0;

        if (hpBar != null)
            hpBar.fillAmount = hpRatio;

        if (armorBar != null)
            armorBar.fillAmount = armorRatio;
    }
}
