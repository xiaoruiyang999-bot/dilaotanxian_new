using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>v2.1.2 统一模态类型。背包/商店先保留稳定接入位，后续版本不再自管 timeScale。</summary>
public enum GameModalKind
{
    PauseMenu,
    PauseSettings,
    Narrative,
    NarrativeArchive,
    Inventory,
    MapPreview,
    RouteChoice,
    RewardChoice,
    Shop,
    SkillTree,
    CharacterSelect,
    Death,
    Victory,
}

/// <summary>模态栈令牌。令牌只可释放自己的层，不能误关其他面板。</summary>
public readonly struct GameModalToken : IEquatable<GameModalToken>
{
    internal GameModalToken(int id) => Id = id;
    internal int Id { get; }
    public bool IsValid => Id > 0;

    public bool Equals(GameModalToken other) => Id == other.Id;
    public override bool Equals(object obj) => obj is GameModalToken other && Equals(other);
    public override int GetHashCode() => Id;
    public static bool operator ==(GameModalToken left, GameModalToken right) => left.Equals(right);
    public static bool operator !=(GameModalToken left, GameModalToken right) => !left.Equals(right);
}

/// <summary>
/// 纯 C# LIFO 模态栈，独立于场景对象，便于验证嵌套、焦点和输入屏蔽合同。
/// 顶层不可取消时，TryCancelTop 仍消费 Esc，防止输入穿透到下层或世界。
/// </summary>
public sealed class GameModalStack
{
    private sealed class Entry
    {
        public int Id;
        public GameModalKind Kind;
        public bool PausesWorld;
        public bool BlocksWorldInput;
        public Action Cancel;
    }

    private readonly List<Entry> entries = new List<Entry>();
    private int nextId = 1;

    public int Count => entries.Count;
    public bool HasModal => entries.Count > 0;
    public bool BlocksWorldInput
    {
        get
        {
            for (int i = entries.Count - 1; i >= 0; i--)
                if (entries[i].BlocksWorldInput) return true;
            return false;
        }
    }
    public bool PausesWorld
    {
        get
        {
            for (int i = entries.Count - 1; i >= 0; i--)
                if (entries[i].PausesWorld) return true;
            return false;
        }
    }
    public GameModalKind? TopKind => entries.Count == 0
        ? (GameModalKind?)null
        : entries[entries.Count - 1].Kind;

    public GameModalToken Push(
        GameModalKind kind,
        bool pausesWorld,
        bool blocksWorldInput,
        Action cancel)
    {
        int id = nextId++;
        if (nextId <= 0) nextId = 1;
        entries.Add(new Entry
        {
            Id = id,
            Kind = kind,
            PausesWorld = pausesWorld,
            BlocksWorldInput = blocksWorldInput,
            Cancel = cancel,
        });
        return new GameModalToken(id);
    }

    public bool Release(GameModalToken token)
    {
        if (!token.IsValid) return false;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].Id != token.Id) continue;
            entries.RemoveAt(i);
            return true;
        }
        return false;
    }

    public bool IsTop(GameModalToken token) => token.IsValid
        && entries.Count > 0
        && entries[entries.Count - 1].Id == token.Id;

    public bool IsTop(GameModalKind kind) => entries.Count > 0
        && entries[entries.Count - 1].Kind == kind;

    public bool TryCancelTop()
    {
        if (entries.Count == 0) return false;
        Action cancel = entries[entries.Count - 1].Cancel;
        cancel?.Invoke();
        return true;
    }

    public void Clear()
    {
        entries.Clear();
        nextId = 1;
    }
}

/// <summary>纯数据 Hit Stop 仲裁：并发请求取最长，模态暂停优先且互不覆盖。</summary>
public sealed class TimePauseArbiter
{
    private float hitStopUntil = float.NegativeInfinity;

    public float HitStopUntil => hitStopUntil;

    public void RequestHitStop(float realtimeNow, float duration)
    {
        if (duration <= 0f) return;
        hitStopUntil = Mathf.Max(hitStopUntil, realtimeNow + duration);
    }

    public bool IsHitStopActive(float realtimeNow) => realtimeNow < hitStopUntil;

    public bool ShouldPause(bool modalPauseActive, float realtimeNow) =>
        modalPauseActive || IsHitStopActive(realtimeNow);

