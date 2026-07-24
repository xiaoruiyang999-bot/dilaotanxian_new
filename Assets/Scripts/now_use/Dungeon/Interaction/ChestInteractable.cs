using DG.Tweening;
using UnityEngine;

/// <summary>
/// 宝箱（v0.5.4 三段式：暗盒身 + 亮金上下两片盖）：walk-over → 盖片上下分开（拉开后仍与盒身保持重合区）
/// → 缺口中央刷新道具占位（pop-in）→ 结算奖励（+2 HP 占位）→ 道具展示后淡出，箱子保持开启态。
/// 奖励从占位变真货的挂点即 ApplyEffect（未来接技能/装备系统）。
/// </summary>
public class ChestInteractable : Interactable
{
    [SerializeField] private float healAmount = 2f;

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
            if (item.TryGetComponent(out SpriteRenderer sr))
                sr.DOFade(0f, 0.5f).SetDelay(0.8f).SetLink(item).OnComplete(() => Destroy(item));   // 展示后淡出
        }
        ApplyEffect(player);   // 结算：+2 HP（占位奖励）+ 日志
    }

    protected override void ApplyEffect(Collider2D player)
    {
        if (player.TryGetComponent(out Health hp)) hp.Heal(healAmount);
        Debug.Log($"[Dungeon] 宝箱开启：HP +{healAmount}（占位奖励）");
    }
}
