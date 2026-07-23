using UnityEngine;

/// <summary>
/// 可破坏障碍物表现（v0.5.2，程序员美术）：
/// 受击闪白 0.15s（沿用 EnemyController 经验）；随血量颜色变深（裂纹占位）；
/// 破坏时销毁 GameObject（销毁后可通行——碰撞体随之消失）。
/// 与 ObstacleHealth 职责分离：本类不含任何生命数据。
/// </summary>
[RequireComponent(typeof(ObstacleHealth))]
public class DestructibleObstacle : MonoBehaviour
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private float flashTime = 0.15f;
    [Tooltip("血量见底时的最深亮度倍率（1 = 不变深，模拟裂纹用 0.4~0.5）")]
    [SerializeField, Range(0f, 1f)] private float minBrightness = 0.45f;

    private ObstacleHealth health;
    private Color baseColor;
    private Coroutine flashRoutine;

    private void Awake()
    {
        health = GetComponent<ObstacleHealth>();
        if (visual == null) visual = GetComponent<SpriteRenderer>();
        if (visual != null) baseColor = visual.color;

        health.OnTakeDamage += HandleTakeDamage;
        health.OnDestroyed += HandleDestroyed;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnTakeDamage -= HandleTakeDamage;
            health.OnDestroyed -= HandleDestroyed;
        }
    }

    private void HandleTakeDamage()
    {
        if (visual == null) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(Flash());
    }

    private void HandleDestroyed()
    {
        Destroy(gameObject);
    }

    private System.Collections.IEnumerator Flash()
    {
        visual.color = Color.white;
        yield return new WaitForSeconds(flashTime);
        visual.color = ShadedColor();
        flashRoutine = null;
    }

    /// <summary>按剩余血量比例把基础色压暗（满血 = 原色，见底 = minBrightness 倍）。</summary>
    private Color ShadedColor()
    {
        float t = health.MaxHp > 0 ? (float)health.CurrentHp / health.MaxHp : 0f;
        float k = Mathf.Lerp(minBrightness, 1f, t);
        return new Color(baseColor.r * k, baseColor.g * k, baseColor.b * k, baseColor.a);
    }
}
