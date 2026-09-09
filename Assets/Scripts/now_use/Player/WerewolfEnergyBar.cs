using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 狼人头顶能量条（v1.1.52）：挂在狼人角色上（WerewolfTransformation 创建/销毁）。
/// - 充能：订阅 DamageResolver.OnPlayerDamageDealt，能量 += 伤害 × 0.5（系数 energyPerDamage；
///   满值 100 → 累计造成约 200 伤害充满，前期 4~6 刀、后期 2~3 刀，节奏适中）。
/// - 常态：frame_bar_large 作框 + fill_banner 作填充（Image.Filled 水平，按能量比例显示）。
/// - 满能待变身：框换 frame_bar_medium，banner 保持满条，右端只显示 emblem_head 金色狼头；兽化后才让两颗狼头循环替换，整条脉动
///   （缩放 1±6%、透明度呼吸）形成"能量已满"的动态提示。素材缺失时回退纯色条，不抛错。
/// 世界空间 Canvas 模式同 PlayerWorldStatusBar：挂 WorldUIRoot、LateUpdate 只平移不旋转，
/// 位置 = HealthBarAnchor 上方（血条/护甲/法力条再往上）。
/// </summary>
public class WerewolfEnergyBar : MonoBehaviour
{
    private const string WorldUIRootName = "WorldUIRoot";

    [Header("充能参数")]
    [Tooltip("能量满值")]
    [SerializeField] private float maxEnergy = 100f;
    [Tooltip("每点实际伤害转化的能量（0.5 → 约 200 伤害充满）")]
    [SerializeField] private float energyPerDamage = 0.5f;

    [Header("位置")]
    [Tooltip("相对 HealthBarAnchor 的额外抬升（血条组上方）")]
    [SerializeField] private float heightOffset = 0.46f;
    [Tooltip("兽化完成后，能量 UI 相对玩家根节点的世界高度")]
    [SerializeField] private float beastWorldHeight = 1.91f;
    [Tooltip("退出兽化时 UI 回落到普通高度的平滑时间")]
    [SerializeField] private float heightReturnSmoothTime = 0.18f;

    [Header("Canvas")]
    [SerializeField] private Vector2 canvasSize = new Vector2(150f, 36f);
    [SerializeField] private float canvasScale = 0.01f;
    [SerializeField] private int canvasSortingOrder = 11;

    [Header("满能脉动")]
    [SerializeField] private float pulseSpeed = 5f;
    [SerializeField] private float pulseScale = 0.06f;

    [Header("头颅张合（双素材交替）")]
    [Tooltip("两颗头颅素材的切换间隔（秒），一对开合 = 两个间隔")]
    [SerializeField] private float chompInterval = 0.35f;
    [Tooltip("张合幅度：1 = 不变形，0.65 = 合口时纵向压扁 35%")]
    [SerializeField, Range(0.5f, 1f)] private float chompSquash = 0.65f;

    [Header("兽化能量流失")]
    [Tooltip("兽化期间每秒流失的能量（10 → 满能量支撑 10 秒兽化）")]
    [SerializeField] private float drainPerSecond = 10f;

    private float energy;
    public float Energy
    {
        get => energy;
        private set
        {
            energy = value;
            Refresh();
        }
    }
    public float MaxEnergy => maxEnergy;
    public bool IsFull => Energy >= maxEnergy;

    private Transform anchor;
    private GameObject canvasGo;
    private Transform canvasTransform;
    private WerewolfTransformation wolf;

    private Image fillImage;            // 常态填充（banner_wide_02）
    private Image fullFillImage;        // 满能/兽化填充（同一 banner，兽化时随能量下降）
    private GameObject normalRoot;      // 常态条（bar_large 框 + 填充）
    private GameObject fullRoot;        // 满能条（bar_medium 框 + 狼头徽记）
    private RectTransform fullBarRect;  // 满能条整体（脉动缩放用）
    private Image emblemA, emblemB;     // 双头颅（交替显示 = 一张一合）
    private float chompTimer;
    private bool lastBeastState;
    private float normalWorldHeight;
    private float formVisualProgress;
    private float targetFormVisualProgress;
    private float formProgressVelocity;

