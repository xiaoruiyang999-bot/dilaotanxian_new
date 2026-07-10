using UnityEngine;

/// <summary>
/// 公共巡逻系统。
/// TODO v0.5+: 实现路径点巡逻（Waypoint）、区域随机巡逻、巡逻路径可视化Gizmos等
/// </summary>
public static class PatrolSystem
{
    /// <summary>
    /// 获取随机巡逻点（以原点为中心，半径内随机）。
    /// </summary>
    public static Vector2 GetRandomPatrolPoint(Vector2 origin, float radius)
    {
        return origin + Random.insideUnitCircle * radius;
    }

    /// <summary>
    /// 获取圆形巡逻边界上的一个点（用于固定半径巡逻）。
    /// </summary>
    public static Vector2 GetPatrolPointOnCircle(Vector2 origin, float radius, float angleDegrees)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        return origin + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
    }
}
