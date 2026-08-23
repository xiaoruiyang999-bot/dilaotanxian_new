using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 狼人变身技能（v0.6.3，T 键触发）：人形态 ↔ 狼形态切换。
/// 狼形态 = 显示 WerewolfVisual（子物体 SpriteRenderer + FrameAnimator 序列帧），
/// 隐藏玩家本体视觉；变身瞬间播放变身序列（单次），播完进入走路循环，
/// 走路按移动朝向自动切左右两套素材（Walk_L / Walk_R，不做 flipX 防白描边镜像错位）。
/// 当前为纯视觉切换（数值加成预留 TODO：接 M2 的 PlayerStats 乘数体系）。
/// 帧加载用编辑器 AssetDatabase 便利（开发期）；打包前需改为 Resources/Addressables。
/// </summary>
public class WerewolfTransformation : MonoBehaviour
{
    [Header("视觉对象")]
    [Tooltip("狼人视觉子物体（含 SpriteRenderer + FrameAnimator），初始应为隐藏")]
    [SerializeField] private GameObject werewolfVisual;

    [Header("素材目录（编辑期便利，打包前需资源化）")]
    [SerializeField] private string transformFolder = "Assets/Art/Werewolf/Transform_L";
    [SerializeField] private string walkLFolder = "Assets/Art/Werewolf/Walk_L";
    [SerializeField] private string walkRFolder = "Assets/Art/Werewolf/Walk_R";
    [SerializeField] private string idleLFolder = "Assets/Art/Werewolf/Idle_L";
    [SerializeField] private string idleRFolder = "Assets/Art/Werewolf/Idle_R";
    [SerializeField] private string beastIdleLFolder = "Assets/Art/Werewolf/BeastIdle_L";
    [SerializeField] private string beastIdleRFolder = "Assets/Art/Werewolf/BeastIdle_R";
    [SerializeField] private string beastWalkLFolder = "Assets/Art/Werewolf/BeastWalk_L";
    [SerializeField] private string beastWalkRFolder = "Assets/Art/Werewolf/BeastWalk_R";

    [Header("参数")]
    [Tooltip("开局即狼形态（不播变身动画，直接待机/走路渲染），v0.6.6")]
    [SerializeField] private bool startAsWolf = true;
    [Tooltip("变身帧率：4fps = 每帧停留 0.25s")]
    [SerializeField] private float transformFps = 4f;
    [SerializeField] private float walkFps = 10f;
    [Tooltip("待机动画帧率（3 帧循环呼吸感，慢于走路）")]
    [SerializeField] private float idleFps = 5f;
    [Tooltip("狼人视觉缩放：720px 素材默认 7.2 世界单位，0.25 后约 1.8 单位（略高于玩家）")]
    [SerializeField] private float wolfScale = 0.25f;
    [Tooltip("狼形态攻击范围/武器视觉放大倍数：放大 WeaponPivot 缩放，判定、武器视觉、" +
             "预警三者走同源链自动同步（v0.6.7，解决狼人体型与细条判定的错配）")]
    [SerializeField] private float wolfAttackScale = 1.5f;
    [Tooltip("兽化血量放大倍数：上限与当前血等比 ×N，退兽化等比还原（v0.6.9）")]
    [SerializeField] private float beastHealthScale = 1.5f;
    [Tooltip("兽化伤害乘数（v0.7.1：乘数体系，与升级加成叠乘）")]
    [SerializeField] private float beastDamageMult = 1.3f;
    [SerializeField] private float beastAttackSpeedMult = 1.1f;

    public bool IsWolf { get; private set; }
    /// <summary>兽化形态（v0.6.8：T 从"狼↔人"改为"狼↔兽化"，绿球退出 T 循环）。</summary>
    public bool IsBeast { get; private set; }
    private bool pendingBeast;   // 变身动画播完后的去向：进兽化

