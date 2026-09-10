using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// V2 狼人怒痕 HUD。运行时真值来自 WerewolfRage，本组件只负责屏幕左下角表现。
/// 普通/满怒状态使用单狼头；进入兽化后才启用双狼头张合，并用满条递减显示剩余兽化时间。
/// </summary>
[DisallowMultipleComponent]
public sealed class WerewolfEnergyBar : MonoBehaviour
{
    [Header("屏幕 HUD")]
    [SerializeField] private Vector2 hudSize = new Vector2(320f, 64f);
    [SerializeField] private Vector2 screenOffset = new Vector2(24f, 24f);
    [SerializeField] private int canvasSortingOrder = 120;

    [Header("满怒/兽化动态")]
    [SerializeField, Min(0f)] private float pulseSpeed = 5f;
    [SerializeField, Range(0f, 0.2f)] private float pulseScale = 0.045f;
    [SerializeField, Min(0.05f)] private float chompInterval = 0.35f;
    [SerializeField, Range(0.5f, 1f)] private float chompSquash = 0.65f;
    [SerializeField, Min(0.05f)] private float insufficientFlashDuration = 0.22f;

    public float Energy => rage != null ? rage.Current : 0f;
    public float MaxEnergy => rage != null ? rage.Max : 0f;
    public bool IsFull => rage != null && rage.IsFull;

    private WerewolfRage rage;
    private WerewolfTransformation wolf;
    private GameObject canvasGo;
    private RectTransform hudRootRect;
    private GameObject normalRoot;
    private GameObject fullRoot;
    private RectTransform fullBarRect;
    private Image fillImage;
    private Image fullFillImage;
    private Image emblemA;
    private Image emblemB;
    private float chompTimer;
    private float insufficientUntil;
    private bool lastBeastState;

    private Sprite frameNormal;
    private Sprite fillSprite;
    private Sprite frameFull;
    private Sprite emblemSprite;
    private Sprite emblemSprite2;

    private void Awake()
    {
        rage = GetComponent<WerewolfRage>();
        if (rage == null)
            rage = gameObject.AddComponent<WerewolfRage>();
        wolf = GetComponent<WerewolfTransformation>();

        frameNormal = Resources.Load<Sprite>("UI/WerewolfEnergy/frame_bar_large");
        fillSprite = Resources.Load<Sprite>("UI/WerewolfEnergy/fill_banner");
        frameFull = Resources.Load<Sprite>("UI/WerewolfEnergy/frame_bar_medium");
        emblemSprite = Resources.Load<Sprite>("UI/WerewolfEnergy/emblem_head");
        emblemSprite2 = Resources.Load<Sprite>("UI/WerewolfEnergy/emblem_wolfhead");
        if (frameNormal == null || fillSprite == null || frameFull == null || emblemSprite == null || emblemSprite2 == null)
            Debug.LogWarning("[WerewolfRageHUD] 部分素材缺失，缺失节点将回退为纯色显示");

        CreateCanvas();
    }

    private void OnEnable()
    {
        if (rage == null) rage = GetComponent<WerewolfRage>();
        if (rage == null) return;
        rage.OnChanged += OnRageChanged;
        rage.OnBecameFull += OnBecameFull;
        rage.OnInsufficient += OnInsufficient;
    }

    private void Start()
    {
        // 运行时动态创建整棵 UGUI 后，在首个 Start 强制完成一次布局/Graphic 注册。
        // 这同时覆盖 Enter Play Mode 禁用 Domain Reload 时的首帧注册时序。
        Refresh();
        Canvas.ForceUpdateCanvases();
    }

    private void OnDisable()
    {
        if (rage == null) return;
        rage.OnChanged -= OnRageChanged;
        rage.OnBecameFull -= OnBecameFull;
        rage.OnInsufficient -= OnInsufficient;
    }

    private void OnDestroy()
    {
        if (canvasGo != null) Destroy(canvasGo);
    }

    private void Update()
    {
        bool beastNow = wolf != null && wolf.IsBeast;
        bool transformingNow = wolf != null && wolf.IsTransforming;
        if (beastNow != lastBeastState)
        {
            lastBeastState = beastNow;
            chompTimer = 0f;
            SetHeadFrame(true);
        }

        Refresh(beastNow, transformingNow);
        AnimateState(beastNow, transformingNow);
    }

    private void OnRageChanged(float current, float max) => Refresh();

    private void OnBecameFull()
    {
        chompTimer = 0f;
        SetHeadFrame(true);
        Refresh();
    }

    private void OnInsufficient()
    {
        insufficientUntil = Time.unscaledTime + insufficientFlashDuration;
    }

    private void AnimateState(bool beast, bool transforming)
    {
        if (fullBarRect == null || hudRootRect == null) return;

        bool pulse = IsFull || beast || transforming;
        float wave = pulse ? Mathf.Sin(Time.unscaledTime * pulseSpeed) : 0f;
        fullBarRect.localScale = Vector3.one * (1f + pulseScale * wave);

        bool insufficient = Time.unscaledTime < insufficientUntil;
        hudRootRect.localScale = insufficient
            ? Vector3.one * (1f + 0.035f * Mathf.Sin(Time.unscaledTime * 55f))
            : Vector3.one;

        // 满怒待释放只显示一颗狼头；真正进入兽化后才双头交替。
        if (!beast)
        {
            SetHeadFrame(true);
            return;
        }

        chompTimer += Time.unscaledDeltaTime;
        if (chompTimer >= chompInterval)
        {
            chompTimer -= chompInterval;
            SetHeadFrame(emblemA == null || !emblemA.gameObject.activeSelf);
        }

        float phase = chompTimer / chompInterval;
        float scaleY = Mathf.Lerp(1f, chompSquash, Mathf.Abs(phase * 2f - 1f));
        Vector3 headScale = new Vector3(1f, scaleY, 1f);
        if (emblemA != null) emblemA.rectTransform.localScale = headScale;
        if (emblemB != null) emblemB.rectTransform.localScale = headScale;
    }

