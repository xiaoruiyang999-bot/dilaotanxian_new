using UnityEngine;

/// <summary>
/// 治疗球拾取物（v0.6.1 两段式拾取验证用）：宝箱开出的 +HP 道具。
/// 实现 IPickupable：走近成为候选 → 按 E 拾取 → 回血并销毁。
/// ChestItem_Heal prefab 为纯视觉（无碰撞体/脚本），Awake 自动补触发器供 PlayerInteractor 探测；
/// 由 ChestInteractable 在开箱动画结束时 AddComponent 挂上。
/// </summary>
public class HealPickup : MonoBehaviour, IPickupable
{
    [SerializeField] private float healAmount = 2f;
    [SerializeField] private string displayName = "治疗球";

    public string DisplayName => $"{displayName} +{healAmount}HP";

    private void Awake()
    {
        // ChestItem_Heal prefab 无碰撞体，运行时补一个触发器供 OverlapCircle 探测
        if (GetComponent<Collider2D>() == null)
        {
            CircleCollider2D col = gameObject.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;
        }
    }

    public void OnPickedUp(GameObject player)
    {
        if (player != null && player.TryGetComponent(out Health hp))
            hp.Heal(healAmount);
        Debug.Log($"[Dungeon] 拾取{DisplayName}");
        Destroy(gameObject);
    }
}
