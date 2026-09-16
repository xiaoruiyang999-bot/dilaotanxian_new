using UnityEngine;

/// <summary>
/// v2.0.10 批4 四足追猎移动决策器（修缮计划 §4.7）：纯 C# 决策——
/// 输入玩家/Boss 位置、朝向、RoomBounds、柱状态、上一招；
/// 输出移动模式（Chase/Orbit/Reposition/SideHop）与期望方向。
/// MonoBehaviour 只负责用 Rigidbody2D 执行结果（不在此做物理查询）。
/// 禁止：瞬间反向、穿墙选点、进入玩家身体、距离不足时强行奔袭。
/// </summary>
public static class GrandQuadLocomotion
{
    public enum MoveMode { Chase, Orbit, Reposition, SideHop }

    public struct Decision
    {
        public MoveMode Mode;
        public Vector2 Direction;     // 期望移动方向(归一化)
        public float SpeedMultiplier;
        public bool ReadyToCharge;    // 满足奔袭起跑条件
        public bool ReadyToMoonHunt;  // 满足围猎退距条件
    }

    // 参数（测试值,§4.7）
    public const float TurnRateDeg = 90f;         // 每秒最大转向(限速转向不瞬移)
    public const float OrbitRadius = 5f;           // 绕侧半径
    public const float MinChargeDistance = 6f;     // 奔袭最小起跑距离
    public const float MaxChaseDistance = 4f;      // 近距不再直接追(转绕侧)
    public const float SideHopCooldown = 2.5f;     // 侧跳间隔

    /// <summary>决策(每帧或 0.1s 节拍调用)。</summary>
    public static Decision Decide(
        Vector2 bossPos, Vector2 playerPos, Vector2 currentFacing,
        Rect roomBounds, float timeSinceLastHop, float timeSinceCharge,
        System.Random rng)
    {
        var d = new Decision { SpeedMultiplier = 1f };
        Vector2 toPlayer = playerPos - bossPos;
        float dist = toPlayer.magnitude;
        Vector2 dirToPlayer = dist > 0.01f ? toPlayer / dist : Vector2.right;

        // 满足奔袭条件:距离够远+冷却好
        if (dist >= MinChargeDistance && timeSinceCharge > 3f)
        {
            d.ReadyToCharge = true;
            d.Mode = MoveMode.Reposition;
            d.Direction = dirToPlayer;   // 面向玩家准备蓄力
            return d;
        }

        // 满足围猎条件:距离中等+上次奔袭后
        if (dist >= MinChargeDistance * 0.7f && timeSinceCharge > 5f)
        {
            d.ReadyToMoonHunt = true;
            d.Mode = MoveMode.Reposition;
            d.Direction = -dirToPlayer;  // 退距
            return d;
        }

        // 近距:绕侧(§4.7 Orbit——不直接贴脸)
        if (dist <= MaxChaseDistance)
        {
            // 随机侧跳(冷却控制)
            if (timeSinceLastHop > SideHopCooldown && rng.NextDouble() < 0.02)
            {
                d.Mode = MoveMode.SideHop;
                // 垂直于朝玩家的方向,随机左右
                Vector2 perp = Vector2.Perpendicular(dirToPlayer);
                d.Direction = rng.NextDouble() < 0.5 ? perp : -perp;
                d.SpeedMultiplier = 1.8f;
                return d;
            }
            // Orbit:沿切线方向绕玩家
            d.Mode = MoveMode.Orbit;
            Vector2 tangent = Vector2.Perpendicular(dirToPlayer);
            // 保持固定半径:距玩家太近时加远离分量
            Vector2 radial = dist < OrbitRadius * 0.7f ? -dirToPlayer * 0.4f : Vector2.zero;
            d.Direction = (tangent + radial).normalized;
            d.SpeedMultiplier = 0.9f;
            return d;
        }

        // 中距:追击(限速转向——不瞬移)
        d.Mode = MoveMode.Chase;
        d.Direction = LimitTurn(currentFacing, dirToPlayer, TurnRateDeg * Time.deltaTime * Mathf.Deg2Rad);   // P1:帧率无关限转
        d.SpeedMultiplier = 1f;
        return d;
    }

    /// <summary>限速转向(§4.7:不能原地瞬间掉头)——当前朝向到目标方向最多转 maxRadians。</summary>
    public static Vector2 LimitTurn(Vector2 current, Vector2 target, float maxRadians)
    {
        float currentAngle = Mathf.Atan2(current.y, current.x);
        float targetAngle = Mathf.Atan2(target.y, target.x);
        float delta = Mathf.DeltaAngle(currentAngle * Mathf.Rad2Deg, targetAngle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        float clamped = Mathf.Clamp(delta, -maxRadians, maxRadians);
        float result = currentAngle + clamped;
        return new Vector2(Mathf.Cos(result), Mathf.Sin(result));
    }
}
