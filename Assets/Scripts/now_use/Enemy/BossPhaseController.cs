using DG.Tweening;
using UnityEngine;

/// <summary>
/// Boss 阶段控制器（M3·v0.8.1；v2.0.8 升级三段，V2 §9.4 守门→追猎→失控）：
/// P2 追猎（HP ≤ 50%）：替换招式池（冲锋/切割入池）、冷却 ×0.7、身体染红脉冲；
/// P3 失控（HP ≤ 25%）：再换池（phase3Attacks 空=沿用 P2）、冷却再 ×0.85、持续赤红。
/// 挂 Enemy_Boss prefab；阈值与倍率全参数化，具体招式差异在 AttackData SO 层配置。
/// </summary>
public class BossPhaseController : MonoBehaviour
{
    [Header("阶段阈值")]
    [Tooltip("P2 触发的血量比例（0.5 = 50%）")]
    [SerializeField, Range(0.05f, 1f)] private float phase2Threshold = 0.5f;

    [Header("P2 招式池（空=不换池）")]
    [SerializeField] private AttackData[] phase2Attacks;

    [Header("P2 强化")]
    [Tooltip("冷却乘数（0.7 ≈ 攻速 +43%，数值书 §5.3 攻击频率 +30% 档）")]
    [SerializeField] private float phase2CooldownScale = 0.7f;
    [Tooltip("P2 身体提示色（染红脉冲一次）")]
    [SerializeField] private Color phase2Tint = new Color(1f, 0.35f, 0.25f);

    [Header("P3 失控（v2.0.8，V2 §9.4）")]
    [Tooltip("P3 触发的血量比例（0 = 禁用第三段）")]
    [SerializeField, Range(0f, 1f)] private float phase3Threshold = 0.25f;
    [Tooltip("P3 招式池（空 = 沿用 P2 池，仅数值强化）")]
    [SerializeField] private AttackData[] phase3Attacks;
    [Tooltip("P3 冷却再乘（叠加 P2：0.85 ≈ 失控期更凶）")]
    [SerializeField] private float phase3CooldownScale = 0.85f;
    [Tooltip("P3 持续赤红色")]
    [SerializeField] private Color phase3Tint = new Color(1f, 0.22f, 0.15f);

    private EnemyHealth health;
    private EnemyCombat combat;
    private SpriteRenderer bodySprite;
    private Color baseColor;
    private bool phase2;
    private bool phase3;

    void Awake()
    {
        health = GetComponent<EnemyHealth>();
        combat = GetComponent<EnemyCombat>();
        bodySprite = GetComponent<SpriteRenderer>();
        if (bodySprite != null) baseColor = bodySprite.color;
        if (health != null)
            health.OnHealthChanged += OnHpChanged;
    }

    void OnDestroy()
    {
        if (health != null)
            health.OnHealthChanged -= OnHpChanged;
    }

    private void OnHpChanged(float current, float max)
    {
        if (max <= 0f) return;
        float ratio = current / max;

        // P3 失控（先于 P2 判定，防止低血直跳时漏段）
        if (!phase3 && phase3Threshold > 0f && ratio <= phase3Threshold)
        {
            phase3 = true;
            phase2 = true;   // 失控涵盖追猎强化
            if (combat != null)
            {
                if (phase3Attacks != null && phase3Attacks.Length > 0)
                    combat.SetAttackPool(phase3Attacks);
                combat.CooldownScale *= phase3CooldownScale;
            }
            if (bodySprite != null)
            {
                bodySprite.DOKill();
                bodySprite.color = phase3Tint;   // 持续赤红（失控态）
            }
            Debug.Log("[Boss] P3 失控：组合前段招式 + 更短安全窗口");
            return;
        }

        if (phase2 || ratio > phase2Threshold) return;

        phase2 = true;
        if (combat != null)
        {
            if (phase2Attacks != null && phase2Attacks.Length > 0)
                combat.SetAttackPool(phase2Attacks);
            combat.CooldownScale = phase2CooldownScale;
        }
        // 染红脉冲提示（DOTween；目标销毁自动 kill）
        if (bodySprite != null)
            bodySprite.DOColor(phase2Tint, 0.3f).SetLoops(4, LoopType.Yoyo)
                .OnComplete(() => bodySprite.color = Color.Lerp(baseColor, phase2Tint, 0.35f))
                .SetLink(gameObject);
        AudioManager.PlaySFX("enemyDie");   // 低吼提示（未配静默）
        Debug.Log("[Boss] 进入 P2：招式池切换 + 攻速提升");
    }
}
