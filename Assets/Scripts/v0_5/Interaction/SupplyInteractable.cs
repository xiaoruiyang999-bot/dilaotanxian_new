using UnityEngine;

/// <summary>补给类型。Armor 走 PlayerStats.ModifyArmor（现有公开方法）。</summary>
public enum SupplyType { Heal, Armor }

/// <summary>
/// 商店补给基座（免费占位，计划书五-D 范围声明：不做货币/购买结算）：
/// walk-over 拾取，治疗球 +HP / 护甲球 +护甲。货币系统落地时的挂点即本类。
/// </summary>
public class SupplyInteractable : Interactable
{
    [SerializeField] private SupplyType supplyType = SupplyType.Heal;
    [SerializeField] private float amount = 2f;

    protected override void ApplyEffect(Collider2D player)
    {
        switch (supplyType)
        {
            case SupplyType.Heal:
                if (player.TryGetComponent(out Health hp)) hp.Heal(amount);
                Debug.Log($"[Dungeon] 拾取治疗球：HP +{amount}（免费占位）");
                break;
            case SupplyType.Armor:
                if (player.TryGetComponent(out PlayerStats ps)) ps.ModifyArmor(amount);
                Debug.Log($"[Dungeon] 拾取护甲球：护甲 +{amount}（免费占位）");
                break;
        }
    }
}
