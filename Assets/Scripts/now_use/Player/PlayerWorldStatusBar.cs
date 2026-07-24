using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家世界空间状态条（头顶：红 HP 条 + 蓝护甲条）。
/// 实现模式与 WorldSpaceHealthBar 相同：运行时自建 World Space Canvas 挂到全局 WorldUIRoot 下，
/// LateUpdate 跟随锚点、只平移不旋转。
/// 数据源：Health.OnHealthChanged / PlayerStats.OnStatsChanged，订阅后立即刷新一次，避免时序问题。
/// 屏幕左下角的 PlayerUI 固定面板不受影响，两者并存。
/// </summary>
public class PlayerWorldStatusBar : MonoBehaviour
{
    private const string WorldUIRootName = "WorldUIRoot";

    [Header("锚点")]
    [Tooltip("状态条将跟随此锚点的世界位置。未指定时自动查找名为 'HealthBarAnchor' 的子物体；仍找不到则使用当前 Transform。")]
    [SerializeField] private Transform anchor;

    [Header("数据源（缺省自动获取）")]
    [SerializeField] private Health health;
    [SerializeField] private PlayerStats stats;

    [Header("外观")]
    [SerializeField] private Sprite barSprite;
    [SerializeField] private Color backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color hpColor = new Color(0.9f, 0.1f, 0.1f, 1f);
    [SerializeField] private Color armorColor = new Color(0.2f, 0.5f, 0.95f, 1f);

    [Header("Canvas 设置（UI 像素）")]
    [Tooltip("Canvas 在 UI 像素空间下的宽高")]
    [SerializeField] private Vector2 canvasSize = new Vector2(84f, 20f);
    [Tooltip("Canvas 在世界空间中的缩放，用于控制状态条实际大小")]
    [SerializeField] private float canvasScale = 0.01f;
    [SerializeField] private int canvasSortingOrder = 10;

    [Header("条形尺寸（UI 像素）")]
    [SerializeField] private Vector2 hpBarSize = new Vector2(80f, 8f);
    [SerializeField] private Vector2 armorBarSize = new Vector2(80f, 5f);
    [SerializeField] private float barSpacing = 3f;

    private GameObject canvasGo;
    private Transform canvasTransform;
    private Image hpFill;
    private Image armorFill;
    private Vector3 anchorBaseOffset; // 锚点相对玩家中心的世界偏移（已去除初始旋转影响），不随旋转变化

    void Awake()
    {
        // 1. 确定锚点，并记录它相对玩家中心的世界偏移（与 WorldSpaceHealthBar 同一套去旋转逻辑）
        if (anchor == null)
            anchor = transform.Find("HealthBarAnchor");
        if (anchor == null)
            anchor = transform;

        Vector3 rawWorldOffset = anchor.position - transform.position;
        anchorBaseOffset = Quaternion.Inverse(transform.rotation) * rawWorldOffset;

        // 2. 自动获取数据源
        if (health == null)
            health = GetComponent<Health>();
        if (stats == null)
            stats = GetComponent<PlayerStats>();

        if (health == null || stats == null)
        {
            Debug.LogWarning($"[PlayerWorldStatusBar] {gameObject.name} 需要 Health 与 PlayerStats 组件！");
            enabled = false;
            return;
        }

        CreateCanvas();
    }

    void OnEnable()
    {
        // 只订阅，不做初始刷新：此时 PlayerStats.Awake 可能尚未执行（CurrentArmor 仍为 0），
        // 初始刷新统一放在 Start()，保证所有组件 Awake 完成后读到正确初值。
        if (health != null)
            health.OnHealthChanged += OnHealthChanged;

        if (stats != null)
            stats.OnStatsChanged += OnStatsChanged;
    }

    void Start()
    {
        // 初始刷新一次，避免脚本执行顺序导致初始显示不正确
        if (health != null)
            OnHealthChanged(health.CurrentHealth, health.MaxHealth);
        if (stats != null)
            OnStatsChanged();
    }

