using UnityEngine;

/// <summary>
/// 事件祭坛（占位，计划书五-D）：按 E 触发（v0.6.1），随机 ±（治疗 2 / 受伤 1），日志提示。
/// ± 结果属运行时事件（同 v0.5.2 第十章 DropEffect 先例），不进种子流，用 UnityEngine.Random。
/// 受伤走 Health.TakeDamage → 减伤甲结算（v0.7.1）：有甲时自伤按 R 减免并扣甲，属预期行为（v0.7.1 计划书 §2.4）。
/// </summary>
public class ShrineInteractable : Interactable
{
    [SerializeField] private float healAmount = 2f;
    [SerializeField] private float damageAmount = 1f;

    protected override void ApplyEffect(Collider2D player)
    {
        bool bless = UnityEngine.Random.value < 0.5f;
        if (bless)
        {
            if (player.TryGetComponent(out Health hp)) hp.Heal(healAmount);
            Debug.Log($"[Dungeon] 祭坛祝福：HP +{healAmount}");
        }
        else
        {
            if (player.TryGetComponent(out Health hp)) hp.TakeDamage(damageAmount);
            Debug.Log($"[Dungeon] 祭坛反噬：受到伤害 {damageAmount}（有甲走减伤甲结算）");
        }
    }
}
