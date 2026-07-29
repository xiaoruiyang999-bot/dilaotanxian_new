using DG.Tweening;
using UnityEngine;

/// <summary>
/// 宝箱（v0.5.4 三段式：暗盒身 + 亮金上下两片盖）：按 E（v0.6.1）→ 盖片上下分开（拉开后仍与盒身保持重合区）
/// → 缺口中央刷新奖励道具（pop-in）→ 道具留在原地成为可拾取物（v0.6.1 两段式拾取）。
/// v0.6.3 奖励二选一：已选职业 → 本职业随机武器（WeaponPickup.Drop）/ 法力瓶（ManaBottlePickup），
/// 概率由 manaBottleChance 调（默认 50/50）；无职业（v0_4/v0_5 旧场景）退回原治疗球（HealPickup）。
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

    [Header("奖励（v0.6.3）")]
    [Tooltip("已选职业时掉落法力瓶的概率（其余掉本职业随机武器）；无职业退回治疗球")]
    [SerializeField, Range(0f, 1f)] private float manaBottleChance = 0.5f;

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
        // v0.6.3 宝箱奖励二选一：玩家已选职业 → 本职业随机武器 / 法力瓶（默认 50/50，可调）；
        // 无职业（v0_4/v0_5 旧场景）退回原 itemPrefab + HealPickup 治疗球路径
        PlayerStats stats = player != null ? player.GetComponent<PlayerStats>() : null;
        ClassData cls = stats != null ? stats.CurrentClass : null;
        if (cls != null && cls.AvailableWeapons.Count > 0)
        {
            if (Random.value < manaBottleChance)
            {
                ManaBottlePickup bottle = ManaBottlePickup.Spawn(transform.position);
                PopIn(bottle.transform);
                Debug.Log("[Dungeon] 宝箱开启：掉落法力瓶（走近按 E 拾取）");
            }
            else
            {
                WeaponData data = cls.AvailableWeapons[Random.Range(0, cls.AvailableWeapons.Count)];
                WeaponPickup pickup = WeaponPickup.Drop(data, transform.position);
                if (pickup != null) PopIn(pickup.transform);
                Debug.Log($"[Dungeon] 宝箱开启：掉落武器 {(data != null ? data.DisplayName : "?")}（走近按 E 拾取）");
            }
            return;
        }

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

    /// <summary>pop-in 弹出（v0.6.3）：从 0 缩放回物体自身基础缩放（WeaponPickup.Drop 已定 0.7× 基础缩放）。</summary>
    private static void PopIn(Transform item)
    {
        Vector3 targetScale = item.localScale;
        item.localScale = Vector3.zero;
        item.DOScale(targetScale, 0.25f).SetEase(Ease.OutBack).SetLink(item.gameObject);
    }

    protected override void ApplyEffect(Collider2D player)
    {
        // v0.6.1：+2HP 奖励已转移至开箱掉落的 HealPickup（拾取时结算）。
        // 本方法仅在 lidTop/lidBottom 未接线的防御路径被 OnConsumed 调用。
        Debug.Log("[Dungeon] 宝箱开启（盖片未接线，未掉落道具）");
    }
}
