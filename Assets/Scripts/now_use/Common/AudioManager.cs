using UnityEngine;

/// <summary>
/// 全局音频管理（M1.3·v0.6.1）：SFX 按名播放 + BGM 单曲循环，SFX/BGM 双路音量。
/// 挂载方式同 MinimapSystem：场景空对象挂本组件，Inspector 里把下载好的音效
/// （Kenney.nl / OpenGameArt，CC0）拖进对应条目即可；BGM 在 Start 自动循环播放。
/// 未挂载或条目未配置时所有调用静默跳过——各系统挂点（命中/受击/敌死/开门/宝箱）
/// 一行式调用，零耦合。
/// SFX 用 PlayOneShot：天然支持同帧多次命中叠加播放，无需多源轮询池。
/// </summary>
public class AudioManager : MonoBehaviour
{
    [System.Serializable]
    public class SoundEntry
    {
        [Tooltip("音效标识：hit / hurt / enemyDie / door / chest（与代码调用一致）")]
        public string id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [Header("音效表（M1.5 五个基础音效）")]
    [SerializeField] private SoundEntry[] sfxEntries;

    [Header("BGM")]
    [SerializeField] private AudioClip bgm;
    [Range(0f, 1f)] [SerializeField] private float bgmVolume = 0.5f;

    [Header("总音量")]
    [Range(0f, 1f)] [SerializeField] private float sfxMasterVolume = 1f;

    public static AudioManager Instance { get; private set; }

    private AudioSource sfxSource;
    private AudioSource bgmSource;

    /// <summary>M5·v1.0.0：暂停菜单音量控制（PlayerConfigs 持久化由 PausePanel 负责）。</summary>
    public static void SetSfxVolume(float v)
    {
        if (Instance != null) Instance.sfxMasterVolume = Mathf.Clamp01(v);
    }

    public static void SetBgmVolume(float v)
    {
        if (Instance == null) return;
        Instance.bgmVolume = Mathf.Clamp01(v);
        if (Instance.bgmSource != null) Instance.bgmSource.volume = Instance.bgmVolume;
    }

    void Awake()
    {
        // 重复挂载兜底：以先到者为准（单场景游戏，正常只有一份）
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[Audio] 场景中已存在 AudioManager，重复实例已禁用。", this);
            enabled = false;
            return;
        }
        Instance = this;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.volume = PlayerPrefs.GetFloat("bgm_volume", bgmVolume);
        sfxMasterVolume = PlayerPrefs.GetFloat("sfx_volume", sfxMasterVolume);
    }

    void Start()
    {
        if (bgm != null && !bgmSource.isPlaying)
            bgmSource.Play();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>按名播放音效（静态入口）：未挂载 / id 不存在 / 未配 clip 时静默。</summary>
    public static void PlaySFX(string id)
    {
        if (Instance == null) return;
        Instance.PlayInternal(id);
    }

    private void PlayInternal(string id)
    {
        if (sfxEntries == null || sfxSource == null) return;
        foreach (SoundEntry entry in sfxEntries)
        {
            if (entry == null || entry.id != id || entry.clip == null) continue;
            sfxSource.PlayOneShot(entry.clip, entry.volume * sfxMasterVolume);
            return;
        }
        // 配了表但缺该条目：每个 id 只警告一次，避免战斗中刷屏
        if (warnedIds.Add(id))
            Debug.LogWarning($"[Audio] 音效表中没有可用的条目：{id}（Inspector 补配置后即生效）");
    }

    private readonly System.Collections.Generic.HashSet<string> warnedIds = new System.Collections.Generic.HashSet<string>();
}
