using UnityEngine;

/// <summary>
/// 敌人只读感知组件：负责 LOS 等物理查询，不修改 AI 状态、移动或攻击。
/// 当前场景的 Walls 与 Player 同在 Default Layer，因此以“首个外部 Collider 是否属于目标”
/// 作为可见性真源，而不是仅凭 LayerMask 判断。
/// v0.6.1（M1.10）：所有查询改 RaycastNonAlloc + 静态缓冲、目标碰撞体单槽缓存——
/// 消除每帧 GC 分配（后撤/侧移探测每帧最多 2~3 次 × 每房 8 敌时的掉帧主嫌疑）。
/// </summary>
public class EnemyPerception : MonoBehaviour
{
    [SerializeField, Min(0.02f)] private float lineOfSightCheckInterval = 0.1f;

    private float nextLineOfSightCheckTime;
    private Transform cachedTarget;
    private int cachedMask;
    private bool cachedLineOfSight;

    // 查询缓冲：RaycastNonAlloc 复用（同步调用、不跨帧持有，单缓冲安全）
    private static readonly RaycastHit2D[] raycastBuffer = new RaycastHit2D[32];
    // 目标碰撞体单槽缓存：target 实际只有玩家一个，命中率极高；目标销毁重建时自动失效重找
    private Transform bufferedTarget;
    private Collider2D bufferedTargetCollider;

    public void Configure(float checkInterval)
    {
        lineOfSightCheckInterval = Mathf.Max(0.02f, checkInterval);
    }

    public bool HasLineOfSight(Transform target, LayerMask targetMask, LayerMask obstacleMask,
        bool forceRefresh = false)
    {
        if (target == null) return false;

        int queryMask = targetMask.value | obstacleMask.value;
        if (queryMask == 0) queryMask = Physics2D.AllLayers;

        if (!forceRefresh && target == cachedTarget && queryMask == cachedMask
            && Time.time < nextLineOfSightCheckTime)
            return cachedLineOfSight;

        cachedTarget = target;
        cachedMask = queryMask;
        nextLineOfSightCheckTime = Time.time + lineOfSightCheckInterval;
        cachedLineOfSight = QueryLineOfSight(target, queryMask);
        return cachedLineOfSight;
    }

    private bool QueryLineOfSight(Transform target, int queryMask)
    {
        Vector2 origin = transform.position;
        Vector2 targetPoint = GetTargetPoint(target);
        Vector2 delta = targetPoint - origin;
        float distance = delta.magnitude;
        if (distance <= 0.001f) return true;

        int count = Physics2D.RaycastNonAlloc(origin, delta / distance, raycastBuffer,
            distance, queryMask);
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = raycastBuffer[i];
            if (hit.collider == null) continue;
            Transform hitTransform = hit.collider.transform;

            // 忽略自身根节点及子对象上的 Weapon/Hitbox Collider。
            if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;

            // v0.5.4.4.3：忽略 Trigger Collider —— 房间进入触发器（RoomTrigger）等
            // 不应阻挡视线。之前 RoomTrigger 放在 Default Layer，会拦截 LOS 射线导致
            // 远程/召唤敌人永远看不到玩家、永远不攻击。Trigger 只负责逻辑事件，
            // 不作为 LOS 障碍物。
            if (hit.collider.isTrigger) continue;

            return hitTransform == target || hitTransform.IsChildOf(target);
        }

        // 目标没有参与指定 LayerMask 时不能视为可见，防止配置错误变成穿墙攻击。
        return false;
    }

    private Vector2 GetTargetPoint(Transform target)
    {
        // v0.6.1：GetComponentInChildren 每次都遍历子树分配，改为单槽缓存。
        // 用 == null 显式判 fake-null（目标销毁/换层重建后自动重找）。
        if (bufferedTarget == null || bufferedTarget != target || bufferedTargetCollider == null)
        {
            bufferedTarget = target;
            bufferedTargetCollider = target.GetComponentInChildren<Collider2D>();
        }
        return bufferedTargetCollider != null
            ? bufferedTargetCollider.bounds.center
            : (Vector2)target.position;
    }

    public bool IsDirectionClear(Vector2 direction, float distance)
    {
        if (direction.sqrMagnitude <= 0.0001f) return false;
        int count = Physics2D.RaycastNonAlloc(transform.position, direction.normalized,
            raycastBuffer, Mathf.Max(0.05f, distance), Physics2D.AllLayers);
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = raycastBuffer[i];
            if (hit.collider == null) continue;

            // v0.5.4.4.4：与 QueryLineOfSight 同款规则——Trigger 只负责逻辑事件，
            // 不作为移动阻挡。之前 RoomTrigger/敌人探测圈会挡住射线，导致远程敌人
            // 的后撤/侧移探测在房间内几乎恒为 false，被近身后只会站桩。
            if (hit.collider.isTrigger) continue;

            Transform hitTransform = hit.collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
            return false;
        }
        return true;
    }
}
