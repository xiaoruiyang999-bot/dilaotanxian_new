using UnityEngine;

/// <summary>
/// 狼人新动画链驱动器（v1.2.2 灰盒，GDD §22.6）：薄 MonoBehaviour——只写 Animator 参数，
/// 不做任何判定/数值/流程决策。消费烘焙资产 Resources/Animation/Werewolf/Werewolf.controller
///（Tools > Werewolf > 烘焙动画资产 生成）。
/// 灰盒开关 useNewAnimatorDriver 默认关闭：开启时禁用旧 FrameAnimator（迁移基线不删，
/// 关掉开关即回旧链——可运行入口永不断）。
/// 参数合同：MoveX（左右移动输入）/ Speed（输入模长）/ IsTransformed（WerewolfTransformation.IsBeast）。
/// 攻击动画状态待素材产出（伤害窗口仍由 PlayerCombat 数据计时，见 AttackDefinition 注释）。
/// </summary>
[RequireComponent(typeof(Animator))]
public class WerewolfAnimatorDriver : MonoBehaviour
{
    [Tooltip("职业角色定义（动画目录合同来源）")]
    [SerializeField] private PlayableCharacterDefinition character;

    [Header("灰盒开关")]
    [Tooltip("启用新 Animator 链（禁用旧 FrameAnimator）；关闭 = 完全回到旧链，零影响")]
    [SerializeField] private bool useNewAnimatorDriver = false;

    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int IsTransformed = Animator.StringToHash("IsTransformed");

    private Animator animator;
    private PlayerController playerController;    // MoveInput 来源
    private WerewolfTransformation transformation; // IsBeast 来源（可能未挂）

    private void Awake()
    {
        animator = GetComponent<Animator>();
        playerController = GetComponent<PlayerController>();
        transformation = GetComponent<WerewolfTransformation>();

        var controller = Resources.Load<RuntimeAnimatorController>("Animation/Werewolf/Werewolf");
        if (controller == null)
        {
            Debug.LogWarning("[WerewolfAnimatorDriver] 未找到烘焙资产 Resources/Animation/Werewolf/Werewolf.controller——" +
                "先运行 Tools > Werewolf > 烘焙动画资产。组件自禁用（旧链不受影响）。");
            enabled = false;
            return;
        }
        animator.runtimeAnimatorController = controller;
        animator.enabled = useNewAnimatorDriver;
        SetOldChainEnabled(!useNewAnimatorDriver);
    }

    /// <summary>Play 中切灰盒开关即时生效（OnValidate 驱动）。</summary>
    private void OnValidate()
    {
        if (animator == null) return;
        bool active = useNewAnimatorDriver && enabled;
        animator.enabled = active;
        SetOldChainEnabled(!active);
    }

    private void Update()
    {
        if (!useNewAnimatorDriver || playerController == null) return;

        Vector2 input = playerController.MoveInput;
        animator.SetFloat(MoveX, input.x);
        animator.SetFloat(Speed, input.sqrMagnitude > 0.0001f ? 1f : 0f);
        if (transformation != null)
            animator.SetBool(IsTransformed, transformation.IsBeast);
    }

    /// <summary>旧 FrameAnimator 让位/复位的唯一入口（迁移期共存合同）。</summary>
    private void SetOldChainEnabled(bool on)
    {
        var oldDriver = GetComponent<FrameAnimator>();
        if (oldDriver != null) oldDriver.enabled = on;
    }
}
