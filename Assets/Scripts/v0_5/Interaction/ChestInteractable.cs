using UnityEngine;

/// <summary>
/// 宝箱（占位，计划书五-D）：walk-over 开启，+2 HP 治疗（调现有 Health.Heal），日志提示。
/// 奖励从占位变真货的挂点即 ApplyEffect（未来接技能/装备系统）。
/// </summary>
public class ChestInteractable : Interactable
{
    [SerializeField] private float healAmount = 2f;

    protected override void ApplyEffect(Collider2D player)
    {
        if (player.TryGetComponent(out Health hp)) hp.Heal(healAmount);
        Debug.Log($"[Dungeon] 宝箱开启：HP +{healAmount}（占位奖励）");
    }
}
