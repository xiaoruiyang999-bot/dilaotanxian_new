using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 世界空间血条。挂载在敌人（或任何有Health的对象）上，在头顶显示血条。
/// </summary>
[RequireComponent(typeof(Health))]
public class WorldSpaceHealthBar : MonoBehaviour
{
    [Header("血条UI预制")]
    [SerializeField] private Sprite backgroundSprite;   // WhiteSquare.asset
    [SerializeField] private Color backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f); // 深灰
    [SerializeField] private Color fillColor = new Color(0.9f, 0.1f, 0.1f, 1f);       // 红色

    [Header("血条尺寸")]
    [SerializeField] private float barWidth = 0.5f;   // 基准宽度（对应1单位大小的敌人）
    [SerializeField] private float barHeight = 0.08f;  // 基准高度
    [SerializeField] private float yOffset = 0.85f;   // 基准头顶偏移

    private Health health;
    private Transform canvasTransform;
    private Image fillImage;
    private float currentYOffset;

    void Awake()
    {
        health = GetComponent<Health>();
        CreateHealthBar();
    }

    void OnEnable()
    {
        if (health != null)
            health.OnHealthChanged += OnHealthChanged;
    }

    void OnDisable()
    {
        if (health != null)
            health.OnHealthChanged -= OnHealthChanged;
    }

    void LateUpdate()
    {
        // 血条位置跟随父对象头顶
        if (canvasTransform != null)
        {
            canvasTransform.position = transform.position + Vector3.up * currentYOffset;
        }
    }

    private void CreateHealthBar()
    {
        // 根据敌人实际大小等比例缩放血条
        float enemyScale = Mathf.Max(transform.localScale.x, transform.localScale.y, 0.001f);
        float width = barWidth * enemyScale;
        float height = barHeight * enemyScale;
        currentYOffset = yOffset * enemyScale;

        // 尺寸用像素表示（CanvasScaler dynamicPixelsPerUnit=100）
        Vector2 sizeInPixels = new Vector2(width * 100f, height * 100f);

        // 创建世界空间Canvas
        GameObject canvasGo = new GameObject("HealthBarCanvas");
        canvasGo.transform.SetParent(transform);
        canvasTransform = canvasGo.transform;
        canvasTransform.localPosition = new Vector3(0, currentYOffset, 0);
        canvasTransform.localScale = new Vector3(0.02f, 0.02f, 0.02f);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10; // 在角色和敌人之上

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 100f;

        // 关键：显式限制Canvas大小，避免默认尺寸过大遮挡画面
        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = sizeInPixels;

        // 创建背景Image（填满Canvas）
        GameObject bgGo = new GameObject("Background");
        bgGo.transform.SetParent(canvasGo.transform, false);
        Image bgImage = bgGo.AddComponent<Image>();
        bgImage.color = backgroundColor;
        if (backgroundSprite != null) bgImage.sprite = backgroundSprite;

        RectTransform bgRect = bgImage.rectTransform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // 创建填充Image（填满背景）
        GameObject fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(bgGo.transform, false);
        fillImage = fillGo.AddComponent<Image>();
        fillImage.color = fillColor;
        if (backgroundSprite != null) fillImage.sprite = backgroundSprite;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0; // Left
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
