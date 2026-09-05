using UnityEngine;

/// <summary>
/// 斩杀反馈入口（v1.1.34，技能组"裸绞"专用）：常规命中/击杀不再震屏（WeaponHitbox 已移除），
/// 屏幕抖动保留给裸绞的斩杀效果——技能实现处击杀目标时调用本入口。
/// 组合：强震屏（0.05/0.08 = 击杀震原版参数，用户"保留"的数值）+ 加长停帧 0.08s。
/// 参数集中在顶部常量，技能手感联调时一处改。
/// </summary>
public static class ExecuteFeedback
{
    private const float ShakeIntensity = 0.05f;
    private const float ShakeDuration = 0.08f;
    private const float HitStopDuration = 0.08f;

    /// <summary>播放斩杀反馈（裸绞技能击杀目标时调用）。position 预留特效挂点（当前无特效资产）。</summary>
    public static void Play(Vector2 position)
    {
        CameraFollow.ShakeMain(ShakeIntensity, ShakeDuration);
        HitStop.Request(HitStopDuration);
        AudioManager.PlaySFX("hit");   // 表未配专用斩杀音时沿用命中音；有专用音改此处 id
        // TODO(裸绞技能组): 击杀特效/音效资产到位后在此挂接；position 为击杀点
    }
}
