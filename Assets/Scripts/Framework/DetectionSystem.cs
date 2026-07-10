using UnityEngine;

/// <summary>
/// 公共检测系统。
/// TODO v0.5+: 实现视线检测（Raycast障碍物遮挡）、听觉检测（玩家噪音）、锥形视野等
/// </summary>
public static class DetectionSystem
{
    /// <summary>
    /// 简单距离检测。当前仅用距离判定，后续扩展视线/听觉。
    /// </summary>
    public static bool IsInRange(Vector2 from, Vector2 to, float range)
    {
        return Vector2.Distance(from, to) <= range;
    }

    /// <summary>
    /// 检测目标是否在扇形视野内。
    /// TODO: 实现锥形视野判定（用于远程敌人、Boss等）
    /// </summary>
    public static bool IsInFieldOfView(Vector2 origin, Vector2 forward, Vector2 target, float range, float angle)
    {
        if (!IsInRange(origin, target, range)) return false;
        float halfAngle = angle * 0.5f;
        float targetAngle = Vector2.Angle(forward, target - origin);
        return targetAngle <= halfAngle;
    }
}
