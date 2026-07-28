using UnityEngine;

/// <summary>
/// E 键交互基类（v0.6.1，计划书 4.3：全部交互统一走 E 键，原为 walk-over 触发）。
/// PlayerInteractor 探测到最近候选并提示"按 E"，玩家按 E → Interact()：
/// 一次性消耗 → ApplyEffect → 视觉压暗为已消耗态。
/// 子类只实现效果本身；触发/消耗/表现由基类收口。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public abstract class Interactable : MonoBehaviour
{
    [SerializeField] protected SpriteRenderer visual;
    [Tooltip("已消耗态亮度倍率（压暗表示已用掉）")]
    [SerializeField, Range(0f, 1f)] private float consumedBrightness = 0.35f;

    private bool consumed;

    /// <summary>是否已消耗（一次性交互完成后为 true，供 PlayerInteractor 过滤候选）。</summary>
    public bool IsConsumed => consumed;

    protected virtual void Awake()
    {
        if (visual == null) visual = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// E 键交互入口（v0.6.1，由 PlayerInteractor 调用）：
    /// 一次性消耗，触发效果与已消耗表现。已消耗后重复调用无副作用。
    /// </summary>
    public virtual void Interact(Collider2D player)
    {
        if (consumed) return;
        consumed = true;
        OnConsumed(player);
    }

    /// <summary>消耗钩子（v0.5.4）：默认 = 旧行为（立即结算 + 压暗）；宝箱（先演动画）/传送门（不压暗）覆盖。</summary>
    protected virtual void OnConsumed(Collider2D player)
    {
        ApplyEffect(player);
        SetConsumedVisual();
    }

    /// <summary>一次性效果（治疗/回甲/祭坛±等），由子类实现。</summary>
    protected abstract void ApplyEffect(Collider2D player);

    protected void SetConsumedVisual()
    {
        if (visual == null) return;
        Color c = visual.color;
        visual.color = new Color(c.r * consumedBrightness, c.g * consumedBrightness,
                                 c.b * consumedBrightness, c.a);
    }
}
