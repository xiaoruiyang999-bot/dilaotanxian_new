using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用序列帧动画切换器（v0.7.5 美术线 Track A，纯表现层，不碰逻辑）：
/// 序列化命名动画组（名字 + Sprite[] + fps + loop + pingPong），API：Play(name) / Stop() / SetFlipX。
/// SpriteRenderer 由组件自身 GetComponent 获取；不使用 Animator / 第三方插件。
///
/// 玩家驱动（内置，Awake 时检测到 Rigidbody2D 才启用）：
/// 每帧读 Rigidbody2D.linearVelocity，水平速度绝对值 > 0.1 播放 "walk" 组，
/// 按速度 x 符号设 flipX（素材实为右向——v0.7.5 验收实测修正，向左走 flipX=true）；
/// 速度 ≤ 0.1 播放 "idle" 组（正面呼吸帧，pingPong 往返，不重置 flipX，保持停步前朝向）；
/// idle 帧缺失时回退现行为：停在 walk 第 1 帧当 idle。
///
/// 四方向行走（v0.7.6，素材骨架先行）：WalkFront/WalkBack 任一目录有帧才启用；
/// 按速度主轴分方向——|vy|≥|vx| 且 vy<0 → "walk_front"（正面朝下）、vy>0 → "walk_back"（背面朝上），
/// 否则侧面 "walk" + flipX（同上）。方向切换带 0.1s 滞回，避免斜向移动时主轴判定抖动。
/// 回退：walk_front/walk_back 组缺失 → 该方向回退侧面 walk（vy 主导时 flipX 保持不动）；
/// 两目录全缺 → 完全走 v0.7.5 水平驱动（零视觉回归，纯上下移动仍播 idle，与现状逐帧一致）。
///
/// 攻击/技能覆盖播放（v0.7.6）：
/// PlayAttack(isSpear, duration)：播 "attack_sword"/"attack_spear" 组（AttackSword/AttackSpear 目录），
/// fps 自动对齐（帧数 ÷ 三阶段合计时长），非循环，播完自动回 walk/idle 驱动；
/// 播放期间隐藏 WeaponPivot 下全部 SpriteRenderer（武器已烘进帧，避免双重武器），结束按原 enabled 恢复；
/// 组缺失/时长非法 → 返回 false 完全不干预（DOTween WeaponAnimator 挥砍照旧）。
/// PlaySkill(groupName, duration)：同机制播命名技能组（Skill 目录加载为 "skill" 组，同样隐藏武器视觉）；
/// SkillExecutor 暂未接入，接法见 now_use/README.md。
///
/// 动画组装配：序列化 groups 留空时按硬编码路径兜底加载战士行走 6 帧 + 待机呼吸 5 帧
/// （编辑器 AssetDatabase，仿 SkillCatalog 模式；打包构建需复制到 Resources/Art/Characters/Warrior/ 下对应目录）。
/// walk/idle 帧率由序列化字段 walkFps/idleFps 决定：兜底建组时读取，
/// Play 模式下选中 Player 的 FrameAnimator 组件改字段值即可实时调速（仅兜底组生效）。
/// 帧组有效 → SpriteRenderer.color 置白（关掉旧绿球/灰底染色）；帧组缺失/为空 → 不动 sprite/color，保留既有视觉兜底，不报错。
/// 颜色协调：死亡灰（PlayerController.OnPlayerDeath 置 0.3 灰）期间不强制置白，
/// Respawn 恢复绿后下一帧自动重新置白。
///
/// 工程铁律：本组件无任何 RequireComponent（运行时 AddComponent 安全）；
/// sortingOrder 不碰，沿用玩家身体 SpriteRenderer 的 10。
/// </summary>
public class FrameAnimator : MonoBehaviour
{
    [System.Serializable]
    public class AnimGroup
    {
        public string name;
        public Sprite[] frames;
        public float fps = 10f;
        public bool loop = true;
        [Tooltip("往返播放（0→N→0），适合呼吸等首尾不接的动画；开启时忽略 loop。")]
        public bool pingPong = false;
    }

