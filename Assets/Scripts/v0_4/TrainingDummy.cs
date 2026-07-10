using System.Collections;
using UnityEngine;

/// <summary>
/// 伤害测试木桩。不移动、不攻击，被攻击时闪烁反馈，死亡后自动重置。
/// 用于v0.4测试玩家攻击伤害和敌人架构（EnemyHealth/WorldSpaceHealthBar）。
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(SpriteRenderer))]
public class TrainingDummy : MonoBehaviour
{
    [Header("木桩配置")]
    [SerializeField] private float resetDelay = 3f;         // 死亡后几秒重置
    [SerializeField] private float flashDuration = 0.15f;     // 受伤闪烁时间
    [SerializeField] private Color flashColor = Color.white;  // 闪烁颜色

    private EnemyHealth health;
    private SpriteRenderer sr;
    private Color originalColor;
    private bool isResetting = false;

    void Awake()
    {
        health = GetComponent<EnemyHealth>();
        sr = GetComponent<SpriteRenderer>();
        originalColor = sr.color;

        // 监听受伤和死亡事件
        health.OnTakeDamage += OnHit;
        health.OnDeath += OnDied;
    }

    void OnDestroy()
    {
        if (health != null)
        {
            health.OnTakeDamage -= OnHit;
            health.OnDeath -= OnDied;
        }
    }

    private void OnHit()
    {
        if (isResetting) return;
        StopAllCoroutines();
        StartCoroutine(HitFlashCoroutine());
    }

    private void OnDied()
    {
        if (isResetting) return;
        StartCoroutine(ResetCoroutine());
    }

    private IEnumerator HitFlashCoroutine()
    {
        sr.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        if (!isResetting) sr.color = originalColor;
    }

    private IEnumerator ResetCoroutine()
    {
        isResetting = true;

        // 变灰表示"死亡"
        sr.color = Color.gray;
        yield return new WaitForSeconds(resetDelay);

        // 重置生命
        health.ResetHealth();
        sr.color = originalColor;
        isResetting = false;

        Debug.Log("[TrainingDummy] 木桩已重置");
    }
}
