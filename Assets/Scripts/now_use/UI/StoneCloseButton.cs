using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 石质叉除键三态动效（v1.1.50）：配合 Button 的 ColorTint 亮度之外补齐缩放与光晕——
/// 无选中原大小 → 悬停稍微放大（1.12x）+ 白色光晕 → 按下稍微缩小（0.9x）。
/// 缩放与光晕透明度均按帧 Lerp 平滑过渡；光晕 = 同素材白色低透明度副本垫在按钮图下层，
/// 不需要额外光晕素材。挂到按钮 GameObject 上即可（引用自身 Image 作按钮图）。
/// </summary>
public class StoneCloseButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    private const float NormalScale = 1f;
    private const float HoverScale = 1.12f;
    private const float PressedScale = 0.9f;
    private const float LerpSpeed = 14f;          // 状态过渡速度（每秒）
    private const float GlowAlpha = 0.45f;        // 悬停时光晕峰值透明度
    private const float GlowScale = 1.38f;        // 光晕相对按钮的放大倍数

    private Image buttonImage;
    private Image glow;
    private bool hovered;
    private bool pressed;

    /// <summary>初始化并构建光晕子物体；由创建方（PausePanel.CreateCloseButton）调用。</summary>
    public void Setup(Image target)
    {
        buttonImage = target;

        // 光晕：同素材白色副本，垫在按钮图下层（siblingIndex 0），默认隐藏
        if (buttonImage != null && buttonImage.sprite != null)
        {
            var glowGo = new GameObject("Glow", typeof(Image));
            glowGo.transform.SetParent(transform, false);
            glow = glowGo.GetComponent<Image>();
            glow.sprite = buttonImage.sprite;
            glow.preserveAspect = true;
            glow.raycastTarget = false;
            glow.color = new Color(1f, 1f, 1f, 0f);
            var gr = glow.rectTransform;
            gr.anchorMin = gr.anchorMax = gr.pivot = new Vector2(0.5f, 0.5f);
            gr.anchoredPosition = Vector2.zero;
            gr.sizeDelta = buttonImage.rectTransform.sizeDelta * GlowScale;
            glowGo.transform.SetSiblingIndex(0);   // 按钮图在上，光晕在下
        }
    }

    public void OnPointerEnter(PointerEventData e) => hovered = true;
    public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData e) => pressed = true;
    public void OnPointerUp(PointerEventData e) => pressed = false;

    void Update()
    {
        float targetScale = pressed ? PressedScale : hovered ? HoverScale : NormalScale;
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetScale, LerpSpeed * Time.unscaledDeltaTime);

        if (glow != null)
        {
            // 光晕只在悬停且未按下时全亮；按下时收掉一半，配合"按下最暗"
            float targetAlpha = hovered ? (pressed ? GlowAlpha * 0.35f : GlowAlpha) : 0f;
            Color c = glow.color;
            c.a = Mathf.Lerp(c.a, targetAlpha, LerpSpeed * Time.unscaledDeltaTime);
            glow.color = c;
        }
    }
}
