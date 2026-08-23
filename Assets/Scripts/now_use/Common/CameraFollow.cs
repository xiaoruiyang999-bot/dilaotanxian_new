using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("跟随目标")]
    [SerializeField] private Transform target;

    [Header("平滑参数")]
    [SerializeField] private float smoothSpeed = 10f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

    [Header("屏幕震动（M1·v0.6.1）")]
    [Tooltip("外部震动强度的全局缩放，方便整体调手感")]
    [SerializeField] private float shakeScale = 1f;

    private Vector3 currentVelocity;

    // 震动状态：unscaled 时间驱动，hit-stop（timeScale=0）期间照常衰减，两者叠加是经典手感组合
    private float shakeTimeRemaining;
    private float shakeTotalTime;
    private float shakeBaseIntensity;
    private static CameraFollow cachedMain;

    void LateUpdate()
    {
        if (target == null) return;

        // 目标位置 = 战士位置 + 偏移（Z轴保持-10，确保相机在2D平面之上）
        Vector3 targetPosition = target.position + offset;

        // 使用SmoothDamp实现平滑跟随，避免生硬抖动
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref currentVelocity,
            1f / smoothSpeed
        );

        // 震动偏移叠加在跟随位之后：幅度随剩余时间线性衰减
        if (shakeTimeRemaining > 0f)
        {
            shakeTimeRemaining -= Time.unscaledDeltaTime;
            float falloff = Mathf.Max(shakeTimeRemaining / shakeTotalTime, 0f);
            transform.position += (Vector3)(Random.insideUnitCircle * (shakeBaseIntensity * falloff));
            if (shakeTimeRemaining <= 0f)
            {
                shakeBaseIntensity = 0f;
                shakeTotalTime = 0f;
            }
        }
    }

    /// <summary>瞬移到目标位置（出生 / 楼层切换时调用，避免镜头横穿全图）。</summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        transform.position = target.position + offset;
        currentVelocity = Vector3.zero;
        // 切层/出生时不携带旧震动
        shakeTimeRemaining = 0f;
        shakeBaseIntensity = 0f;
        shakeTotalTime = 0f;
    }

    /// <summary>屏幕震动（M1.7·v0.6.1）：intensity = 偏移幅度（世界单位），duration = 持续时间。重复调用取更强/更久者。</summary>
    public void Shake(float intensity, float duration)
    {
        shakeBaseIntensity = Mathf.Max(shakeBaseIntensity, intensity * shakeScale);
        shakeTimeRemaining = Mathf.Max(shakeTimeRemaining, duration);
        shakeTotalTime = Mathf.Max(shakeTotalTime, shakeTimeRemaining);
    }

    /// <summary>静态便捷入口：命中/受击等反馈方一行调用。找不到主相机时静默跳过。</summary>
    public static void ShakeMain(float intensity, float duration)
    {
        // 用 == null 判断（走 Unity 重载）：场景切换后缓存引用变 fake-null 时自动重找
        if (cachedMain == null)
        {
            Camera main = Camera.main;
            if (main != null) main.TryGetComponent(out cachedMain);
        }
        if (cachedMain != null) cachedMain.Shake(intensity, duration);
    }
}
