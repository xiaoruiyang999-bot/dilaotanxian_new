using UnityEngine;

/// <summary>
/// 攻击范围预警显示组件。纯视觉层（Presentation Layer）。
/// 作为 Enemy 的子物体预先存在。EnemyAI 通过 Show/Hide/SetRadius/SetColor 控制。
/// 禁止：攻击逻辑、AI逻辑、状态机、计时器、事件、TryAttack、Enemy引用控制、Chase控制、Recovery控制。
/// </summary>
public class AttackIndicator : MonoBehaviour
{
    public enum ShapeType
    {
        Circle,
        Box,
        Sector
    }

    [Header("渲染组件")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("颜色")]
    [SerializeField] private Color warningColor = new Color(1f, 1f, 0f, 120f / 255f);
    [SerializeField] private Color dangerColor = new Color(1f, 0f, 0f, 140f / 255f);

    [Header("形状")]
    [SerializeField] private ShapeType shape = ShapeType.Circle;

    [Header("位置偏移")]
    [SerializeField] private Vector3 localOffset = Vector3.zero;

    private Color currentColor;

    public Color WarningColor => warningColor;
    public Color DangerColor => dangerColor;

    void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        currentColor = warningColor;
        UpdateVisual();
    }

    void OnDisable()
    {
        Hide();
    }

    /// <summary>显示并定位攻击范围指示器。</summary>
    public void Show()
    {
        transform.localPosition = localOffset;
        transform.localRotation = Quaternion.identity;
        gameObject.SetActive(true);
    }

    /// <summary>隐藏攻击范围指示器。</summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>设置攻击范围半径（视觉缩放）。</summary>
    public void SetRadius(float radius)
    {
        transform.localScale = Vector3.one * (radius * 2f);
    }

    /// <summary>设置当前颜色。</summary>
    public void SetColor(Color color)
    {
        currentColor = color;
        UpdateVisual();
    }

    /// <summary>设置指示器形状（扩展点，当前主要支持 Circle）。</summary>
    public void SetShape(ShapeType newShape)
    {
        shape = newShape;
        UpdateVisual();
    }

    /// <summary>设置透明度（0~1）。</summary>
    public void SetAlpha(float alpha)
    {
        currentColor.a = Mathf.Clamp01(alpha);
        UpdateVisual();
    }

    /// <summary>刷新视觉表现。</summary>
    private void UpdateVisual()
    {
        if (spriteRenderer == null) return;
        spriteRenderer.color = currentColor;

        // TODO v0.5+: 根据 shape 更换 sprite / mesh / LineRenderer
    }
}