    void OnDisable()
    {
        if (health != null)
            health.OnHealthChanged -= OnHealthChanged;
        if (stats != null)
            stats.OnStatsChanged -= OnStatsChanged;
    }

    void OnDestroy()
    {
        if (canvasGo != null)
            Destroy(canvasGo);
    }

    void LateUpdate()
    {
        if (canvasTransform == null || anchor == null) return;

        // 状态条相对玩家中心保持静止：只平移不旋转
        canvasTransform.position = transform.position + anchorBaseOffset;
        canvasTransform.rotation = Quaternion.identity;
    }

    private void OnHealthChanged(float current, float max)
    {
        if (hpFill != null)
            hpFill.fillAmount = max > 0 ? current / max : 0f;
    }

    private void OnStatsChanged()
    {
        if (armorFill != null && stats != null)
            armorFill.fillAmount = stats.MaxArmor > 0 ? stats.CurrentArmor / stats.MaxArmor : 0f;
    }

    /// <summary>
    /// 获取或创建全局 WorldUIRoot，所有世界空间 UI Canvas 都挂载在它下面。
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

    private void CreateCanvas()
    {
        canvasGo = new GameObject($"PlayerStatusBarCanvas_{gameObject.name}");
        canvasGo.transform.SetParent(EnsureWorldUIRoot(), false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = canvasSortingOrder;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 100f;

        // 必须在添加 Canvas 后再取 Transform，因为 Canvas 会把 Transform 替换为 RectTransform
        canvasTransform = canvasGo.GetComponent<RectTransform>();

        RectTransform canvasRect = (RectTransform)canvasTransform;
        canvasRect.localPosition = Vector3.zero;
        canvasRect.localRotation = Quaternion.identity;
        // Pivot 设为底部中心，使状态条整体位于锚点正上方
        canvasRect.anchorMin = new Vector2(0.5f, 0f);
        canvasRect.anchorMax = new Vector2(0.5f, 0f);
        canvasRect.pivot = new Vector2(0.5f, 0f);
        canvasRect.anchoredPosition = Vector2.zero;
        canvasRect.sizeDelta = canvasSize;
        canvasRect.localScale = Vector3.one * canvasScale;

        Sprite sprite = barSprite != null ? barSprite : CreateDefaultSprite();

        // 护甲条（上）与 HP 条（下），均以 Canvas 顶部为基准向下排布
        armorFill = CreateBar("ArmorBar", Vector2.zero, armorBarSize, armorColor, sprite);
        hpFill = CreateBar("HPBar", new Vector2(0f, -(armorBarSize.y + barSpacing)), hpBarSize, hpColor, sprite);
    }

    /// <summary>
    /// 创建一条 背景+填充 的状态条，返回填充 Image。anchoredPos 相对 Canvas 顶部中心。
    /// </summary>
    private Image CreateBar(string name, Vector2 anchoredPos, Vector2 size, Color fillColor, Sprite sprite)
    {
        // 背景
        GameObject bgGo = new GameObject(name);
        bgGo.transform.SetParent(canvasGo.transform, false);

        RectTransform bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.5f, 1f);
        bgRect.anchorMax = new Vector2(0.5f, 1f);
        bgRect.pivot = new Vector2(0.5f, 1f);
        bgRect.anchoredPosition = anchoredPos;
        bgRect.sizeDelta = size;

        Image bgImage = bgGo.AddComponent<Image>();
        bgImage.color = backgroundColor;
        bgImage.sprite = sprite;

        // 填充
        GameObject fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(bgGo.transform, false);

        Image fillImage = fillGo.AddComponent<Image>();
        fillImage.color = fillColor;
        fillImage.sprite = sprite;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0;
        fillImage.fillAmount = 1f;

        RectTransform fillRect = fillImage.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        return fillImage;
    }
}
