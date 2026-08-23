using DG.Tweening;
using UnityEngine;

/// <summary>
/// 宝箱（v0.5.4 三段式：暗盒身 + 亮金上下两片盖）：walk-over → 盖片上下分开（拉开后仍与盒身保持重合区）
/// → 缺口中央刷新道具占位（pop-in）→ 结算奖励（+2 HP 占位）→ 道具展示后淡出，箱子保持开启态。
/// 奖励从占位变真货的挂点即 ApplyEffect（未来接技能/装备系统）。
/// </summary>
public class ChestInteractable : Interactable
{
    [SerializeField] private float healAmount = 20f;

    [Header("掉落表（M2·v0.7.1：权重替代单一占位治疗）")]
    [Tooltip("金币掉落的权重（散落 6~10 枚）")]
    [SerializeField, Min(0)] private float coinWeight = 50f;
    [Tooltip("治疗权重（+2 HP，旧占位行为降级为表中一项）")]
    [SerializeField, Min(0)] private float healWeight = 30f;
    [Tooltip("三选一升级权重")]
    [SerializeField, Min(0)] private float upgradeWeight = 20f;
    [Tooltip("金币掉落枚数区间")]
    [SerializeField, Min(1)] private int coinsMin = 6;
    [SerializeField, Min(1)] private int coinsMax = 10;

    [Header("开箱动画")]
    [SerializeField] private Transform lidTop;
    [SerializeField] private Transform lidBottom;
    [SerializeField] private GameObject itemPrefab;
    [Tooltip("盖片平移距离：盒身高 0.6、盖片各高 0.3，移 0.25 后与盒身保持 0.05 重合区")]
    [SerializeField] private float lidOffset = 0.25f;
    [SerializeField] private float openDuration = 0.35f;

    protected override void OnConsumed(Collider2D player)
    {
        // 开箱反馈（M1.5·v0.6.1）
        AudioManager.PlaySFX("chest");

        // v0.7.1 还债（M2.7）：奖励结算从开盖动画的 OnComplete 提前到触碰瞬间——
        // 之前若 0.35s 动画期间踩传送门切层，OnComplete 被 SetLink 杀掉，奖励被吞且 consumed 已置位。
        // 动画降级为纯视觉表现（SpawnItem 的展示弹跳保留）。
        RollAndApply(player);

        if (lidTop == null || lidBottom == null)
        {
            SetConsumedVisual();
            return;
        }
        lidTop.DOLocalMoveY(lidTop.localPosition.y + lidOffset, openDuration)
              .SetLink(lidTop.gameObject);   // 目标销毁时自动 kill，避免 DOTween safe mode 报 missing target
        lidBottom.DOLocalMoveY(lidBottom.localPosition.y - lidOffset, openDuration)
              .SetLink(lidBottom.gameObject);
    }

    /// <summary>M2·v0.7.1：按权重 roll 一次奖励（金币/治疗/三选一升级）。</summary>
    private void RollAndApply(Collider2D player)
    {
        float total = coinWeight + healWeight + upgradeWeight;
        if (total <= 0f) return;
        float roll = Random.value * total;

        if (roll < coinWeight)
        {
            CoinDrop.Spawn(transform.position, Random.Range(coinsMin, coinsMax + 1));
            Debug.Log($"[Dungeon] 宝箱开出金币");
        }
        else if (roll < coinWeight + healWeight)
        {
            if (player.TryGetComponent(out Health hp)) hp.Heal(healAmount);
            Debug.Log($"[Dungeon] 宝箱开出治疗：HP +{healAmount}");
        }
        else
        {
            UpgradePanel.Show();
            Debug.Log("[Dungeon] 宝箱开出三选一强化");
        }
    }


    protected override void ApplyEffect(Collider2D player)
    {
        // M2·v0.7.1：结算已前移到 OnConsumed 的 RollAndApply（防切层吞奖励），此处空实现保基类契约
    }
}
