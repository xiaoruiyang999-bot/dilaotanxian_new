using UnityEngine;

/// <summary>补给类型。Armor 走 PlayerStats.ModifyArmor（现有公开方法）；Mana 走 PlayerStats.AddMana（v0.6.3）。</summary>
public enum SupplyType { Heal, Armor, Mana }

/// <summary>
/// 商店补给基座（免费占位，计划书五-D 范围声明：不做货币/购买结算）：
/// 按 E 拾取（v0.6.1），治疗球 +HP / 护甲球 +护甲 / 法力瓶 +法力（v0.6.3）。货币系统落地时的挂点即本类。
/// </summary>
public class SupplyInteractable : Interactable
{
    [SerializeField] private SupplyType supplyType = SupplyType.Heal;
    [SerializeField] private float amount = 2f;

    /// <summary>补给类型（v0.7.5 商店陈列改造：InteractableSpawner 据类型映射正式消耗包）。</summary>
    public SupplyType Type => supplyType;

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
            case SupplyType.Mana:
                if (player.TryGetComponent(out PlayerStats mps)) mps.AddMana(amount);
                Debug.Log($"[Dungeon] 拾取法力瓶：法力 +{amount}（免费占位）");
                break;
        }
    }
}