    private void Refresh()
    {
        Refresh(wolf != null && wolf.IsBeast, wolf != null && wolf.IsTransforming);
    }

    private void Refresh(bool beast, bool transforming)
    {
        bool useFullStyle = IsFull || beast || transforming;
        if (normalRoot != null && normalRoot.activeSelf == useFullStyle) normalRoot.SetActive(!useFullStyle);
        if (fullRoot != null && fullRoot.activeSelf != useFullStyle) fullRoot.SetActive(useFullStyle);

        float rageAmount = rage != null ? rage.Normalized : 0f;
        if (fillImage != null) fillImage.fillAmount = rageAmount;
        if (fullFillImage != null)
        {
            if (transforming) fullFillImage.fillAmount = 1f;
            else if (beast) fullFillImage.fillAmount = wolf.BeastNormalizedRemaining;
            else fullFillImage.fillAmount = rageAmount;
        }
    }

    public void ResetEnergy()
    {
        rage?.ResetRage();
        chompTimer = 0f;
        Refresh();
    }

    // 兼容旧变身演出调用；HUD 已固定屏幕坐标，只刷新表现，不再移动世界高度。
    public void SetBeastVisualProgress(float progress) => Refresh();
    public void SetBeastVisualTarget(bool beast) => Refresh();

    private void SetHeadFrame(bool showPrimary)
    {
        if (emblemA != null) emblemA.gameObject.SetActive(showPrimary);
        if (emblemB != null) emblemB.gameObject.SetActive(!showPrimary);
    }

    private void CreateCanvas()
    {
        canvasGo = new GameObject($"WerewolfRageHUD_{gameObject.name}");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject hudRoot = new GameObject("RageHUD", typeof(RectTransform));
        hudRoot.transform.SetParent(canvasGo.transform, false);
        hudRootRect = (RectTransform)hudRoot.transform;
        hudRootRect.anchorMin = hudRootRect.anchorMax = hudRootRect.pivot = Vector2.zero;
        hudRootRect.anchoredPosition = screenOffset;
        hudRootRect.sizeDelta = hudSize;

        CreateNormalBar();
        CreateFullBar();
        Refresh();
    }

    private void CreateNormalBar()
    {
        normalRoot = new GameObject("NormalBar", typeof(RectTransform));
        normalRoot.transform.SetParent(hudRootRect, false);
        RectTransform root = (RectTransform)normalRoot.transform;
        Stretch(root);

        Image fill = CreateImage("Fill", normalRoot.transform, fillSprite, new Color(0.95f, 0.72f, 0.15f, 0.95f));
        fillImage = fill;
        ConfigureFill(fillImage);
        RectTransform fillRect = fillImage.rectTransform;
        fillRect.anchorMin = new Vector2(0.025f, 0.29f);
        fillRect.anchorMax = new Vector2(0.82f, 0.71f);
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;

        Image frame = CreateImage("Frame", normalRoot.transform, frameNormal, new Color(0.24f, 0.24f, 0.24f, 0.98f));
        frame.preserveAspect = true;
        Stretch(frame.rectTransform);
    }

    private void CreateFullBar()
    {
        fullRoot = new GameObject("FullBar", typeof(RectTransform));
        fullRoot.transform.SetParent(hudRootRect, false);
        fullBarRect = (RectTransform)fullRoot.transform;
        Stretch(fullBarRect);

        fullFillImage = CreateImage("FillFull", fullRoot.transform, fillSprite, new Color(1f, 0.74f, 0.1f, 0.98f));
        ConfigureFill(fullFillImage);
        SetRect(fullFillImage.rectTransform, new Vector2(-24f, 0f), new Vector2(245f, 27f));

        Image frame = CreateImage("FrameFull", fullRoot.transform, frameFull, new Color(0.28f, 0.28f, 0.28f, 0.98f));
        frame.preserveAspect = true;
        SetRect(frame.rectTransform, new Vector2(-16f, 0f), new Vector2(276f, 48.5f));

        emblemA = CreateImage("Emblem", fullRoot.transform, emblemSprite, new Color(1f, 0.78f, 0.16f, 1f));
        emblemA.preserveAspect = true;
        SetRect(emblemA.rectTransform, new Vector2(119f, 0f), new Vector2(64f, 64f));

        emblemB = CreateImage("Emblem2", fullRoot.transform, emblemSprite2, new Color(1f, 0.84f, 0.3f, 1f));
        emblemB.preserveAspect = true;
        SetRect(emblemB.rectTransform, emblemA.rectTransform.anchoredPosition, emblemA.rectTransform.sizeDelta);
        emblemB.gameObject.SetActive(false);
    }

    private static Image CreateImage(string objectName, Transform parent, Sprite sprite, Color fallbackColor)
    {
        GameObject go = new GameObject(objectName, typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = sprite != null ? Color.white : fallbackColor;
        image.raycastTarget = false;
        return image;
    }

    private static void ConfigureFill(Image image)
    {
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.fillAmount = 0f;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