    [Header("动画组（留空则按硬编码路径加载战士行走/待机帧）")]
    [SerializeField] private List<AnimGroup> groups = new List<AnimGroup>();

    [Header("帧率（仅兜底加载的 walk/idle 组生效，Play 模式改值实时生效）")]
    [Tooltip("行走动画帧率（帧/秒）。Play 模式下选中本组件改值可实时调速；满意后记下数值退出，回填脚本默认值。")]
    [SerializeField] private float walkFps = 12f;   // 用户调定 2026-08-06（Play 实测，原 10 偏慢）
    [Tooltip("待机呼吸动画帧率（帧/秒）。Play 模式下选中本组件改值可实时调速；满意后记下数值退出，回填脚本默认值。")]
    [SerializeField] private float idleFps = 3f;    // 用户调定 2026-08-06（Play 实测，原 5 偏快）

    // 玩家驱动参数（任务书 v0.7.5 Track A 定值）
    private const string WalkGroupName = "walk";
    private const string IdleGroupName = "idle";
    private const float MoveSpeedThreshold = 0.1f;

    // v0.7.6：四方向行走与攻击/技能组名（目录缺失时对应组不建，驱动自动回退）
    private const string WalkFrontGroupName = "walk_front";
    private const string WalkBackGroupName = "walk_back";
    private const string AttackSwordGroupName = "attack_sword";
    private const string AttackSpearGroupName = "attack_spear";
    private const string SkillGroupName = "skill";

    // 战士行走帧硬编码路径（编辑器 AssetDatabase / 构建 Resources，SkillCatalog 同模式；帧数可变 1~8 扫描）
    private const string EditorWalkDir = "Assets/Art/Characters/Warrior/Walk";
    private const string ResourcesWalkDir = "Art/Characters/Warrior/Walk";

    // 战士待机呼吸帧硬编码路径（同模式；5 帧，正面朝向，不做 flipX）
    private const string EditorIdleDir = "Assets/Art/Characters/Warrior/idle";
    private const string ResourcesIdleDir = "Art/Characters/Warrior/idle";
    private const int IdleFrameCount = 5;

    // v0.7.6：可选帧组目录（帧数不定，按命名前缀连续扫描 1~MaxScannedFrames，缺号即停）
    private const string EditorWalkFrontDir = "Assets/Art/Characters/Warrior/WalkFront";
    private const string ResourcesWalkFrontDir = "Art/Characters/Warrior/WalkFront";
    private const string WalkFrontPrefix = "warrior_walk_front_";
    private const string EditorWalkBackDir = "Assets/Art/Characters/Warrior/WalkBack";
    private const string ResourcesWalkBackDir = "Art/Characters/Warrior/WalkBack";
    private const string WalkBackPrefix = "warrior_walk_back_";
    private const string EditorAttackSwordDir = "Assets/Art/Characters/Warrior/AttackSword";
    private const string ResourcesAttackSwordDir = "Art/Characters/Warrior/AttackSword";
    private const string AttackSwordPrefix = "warrior_attack_sword_";
    private const string EditorAttackSpearDir = "Assets/Art/Characters/Warrior/AttackSpear";
    private const string ResourcesAttackSpearDir = "Art/Characters/Warrior/AttackSpear";
    private const string AttackSpearPrefix = "warrior_attack_spear_";
    private const string EditorSkillDir = "Assets/Art/Characters/Warrior/Skill";
    private const string ResourcesSkillDir = "Art/Characters/Warrior/Skill";
    private const string SkillPrefix = "warrior_skill_";
    private const int MaxScannedFrames = 8;

    // v0.7.6：方向切换施密特滞回倍率——侧面切正/背面需 ay>ax×Margin，正/背面切侧面需 ax>ay×Margin
    private const float FacingSwitchMargin = 1.25f;

