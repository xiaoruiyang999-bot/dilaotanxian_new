using System;
using UnityEngine;

/// <summary>
/// V2 狼人怒痕运行时真值。负责时间/有效伤害充能、上限保护、消耗与重置；
/// UI 只订阅事件呈现，兽化组件只发送“尝试发动/暂停充能”意图。
/// </summary>
[DisallowMultipleComponent]
public sealed class WerewolfRage : MonoBehaviour
{
    [Header("怒痕参数（v2.0.1 测试值）")]
    [SerializeField, Min(1f)] private float maxRage = 100f;
    [SerializeField, Min(0f)] private float passiveRagePerSecond = 2f;
    [SerializeField, Min(0f)] private float ragePerDamage = 0.5f;
    [SerializeField, Min(0f)] private float maxRagePerDamageEvent = 15f;
    [SerializeField, Min(0f)] private float maxRagePerFrame = 20f;

    public float Current { get; private set; }
    public float Max => maxRage;
    public float Normalized => maxRage > 0f ? Current / maxRage : 0f;
    public bool IsFull => Current >= maxRage;
    public bool PassiveRecoveryEnabled { get; private set; }
    public bool GainPaused { get; private set; }

    public event Action<float, float> OnChanged;
    public event Action OnBecameFull;
    public event Action OnInsufficient;

    private int damageGainFrame = -1;
    private float damageGainThisFrame;

    private void OnEnable()
    {
        DamageResolver.OnPlayerDamageDealt += OnPlayerDamageDealt;
    }

    private void OnDisable()
    {
        DamageResolver.OnPlayerDamageDealt -= OnPlayerDamageDealt;
    }

    private void Update()
    {
        if (!PassiveRecoveryEnabled || GainPaused || IsFull) return;
        AddRage(passiveRagePerSecond * (1f + RelicRuntime.Run.RageGainBonus) * Time.deltaTime);   // VS 第四批 狼群之饥
    }

    /// <summary>由 Run 生命周期显式控制；准备房默认关闭，避免站在大厅自动充满。</summary>
    public void SetPassiveRecoveryEnabled(bool enabled)
    {
        PassiveRecoveryEnabled = enabled;
    }

    /// <summary>兽化及切换演出期间默认暂停所有怒痕获取。</summary>
    public void SetGainPaused(bool paused)
    {
        GainPaused = paused;
    }

    public bool TryConsumeFull()
    {
        if (!IsFull)
        {
            OnInsufficient?.Invoke();
            return false;
        }

        SetCurrent(0f);
        GainPaused = true;
        return true;
    }

    public void ResetRage()
    {
        GainPaused = false;
        damageGainFrame = -1;
        damageGainThisFrame = 0f;
        SetCurrent(0f);
    }

#if UNITY_EDITOR
    public void DebugFill()
    {
        SetCurrent(Max);
    }
#endif

    private void OnPlayerDamageDealt(float actualDamage)
    {
        if (GainPaused || actualDamage <= 0f || IsFull) return;

        if (damageGainFrame != Time.frameCount)
        {
            damageGainFrame = Time.frameCount;
            damageGainThisFrame = 0f;
        }

        float relicMul = 1f + RelicRuntime.Run.RageGainBonus;   // VS 第四批 狼群之饥（伤害转化路）
        float gain = CalculateDamageGain(
            actualDamage * relicMul,
            ragePerDamage,
            maxRagePerDamageEvent,
            damageGainThisFrame,
            maxRagePerFrame);
        if (gain <= 0f) return;

        damageGainThisFrame += gain;
        AddRage(gain);
    }

    private void AddRage(float amount)
    {
        if (amount <= 0f || GainPaused || IsFull) return;
        SetCurrent(Current + amount);
    }

    private void SetCurrent(float value)
    {
        bool wasFull = IsFull;
        Current = Mathf.Clamp(value, 0f, maxRage);
        OnChanged?.Invoke(Current, maxRage);
        if (!wasFull && IsFull)
            OnBecameFull?.Invoke();
    }

    public static float CalculateDamageGain(
        float actualDamage,
        float gainPerDamage,
        float perEventCap,
        float gainedThisFrame,
        float perFrameCap)
    {
        if (actualDamage <= 0f || gainPerDamage <= 0f) return 0f;

        float eventGain = actualDamage * gainPerDamage;
        if (perEventCap > 0f)
            eventGain = Mathf.Min(eventGain, perEventCap);
        if (perFrameCap > 0f)
            eventGain = Mathf.Min(eventGain, Mathf.Max(0f, perFrameCap - gainedThisFrame));
        return Mathf.Max(0f, eventGain);
    }

    private void OnValidate()
    {
        maxRage = Mathf.Max(1f, maxRage);
        passiveRagePerSecond = Mathf.Max(0f, passiveRagePerSecond);
        ragePerDamage = Mathf.Max(0f, ragePerDamage);
        maxRagePerDamageEvent = Mathf.Max(0f, maxRagePerDamageEvent);
        maxRagePerFrame = Mathf.Max(0f, maxRagePerFrame);
        Current = Mathf.Clamp(Current, 0f, maxRage);
    }
}
