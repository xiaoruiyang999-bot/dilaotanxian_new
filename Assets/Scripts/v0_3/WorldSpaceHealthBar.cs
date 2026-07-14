using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 世界空间血条。基于 World Space Canvas 实现。
/// v0.4 修改：支持 Health（玩家/通用）和 EnemyHealth（敌人）。
/// v0.4.2 修改：引入 HealthBarAnchor，血条挂载到独立的 WorldUIRoot 下，
///             完全脱离敌人 Transform 层级，不跟随敌人旋转/缩放。
/// </summary>
public class WorldSpaceHealthBar : MonoBehaviour
{
    private const string WorldUIRootName = "WorldUIRoot";

    [Header("血条锚点")]
    [Tooltip("血条将跟随此锚点的世界位置。如果未指定，自动查找名为 'HealthBarAnchor' 的子物体；若仍找不到，则使用当前 Transform。")]
    [SerializeField] private Transform healthBarAnchor;

    [Header("数据源（二选一）")]
    [SerializeField] private Health playerHealth;      // 给玩家或有 Health 的对象用
    [SerializeField] private EnemyHealth enemyHealth;  // 给有 EnemyHealth 的敌人用

    [Header("血条外观")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Color backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color fillColor = new Color(0.9f, 0.1f, 0.1f, 1f);

    [Header("Canvas 设置（UI 像素）")]
    [Tooltip("Canvas 在 UI 像素空间下的宽高，直接决定血条分辨率")]
    [SerializeField] private Vector2 canvasSize = new Vector2(80f, 12f);
    [Tooltip("Canvas 在世界空间中的缩放，用于控制血条实际大小")]
    [SerializeField] private float canvasScale = 0.01f;
    [Tooltip("Canvas 渲染排序")]
    [SerializeField] private int canvasSortingOrder = 10;

    private GameObject canvasGo;
    private Transform canvasTransform;
    private Image fillImage;
    private Vector3 anchorBaseOffset; // 锚点相对敌人中心的世界偏移（已去除敌人初始旋转影响），不随旋转变化

    void Awake()
    {
        // 1. 确定血条锚点，并记录它相对敌人中心的世界偏移
        if (healthBarAnchor == null)
            healthBarAnchor = transform.Find("HealthBarAnchor");
        if (healthBarAnchor == null)
            healthBarAnchor = transform;

        // 先把当前世界偏移转换到敌人本地空间，去除敌人当前旋转/缩放父级的影响，
        // 之后每帧直接用 transform.position + anchorBaseOffset，保证血条只平移不旋转。
        Vector3 rawWorldOffset = healthBarAnchor.position - transform.position;
        anchorBaseOffset = Quaternion.Inverse(transform.rotation) * rawWorldOffset;

        // 2. 自动查找数据源
        if (playerHealth == null && enemyHealth == null)
        {
            playerHealth = GetComponent<Health>();
            enemyHealth = GetComponent<EnemyHealth>();
        }

        // 至少需要一个数据源
        if (playerHealth == null && enemyHealth == null)
        {
            Debug.LogWarning($"[WorldSpaceHealthBar] {gameObject.name} 上未找到 Health 或 EnemyHealth 组件！");
            enabled = false;
            return;
        }

        CreateHealthBar();
    }

    void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += OnHealthChanged;
            OnHealthChanged(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }
        else if (enemyHealth != null)
        {
            enemyHealth.OnHealthChanged += OnHealthChanged;
            OnHealthChanged(enemyHealth.CurrentHealth, enemyHealth.MaxHealth);
        }
    }

    void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= OnHealthChanged;
        else if (enemyHealth != null)
            enemyHealth.OnHealthChanged -= OnHealthChanged;
    }

    void OnDestroy()
    {
        if (canvasGo != null)
            Destroy(canvasGo);
    }

    void LateUpdate()
    {
        if (canvasTransform == null || healthBarAnchor == null) return;

        // 血条相对敌人中心保持静止：使用去旋转后的世界偏移，不随敌人旋转而改变
        canvasTransform.position = transform.position + anchorBaseOffset;
        canvasTransform.rotation = Quaternion.identity;
    }

    /// <summary>
    /// 获取或创建全局 WorldUIRoot，所有世界空间血条 Canvas 都挂载在它下面。
    /// </summary>
    private static Transform EnsureWorldUIRoot()
    {
        GameObject root = GameObject.Find(WorldUIRootName);
        if (root == null)
        {
            root = new GameObject(WorldUIRootName);
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
        }
        return root.transform;
    }

    private Sprite CreateDefaultSprite()
    {
        Texture2D tex = Texture2D.whiteTexture;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    private void CreateHealthBar()
    {
        canvasGo = new GameObject($"HealthBarCanvas_{gameObject.name}");
        // 挂载到独立的 WorldUIRoot，而不是敌人或锚点下
        Transform root = EnsureWorldUIRoot();
        canvasGo.transform.SetParent(root, false);

        // World Space Canvas
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = canvasSortingOrder;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 100f;

        // 必须在添加 Canvas 后再取 Transform，因为 Canvas 会把 Transform 替换为 RectTransform
        canvasTransform = canvasGo.GetComponent<RectTransform>();

        // 使用 UI 像素设置 Canvas 尺寸，再用 canvasScale 控制世界空间大小
        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.localPosition = Vector3.zero;
        canvasRect.localRotation = Quaternion.identity;
        // Pivot 设为底部中心，使血条整体位于锚点正上方，不穿透敌人
        canvasRect.anchorMin = new Vector2(0.5f, 0f);
        canvasRect.anchorMax = new Vector2(0.5f, 0f);
        canvasRect.pivot = new Vector2(0.5f, 0f);
        canvasRect.anchoredPosition = Vector2.zero;
        canvasRect.sizeDelta = canvasSize;
        canvasRect.localScale = Vector3.one * canvasScale;

        // 背景
        GameObject bgGo = new GameObject("Background");
        bgGo.transform.SetParent(canvasGo.transform, false);
        Sprite barSprite = backgroundSprite != null ? backgroundSprite : CreateDefaultSprite();

        Image bgImage = bgGo.AddComponent<Image>();
        bgImage.color = backgroundColor;
        bgImage.sprite = barSprite;

        RectTransform bgRect = bgImage.rectTransform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // 填充
        GameObject fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(bgGo.transform, false);
        fillImage = fillGo.AddComponent<Image>();
        fillImage.color = fillColor;
        fillImage.sprite = barSprite;
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
