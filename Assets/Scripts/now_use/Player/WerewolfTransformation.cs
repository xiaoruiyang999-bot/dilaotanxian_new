using System.Collections;
using UnityEngine;

/// <summary>
/// 狼人变身（v1.0.9，自 v0.6.8 WerewolfTransformation 移植到 v0.7.5 架构还原）：
/// 角色=狼人（CharacterSelectUI 选择）时挂载，怒痕满后由 Q 触发兽化。
/// - 兽化：变身演出（Transform_L/R 帧逐段膨胀 ×1→1.5）→ Beast 帧（1080px 画布自带大体型）驱动；
///   血量等比 ×1.5（Health.ScaleMaxHealth，退兽化 1/N 还原）、伤害 ×1.3、攻速 ×1.1、移速 ×1.1（PlayerStats 兽化乘数）。
/// - 狼形态常驻：WeaponPivot ×1.5（wolfAttackScale）——判定长度/宽度、武器视觉、预警三者派生自
///   pivot.lossyScale 同源链，一处放大三处同步（v0.6.7 原设计，不破坏 R7）。
/// - 血条锚点：狼 1.0 / 兽化 1.45。
/// 输入统一由 PlayerController 的 Ultimate 动作转发；本组件不直接轮询键盘。
/// 切换到其他职业角色时由装配侧 Destroy，本组件在 OnDestroy 还原临时状态。
/// 【美术资产缺失】BeastBurstFX 变身粒子（v0.6.9）未随合并保留，待重新制作后接回。
/// </summary>
public class WerewolfTransformation : MonoBehaviour
{
    [Header("参数（v0.6.8 原值）")]
    [Tooltip("变身动画帧率（4fps = 每帧 0.25s）")]
    [SerializeField] private float transformFps = 4f;
    [Tooltip("狼形态攻击范围/武器视觉放大倍数（WeaponPivot 同源链）")]
    [SerializeField] private float wolfAttackScale = 1.5f;
    [Tooltip("兽化血量放大倍数（上限与当前血等比 ×N，退兽化等比还原）")]
    [SerializeField] private float beastHealthScale = 1.5f;
    [SerializeField] private float beastDamageMult = 1.3f;
    [SerializeField] private float beastAttackSpeedMult = 1.1f;
    [SerializeField] private float beastMoveSpeedMult = 1.1f;
    [Tooltip("兽化持续时间（秒）；结束后自动退回普通形态")]
    [SerializeField, Min(0.1f)] private float beastDuration = 12f;

    private const float BarHeightWolf = 1.0f;
    private const float BarHeightBeast = 1.45f;

    public bool IsBeast { get; private set; }

    private bool transforming;
    private bool lastFacingLeft;
    private Health health;
    private PlayerStats stats;
    private FrameAnimator animator;
    private Transform weaponPivot;
    private Vector3 weaponPivotBaseScale = Vector3.one;
    private Transform healthBarAnchor;
    private WerewolfEnergyBar energyBar;
    private WerewolfRage rage;
    private float beastTimeRemaining;

    public WerewolfRage Rage => rage;
    public bool IsTransforming => transforming;
    public float BeastTimeRemaining => beastTimeRemaining;
    public float BeastNormalizedRemaining => IsBeast && beastDuration > 0f
        ? Mathf.Clamp01(beastTimeRemaining / beastDuration)
        : 0f;

    /// <summary>确保玩家身上挂了狼人变身组件（场景应用侧/选择页确认时调用）。</summary>
    public static WerewolfTransformation EnsureOn(GameObject player)
    {
        WerewolfTransformation w = player.GetComponent<WerewolfTransformation>();
        if (w == null) w = player.AddComponent<WerewolfTransformation>();
        return w;
    }

    void Awake()
    {
        health = GetComponent<Health>();
        stats = GetComponent<PlayerStats>();
        animator = GetComponent<FrameAnimator>();
        rage = GetComponent<WerewolfRage>();
        if (rage == null)
            rage = gameObject.AddComponent<WerewolfRage>();

        // 狼形态常驻：判定/武器视觉/预警同源放大；切换其他角色时 OnDestroy 复位
        weaponPivot = transform.Find("WeaponPivot");
        if (weaponPivot != null)
        {
            weaponPivotBaseScale = weaponPivot.localScale;
            weaponPivot.localScale = weaponPivotBaseScale * wolfAttackScale;
        }

        SetHealthBarHeight(BarHeightWolf);

        // v1.1.52：狼人专属头顶能量条（伤害充能，充满脉动提示；随组件销毁）
        energyBar = GetComponent<WerewolfEnergyBar>();
        if (energyBar == null)
            energyBar = gameObject.AddComponent<WerewolfEnergyBar>();

        Debug.Log("[Werewolf] 狼形态就绪：怒痕满后按 Q 兽化（血量×1.5 伤害×1.3 攻速×1.1 移速×1.1 判定×1.5）");
    }

    void OnDestroy()
    {
        // 还原狼人形态残留（组件销毁 = 切换职业角色或场景卸载）。
        if (weaponPivot != null)
            weaponPivot.localScale = weaponPivotBaseScale;
        ResetTransformation();
        SetHealthBarHeight(1.0f);
        if (energyBar != null) Destroy(energyBar);
        if (rage != null) Destroy(rage);
    }