    // 死亡灰（PlayerController.OnPlayerDeath 定值；灰显期间不抢颜色）
    private static readonly Color DeathGray = new Color(0.3f, 0.3f, 0.3f, 1f);

    // v0.7.6：行走朝向（Front = 朝下朝屏幕/正面，Back = 朝上/背面；素材右向为 Side 基准）
    private enum Facing { Side, Front, Back }

    private SpriteRenderer sr;
    private Rigidbody2D rb;

    private AnimGroup current;
    private int frameIndex;
    private int frameDir = 1; // pingPong 往返方向（+1 顺放 / -1 倒放）
    private float frameTimer;
    private bool framesValid; // walk 帧组是否可用（决定置白换帧 or 保留原视觉兜底）
    private bool fallbackGroups; // 本次 Awake 是否走硬编码兜底建组（决定 walkFps/idleFps 字段是否实时同步进组）
    private AnimGroup walkGroup;
    private AnimGroup idleGroup;

    // v0.7.6：四方向行走状态
    private AnimGroup walkFrontGroup;
    private AnimGroup walkBackGroup;
    private Facing facing = Facing.Side;

    // v0.7.6：攻击/技能覆盖播放状态（非循环，播完自动回行走/待机驱动）
    private AnimGroup overrideGroup;
    private float overrideDuration;
    private float overrideTimer;
    private WeaponController weaponController;      // 延迟缓存（同 GameObject，GetComponent）
    private SpriteRenderer[] hiddenWeaponRenderers; // 覆盖播放期间被隐藏的武器视觉
    private bool[] hiddenWeaponStates;              // 各渲染器原 enabled 状态（结束逐一恢复）

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();

        if (groups == null || groups.Count == 0)
        {
            TryLoadDefaultWarriorFrames();
            fallbackGroups = true;
        }

        walkGroup = FindGroup(WalkGroupName);
        idleGroup = FindGroup(IdleGroupName);
        walkFrontGroup = FindGroup(WalkFrontGroupName);
        walkBackGroup = FindGroup(WalkBackGroupName);
        framesValid = walkGroup != null && walkGroup.frames != null && walkGroup.frames.Length > 0 && walkGroup.frames[0] != null;
        SyncFallbackFps();

