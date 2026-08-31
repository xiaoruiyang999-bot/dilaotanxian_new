using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 准备房间展台类型。
/// ClassSelector：职业选择台（三层基座 + 顶部悬浮三色棱晶）；
/// WeaponDisplay：武器展示台（矮基座 + 金色托盘 + 悬浮展示位）。
/// </summary>
public enum PrepPedestalType
{
    ClassSelector = 0,
    WeaponDisplay = 1
}

/// <summary>
/// 准备房间展台（v0.6.2 阶段 B，计划书 4.5 / 美术清单第六节）。
/// 视觉全部运行时代码构建（程序员美术多色块，白图 SpriteRenderer 染色），不写 prefab YAML。
/// 继承 Interactable 接入 v0.6.1 E 键候选体系：
/// - 职业选择台：Interact 打开 ClassSelectUI（可重复打开，不消耗，覆盖 Interact 跳过一次性消耗）；
///   走近时头顶显示名称"职业选择台"（TMP 世界空间标签）。
/// - 武器展示台：碰撞体禁用（E 由展示位上的 WeaponPickup 承担），
///   ShowWeapon(WeaponData) 在托盘上方生成展示武器，被拾走后托盘压暗空置。
/// </summary>
public class PrepPedestal : Interactable
{
    private static readonly Color stoneGray = new Color(0.5f, 0.55f, 0.55f);
    private static readonly Color stoneLight = new Color(0.62f, 0.66f, 0.66f);
    private static readonly Color trayGold = new Color(0.9451f, 0.7686f, 0.0588f);   // #F1C40F
    private static readonly Color trayEmpty = new Color(0.9451f * 0.35f, 0.7686f * 0.35f, 0.0588f * 0.35f);
    private static readonly Color[] classColors =
    {
        new Color(0.75294f, 0.23137f, 0.16863f),   // 战士 #C0392B
        new Color(0.15294f, 0.68235f, 0.37647f),   // 射手 #27AE60
        new Color(0.55686f, 0.26667f, 0.67843f)    // 法师 #8E44AD
    };

    private static Sprite whiteSprite;

    [SerializeField] private PrepPedestalType type = PrepPedestalType.ClassSelector;
    [SerializeField] private float nameLabelDistance = 3f;   // 走近该距离显示展台名

    [Header("展台名标签（职业选择台，Inspector 调整即时生效）")]
    [SerializeField] private int nameLabelFontSize = 16;
    [SerializeField] private Vector2 nameLabelPanelSize = new Vector2(120f, 22f);
    [SerializeField] private float nameLabelWorldScale = 0.03f;   // 用户反馈 0.01 太小，默认调大

    private Transform displaySlot;
    private SpriteRenderer trayRenderer;
    private WeaponPickup displayed;
    private bool trayFilled;

    private GameObject nameLabelGo;
    private Transform nameLabelTransform;
    private TMP_Text nameLabelText;
    private Transform player;

    /// <summary>运行时构建一个展台（PrepRoomPlacer 调用）。</summary>
    public static PrepPedestal Create(PrepPedestalType type, Vector3 position, Transform parent)
    {
        GameObject go = new GameObject($"PrepPedestal_{type}");
        go.transform.position = position;
        go.transform.SetParent(parent, true);

        // 先显式加 CircleCollider2D：基类 RequireComponent(typeof(Collider2D)) 是抽象类型，
        // 若不先加，AddComponent 时 Unity 尝试自动补抽象组件会抛 NullReferenceException。
        CircleCollider2D col = go.AddComponent<CircleCollider2D>();

        PrepPedestal p = go.AddComponent<PrepPedestal>();
        p.type = type;

        // 职业台：启用，供 E 交互；武器台：禁用（E 由展示武器的 WeaponPickup 承担，避免抢候选）
        col.isTrigger = true;
        col.radius = 0.6f;
        col.enabled = type == PrepPedestalType.ClassSelector;

        p.BuildVisual();
        return p;
    }

    /// <summary>覆盖基类：职业选择台可重复交互（打开选择 UI），不走一次性消耗。</summary>
    public override void Interact(Collider2D playerCollider)
    {
        if (type != PrepPedestalType.ClassSelector) return;
        ClassSelectUI.Open();
    }

    /// <summary>一次性效果：展台不使用基类消耗流程（本类覆盖 Interact 后不会走到）。</summary>
    protected override void ApplyEffect(Collider2D playerCollider) { }

    // ========== 武器展示台 ==========

