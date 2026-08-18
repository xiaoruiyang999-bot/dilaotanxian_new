using UnityEngine;

/// <summary>
/// walk-over 交互基类（计划书评审意见 #8：走过去触发，不改输入系统）。
/// Trigger 检测 Player → 一次性触发 ApplyEffect → 视觉压暗为已消耗态。
/// 子类只实现效果本身；触发/消耗/表现由基类收口。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public abstract class Interactable : MonoBehaviour
{
    [SerializeField] protected SpriteRenderer visual;
    [Tooltip("已消耗态亮度倍率（压暗表示已用掉）")]
    [SerializeField, Range(0f, 1f)] private float consumedBrightness = 0.35f;

    private bool consumed;

    protected virtual void Awake()
    {
        if (visual == null) visual = GetComponent<SpriteRenderer>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || !other.CompareTag("Player")) return;
        consumed = true;
        OnConsumed(other);
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
