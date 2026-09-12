using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("跟随目标")]
    [SerializeField] private Transform target;

    [Header("平滑参数")]
    [SerializeField] private float smoothSpeed = 10f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);
    [Tooltip("将相机最终位置对齐到屏幕像素，防止 Tilemap 亚像素采样产生细缝")]
    [SerializeField] private bool snapToScreenPixels = true;

    [Header("像素对齐（v1.1.49）")]
    [Tooltip("正交尺寸自动换算为『每地皮纹素 = 整数屏幕像素』——非整数缩放比下砖缝随相机移动取舍闪烁的根治；关闭则用场景原值")]
    [SerializeField] private bool pixelPerfectOrtho = true;
    [Tooltip("主 Tilemap（地皮）的 PPU。ortho = 屏幕高 ÷ (2×PPU×n)，n 取最贴近原视野的整数档")]
    [SerializeField] private float tilePPU = 144f;
    [Tooltip("目标视野（正交尺寸，越大看得越多；0 = 沿用相机在场景里的初始值）。整纹素档自动向它逼近——注意 1080p 下最大档约 3.75，想更远需开下方亚像素开关")]
    [SerializeField] private float targetOrthoSize = 0f;
    [Tooltip("允许视野超过整纹素最大档（纹理小于屏幕像素：轻微画质损失/可能轻闪）——需要看得更远时自担代价打开")]
    [SerializeField] private bool allowSubPixelZoom = false;

    /// <summary>Awake 记录的场景原正交值（targetOrthoSize=0 时的设计视野回退）。</summary>
    private float designOrthoSize;
    private int lastScreenHeight;

    [Header("屏幕震动（M1·v0.6.1）")]
    [Tooltip("外部震动强度的全局缩放，方便整体调手感")]
    [SerializeField] private float shakeScale = 1f;

    private Vector3 currentVelocity;
    private Camera attachedCamera;

    // 震动状态：unscaled 时间驱动，hit-stop（timeScale=0）期间照常衰减，两者叠加是经典手感组合
    private float shakeTimeRemaining;
    private float shakeTotalTime;
    private float shakeBaseIntensity;
    private static CameraFollow cachedMain;

    private void Awake()
    {
        attachedCamera = GetComponent<Camera>();
        designOrthoSize = attachedCamera != null ? attachedCamera.orthographicSize : 5f;
        ApplyPixelPerfectOrtho();
    }

    /// <summary>
    /// v1.1.49 整纹素正交换算：ortho = Screen.height / (2×PPU×n)，n 自动取最贴近目标视野的整数档
    ///（每纹素恰 n 屏幕像素 → 纹素边界恒落屏幕像素边界，移动采样零漂移）。
    /// v1.1.51.2 视野可调：Target Ortho Size 指定目标（0=场景相机原值）；目标超过整纹素最大档时，
    /// 仅在 Allow SubPixel Zoom 打开后直取目标值（纹理小于屏幕像素，轻微画质损失自担）。
    /// </summary>
    private void ApplyPixelPerfectOrtho()
    {
        if (!pixelPerfectOrtho || attachedCamera == null || !attachedCamera.orthographic) return;
        if (Screen.height <= 0) return;

        float design = targetOrthoSize > 0f ? targetOrthoSize : designOrthoSize;
        float texelOnePixel = Screen.height / (2f * tilePPU);

        if (allowSubPixelZoom && design > texelOnePixel)
        {
            attachedCamera.orthographicSize = design;   // 超档直取（亚像素代价自担）
            return;
        }

        int n = Mathf.Max(1, Mathf.RoundToInt(texelOnePixel / design));
        attachedCamera.orthographicSize = texelOnePixel / n;
    }

    void LateUpdate()
    {
        // 窗口分辨率变化时重算整纹素正交档（开销：一次 int 比较）
        if (Screen.height != lastScreenHeight)
        {
            lastScreenHeight = Screen.height;
            ApplyPixelPerfectOrtho();
        }

        if (target == null) return;

        // 目标位置 = 玩家位置 + 偏移（Z轴保持-10，确保相机在2D平面之上）
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

        ClampToMapBounds();
        SnapCameraToScreenPixel();
    }

    // ========== 地图边界锁定（v1.1.50）==========
    // 相机中心被钳制在「地图边界内缩半屏」矩形内：角色贴近地图边缘时相机钉在边界不动
    //（屏幕边缘恰与地图边缘齐，不露虚空）；角色往回走、跟随目标回到钳制线内相机自然恢复——
    // 即"角色回到相机中心线附近才再次跟随"的观感。地图某轴小于屏幕时该轴居中。

    [Header("地图边界锁定（v1.1.50，Inspector 可调）")]
    [Tooltip("钳制线在半屏基础上的额外水平余量（世界单位）：正 = 屏幕边离地图边更远（少露地图）；负 = 允许多露出地图外")]
    [SerializeField] private float boundsMarginX = 0f;
    [Tooltip("钳制线在半屏基础上的额外垂直余量（世界单位）：正 = 少露地图；负 = 多露地图外")]
    [SerializeField] private float boundsMarginY = 0f;
    [Tooltip("选中相机时在 Scene 视图画出地图边界（黄）与钳制线（红）辅助手动调节")]
    [SerializeField] private bool drawBoundsGizmos = true;

    private static Rect mapBounds;
    private static bool hasMapBounds;

    /// <summary>设置地图世界边界（DungeonBuilder.Build / PrepRoomManager.Start 调用）。</summary>
    public static void SetMapBounds(Rect worldBounds)
    {
        mapBounds = worldBounds;
        hasMapBounds = true;
    }

    /// <summary>清除边界锁定（场景卸载/无边界场景用）。</summary>
    public static void ClearMapBounds() => hasMapBounds = false;

    /// <summary>把当前相机位置钳制进边界（跟随与 SnapToTarget 共用）。</summary>
    private void ClampToMapBounds()
    {
        if (!hasMapBounds || attachedCamera == null || !attachedCamera.orthographic) return;

        float halfH = attachedCamera.orthographicSize;
        float halfW = halfH * attachedCamera.aspect;
        Vector3 p = transform.position;

        p.x = ClampAxis(p.x, mapBounds.xMin, mapBounds.xMax, halfW + boundsMarginX);
        // v2.0.7 用户定案：上边界不再固定（向上看时镜头可出地图上缘，后期换背景）——Y 只钳下界        float yLo = mapBounds.yMin + halfH + boundsMarginY;        p.y = p.y < yLo ? yLo : p.y;
        transform.position = p;
    }

    /// <summary>单轴钳制：范围不足（含余量后地图小于屏幕）时取中点居中。</summary>
    private static float ClampAxis(float v, float min, float max, float half)
    {
        float lo = min + half, hi = max - half;
        if (lo > hi) return (min + max) * 0.5f;
        return Mathf.Clamp(v, lo, hi);
    }

    /// <summary>选中相机时可视化：黄 = 地图边界，红 = 钳制线（相机中心活动范围）。</summary>
    private void OnDrawGizmosSelected()
    {
        if (!drawBoundsGizmos || !hasMapBounds) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(mapBounds.center, new Vector3(mapBounds.width, mapBounds.height, 0f));

        float halfH = attachedCamera != null && attachedCamera.orthographic
            ? attachedCamera.orthographicSize + boundsMarginY : 5f;
        float halfW = attachedCamera != null && attachedCamera.orthographic
            ? attachedCamera.orthographicSize * attachedCamera.aspect + boundsMarginX : 8f;
        float w = Mathf.Max(0f, mapBounds.width - halfW * 2f);
        float h = Mathf.Max(0f, mapBounds.height - halfH * 2f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(mapBounds.center, new Vector3(w, h, 0f));
    }

    private void SnapCameraToScreenPixel()
    {
        if (!snapToScreenPixels || attachedCamera == null || !attachedCamera.orthographic || Screen.height <= 0) return;
        float unitsPerPixel = attachedCamera.orthographicSize * 2f / Screen.height;
        Vector3 p = transform.position;
        p.x = Mathf.Round(p.x / unitsPerPixel) * unitsPerPixel;
        p.y = Mathf.Round(p.y / unitsPerPixel) * unitsPerPixel;
        transform.position = p;
    }

    /// <summary>瞬移到目标位置（出生 / 楼层切换时调用，避免镜头横穿全图）。</summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        transform.position = target.position + offset;
        currentVelocity = Vector3.zero;
        ClampToMapBounds();
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
