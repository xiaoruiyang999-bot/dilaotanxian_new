using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 序列帧动画播放器（v0.6.3 狼人素材用，程序员美术风格零资产依赖）：
/// SpriteRenderer 按帧率循环/单次播放 sprites 数组，帧列表可在 Inspector 手动拖入，
/// 也可代码调用 LoadFrames 按目录加载（Resources 不适用 Art 目录，故提供两种方式）。
/// 狼人素材说明：Walk_L / Walk_R 为左右朝向两套素材（不做 flipX，避免白描边内部镜像错位）。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class FrameAnimator : MonoBehaviour
{
    [Tooltip("帧序列（Inspector 拖入，或运行时 LoadFrames）")]
    [SerializeField] private List<Sprite> frames = new List<Sprite>();
    [Tooltip("帧率（帧/秒）"), Min(0.1f)]
    [SerializeField] private float fps = 10f;
    [Tooltip("循环播放；false 时播完停在最后一帧")]
    [SerializeField] private bool loop = true;
    [Tooltip("单次模式播完后的回调（变身动画接走路动画等）")]
    public System.Action OnFinished;

    private SpriteRenderer spriteRenderer;
    private int frameIndex;
    private float timer;
    private bool playing = true;

    /// <summary>当前帧序列引用（外部比较"是否已在播这组帧"用，v0.6.3）。</summary>
    public IReadOnlyList<Sprite> CurrentFrames => frames;
    /// <summary>当前显示的 Sprite（v0.6.5：变身逐帧体型补偿等外部效果用）。</summary>
    public Sprite CurrentSprite => frames.Count > 0 && frameIndex < frames.Count ? frames[frameIndex] : null;
    /// <summary>帧率读写（v0.6.3：变身 8fps / 走路 10fps 动态切换用）。</summary>
    public float Fps
    {
        get => fps;
        set => fps = Mathf.Max(0.1f, value);
    }

#if UNITY_EDITOR
    [Tooltip("（编辑器便利）帧目录：Awake 时自动按文件名加载该目录全部 Sprite，免手动拖引用。仅编辑器/开发期生效")]
    [SerializeField] private string framesFolder;
#endif

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
#if UNITY_EDITOR
        if (frames.Count == 0 && !string.IsNullOrEmpty(framesFolder))
            LoadFromFolder(framesFolder);
#endif
        ApplyFrame();
    }

    void Update()
    {
        if (!playing || frames.Count < 2) return;

        timer += Time.deltaTime;
        float interval = 1f / fps;
        while (timer >= interval)
        {
            timer -= interval;
            frameIndex++;
            if (frameIndex >= frames.Count)
            {
                if (loop) frameIndex = 0;
                else
                {
                    frameIndex = frames.Count - 1;
                    playing = false;
                    OnFinished?.Invoke();
                }
            }
            ApplyFrame();
        }
    }

    private void ApplyFrame()
    {
        if (spriteRenderer != null && frames.Count > 0 && frameIndex < frames.Count)
            spriteRenderer.sprite = frames[frameIndex];
    }

    /// <summary>切换帧序列并从头播放（如 走路→变身→走路）。</summary>
    public void Play(IReadOnlyList<Sprite> newFrames, bool shouldLoop = true)
    {
        frames = new List<Sprite>(newFrames);
        loop = shouldLoop;
        frameIndex = 0;
        timer = 0f;
        playing = true;
        ApplyFrame();
    }

    public void Pause() => playing = false;
    public void Resume() => playing = true;

#if UNITY_EDITOR
    /// <summary>编辑器便利：按目录加载全部 Sprite（按文件名排序，001→009 顺序正确）。</summary>
    private void LoadFromFolder(string folder)
    {
        if (!folder.StartsWith("Assets/")) folder = "Assets/" + folder;
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { folder });
        var list = new List<Sprite>();
        foreach (string g in guids)
        {
            string p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
            if (!p.EndsWith(".png")) continue;
            Sprite s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (s != null) list.Add(s);
        }
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        if (list.Count > 0) frames = list;
    }
#endif
}
