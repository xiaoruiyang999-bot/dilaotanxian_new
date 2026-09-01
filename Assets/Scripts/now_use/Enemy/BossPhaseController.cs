using DG.Tweening;
using UnityEngine;

/// <summary>
/// Boss 阶段控制器（M3·v0.8.1）：血量阈值切换阶段。
/// P2（HP ≤ 50%）：替换招式池（大招入池）、冷却 ×0.7（攻速 +43%）、身体染红脉冲提示。
/// Boss 死亡：大停顿（0.25s）+ 大震屏（M1 接口复用）——Boss 战的仪式感反馈。
/// 挂 Enemy_Boss prefab；与数值书 §5.3 的 P1/P2 行为表对应。
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

    private EnemyHealth health;
    private EnemyCombat combat;
    private SpriteRenderer bodySprite;
    private Color baseColor;
    private bool phase2;

    void Awake()
    {
        health = GetComponent<EnemyHealth>();
        combat = GetComponent<EnemyCombat>();
        bodySprite = GetComponent<SpriteRenderer>();
        if (bodySprite != null) baseColor = bodySprite.color;
        if (health != null)
        {
            health.OnHealthChanged += OnHpChanged;
            health.OnDeath += OnBossDeath;
        }
    }

    void OnDestroy()
    {
        if (health != null)
        {
            health.OnHealthChanged -= OnHpChanged;
            health.OnDeath -= OnBossDeath;
        }
    }

    private void OnHpChanged(float current, float max)
    {
        if (phase2 || max <= 0f) return;
        if (current / max > phase2Threshold) return;

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

    private void OnBossDeath()
    {
        // Boss 死亡仪式感：大停顿 + 大震屏（M1 接口）
        HitStop.Request(0.25f);
        CameraFollow.ShakeMain(0.5f, 0.8f);
        Debug.Log("[Boss] Boss 死亡：大停顿 + 大震屏");
    }
}