    public void Clear() => hitStopUntil = float.NegativeInfinity;
}

/// <summary>
/// v2.1.2 唯一暂停与模态输入服务。所有面板通过令牌进入/离开；世界输入只查询本服务。
/// UI 动画必须继续使用 unscaledDeltaTime。场景切换会清空旧场景令牌，避免假引用永久锁死。
/// </summary>
public static class GameModalService
{
    private static readonly GameModalStack stack = new GameModalStack();
    private static readonly TimePauseArbiter timeArbiter = new TimePauseArbiter();
    private static bool pauseApplied;
    private static float resumeTimeScale = 1f;
    private static int blockWorldInputThroughFrame = -1;
    private static GameModalDriver driver;

    public static event Action StateChanged;

    public static bool HasModal => stack.HasModal;
    public static bool BlocksWorldInput => stack.BlocksWorldInput
        || Time.frameCount <= blockWorldInputThroughFrame;
    public static bool IsWorldPaused => timeArbiter.ShouldPause(stack.PausesWorld, Time.realtimeSinceStartup);
    public static int ModalCount => stack.Count;
    public static GameModalKind? TopKind => stack.TopKind;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        stack.Clear();
        timeArbiter.Clear();
        pauseApplied = false;
        resumeTimeScale = 1f;
        blockWorldInputThroughFrame = -1;
        driver = null;
        StateChanged = null;
        Time.timeScale = 1f;
    }

    public static GameModalToken Push(
        GameModalKind kind,
        Action cancel = null,
        bool pausesWorld = true,
        bool blocksWorldInput = true)
    {
        EnsureDriver();
        GameModalToken token = stack.Push(kind, pausesWorld, blocksWorldInput, cancel);
        ApplyTimeState();
        StateChanged?.Invoke();
        return token;
    }

    public static void Release(ref GameModalToken token)
    {
        bool blockedBeforeRelease = stack.BlocksWorldInput;
        if (!stack.Release(token))
        {
            token = default;
            return;
        }

        token = default;
        if (blockedBeforeRelease && !stack.BlocksWorldInput)
            blockWorldInputThroughFrame = Time.frameCount;
        ApplyTimeState();
        StateChanged?.Invoke();
    }

    public static bool IsTop(GameModalToken token) => stack.IsTop(token);
    public static bool IsTop(GameModalKind kind) => stack.IsTop(kind);

    /// <summary>有顶层模态时始终消费取消输入；不可取消层不会把 Esc 泄漏到世界。</summary>
    public static bool TryCancelTop() => stack.TryCancelTop();

    public static void RequestHitStop(float duration)
    {
        if (duration <= 0f) return;
        EnsureDriver();
        timeArbiter.RequestHitStop(Time.realtimeSinceStartup, duration);
        ApplyTimeState();
    }

    private static void Tick()
    {
        ApplyTimeState();
    }

    private static void ApplyTimeState()
    {
        bool shouldPause = timeArbiter.ShouldPause(stack.PausesWorld, Time.realtimeSinceStartup);
        if (shouldPause)
        {
            if (!pauseApplied)
            {
                resumeTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                pauseApplied = true;
            }
            if (Time.timeScale != 0f) Time.timeScale = 0f;
            return;
        }

        if (!pauseApplied) return;
        pauseApplied = false;
        Time.timeScale = resumeTimeScale > 0f ? resumeTimeScale : 1f;
    }

    private static void ClearForSceneChange()
    {
        bool hadState = stack.HasModal || pauseApplied;
        stack.Clear();
        timeArbiter.Clear();
        blockWorldInputThroughFrame = Time.frameCount;
        ApplyTimeState();
        if (hadState) StateChanged?.Invoke();
    }

    private static void EnsureDriver()
    {
        if (driver != null) return;
        GameObject go = new GameObject("GameModalService");
        go.hideFlags = HideFlags.HideInHierarchy;
        UnityEngine.Object.DontDestroyOnLoad(go);
        driver = go.AddComponent<GameModalDriver>();
    }

    private sealed class GameModalDriver : MonoBehaviour
    {
        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
        private void Update() => Tick();
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ClearForSceneChange();
    }
}