    // v0.6.3 修复③：动画状态用枚举跟踪。不能用帧数组引用比较——FrameAnimator.Play
    // 每次复制新 List，引用永远不等，曾导致 Update 每帧重播、走路永远卡在第一帧。
    private enum WolfAnim { None, Transforming, WalkL, WalkR, IdleL, IdleR, BeastIdleL, BeastIdleR, BeastWalkL, BeastWalkR }
    private WolfAnim currentAnim = WolfAnim.None;

    private FrameAnimator animator;
    private SpriteRenderer wolfSprite;
    private ParticleSystem beastBurstFx;         // v0.6.9 变身粒子（BeastBurstFX 子物体，Inspector 可调）
    private PlayerController playerController;
    private PlayerInput playerInput;
    private Rigidbody2D rb;
    private Health health;                        // 兽化血量缩放（v0.6.9）
    private SpriteRenderer[] playerVisuals;      // 玩家本体视觉（变身时隐藏；不含狼人子树）
    private Transform weaponPivot;               // 狼形态放大攻击体系用（v0.6.7）
    private Transform healthBarAnchor;           // v0.8 血条锚点随形态高度调整（防与狼人身体重合）
    // v0.8.1 修正：按实际渲染尺寸定锚（720 画布×0.25=1.8 单位，角色占 ~2/3 → 顶 ≈0.86；兽化顶 ≈1.28）
    private const float BarHeightWolf = 1.0f;
    private const float BarHeightBeast = 1.45f;
    private Vector3 weaponPivotBaseScale = Vector3.one;
    private List<Sprite> framesTransform, framesWalkL, framesWalkR, framesIdleL, framesIdleR,
        framesBeastIdleL, framesBeastIdleR, framesBeastWalkL, framesBeastWalkR;
    private bool transforming;

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
        playerInput = GetComponent<PlayerInput>();
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        WeaponController wc = GetComponent<WeaponController>();
        if (wc != null && wc.WeaponPivot != null)
        {
            weaponPivot = wc.WeaponPivot;
            weaponPivotBaseScale = weaponPivot.localScale;
        }
        // 引用自愈：Inspector 未接线时按名字找子物体（prefab 应用/场景迁移时不依赖序列化引用）
        if (werewolfVisual == null)
        {
            Transform child = transform.Find("WerewolfVisual");
            if (child != null) werewolfVisual = child.gameObject;
        }
        if (werewolfVisual != null)
        {
            animator = werewolfVisual.GetComponentInChildren<FrameAnimator>(true);
            wolfSprite = werewolfVisual.GetComponentInChildren<SpriteRenderer>(true);
            // v0.6.9：变身粒子特效（prefab 内 BeastBurstFX 子物体，参数在 Inspector 调）
            Transform fx = werewolfVisual.transform.Find("BeastBurstFX");
            if (fx != null) beastBurstFx = fx.GetComponent<ParticleSystem>();
            // v0.6.3 修复②：720px 素材默认 7.2 世界单位（大半间房），按 wolfScale 缩到玩家体量
            werewolfVisual.transform.localScale = Vector3.one * wolfScale;
            // v0.6.4 修复：排序对齐玩家本体(2)。之前 50 会盖住武器(3)、头顶血条(10)——
            // 变身后"攻击范围和武器全被盖住"。排序 2 后：狼人替换本体层，武器在其上
            // （狼形态持武器）、血条/预警不受影响。
            if (wolfSprite != null) wolfSprite.sortingOrder = 2;
        }

