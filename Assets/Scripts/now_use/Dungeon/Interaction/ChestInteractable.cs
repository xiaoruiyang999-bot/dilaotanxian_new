using DG.Tweening;
using UnityEngine;

/// <summary>
/// 宝箱（v0.5.4 三段式：暗盒身 + 亮金上下两片盖）：按 E（v0.6.1）→ 盖片上下分开（拉开后仍与盒身保持重合区）
/// → 缺口中央刷新治疗球道具（pop-in）→ 道具留在原地成为可拾取物（v0.6.1 两段式拾取：
/// 不再展示后淡出，+2HP 结算由 HealPickup.OnPickedUp 在玩家按 E 拾取时执行）。
/// </summary>
public class ChestInteractable : Interactable
{
    [Header("开箱动画")]
    [SerializeField] private Transform lidTop;
    [SerializeField] private Transform lidBottom;
    [SerializeField] private GameObject itemPrefab;
    [Tooltip("盖片平移距离：盒身高 0.6、盖片各高 0.3，移 0.25 后与盒身保持 0.05 重合区")]
    [SerializeField] private float lidOffset = 0.25f;
    [SerializeField] private float openDuration = 0.35f;

    protected override void OnConsumed(Collider2D player)
    {
        if (lidTop == null || lidBottom == null)
        {
            // 防御：prefab 未接线时退回旧行为，不卡死
            ApplyEffect(player);
            SetConsumedVisual();
            return;
        }
        lidTop.DOLocalMoveY(lidTop.localPosition.y + lidOffset, openDuration)
              .SetLink(lidTop.gameObject);   // 目标销毁时自动 kill，避免 DOTween safe mode 报 missing target
        lidBottom.DOLocalMoveY(lidBottom.localPosition.y - lidOffset, openDuration)
              .SetLink(lidBottom.gameObject)
              .OnComplete(() => SpawnItem(player));
    }

    private void SpawnItem(Collider2D player)
    {
        if (itemPrefab != null)
        {
            GameObject item = Instantiate(itemPrefab, transform.position, Quaternion.identity, transform);
            Vector3 targetScale = itemPrefab.transform.localScale;
            item.transform.localScale = Vector3.zero;
            item.transform.DOScale(targetScale, 0.25f).SetEase(Ease.OutBack).SetLink(item);   // pop-in 弹出
            // v0.6.1 两段式拾取：道具留在原地成为可拾取物（不再展示后淡出销毁），
            // +2HP 结算改由 HealPickup.OnPickedUp 在玩家按 E 拾取时执行
            item.AddComponent<HealPickup>();
        }
        Debug.Log("[Dungeon] 宝箱开启：掉落治疗球（走近按 E 拾取）");
    }

    protected override void ApplyEffect(Collider2D player)
    {
        // v0.6.1：+2HP 奖励已转移至开箱掉落的 HealPickup（拾取时结算）。
        // 本方法仅在 lidTop/lidBottom 未接线的防御路径被 OnConsumed 调用。
        Debug.Log("[Dungeon] 宝箱开启（盖片未接线，未掉落道具）");
    }
}
