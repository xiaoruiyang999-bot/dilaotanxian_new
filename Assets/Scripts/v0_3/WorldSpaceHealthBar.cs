using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 世界空间血条。挂载在需要显示血条的物体上。
/// v0.4修改：支持Health（玩家/通用）和EnemyHealth（敌人）。
/// </summary>
public class WorldSpaceHealthBar : MonoBehaviour
{
    [Header("数据源（二选一）")]
    [SerializeField] private Health playerHealth;      // 给玩家或有Health的对象用
    [SerializeField] private EnemyHealth enemyHealth;  // 给有EnemyHealth的敌人用

    [Header("血条外观")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Color backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color fillColor = new Color(0.9f, 0.1f, 0.1f, 1f);

    [Header("血条尺寸")]
    [SerializeField] private float barWidth = 0.8f;
    [SerializeField] private float barHeight = 0.12f;
    [SerializeField] private float yOffset = 0.6f;

    private Transform canvasTransform;
    private Image fillImage;
    private Camera mainCamera;

    void Awake()
    {
        mainCamera = Camera.main;

        // 自动查找数据源（如果没在Inspector中设置）
        if (playerHealth == null && enemyHealth == null)
        {
            playerHealth = GetComponent<Health>();
            enemyHealth = GetComponent<EnemyHealth>();
        }

        // 至少需要一个数据源
        if (playerHealth == null && enemyHealth == null)
        {
            Debug.LogWarning($"[WorldSpaceHealthBar] {gameObject.name} 上未找到Health或EnemyHealth组件！");
            enabled = false;
            return;
        }

        CreateHealthBar();
    }

    void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged += OnHealthChanged;
        else if (enemyHealth != null)
            enemyHealth.OnHealthChanged += OnHealthChanged;
    }

    void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= OnHealthChanged;
        else if (enemyHealth != null)
            enemyHealth.OnHealthChanged -= OnHealthChanged;
    }

    void LateUpdate()
    {
        if (canvasTransform != null)
        {
            canvasTransform.position = transform.position + Vector3.up * yOffset;
        }
    }

    private void CreateHealthBar()
    {
        GameObject canvasGo = new GameObject("HealthBarCanvas");
        canvasGo.transform.SetParent(transform);
        canvasTransform = canvasGo.transform;
        canvasTransform.localPosition = new Vector3(0, yOffset, 0);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 100f;

        // 背景
        GameObject bgGo = new GameObject("Background");
        bgGo.transform.SetParent(canvasGo.transform, false);
        Image bgImage = bgGo.AddComponent<Image>();
        bgImage.color = backgroundColor;
        if (backgroundSprite != null) bgImage.sprite = backgroundSprite;
        bgImage.rectTransform.sizeDelta = new Vector2(barWidth * 100f, barHeight * 100f);

        // 填充
        GameObject fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(bgGo.transform, false);
        fillImage = fillGo.AddComponent<Image>();
        fillImage.color = fillColor;
        if (backgroundSprite != null) fillImage.sprite = backgroundSprite;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0;
        fillImage.fillAmount = 1f;

        RectTransform fillRect = fillImage.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
    }

    private void OnHealthChanged(float current, float max)
    {
        if (fillImage != null)
            fillImage.fillAmount = max > 0 ? current / max : 0f;
    }
}
