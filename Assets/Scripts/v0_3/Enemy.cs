using System.Collections;
using UnityEngine;

/// <summary>
/// 测试敌人。使用Health组件管理生命，WorldSpaceHealthBar显示头顶血条。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Health))]
public class Enemy : MonoBehaviour, IDamageable
{
    [Header("敌人属性")]
    [SerializeField] private float maxHealth = 3f;
    [SerializeField] private float hitFlashDuration = 0.15f;

    private SpriteRenderer sr;
    private Color originalColor;
    private Health health;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        originalColor = sr.color;
        health = GetComponent<Health>();

        // 初始化生命
        health.Initialize(maxHealth);
        health.OnDeath += OnEnemyDeath;
    }

    void OnDestroy()
    {
        if (health != null) health.OnDeath -= OnEnemyDeath;
    }

    /// <summary>
    /// 实现IDamageable接口
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (health.IsDead) return;

        health.TakeDamage(damage);
        StartCoroutine(HitFlashCoroutine());
        Debug.Log($"[Enemy] 受伤 {damage}，剩余 {health.CurrentHealth}/{health.MaxHealth}");
    }

    private IEnumerator HitFlashCoroutine()
    {
        sr.color = Color.white;
        yield return new WaitForSeconds(hitFlashDuration);
        sr.color = originalColor;
    }

    private void OnEnemyDeath()
    {
        sr.color = Color.gray;
        StartCoroutine(DeathCoroutine());
    }

    private IEnumerator DeathCoroutine()
    {
        float elapsed = 0f;
        float duration = 0.3f;
        Vector3 startScale = transform.localScale;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, elapsed / duration);
            yield return null;
        }
        Destroy(gameObject);
    }
}