        // v0.6.4：变身只替换本体视觉（绿球 SR）。武器/血条/预警等子物体不隐藏——
        // 狼形态攻击判定本来就在跑，武器与范围预警必须照常显示。
        playerVisuals = new[] { GetComponent<SpriteRenderer>() };

#if UNITY_EDITOR
        framesTransform = EditorLoadSprites(transformFolder);
        framesWalkL = EditorLoadSprites(walkLFolder);
        framesWalkR = EditorLoadSprites(walkRFolder);
        framesIdleL = EditorLoadSprites(idleLFolder);
        framesIdleR = EditorLoadSprites(idleRFolder);
        framesBeastIdleL = EditorLoadSprites(beastIdleLFolder);
        framesBeastIdleR = EditorLoadSprites(beastIdleRFolder);
        framesBeastWalkL = EditorLoadSprites(beastWalkLFolder);
        framesBeastWalkR = EditorLoadSprites(beastWalkRFolder);
        Debug.Log($"[Werewolf] 帧加载v2：变身 {framesTransform?.Count ?? 0} | 走路L {framesWalkL?.Count ?? 0} | 走路R {framesWalkR?.Count ?? 0} | 待机L {framesIdleL?.Count ?? 0} | 待机R {framesIdleR?.Count ?? 0} | 兽化L {framesBeastIdleL?.Count ?? 0} | 兽化R {framesBeastIdleR?.Count ?? 0}");
#endif
        if (werewolfVisual != null) werewolfVisual.SetActive(false);
    }

    void Start()
    {
        // v0.6.6：开局即狼形态（素材已在 Awake 加载完毕；不播变身动画直接进待机/走路）
        Debug.Log($"[Werewolf] Start：startAsWolf={startAsWolf} | 帧表 变身{framesTransform?.Count ?? 0}/走L{framesWalkL?.Count ?? 0}/走R{framesWalkR?.Count ?? 0}/待L{framesIdleL?.Count ?? 0}/待R{framesIdleR?.Count ?? 0} | animator={(animator != null ? "OK" : "null")} | weaponPivot={(weaponPivot != null ? weaponPivot.name : "NULL")} | baseScale={weaponPivotBaseScale}");
        if (startAsWolf) EnterWolf(playTransformAnim: false);
    }

    void OnEnable()
    {
        if (playerInput != null) playerInput.onActionTriggered += OnActionTriggered;
    }

    void OnDisable()
    {
        if (playerInput != null) playerInput.onActionTriggered -= OnActionTriggered;
        if (animator != null) animator.OnFinished -= OnTransformFinished;
    }

    private void OnActionTriggered(InputAction.CallbackContext context)
    {
        if (context.action?.name == "Transform" && context.performed)
            Toggle();
    }

    /// <summary>
    /// 切换形态（v0.6.8：T = 狼 ↔ 兽化）。普通狼按 T 播变身动画后进兽化（BeastIdle 循环）；
    /// 兽化按 T 直接回普通狼。绿球（人形态）不再由 T 触达——开局即狼（startAsWolf）。
    /// </summary>
    public void Toggle()
    {
        if (transforming) return;
        if (IsBeast)
        {
            IsBeast = false;
            // v0.6.9：退兽化血量等比还原（7.5 上限的 6 血 → 5 上限的 4 血）
            if (health != null) health.ScaleMaxHealth(1f / beastHealthScale);
            ApplyBeastStats(false);   // v0.7.1 数值乘数还原
            currentAnim = WolfAnim.None;   // Update 接管回普通狼 idle/walk
            return;
        }
        if (!IsWolf)
        {
            EnterWolf(playTransformAnim: true);   // 保底：未开局狼形态时先进狼
            return;
        }
        StartBeastTransform();
    }

    /// <summary>兽化变身：播变身动画，播完由 OnTransformFinished 进兽化待机。</summary>
    private void StartBeastTransform()
    {
        // v0.6.9：变身开始触发粒子（参数全在 BeastBurstFX 的 Inspector，不依赖代码绘制）
        if (beastBurstFx != null)
        {
            beastBurstFx.Clear();
            beastBurstFx.Play();
        }

        if (animator == null || framesTransform == null || framesTransform.Count == 0)
        {
            // 无变身素材：直接进兽化（血量照常放大）
            IsBeast = true;
            if (health != null) health.ScaleMaxHealth(beastHealthScale);
            ApplyBeastStats(true);
            currentAnim = WolfAnim.None;
            return;
        }
        transforming = true;
        pendingBeast = true;
        currentAnim = WolfAnim.Transforming;
        animator.OnFinished += OnTransformFinished;
        animator.Fps = transformFps;
        animator.Play(framesTransform, false);
    }

    /// <summary>进入狼形态。playTransformAnim=false 时跳过变身动画直接进待机/走路。</summary>
    private void EnterWolf(bool playTransformAnim)
    {
        IsWolf = true;
        SetHealthBarHeight(BarHeightWolf);   // v0.8.1 修复：开局普通狼也要抬血条（此前只在兽化切换时调）
        foreach (var sr in playerVisuals)
            if (sr != null) sr.enabled = false;

        // v0.6.7：狼形态放大攻击体系。判定长度/宽度、武器视觉、预警三者都派生自
        // weaponPivot.lossyScale（同源链），放大 Pivot 一处 = 三处同步，不破坏 R7。
        if (weaponPivot != null)
            weaponPivot.localScale = weaponPivotBaseScale * wolfAttackScale;

        if (werewolfVisual != null) werewolfVisual.SetActive(true);
        if (playTransformAnim && animator != null && framesTransform != null && framesTransform.Count > 0)
        {
            transforming = true;
            currentAnim = WolfAnim.Transforming;
            animator.OnFinished += OnTransformFinished;
            animator.Fps = transformFps;
            animator.Play(framesTransform, false);
        }
        else
            currentAnim = WolfAnim.None;   // 无变身素材或跳过：由 Update 按移动状态直接接管
    }

    /// <summary>血条锚点高度（v0.8.1：修开局不生效——EnterWolf 时也设普通高度）。</summary>
    private void SetHealthBarHeight(float y)
    {
        if (healthBarAnchor == null)
            healthBarAnchor = transform.Find("HealthBarAnchor");
        if (healthBarAnchor != null)
        {
            healthBarAnchor.localPosition = new Vector3(0f, y, 0f);
            Debug.Log($"[Werewolf] 血条锚点v2 -> y={y}（anchor={healthBarAnchor.name}）");
        }
        else
            Debug.LogWarning("[Werewolf] 血条锚点未找到！");
    }

    /// <summary>兽化数值乘数开关（v0.7.1：走 PlayerStats 乘数体系，与三选一升级叠乘）。</summary>
    private void ApplyBeastStats(bool on)
    {
        PlayerStats stats = GetComponent<PlayerStats>();
        if (stats == null) return;
        stats.BeastDamageMult = on ? beastDamageMult : 1f;
        stats.BeastAttackSpeedMult = on ? beastAttackSpeedMult : 1f;
        stats.BeastMoveSpeedMult = on ? 1.1f : 1f;   // v0.8 §12.2 兽化移速 +10%

        SetHealthBarHeight(on ? BarHeightBeast : BarHeightWolf);
    }

    private void ExitWolf()
    {
        IsWolf = false;
        foreach (var sr in playerVisuals)
            if (sr != null) sr.enabled = true;
        if (weaponPivot != null)
            weaponPivot.localScale = weaponPivotBaseScale;
        if (werewolfVisual != null) werewolfVisual.SetActive(false);
        currentAnim = WolfAnim.None;
    }

    private void OnTransformFinished()
    {
        if (animator != null) animator.OnFinished -= OnTransformFinished;
        transforming = false;
        if (pendingBeast)
        {
            // v0.6.8：进兽化。兽化帧画布 1080 自带大体型（普通狼 720），
            // 变身末帧的膨胀补偿（×1.5）在此重置回基准，衔接无缝。
            pendingBeast = false;
            IsBeast = true;
            if (health != null) health.ScaleMaxHealth(beastHealthScale);   // v0.6.9 血量等比放大
            ApplyBeastStats(true);                                          // v0.7.1 伤害/攻速乘数
            if (werewolfVisual != null)
                werewolfVisual.transform.localScale = Vector3.one * wolfScale;
        }
        currentAnim = WolfAnim.None;   // 交给 Update 按形态接管
    }

    void Update()
    {
        if (!IsWolf || transforming || animator == null) return;

        bool right = playerController != null && playerController.FacingDirection.x >= 0f;

        // v0.6.10 兽化形态：移动→兽化走路循环；静止→兽化待机循环（BeastWalk 素材到位后拆分）
        if (IsBeast)
        {
            bool beastMoving = rb != null && rb.linearVelocity.sqrMagnitude > 0.01f;
            WolfAnim beastWant = beastMoving
                ? (right ? WolfAnim.BeastWalkR : WolfAnim.BeastWalkL)
                : (right ? WolfAnim.BeastIdleR : WolfAnim.BeastIdleL);
            if (beastWant != currentAnim)
            {
                List<Sprite> beastFrames = null;
                float fps = idleFps;
                switch (beastWant)
                {
                    case WolfAnim.BeastWalkL:
                        beastFrames = framesBeastWalkL; fps = walkFps; break;
                    case WolfAnim.BeastWalkR:
                        beastFrames = framesBeastWalkR; fps = walkFps; break;
                    case WolfAnim.BeastIdleL:
                        beastFrames = framesBeastIdleL; break;
                    case WolfAnim.BeastIdleR:
                        beastFrames = framesBeastIdleR; break;
                }
                currentAnim = beastWant;
                if (beastFrames != null && beastFrames.Count > 0)
                {
                    animator.Resume();
                    animator.Fps = fps;
                    animator.Play(beastFrames, true);
                }
            }
            return;
        }

        // 普通狼：移动→走路循环；停止→待机循环
        bool moving = rb != null && rb.linearVelocity.sqrMagnitude > 0.01f;
        WolfAnim want = moving
            ? (right ? WolfAnim.WalkR : WolfAnim.WalkL)
            : (right ? WolfAnim.IdleR : WolfAnim.IdleL);
        if (want == currentAnim) return;

        currentAnim = want;
        switch (want)
        {
            case WolfAnim.WalkL:
                animator.Resume();
                animator.Fps = walkFps;
                if (framesWalkL != null && framesWalkL.Count > 0) animator.Play(framesWalkL, true);
                break;
            case WolfAnim.WalkR:
                animator.Resume();
                animator.Fps = walkFps;
                if (framesWalkR != null && framesWalkR.Count > 0) animator.Play(framesWalkR, true);
                break;
            case WolfAnim.IdleL:
            case WolfAnim.IdleR:
                // 停止移动：待机动画循环（v0.6.4 从单张模板图升级为帧循环，保持最后朝向）
                var idle = want == WolfAnim.IdleR ? framesIdleR : framesIdleL;
                if (idle != null && idle.Count > 0)
                {
                    animator.Resume();
                    animator.Fps = idleFps;
                    animator.Play(idle, true);
                }
                break;
        }
    }

    /// <summary>
    /// v0.6.5：变身动画逐帧体型放大。首帧 = 标准体型（wolfScale 基准，与平时一致），
    /// 后续帧按画布宽度相对首帧的比例逐帧变大（720→1080 素材 → 末帧约 1.5 倍体型）。
    /// 变身演出结束后由 Awake 设置的标准 wolfScale 接管走路/待机（回归常态体型）。
    /// </summary>
    void LateUpdate()
    {
        if (!transforming || animator == null || framesTransform == null || framesTransform.Count == 0) return;

        Sprite cur = animator.CurrentSprite;
        Sprite first = framesTransform[0];
        if (cur == null || first == null) return;

        float ratio = first.bounds.size.x > 0.01f ? cur.bounds.size.x / first.bounds.size.x : 1f;
        if (werewolfVisual != null)
            werewolfVisual.transform.localScale = Vector3.one * (wolfScale * ratio);
    }

#if UNITY_EDITOR
    /// <summary>编辑器便利：按目录加载全部 Sprite（按文件名排序）。打包前移除并资源化。</summary>
    private static List<Sprite> EditorLoadSprites(string folder)
    {
        if (string.IsNullOrEmpty(folder)) return null;
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { folder });
        var list = new List<Sprite>();
        foreach (string g in guids)
        {
            string p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
            if (!p.EndsWith(".png")) continue;
            Sprite s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (s != null) list.Add(s);
        }
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return list;
    }
#endif
}
