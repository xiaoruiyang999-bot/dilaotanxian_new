using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 序列帧播放器（v1.1.8）：驱动一个 Image 按序列帧播放（主界面横幅禁用动画等）。
/// 时间走 unscaledDeltaTime（timeScale=0 暂停期间 UI 动画照播，与暂停菜单共存）。
/// 支持区间播放（from..to，含两端，支持倒放）与完成回调；播放中每帧零分配（预存 Sprite[]）。
/// 帧资产按 001.png..NNN.png 数字命名扫描加载（FrameAnimator.LoadWerewolfFrames 同模式，
/// 编辑器 AssetDatabase / 构建 Resources 同构）。
/// </summary>
public class UIFramePlayer : MonoBehaviour
{
    [Tooltip("帧率（张/秒）")]
    public float fps = 12f;

    private Sprite[] frames;
    private Image image;
    private float timer;
    private float step;
    private int from, to;              // 播放区间（含两端）
    private int current;               // 当前帧索引（浮点进度取整）
    private bool playing;
    private bool forward;
    private System.Action onComplete;

    /// <summary>是否正在播放。</summary>
    public bool IsPlaying => playing;

    /// <summary>加载 Resources 子目录的数字命名帧（001 起，上限 64 保险）。目录无帧返回 null。</summary>
    public static Sprite[] LoadFrames(string resourcesSubDir)
    {
        var list = new List<Sprite>(16);
        for (int i = 1; i <= 64; i++)
        {
            string fileName = i.ToString("000");
#if UNITY_EDITOR
            Sprite s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Resources/{resourcesSubDir}/{fileName}.png");
#else
            Sprite s = Resources.Load<Sprite>($"{resourcesSubDir}/{fileName}");
#endif
            if (s == null) break;
            list.Add(s);
        }
        return list.Count > 0 ? list.ToArray() : null;
    }

    /// <summary>绑定帧组与目标 Image，并静止显示第 index 帧。</summary>
    public void Setup(Sprite[] spriteFrames, Image target, int showIndex = 0)
    {
        frames = spriteFrames;
        image = target;
        ShowFrame(showIndex);
    }

    /// <summary>静止显示第 index 帧（越界钳制）。</summary>
    public void ShowFrame(int index)
    {
        if (frames == null || frames.Length == 0 || image == null) return;
        current = Mathf.Clamp(index, 0, frames.Length - 1);
        image.sprite = frames[current];
    }

    /// <summary>
    /// 播放区间帧（含两端；from > to 即倒放）。播放中重复调用会被忽略（互斥）。
    /// 完成时回调 onComplete（回调内可安全再次 Play）。
    /// </summary>
    public void Play(int fromFrame, int toFrame, System.Action onDone = null)
    {
        if (frames == null || frames.Length == 0 || image == null) return;
        if (playing) return;

        from = Mathf.Clamp(fromFrame, 0, frames.Length - 1);
        to = Mathf.Clamp(toFrame, 0, frames.Length - 1);
        if (from == to)
        {
            ShowFrame(from);
            onDone?.Invoke();
            return;
        }

        forward = to > from;
        current = from;
        step = 1f / Mathf.Max(1f, fps);
        timer = 0f;
        onComplete = onDone;
        playing = true;
        image.sprite = frames[current];
    }

    /// <summary>立即停播并停留在当前帧。</summary>
    public void Stop() => playing = false;

    void Update()
    {
        if (!playing) return;

        timer += Time.unscaledDeltaTime;
        while (timer >= step)
        {
            timer -= step;
            int next = current + (forward ? 1 : -1);
            if ((!forward && next < to) || (forward && next > to))
            {
                // 到达终点：定格终帧 → 停播 → 回调（先清 playing，回调里可再 Play）
                current = to;
                image.sprite = frames[current];
                playing = false;
                var cb = onComplete;
                onComplete = null;
                cb?.Invoke();
                return;
            }
            current = next;
            image.sprite = frames[current];
        }
    }
}
