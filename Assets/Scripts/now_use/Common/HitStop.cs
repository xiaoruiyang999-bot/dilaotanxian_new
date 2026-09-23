using UnityEngine;

/// <summary>
/// Hit-stop 兼容入口。v2.1.2 起不再直接写 Time.timeScale，
/// 由 GameModalService 与 UI 暂停令牌统一仲裁，并发请求仍取最长剩余时间。
/// </summary>
public static class HitStop
{
    /// <summary>请求一次打击停顿：duration 秒内游戏逻辑冻结（建议 0.03~0.05s）。</summary>
    public static void Request(float duration)
    {
        GameModalService.RequestHitStop(duration);
    }
}
