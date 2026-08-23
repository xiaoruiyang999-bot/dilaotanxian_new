using UnityEngine;

/// <summary>
/// Hit-stop 工具（M1.6·v0.6.1）：命中/死亡等强反馈瞬间把 Time.timeScale 归零一小段，
/// 制造"打击停顿"。用真实时间（realtimeSinceStartup）计时恢复——timeScale=0 期间
/// 计时照常走表，不会自我卡死。并发请求取最长剩余时间，不叠加。
/// 驱动方式：游戏启动时自动创建隐藏 Driver（RuntimeInitializeOnLoadMethod），
/// 无需场景挂载。Update 在 timeScale=0 下照常执行，恢复可靠。
/// 注意：当前项目无其他 timeScale 消费方；M5 做暂停菜单时需重构为多档 timeScale 管理。
/// </summary>
public static class HitStop
{
    private static float resumeAtRealtime;

    /// <summary>UI 暂停抑制（M2·v0.7.1）：升级面板等把 timeScale 归零时置 true，
    /// HitStop 不得擅自把 timeScale 恢复为 1（否则面板还没选完游戏就动了）。</summary>
    public static bool SuppressByUI { get; set; }

    /// <summary>请求一次打击停顿：duration 秒内游戏逻辑冻结（建议 0.03~0.05s）。</summary>
    public static void Request(float duration)
    {
        if (duration <= 0f) return;
        resumeAtRealtime = Mathf.Max(resumeAtRealtime, Time.realtimeSinceStartup + duration);
        Time.timeScale = 0f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateDriver()
    {
        var go = new GameObject("HitStopDriver");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        go.AddComponent<HitStopDriver>();
    }

    /// <summary>每帧检查恢复（挂在隐藏常驻对象上，随游戏生命周期存活）。</summary>
    private class HitStopDriver : MonoBehaviour
    {
        void Update()
        {
            if (SuppressByUI) return;
            if (Time.timeScale == 0f && Time.realtimeSinceStartup >= resumeAtRealtime)
                Time.timeScale = 1f;
        }
    }
}