    private Sprite frameNormal, fillSprite, frameFull, emblemSprite, emblemSprite2;

    void Awake()
    {
        anchor = transform.Find("HealthBarAnchor");
        if (anchor == null) anchor = transform;
        normalWorldHeight = (anchor == transform ? 0f : anchor.localPosition.y) + heightOffset;
        wolf = GetComponent<WerewolfTransformation>();

        frameNormal = Resources.Load<Sprite>("UI/WerewolfEnergy/frame_bar_large");
        fillSprite  = Resources.Load<Sprite>("UI/WerewolfEnergy/fill_banner");
        frameFull   = Resources.Load<Sprite>("UI/WerewolfEnergy/frame_bar_medium");
        emblemSprite = Resources.Load<Sprite>("UI/WerewolfEnergy/emblem_head");
        emblemSprite2 = Resources.Load<Sprite>("UI/WerewolfEnergy/emblem_wolfhead");
        if (frameNormal == null || fillSprite == null || frameFull == null
            || emblemSprite == null || emblemSprite2 == null)
            Debug.LogWarning("[WerewolfEnergy] 部分素材缺失，能量条回退纯色显示");

        CreateCanvas();
    }

    void OnEnable() => DamageResolver.OnPlayerDamageDealt += OnDamageDealt;
    void OnDisable() => DamageResolver.OnPlayerDamageDealt -= OnDamageDealt;

    void OnDestroy()
    {
        if (canvasGo != null) Destroy(canvasGo);
    }

    private void OnDamageDealt(float damage)
    {
        if (damage <= 0f) return;
        Energy = Mathf.Min(maxEnergy, Energy + damage * energyPerDamage);
        Refresh();
    }

