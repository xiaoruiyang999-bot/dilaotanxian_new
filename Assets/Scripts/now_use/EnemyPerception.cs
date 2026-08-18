using UnityEngine;

/// <summary>
/// 敌人只读感知组件：负责 LOS 等物理查询，不修改 AI 状态、移动或攻击。
/// 当前场景的 Walls 与 Player 同在 Default Layer，因此以“首个外部 Collider 是否属于目标”
/// 作为可见性真源，而不是仅凭 LayerMask 判断。
/// </summary>
public class EnemyPerception : MonoBehaviour
{
    [SerializeField, Min(0.02f)] private float lineOfSightCheckInterval = 0.1f;

    private float nextLineOfSightCheckTime;
    private Transform cachedTarget;
    private int cachedMask;
    private bool cachedLineOfSight;

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

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, delta / distance, distance, queryMask);
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null) continue;
            Transform hitTransform = hit.collider.transform;

            // 忽略自身根节点及子对象上的 Weapon/Hitbox Collider。
            if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;

            return hitTransform == target || hitTransform.IsChildOf(target);
        }

        // 目标没有参与指定 LayerMask 时不能视为可见，防止配置错误变成穿墙攻击。
        return false;
    }

    private static Vector2 GetTargetPoint(Transform target)
    {
        Collider2D targetCollider = target.GetComponentInChildren<Collider2D>();
        return targetCollider != null ? targetCollider.bounds.center : (Vector2)target.position;
    }

    public bool IsDirectionClear(Vector2 direction, float distance)
    {
        if (direction.sqrMagnitude <= 0.0001f) return false;
        RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, direction.normalized,
            Mathf.Max(0.05f, distance), Physics2D.AllLayers);
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null) continue;
            Transform hitTransform = hit.collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
            return false;
        }
        return true;
    }
}
