using UnityEngine;

/// <summary>Projectile 攻击专用执行器；不负责 AI、攻击阶段、冷却或 LOS。</summary>
public class ProjectileEmitter : MonoBehaviour
{
    [SerializeField] private Transform muzzle;
    [SerializeField, Min(0f)] private float fallbackOffset = 0.8f;

    public Transform Muzzle => muzzle;

    public bool Emit(AttackData data, Vector2 direction, Transform source)
    {
        if (data == null || !data.IsProjectile || data.ProjectilePrefab == null)
        {
            Debug.LogWarning($"[ProjectileEmitter] {name} 缺少有效 Projectile AttackData/Prefab。", this);
            return false;
        }

        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized : (Vector2)transform.right;
        Vector2 spawnPosition = muzzle != null
            ? (Vector2)muzzle.position
            : (Vector2)transform.position + safeDirection * fallbackOffset;

        GameObject projectileObject = Object.Instantiate(
            data.ProjectilePrefab, spawnPosition, Quaternion.identity);
        Projectile projectile = projectileObject.GetComponent<Projectile>();
        if (projectile == null)
        {
            Debug.LogWarning($"[ProjectileEmitter] {data.ProjectilePrefab.name} 缺少 Projectile 组件。", this);
            Object.Destroy(projectileObject);
            return false;
        }

        projectile.Launch(safeDirection, data.AttackDamage, data.ProjectileSpeed,
            data.TargetLayer, data.ObstacleLayer, source);
        return true;
    }
}