    void Update()
    {
        if (!IsBeast || transforming) return;

        if (health != null && health.IsDead)
        {
            ExitBeastForm();
            return;
        }

        beastTimeRemaining = Mathf.Max(0f, beastTimeRemaining - Time.deltaTime);
        if (beastTimeRemaining <= 0f)
        {
            ExitBeastForm();
            Debug.Log("[Werewolf] 兽化持续时间结束，自动退出兽化形态");
        }
    }

    /// <summary>由 Ultimate(Q) 调用。仅怒痕满且当前为普通形态时进入兽化。</summary>
    public bool TryActivateUltimate()
    {
        if (transforming || IsBeast) return false;
        if (CharacterSelectUI.IsOpen) return false;
        if (health != null && health.IsDead) return false;

        if (rage == null || !rage.TryConsumeFull())
        {
            float currentRage = rage != null ? rage.Current : 0f;
            float requiredRage = rage != null ? rage.Max : 0f;
            Debug.Log($"[Werewolf] 怒痕未满，无法兽化（{currentRage:0}/{requiredRage:0}）");
            return false;
        }

        rage.SetGainPaused(true);
        StartCoroutine(BeastTransformRoutine());
        return true;
    }

    /// <summary>保留给旧编辑器调试入口；正式输入链不再调用。</summary>
    public void Toggle()
    {
        if (IsBeast)
        {
            ExitBeastForm();
            return;
        }

        TryActivateUltimate();
    }

    [System.Obsolete("兽化现在按持续时间结束，不再由能量条耗尽驱动。")]
    public void ExitBeastFromEnergyDepleted()
    {
        if (!IsBeast) return;
        ExitBeastForm();
        Debug.Log("[Werewolf] 能量耗尽，自动退出兽化形态");
    }

    public void ResetTransformation()
    {
        StopAllCoroutines();
        transforming = false;
        beastTimeRemaining = 0f;
        if (IsBeast) ExitBeastForm();
        else
        {
            ApplyBeastStats(false);
            if (animator != null)
            {
                animator.SetBeastForm(false);
                animator.SetWerewolfVisualGrow(1f);
            }
            energyBar?.SetBeastVisualTarget(false);
            rage?.SetGainPaused(false);
        }
    }

    private void ExitBeastForm()
    {
        IsBeast = false;
        beastTimeRemaining = 0f;
        if (health != null) health.ScaleMaxHealth(1f / beastHealthScale);
        ApplyBeastStats(false);
        if (animator != null)
        {
            animator.SetBeastForm(false);
            animator.SetWerewolfVisualGrow(1f);
        }
        energyBar?.SetBeastVisualTarget(false);
        rage?.SetGainPaused(false);
    }

    private IEnumerator BeastTransformRoutine()
    {
        transforming = true;
        energyBar?.SetBeastVisualProgress(0f);
        lastFacingLeft = animator != null && animator.LastHorizontalInput < 0f;

        // 变身演出：Transform 帧覆盖播放 + 视觉逐段膨胀 1→1.5（v0.6.5 原逻辑）；
        // 帧组缺失时跳过演出直接进兽化（血量照常放大）
        float duration = 6f / transformFps;   // 变身帧 6 张（帧数可变，组内实际帧数对齐时长）
        bool playing = animator != null && animator.PlayTransform(lastFacingLeft, duration);
        if (playing)
        {
            int frames = animator.TransformFrameCount(lastFacingLeft);
            if (frames > 0) duration = frames / transformFps;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float ratio = Mathf.Clamp01(t / duration);
                animator.SetWerewolfVisualGrow(1f + 0.5f * ratio);   // 基准 → ×1.5
                energyBar?.SetBeastVisualProgress(ratio);
                yield return null;
            }
        }

        energyBar?.SetBeastVisualProgress(1f);

        IsBeast = true;
        beastTimeRemaining = beastDuration;
        if (health != null) health.ScaleMaxHealth(beastHealthScale);
        ApplyBeastStats(true);
        if (animator != null) animator.SetBeastForm(true);   // 切 Beast 帧组，缩放回基准（1080px 画布自带大体型）
        energyBar?.SetBeastVisualTarget(true);
        transforming = false;
        Debug.Log("[Werewolf] 兽化完成：血量/伤害/攻速/移速已提升");
    }
    /// <summary>兽化数值乘数（v0.7.1 乘数体系：写入 PlayerStats，消费点为 Attack/MoveSpeed/AttackSpeedMul）。</summary>
    private void ApplyBeastStats(bool on)
    {
        if (stats == null) return;
        stats.BeastDamageMult = on ? beastDamageMult : 1f;
        stats.BeastAttackSpeedMult = on ? beastAttackSpeedMult : 1f;
        stats.BeastMoveSpeedMult = on ? beastMoveSpeedMult : 1f;
        SetHealthBarHeight(on ? BarHeightBeast : BarHeightWolf);
    }

    /// <summary>血条锚点高度（v0.8.1：开局普通狼也抬血条）。</summary>
    private void SetHealthBarHeight(float y)
    {
        if (healthBarAnchor == null)
            healthBarAnchor = transform.Find("HealthBarAnchor");
        if (healthBarAnchor != null)
            healthBarAnchor.localPosition = new Vector3(healthBarAnchor.localPosition.x, y, healthBarAnchor.localPosition.z);
    }
}
