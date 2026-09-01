using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 狼人变身（v1.0.9，自 v0.6.8 WerewolfTransformation 移植到 v0.7.5 架构还原）：
/// 角色=狼人（CharacterSelectUI 选择）时挂载，**T = 狼↔兽化**。
/// - 兽化：变身演出（Transform_L/R 帧逐段膨胀 ×1→1.5）→ Beast 帧（1080px 画布自带大体型）驱动；
///   血量等比 ×1.5（Health.ScaleMaxHealth，退兽化 1/N 还原）、伤害 ×1.3、攻速 ×1.1、移速 ×1.1（PlayerStats 兽化乘数）。
/// - 狼形态常驻：WeaponPivot ×1.5（wolfAttackScale）——判定长度/宽度、武器视觉、预警三者派生自
///   pivot.lossyScale 同源链，一处放大三处同步（v0.6.7 原设计，不破坏 R7）。
/// - 血条锚点：狼 1.0 / 兽化 1.45。
/// 输入：直接轮询 Keyboard.current.tKey（输入资产无 Transform action，避免动 .inputactions）。
/// 选择页改回战士时由应用侧 Destroy 本组件（OnDestroy 还原 pivot/数值/血条）。
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

        // 狼形态常驻：判定/武器视觉/预警同源放大（v0.6.7）；改回战士时 OnDestroy 复位
        weaponPivot = transform.Find("WeaponPivot");
        if (weaponPivot != null)
        {
            weaponPivotBaseScale = weaponPivot.localScale;
            weaponPivot.localScale = weaponPivotBaseScale * wolfAttackScale;
        }

        SetHealthBarHeight(BarHeightWolf);
        Debug.Log("[Werewolf] 狼形态就绪：T=兽化（血量×1.5 伤害×1.3 攻速×1.1 移速×1.1 判定×1.5）");
    }

    void OnDestroy()
    {
        // 还原一切狼人形态残留（组件销毁 = 改选战士或场景卸载；场景卸载时对象将死，复位无副作用）
        if (weaponPivot != null)
            weaponPivot.localScale = weaponPivotBaseScale;
        ApplyBeastStats(false);
        SetHealthBarHeight(1.0f);
    }

    void Update()
    {
        if (Keyboard.current == null) return;
        if (!Keyboard.current.tKey.wasPressedThisFrame) return;
        if (transforming) return;
        if (ClassSelectUI.IsOpen || CharacterSelectUI.IsOpen) return;   // 选择页打开时不变身
        if (health != null && health.IsDead) return;
        Toggle();
    }

    /// <summary>兽化按 T 直接回普通狼；普通狼按 T 进兽化（播变身演出）。</summary>
    public void Toggle()
    {
        if (IsBeast)
        {
            IsBeast = false;
            if (health != null) health.ScaleMaxHealth(1f / beastHealthScale);   // 等比还原（v0.6.9）
            ApplyBeastStats(false);
            if (animator != null) animator.SetBeastForm(false);
            Debug.Log("[Werewolf] 退兽化：回普通狼");
            return;
        }
        StartCoroutine(BeastTransformRoutine());
    }

    private IEnumerator BeastTransformRoutine()
    {
        transforming = true;
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
                yield return null;
            }
        }

        IsBeast = true;
        if (health != null) health.ScaleMaxHealth(beastHealthScale);
        ApplyBeastStats(true);
        if (animator != null) animator.SetBeastForm(true);   // 切 Beast 帧组，缩放回基准（1080px 画布自带大体型）
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