    /// <summary>在展示位呈现一把武器（展台刷新/换职业时调用；null = 清空）。</summary>
    public void ShowWeapon(WeaponData data)
    {
        ClearDisplayed();
        if (data == null || displaySlot == null)
        {
            SetTrayEmpty();
            return;
        }

        displayed = WeaponPickup.Drop(data, displaySlot.position);
        if (displayed != null)
        {
            displayed.transform.SetParent(transform, true);
            // 悬浮展示：图标缓浮动（SetLink 规范）
            displayed.transform
                .DOLocalMoveY(displayed.transform.localPosition.y + 0.15f, 1f)
                .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine)
                .SetLink(displayed.gameObject);
        }
        SetTrayFilled();
    }

    private void ClearDisplayed()
    {
        if (displayed != null)
        {
            Destroy(displayed.gameObject);
            displayed = null;
        }
    }

    private void SetTrayFilled()
    {
        trayFilled = true;
        if (trayRenderer != null) trayRenderer.color = trayGold;
    }

    private void SetTrayEmpty()
    {
        trayFilled = false;
        if (trayRenderer != null) trayRenderer.color = trayEmpty;
    }

    void Update()
    {
        // 展示物被拾走 → 托盘压暗空置
        if (type == PrepPedestalType.WeaponDisplay && trayFilled && displayed == null)
            SetTrayEmpty();

        // 职业选择台：走近显示名称标签（可见时每帧应用序列化参数，Inspector 调整即时生效）
        if (type == PrepPedestalType.ClassSelector && nameLabelGo != null)
        {
            if (player == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) player = p.transform;
            }
            bool near = player != null
                && Vector2.Distance(player.position, transform.position) <= nameLabelDistance;
            if (nameLabelGo.activeSelf != near)
                nameLabelGo.SetActive(near);
            if (near)
            {
                nameLabelTransform.position = transform.position + Vector3.up * 1.9f;
                if (nameLabelText != null)
                {
                    nameLabelText.fontSize = nameLabelFontSize;
                    ((RectTransform)nameLabelGo.transform).sizeDelta = nameLabelPanelSize;
                    nameLabelGo.transform.localScale = Vector3.one * nameLabelWorldScale;
                }
            }
        }
    }

    // ========== 视觉构建（程序员美术多色块） ==========

    private void BuildVisual()
    {
        if (type == PrepPedestalType.ClassSelector)
            BuildClassSelectorVisual();
        else
            BuildWeaponDisplayVisual();
    }

    private void BuildClassSelectorVisual()
    {
        // 三层基座（石灰）
        CreateBlock("Base1", new Vector2(1.2f, 0.25f), new Vector3(0f, 0.125f), stoneGray);
        CreateBlock("Pillar", new Vector2(0.4f, 0.5f), new Vector3(0f, 0.5f), stoneLight);
        CreateBlock("Top", new Vector2(0.7f, 0.18f), new Vector3(0f, 0.84f), stoneGray);

        // 顶部悬浮三面棱晶（三职业色，缓转 + 上下浮动）
        GameObject prismRoot = new GameObject("Prism");
        prismRoot.transform.SetParent(transform, false);
        prismRoot.transform.localPosition = new Vector3(0f, 1.45f, 0f);

        Vector3[] offsets =
        {
            new Vector3(-0.22f, -0.1f), new Vector3(0.22f, -0.1f), new Vector3(0f, 0.25f)
        };
        for (int i = 0; i < 3; i++)
        {
            SpriteRenderer facet = CreateBlock($"Facet{i}", new Vector2(0.24f, 0.24f),
                Vector3.zero, classColors[i], prismRoot.transform);
            facet.transform.localPosition = offsets[i];
            facet.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);   // 菱形
        }

        prismRoot.transform
            .DORotate(new Vector3(0f, 0f, 360f), 8f, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart)
            .SetLink(prismRoot);
        prismRoot.transform
            .DOLocalMoveY(prismRoot.transform.localPosition.y + 0.15f, 1.6f)
            .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine)
            .SetLink(prismRoot);

        BuildNameLabel();
    }

    private void BuildWeaponDisplayVisual()
    {
        // 矮基座 + 金色托盘 + 悬浮展示位（与职业台一眼区分：矮、平顶、有托盘）
        CreateBlock("Base", new Vector2(0.9f, 0.22f), new Vector3(0f, 0.11f), stoneGray);
        trayRenderer = CreateBlock("Tray", new Vector2(0.8f, 0.12f), new Vector3(0f, 0.28f), trayEmpty);

        GameObject slot = new GameObject("DisplaySlot");
        slot.transform.SetParent(transform, false);
        slot.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        displaySlot = slot.transform;
    }

    /// <summary>创建一个染色方块（Root + 部件层级，独立 SpriteRenderer 便于染色/闪烁）。</summary>
    private SpriteRenderer CreateBlock(string name, Vector2 size, Vector3 localPos, Color color, Transform parentOverride = null)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parentOverride != null ? parentOverride : transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetWhiteSprite();
        sr.color = color;
        sr.sortingOrder = 0;
        return sr;
    }

    /// <summary>展台名标签（TMP 世界空间，走近显示，与"按 E"标签同模式）。</summary>
    private void BuildNameLabel()
    {
        nameLabelGo = new GameObject("NameLabel");
        nameLabelGo.transform.SetParent(null, true);

        Canvas canvas = nameLabelGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 11;

        RectTransform rect = (RectTransform)nameLabelGo.transform;
        rect.sizeDelta = nameLabelPanelSize;
        rect.localScale = Vector3.one * nameLabelWorldScale;

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(nameLabelGo.transform, false);
        TMP_Text text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = "职业选择台";
        text.font = TMPFontProvider.Font;
        text.fontSize = nameLabelFontSize;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        nameLabelText = text;
        RectTransform textRect = (RectTransform)textGo.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        nameLabelTransform = nameLabelGo.transform;
        nameLabelTransform.position = transform.position + Vector3.up * 1.9f;
        nameLabelGo.SetActive(false);
    }

    void OnDestroy()
    {
        if (nameLabelGo != null) Destroy(nameLabelGo);
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), tex.width);   // 1×1 单位方块
        }
        return whiteSprite;
    }
}
