using UnityEngine;

/// <summary>
/// 一局运行统计（v1.0.5，死亡结算面板数据后端）。
/// 静态计数：击杀数 + 开局时间。RunManager.InitDelayed 调 BeginRun（每次地牢场景加载重置，
/// NextFloor 同场景不重置——整局累计）；EnemyHealth.Die 调 OnEnemyKilled。
/// 楼层数不在此重复记录（唯一真源 RunManager.FloorNumber，避免双份状态漂移）。
/// </summary>
public static class RunTracker
{
    /// <summary>本局击杀数（跨楼层累计，进入新地牢场景清零）。</summary>
    public static int Kills { get; private set; }

    /// <summary>本局开局时间（Time.time 口径，进入地牢场景时刻）。</summary>
    public static float RunStartTime { get; private set; }

    /// <summary>本局存活时长（秒，调用时刻距开局）。</summary>
    public static float Elapsed => Time.time - RunStartTime;

    /// <summary>开始新一局：地牢场景加载时由 RunManager 调用。</summary>
    public static void BeginRun()
    {
        Kills = 0;
        RunStartTime = Time.time;
    }

    /// <summary>击杀 +1：EnemyHealth.Die 调用（含 Debug 菜单击杀，口径统一）。</summary>
    public static void OnEnemyKilled() => Kills++;
}
