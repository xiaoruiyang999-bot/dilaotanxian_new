using DG.Tweening;
using UnityEngine;

/// <summary>
/// 格兰双阶段生命阈值监听器。只负责发送“临界狼嚎”和“进入二阶段”命令，
/// 招式启动权始终属于 GrandBossBrain。旧 P3 三阶段逻辑已停用。
/// </summary>
public class BossPhaseController : MonoBehaviour
{
    [Header("阶段阈值")]
    [SerializeField, Range(0.05f, 1f)] private float phase2Threshold = 0.5f;
    [SerializeField, Range(0f, 0.25f)] private float phaseWarningLead = 0.05f;

    [Header("P2 AttackData 兼容池")]
    [SerializeField] private AttackData[] phase2Attacks;
    [SerializeField, Min(0.05f)] private float phase2CooldownScale = 0.7f;
    [SerializeField] private Color phase2Tint = new Color(1f, 0.35f, 0.25f);

    // 仅承接旧 Prefab 已序列化的 P3 引用，避免迁移期产生悬空覆盖项；运行逻辑禁止消费。
    [SerializeField, HideInInspector] private AttackData[] phase3Attacks;

    private EnemyHealth health;
    private EnemyCombat combat;
    private GrandBossBrain brain;
    private SpriteRenderer bodySprite;
    private Color baseColor;
    private float baseCooldownScale = 1f;
    private bool warningSent;
    private bool phase2;

    public bool WarningSent => warningSent;
    public bool PhaseTwoEntered => phase2;
    public event System.Action<int> OnPhaseEntered;

    public static bool ShouldEnterPhaseTwo(float healthRatio, float threshold, bool alreadyEntered)
        => !alreadyEntered && healthRatio <= threshold;

    public static bool ShouldSendWarning(float healthRatio, float phaseThreshold, float warningLead,
        bool alreadySent, bool phaseEntered)
    {
        float warningThreshold = Mathf.Clamp01(phaseThreshold + warningLead);
        return !alreadySent && !phaseEntered
            && healthRatio > phaseThreshold && healthRatio <= warningThreshold;
    }

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        combat = GetComponent<EnemyCombat>();
        brain = GetComponent<GrandBossBrain>();
        bodySprite = GetComponent<SpriteRenderer>();
        if (bodySprite != null) baseColor = bodySprite.color;
        if (combat != null) baseCooldownScale = combat.CooldownScale;
        if (health != null) health.OnHealthChanged += OnHpChanged;
    }

    private void OnEnable()
    {
        warningSent = false;
        phase2 = false;
        if (combat != null) combat.CooldownScale = baseCooldownScale;
        if (bodySprite != null)
        {
            bodySprite.DOKill();
            bodySprite.color = baseColor;
        }
    }

    private void OnDisable()
    {
        if (bodySprite != null) bodySprite.DOKill();
    }

    private void OnDestroy()
    {
        if (health != null) health.OnHealthChanged -= OnHpChanged;
    }

    /// <summary>运行时自举时补接 Brain，消除 Prefab Awake 早于 AddComponent 的顺序依赖。</summary>
    public void BindBrain(GrandBossBrain owner)
    {
        brain = owner;
    }

    private void OnHpChanged(float current, float max)
    {
        if (max <= 0f || phase2) return;
        float ratio = current / max;

        if (ShouldEnterPhaseTwo(ratio, phase2Threshold, phase2))
        {
            phase2 = true;
            if (combat != null)
            {
                if (phase2Attacks != null && phase2Attacks.Length > 0)
                    combat.SetAttackPool(phase2Attacks);
                combat.CooldownScale = baseCooldownScale * phase2CooldownScale;
            }

            brain?.EnterPhaseTwo();
            OnPhaseEntered?.Invoke(2);
            if (bodySprite != null)
            {
                bodySprite.DOKill();
                bodySprite.color = Color.Lerp(baseColor, phase2Tint, 0.65f);
            }
            return;
        }

        if (ShouldSendWarning(ratio, phase2Threshold, phaseWarningLead, warningSent, phase2))
        {
            warningSent = true;
            brain?.EnterPhaseWarning();
            if (bodySprite != null)
            {
                bodySprite.DOKill();
                bodySprite.DOColor(phase2Tint, 0.22f)
                    .SetLoops(6, LoopType.Yoyo)
                    .SetLink(gameObject);
            }
        }
    }
}