using DG.Tweening;
using UnityEngine;

/// <summary>
/// 传送门（v0.5.4，美术：灰石块中央嵌蓝色漩涡门）：按 E（v0.6.1）→ RunManager.NextFloor()。
/// 漩涡动效 = 旋转臂 + 差速亮核（DOTween 无限循环，随对象销毁自动 kill）。
/// 消耗态不压暗（覆盖基类钩子）：踩门即切楼层，对象随旧楼层销毁，无需已消耗表现。
/// </summary>
public class PortalInteractable : Interactable
{
    [SerializeField] private Transform vortexArm;
    [SerializeField] private Transform vortexCore;

    private RunManager runManager;

    protected override void Awake()
    {
        base.Awake();
        if (vortexArm != null)
            vortexArm.DORotate(new Vector3(0f, 0f, -360f), 2f, RotateMode.FastBeyond360)
                     .SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart)
                     .SetLink(vortexArm.gameObject);   // 楼层清理销毁时自动 kill，避免 DOTween safe mode 报 missing target
        if (vortexCore != null)
            vortexCore.DORotate(new Vector3(0f, 0f, 360f), 3.5f, RotateMode.FastBeyond360)
                      .SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart)
                      .SetLink(vortexCore.gameObject);
    }

    public void Init(RunManager rm) => runManager = rm;

    /// <summary>覆盖基类钩子：直接结算、不压暗（踩门即切楼层，对象随旧楼层销毁）。</summary>
    protected override void OnConsumed(Collider2D player) => ApplyEffect(player);

    protected override void ApplyEffect(Collider2D player)
    {
        if (runManager == null)
        {
            Debug.LogError("[Run] 传送门未初始化（RunManager 缺失）");
            return;
        }
        Debug.Log("[Run] 踏入传送门 → 下一层");
        runManager.NextFloor();
    }
}