        if (sr != null && framesValid)
        {
            sr.color = Color.white;
            sr.sprite = walkGroup.frames[0];
        }
        // 帧组缺失/为空：不改 sprite/color，保留既有视觉（绿球或 warrior_idle 底图）兜底，不报错
    }

    void Update()
    {
        if (sr == null || !framesValid) return;

        SyncFallbackFps(); // 兜底组的 fps 跟随序列化字段，Play 模式改值实时生效

        // 死亡灰显期间不强制置白（PlayerController 死亡表现优先），其余情况保持白色
        if (sr.color != Color.white && sr.color != DeathGray)
            sr.color = Color.white;

        // 攻击/技能覆盖播放期间：覆盖组独占帧推进，行走/待机驱动挂起，播完自动恢复
        if (overrideGroup != null)
        {
            UpdateOverride();
            return;
        }

        // 玩家驱动：速度决定 walk / idle（四方向组存在时按主轴分方向，否则 v0.7.5 水平驱动）
        if (rb != null)
        {
            if (walkFrontGroup != null || walkBackGroup != null)
                UpdateFourWayDrive();
            else
                UpdateSideDrive();
        }

        // 帧推进
        if (current == null || current.frames == null || current.frames.Length <= 1 || current.fps <= 0f)
            return;

        frameTimer += Time.deltaTime;
        float frameDuration = 1f / current.fps;
        if (frameTimer < frameDuration) return;
        frameTimer -= frameDuration;

        if (current.pingPong)
        {
            // 往返播放 0→N→0（端点各播一次，避免首尾跳变）
            frameIndex += frameDir;
            if (frameIndex >= current.frames.Length - 1) { frameIndex = current.frames.Length - 1; frameDir = -1; }
            else if (frameIndex <= 0) { frameIndex = 0; frameDir = 1; }
        }
        else
        {
            frameIndex++;
            if (frameIndex >= current.frames.Length)
            {
                if (current.loop) frameIndex = 0;
                else { frameIndex = current.frames.Length - 1; current = null; }
            }
        }

        Sprite next = current != null ? current.frames[frameIndex] : null;
        if (next != null) sr.sprite = next;
    }

    /// <summary>v0.7.5 水平驱动（四方向组全缺时的原样行为）：水平速度决定 walk / idle。</summary>
    private void UpdateSideDrive()
    {
        float vx = rb.linearVelocity.x;
        if (Mathf.Abs(vx) > MoveSpeedThreshold)
        {
            Play(WalkGroupName);
            sr.flipX = vx < 0f; // 素材实为右向（v0.7.5 验收实测修正），向左走镜像
        }
        else
        {
            PlayIdleOrStop();
        }
    }

    /// <summary>
    /// v0.7.6 四方向驱动：按速度主轴分方向（|vy|≥|vx| 且 vy<0 → 正面、vy>0 → 背面、否则侧面），
    /// 方向切换用施密特滞回（进垂直需 ay>ax×Margin，回侧面需 ax>ay×Margin）——
    /// 垂直→侧面立即切换（v0.7.5 验收：时间滞回会让背身帧在转向后多播 1 帧），斜向边界防抖。
    /// 主轴速度 ≤ 阈值播 idle（不重置朝向，保持停步前朝向）。
    /// </summary>
    private void UpdateFourWayDrive()
    {
        Vector2 v = rb.linearVelocity;
        float ax = Mathf.Abs(v.x);
        float ay = Mathf.Abs(v.y);

        if (Mathf.Max(ax, ay) > MoveSpeedThreshold)
        {
            Facing want = facing;
            if (facing == Facing.Side)
            {
                // 侧面 → 正/背面：垂直分量明显占优才切（斜向防抖）
                if (ay > ax * FacingSwitchMargin)
                    want = v.y < 0f ? Facing.Front : Facing.Back;
            }
            else
            {
                // 正/背面 → 侧面：水平分量一占优立即切（时间滞回会闪背身帧，验收实测修正）
                if (ax > ay * FacingSwitchMargin)
                    want = Facing.Side;
                else if (ay > MoveSpeedThreshold)
                    want = v.y < 0f ? Facing.Front : Facing.Back;
                // 垂直速度趋零且水平未占优：保持当前朝向（下一帧即进 idle）
            }
            facing = want;

            switch (facing)
            {
                case Facing.Front when walkFrontGroup != null:
                    Play(WalkFrontGroupName);
                    break;
                case Facing.Back when walkBackGroup != null:
                    Play(WalkBackGroupName);
                    break;
                default:
                    // 侧面，或正/背面组缺失的回退：侧面 walk；仅侧面时按 vx 符号更新 flipX，
                    // 正/背面回退下来时保持 flipX 不动（vy 主导无左右信息）
                    Play(WalkGroupName);
                    if (facing == Facing.Side)
                        sr.flipX = v.x < 0f;
                    break;
            }
        }
        else
        {
            PlayIdleOrStop();
        }
    }

    /// <summary>停步处理：播 idle 组；idle 缺失回退停 walk 第 1 帧（现行为）。</summary>
    private void PlayIdleOrStop()
    {
        if (idleGroup != null && idleGroup.frames != null && idleGroup.frames.Length > 0 && idleGroup.frames[0] != null)
        {
            Play(IdleGroupName); // 正面呼吸帧，不重置 flipX，保持停步前朝向
        }
        else if (current != null)
        {
            Stop(); // idle 帧缺失：回退现行为，停在 walk 第 1 帧
        }
    }

    /// <summary>
    /// 播放攻击序列帧组（v0.7.6，PlayerCombat.StartWindup 调用）：
    /// isSpear 选 "attack_spear"/"attack_sword" 组；fps 自动对齐 duration（帧数 ÷ 三阶段合计时长），
    /// 非循环播完自动回行走/待机驱动；播放期间隐藏 WeaponPivot 下武器视觉（武器烘进帧里），结束恢复。
    /// 组缺失/时长非法 → 返回 false 完全不干预（WeaponAnimator 挥砍照旧）。
    /// </summary>
    public bool PlayAttack(bool isSpear, float duration)
    {
        AnimGroup g = FindGroup(isSpear ? AttackSpearGroupName : AttackSwordGroupName);
        return TryBeginOverride(g, duration);
    }

    /// <summary>
    /// 播放命名技能序列帧组（v0.7.6 骨架，SkillExecutor 暂未接入）：机制同 PlayAttack
    /// （时长自动对齐、非循环、期间隐藏武器视觉、播完回驱动）。组缺失 → 返回 false 不干预。
    /// Skill 目录兜底加载为 "skill" 组；多技能分组时按约定组名传入即可。
    /// </summary>
    public bool PlaySkill(string groupName, float duration)
    {
        return TryBeginOverride(FindGroup(groupName), duration);
    }

    /// <summary>覆盖播放公共入口：校验组与时长，启动覆盖并隐藏武器视觉。</summary>
    private bool TryBeginOverride(AnimGroup g, float duration)
    {
        if (g == null || g.frames == null || g.frames.Length == 0 || g.frames[0] == null) return false;
        if (duration <= 0f) return false;
        if (sr == null || !framesValid) return false; // 无 walk 组时组件整体不介入视觉（与驱动一致）

        EndOverride(); // 上一次覆盖未播完被新攻击/技能覆盖：先恢复武器视觉再开新的

        overrideGroup = g;
        overrideDuration = duration;
        overrideTimer = duration;

        current = g;
        frameIndex = 0;
        frameDir = 1;
        frameTimer = 0f;
        sr.sprite = g.frames[0];

        HideWeaponVisuals();
        return true;
    }

    /// <summary>覆盖播放帧推进：按已过时间精确取帧（fps = 帧数 ÷ duration 的等价实现），播完自动结束。</summary>
    private void UpdateOverride()
    {
        overrideTimer -= Time.deltaTime;

        float frameDuration = overrideDuration / overrideGroup.frames.Length;
        int idx = Mathf.Clamp(
            Mathf.FloorToInt((overrideDuration - Mathf.Max(overrideTimer, 0f)) / frameDuration),
            0, overrideGroup.frames.Length - 1);
        if (idx != frameIndex)
        {
            frameIndex = idx;
            Sprite next = overrideGroup.frames[frameIndex];
            if (next != null) sr.sprite = next;
        }

        if (overrideTimer <= 0f)
            EndOverride();
    }

    /// <summary>结束覆盖播放：清覆盖组（下一帧起行走/待机驱动接管），恢复武器视觉。</summary>
    private void EndOverride()
    {
        if (overrideGroup == null && hiddenWeaponRenderers == null) return;
        overrideGroup = null;
        current = null; // 驱动侧下一帧 Play walk/idle 时从头开始
        RestoreWeaponVisuals();
    }

    /// <summary>隐藏 WeaponPivot 下全部 SpriteRenderer（weaponSprite + customVisual 部件），记录原 enabled 状态。</summary>
    private void HideWeaponVisuals()
    {
        if (weaponController == null)
            weaponController = GetComponent<WeaponController>();
        Transform pivot = weaponController != null ? weaponController.WeaponPivot : null;
        if (pivot == null) return;

        hiddenWeaponRenderers = pivot.GetComponentsInChildren<SpriteRenderer>(true);
        hiddenWeaponStates = new bool[hiddenWeaponRenderers.Length];
        for (int i = 0; i < hiddenWeaponRenderers.Length; i++)
        {
            hiddenWeaponStates[i] = hiddenWeaponRenderers[i].enabled;
            hiddenWeaponRenderers[i].enabled = false;
        }
    }

    /// <summary>按记录的原 enabled 状态逐一恢复武器视觉（中途换装/卸下导致销毁的渲染器跳过）。</summary>
    private void RestoreWeaponVisuals()
    {
        if (hiddenWeaponRenderers == null) return;
        for (int i = 0; i < hiddenWeaponRenderers.Length; i++)
            if (hiddenWeaponRenderers[i] != null)
                hiddenWeaponRenderers[i].enabled = hiddenWeaponStates[i];
        hiddenWeaponRenderers = null;
        hiddenWeaponStates = null;
    }

    /// <summary>播放命名动画组（同名重复调用不重启）。</summary>
    public void Play(string groupName)
    {
        AnimGroup g = FindGroup(groupName);
        if (g == null || g.frames == null || g.frames.Length == 0) return;
        if (current == g) return;

        current = g;
        frameIndex = 0;
        frameDir = 1;
        frameTimer = 0f;
        if (sr != null && g.frames[0] != null)
            sr.sprite = g.frames[0];
    }

    /// <summary>停止播放并停在当前组第 1 帧（当 idle 回退）。</summary>
    public void Stop()
    {
        if (current == null) return;
        frameIndex = 0;
        frameDir = 1;
        frameTimer = 0f;
        if (sr != null && current.frames != null && current.frames.Length > 0 && current.frames[0] != null)
            sr.sprite = current.frames[0];
        current = null;
    }

    /// <summary>设置水平镜像（素材左向：向右走传 true）。</summary>
    public void SetFlipX(bool flip)
    {
        if (sr != null) sr.flipX = flip;
    }

    private AnimGroup FindGroup(string groupName)
    {
        if (groups == null) return null;
        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i] != null && groups[i].name == groupName)
                return groups[i];
        }
        return null;
    }

    /// <summary>序列化组为空时按硬编码路径加载战士行走帧 + 待机呼吸 5 帧（SkillCatalog 同模式）。</summary>
    private void TryLoadDefaultWarriorFrames()
    {
        groups = new List<AnimGroup>();

        // walk 帧数可变（1~8 扫描，v0.7.5 起素材帧数以用户实际提供为准，曾 6→5）
        Sprite[] walkFrames = LoadSpritesScanned(EditorWalkDir, ResourcesWalkDir, "warrior_walk_1_");
        if (walkFrames != null)
        {
            groups.Add(new AnimGroup { name = WalkGroupName, frames = walkFrames, fps = walkFps, loop = true });
        }
        else
        {
            groups.Clear();
            Debug.LogWarning($"[FrameAnimator] 战士行走帧未加载到（{EditorWalkDir}/warrior_walk_1_1~N.png），保留既有视觉兜底。编辑器应走 AssetDatabase；打包需复制到 Resources/{ResourcesWalkDir}/。");
            return;
        }

        Sprite[] idleFrames = LoadSprites(EditorIdleDir, ResourcesIdleDir, i => $"warrior_idle_{i}", IdleFrameCount, out int idleLoaded);
        if (idleLoaded == IdleFrameCount)
        {
            groups.Add(new AnimGroup { name = IdleGroupName, frames = idleFrames, fps = idleFps, loop = true, pingPong = true });
        }
        else
        {
            // idle 缺失/不全：仅警告，停步回退为停在 walk 第 1 帧（现行为）
            Debug.LogWarning($"[FrameAnimator] 战士待机帧加载不完整（{idleLoaded}/{IdleFrameCount}），停步回退为 walk 第 1 帧。编辑器应走 AssetDatabase；打包需复制到 Resources/{ResourcesIdleDir}/。");
        }

        // v0.7.6：可选帧组（素材未生产时静默跳过，驱动/API 自动回退，不警告——缺目录是当前常态）
        Sprite[] frontFrames = LoadSpritesScanned(EditorWalkFrontDir, ResourcesWalkFrontDir, WalkFrontPrefix);
        if (frontFrames != null)
            groups.Add(new AnimGroup { name = WalkFrontGroupName, frames = frontFrames, fps = walkFps, loop = true });

        Sprite[] backFrames = LoadSpritesScanned(EditorWalkBackDir, ResourcesWalkBackDir, WalkBackPrefix);
        if (backFrames != null)
            groups.Add(new AnimGroup { name = WalkBackGroupName, frames = backFrames, fps = walkFps, loop = true });

        Sprite[] attackSwordFrames = LoadSpritesScanned(EditorAttackSwordDir, ResourcesAttackSwordDir, AttackSwordPrefix);
        if (attackSwordFrames != null)
            groups.Add(new AnimGroup { name = AttackSwordGroupName, frames = attackSwordFrames, fps = 10f, loop = false });

        Sprite[] attackSpearFrames = LoadSpritesScanned(EditorAttackSpearDir, ResourcesAttackSpearDir, AttackSpearPrefix);
        if (attackSpearFrames != null)
            groups.Add(new AnimGroup { name = AttackSpearGroupName, frames = attackSpearFrames, fps = 10f, loop = false });

        Sprite[] skillFrames = LoadSpritesScanned(EditorSkillDir, ResourcesSkillDir, SkillPrefix);
        if (skillFrames != null)
            groups.Add(new AnimGroup { name = SkillGroupName, frames = skillFrames, fps = 10f, loop = false });
    }

    /// <summary>按命名规则加载一组序列帧；返回数组（可能含 null），loaded 为成功数。</summary>
    private static Sprite[] LoadSprites(string editorDir, string resourcesDir, System.Func<int, string> nameOf, int count, out int loaded)
    {
        Sprite[] frames = new Sprite[count];
        loaded = 0;
        for (int i = 0; i < count; i++)
        {
            string assetName = nameOf(i);
#if UNITY_EDITOR
            frames[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{editorDir}/{assetName}.png");
#else
            frames[i] = Resources.Load<Sprite>($"{resourcesDir}/{assetName}");
#endif
            if (frames[i] != null) loaded++;
        }
        return frames;
    }

    /// <summary>
    /// v0.7.6：按命名前缀连续扫描 1~MaxScannedFrames 帧（帧数不定，缺号即停——要求素材从 1 连续编号）；
    /// 一帧都没有返回 null（对应组不建，调用方静默回退）。
    /// </summary>
    private static Sprite[] LoadSpritesScanned(string editorDir, string resourcesDir, string namePrefix)
    {
        List<Sprite> frames = new List<Sprite>();
        for (int i = 1; i <= MaxScannedFrames; i++)
        {
#if UNITY_EDITOR
            Sprite s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{editorDir}/{namePrefix}{i}.png");
#else
            Sprite s = Resources.Load<Sprite>($"{resourcesDir}/{namePrefix}{i}");
#endif
            if (s == null) break;
            frames.Add(s);
        }
        return frames.Count > 0 ? frames.ToArray() : null;
    }

    /// <summary>兜底建组时把 walkFps/idleFps 字段同步进组（Play 模式改字段实时调速）；序列化自配组不受影响。</summary>
    private void SyncFallbackFps()
    {
        if (!fallbackGroups) return;
        if (walkGroup != null) walkGroup.fps = walkFps;
        if (idleGroup != null) idleGroup.fps = idleFps;
        // v0.7.6：正/背面行走组同为行走帧率，跟随 walkFps；攻击/技能组 fps 由覆盖播放按 duration 对齐，不同步
        if (walkFrontGroup != null) walkFrontGroup.fps = walkFps;
        if (walkBackGroup != null) walkBackGroup.fps = walkFps;
    }
}