    void LateUpdate()
    {
        if (canvasTransform == null) return;
        formVisualProgress = Mathf.SmoothDamp(
            formVisualProgress, targetFormVisualProgress, ref formProgressVelocity,
            heightReturnSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        float currentHeight = Mathf.Lerp(normalWorldHeight, beastWorldHeight, formVisualProgress);
        canvasTransform.position = transform.position + Vector3.up * currentHeight;
        canvasTransform.rotation = Quaternion.identity;
    }

    public void SetBeastVisualProgress(float progress)
    {
        formVisualProgress = targetFormVisualProgress = Mathf.Clamp01(progress);
        formProgressVelocity = 0f;
    }

    public void SetBeastVisualTarget(bool beast)
    {
        targetFormVisualProgress = beast ? 1f : 0f;
    }

    void Update()
    {
        bool beastNow = wolf != null && wolf.IsBeast;
        if (beastNow != lastBeastState)
        {
            lastBeastState = beastNow;
            chompTimer = 0f;
            SetHeadFrame(true);
            Refresh();
        }

        // 兽化期间能量流失（T 变身生效后开始，退兽化即停）
        if (beastNow && Energy > 0f)
        {
            Energy = Mathf.Max(0f, Energy - drainPerSecond * Time.deltaTime);
            Refresh();

            if (Energy <= 0f)
                wolf.ExitBeastFromEnergyDepleted();
        }

        // 只有进入兽化后才让双头颅循环替换；满能待变身时固定显示金色狼头。
        if (beastNow && fullRoot != null && fullRoot.activeSelf && fullBarRect != null)
        {
            float wave = Mathf.Sin(Time.unscaledTime * pulseSpeed);
            fullBarRect.localScale = Vector3.one * (1f + pulseScale * wave);

            chompTimer += Time.unscaledDeltaTime;
            if (chompTimer >= chompInterval)
            {
                chompTimer -= chompInterval;
                if (emblemA != null && emblemB != null)
                {
                    SetHeadFrame(!emblemA.gameObject.activeSelf);
                }
            }
            // 张合：切换后的前半程"张"（纵向撑满），后半程"合"（压扁），三角波
            float phase = chompTimer / chompInterval;
            float scaleY = Mathf.Lerp(1f, chompSquash, Mathf.Abs(phase * 2f - 1f));
            if (emblemA != null) emblemA.rectTransform.localScale = new Vector3(1f, scaleY, 1f);
            if (emblemB != null) emblemB.rectTransform.localScale = new Vector3(1f, scaleY, 1f);
        }
    }

    private void Refresh()
    {
        bool beast = wolf != null && wolf.IsBeast;
        bool useFullStyle = IsFull || beast;
        if (normalRoot != null && normalRoot.activeSelf != !useFullStyle) normalRoot.SetActive(!useFullStyle);
        if (fullRoot != null && fullRoot.activeSelf != useFullStyle) fullRoot.SetActive(useFullStyle);
        float amount = maxEnergy > 0f ? Energy / maxEnergy : 0f;
        if (fillImage != null) fillImage.fillAmount = amount;
        if (fullFillImage != null) fullFillImage.fillAmount = amount;
        if (!beast) SetHeadFrame(true);
    }

    private void SetHeadFrame(bool showPrimary)
    {
        if (emblemA != null) emblemA.gameObject.SetActive(showPrimary);
        if (emblemB != null) emblemB.gameObject.SetActive(!showPrimary);
    }

    public void ResetEnergy()
    {
        Energy = 0f;
        chompTimer = 0f;
        Refresh();
    }

    // ---------- 构建 ----------

    private void CreateCanvas()
    {
        GameObject root = GameObject.Find(WorldUIRootName);
        if (root == null) root = new GameObject(WorldUIRootName);

        canvasGo = new GameObject($"WerewolfEnergyCanvas_{gameObject.name}");
        canvasGo.transform.SetParent(root.transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = canvasSortingOrder;
        canvasGo.AddComponent<GraphicRaycaster>();

        canvasTransform = canvasGo.GetComponent<RectTransform>();
        RectTransform rect = (RectTransform)canvasTransform;
        rect.localPosition = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = canvasSize;
        rect.localScale = Vector3.one * canvasScale;

        // 常态条：bar_large 框（撑满画布主体）+ banner 填充（框内按素材原比例内缩）
        normalRoot = new GameObject("NormalBar", typeof(RectTransform));
        normalRoot.transform.SetParent(canvasGo.transform, false);
        var nr = (RectTransform)normalRoot.transform;
        nr.anchorMin = nr.anchorMax = nr.pivot = new Vector2(0.5f, 0.5f);
        nr.sizeDelta = canvasSize;

        var frameGo = new GameObject("Frame", typeof(Image));
        frameGo.transform.SetParent(normalRoot.transform, false);
        Image frameImg = frameGo.GetComponent<Image>();
        frameImg.sprite = frameNormal;
        frameImg.preserveAspect = true;
        if (frameImg.sprite == null) frameImg.color = new Color(0.25f, 0.2f, 0.12f, 0.95f);
        Stretch(frameImg.rectTransform);

        var fillGo = new GameObject("Fill", typeof(Image));
        fillGo.transform.SetParent(normalRoot.transform, false);
        fillImage = fillGo.GetComponent<Image>();
        fillImage.sprite = fillSprite;
        fillImage.preserveAspect = false;
        if (fillImage.sprite != null)
        {
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 0f;
        }
        else
        {
            fillImage.color = new Color(0.95f, 0.75f, 0.2f, 0.9f);
        }
        var fr = fillImage.rectTransform;
        // large 框右侧自带银狼头，填充只进入左侧槽体，避免黄色 banner 穿到狼头下方。
        fr.anchorMin = new Vector2(0.025f, 0.29f);
        fr.anchorMax = new Vector2(0.82f, 0.71f);
        fr.offsetMin = fr.offsetMax = Vector2.zero;

        // 满能条：bar_medium 框 + 左端狼头徽记（组合成新的动态能量 UI）
        fullRoot = new GameObject("FullBar", typeof(RectTransform));
        fullRoot.transform.SetParent(canvasGo.transform, false);
        fullBarRect = (RectTransform)fullRoot.transform;
        fullBarRect.anchorMin = fullBarRect.anchorMax = fullBarRect.pivot = new Vector2(0.5f, 0.5f);
        fullBarRect.sizeDelta = canvasSize;

        // 满能态也必须是满条，而不是空框：复用 banner，并放在框体下面。
        var fullFillGo = new GameObject("FillFull", typeof(Image));
        fullFillGo.transform.SetParent(fullRoot.transform, false);
        fullFillImage = fullFillGo.GetComponent<Image>();
        fullFillImage.sprite = fillSprite;
        fullFillImage.preserveAspect = false;
        fullFillImage.type = Image.Type.Filled;
        fullFillImage.fillMethod = Image.FillMethod.Horizontal;
        fullFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        RectTransform fullFillRect = fullFillImage.rectTransform;
        fullFillRect.anchorMin = fullFillRect.anchorMax = fullFillRect.pivot = new Vector2(0.5f, 0.5f);
        fullFillRect.anchoredPosition = new Vector2(-12f, 0f);
        fullFillRect.sizeDelta = new Vector2(118f, 14f);

        var fullFrameGo = new GameObject("FrameFull", typeof(Image));
        fullFrameGo.transform.SetParent(fullRoot.transform, false);
        Image fullFrameImg = fullFrameGo.GetComponent<Image>();
        fullFrameImg.sprite = frameFull;
        fullFrameImg.preserveAspect = true;
        if (fullFrameImg.sprite == null) fullFrameImg.color = new Color(0.35f, 0.25f, 0.1f, 0.95f);
        // medium 原图宽高比 1093:192。以 132×23.2 保持原比例，并给右端头颅预留拼接区。
        RectTransform fullFrameRect = fullFrameImg.rectTransform;
        fullFrameRect.anchorMin = fullFrameRect.anchorMax = fullFrameRect.pivot = new Vector2(0.5f, 0.5f);
        fullFrameRect.anchoredPosition = new Vector2(-9f, 0f);
        fullFrameRect.sizeDelta = new Vector2(132f, 23.2f);

        var emblemGo = new GameObject("Emblem", typeof(Image));
        emblemGo.transform.SetParent(fullRoot.transform, false);
        emblemA = emblemGo.GetComponent<Image>();
        emblemA.sprite = emblemSprite;
        emblemA.preserveAspect = true;
        if (emblemA.sprite == null) emblemA.color = new Color(0.95f, 0.8f, 0.3f, 0.95f);
        var er = emblemA.rectTransform;
        er.anchorMin = er.anchorMax = er.pivot = new Vector2(0.5f, 0.5f);
        er.anchoredPosition = new Vector2(57f, 0f);
        er.sizeDelta = new Vector2(36f, 36f);   // 与 medium 右端重叠 18px，用头颅轮廓遮住接缝

        // 第二颗头颅（狼头）：与徽记同位叠放，满能态两素材交替 = 一张一合
        var emblem2Go = new GameObject("Emblem2", typeof(Image));
        emblem2Go.transform.SetParent(fullRoot.transform, false);
        emblemB = emblem2Go.GetComponent<Image>();
        emblemB.sprite = emblemSprite2;
        emblemB.preserveAspect = true;
        if (emblemB.sprite == null) emblemB.color = new Color(0.95f, 0.85f, 0.4f, 0.95f);
        var er2 = emblemB.rectTransform;
        er2.anchorMin = er2.anchorMax = er2.pivot = new Vector2(0.5f, 0.5f);
        er2.anchoredPosition = er.anchoredPosition;
        er2.sizeDelta = er.sizeDelta;
        emblem2Go.SetActive(false);

        Refresh();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
