using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 滑钮指针状态机（v1.1.16 重写为纯代码视觉）：
/// - 悬停：滑钮后方显示程序生成的径向光晕（零美术内存——不用 182×182 的 hover 大图，
///   也不需 pressed/selected/disabled 各一张）；光晕 Sprite 静态缓存全项目共享一份 64×64 纹理。
/// - 按下：滑钮 Image 颜色压暗到 0.65（与按键亮度规范同值），抬起恢复。
/// Slider 的 Handle 不是独立 Selectable（拿不到 SpriteState），指针状态由本组件接管；
/// 光晕挂在滑条根下、渲染序在轨道/填充之后滑钮之前（透过滑钮透明边显现，不遮钮体），
/// 每帧跟随滑钮位置（Slider 驱动 handleRect，光晕不能做滑钮子物体——子物体必渲染在钮体之上）。
/// </summary>
public class SliderHandleState : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    private const float PressedDim = 0.65f;        // 按下亮度（= 按键规范 Pressed）
    private static readonly Color GlowTint = new Color(1f, 0.85f, 0.55f, 0.55f);   // 暖金"一丝丝"

    private Image image;
    private RectTransform glow;
    private bool hovering;
    private bool pressing;

    private Image Img => image != null ? image : image = GetComponent<Image>();

    private static Sprite glowSprite;

    /// <summary>程序生成光晕 Sprite（64×64 径向三次方衰减，白芯暖金由 Image.color 调色），全项目静态共享。</summary>
    private static Sprite GlowSprite
    {
        get
        {
            if (glowSprite != null) return glowSprite;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = size * 0.5f - 0.5f, r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r;
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * a));   // 三次方衰减=边缘一丝丝
                }
            tex.Apply();
            glowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
            return glowSprite;
        }
    }

    /// <summary>
    /// 在滑条根下创建光晕（渲染序插到滑钮区之前）。由 CreateSlider 调一次；
    /// size 为光晕显示尺寸（建议 ≈ 滑钮 × 1.4）。
    /// </summary>
    public void SetupGlow(Transform sliderRoot, Vector2 size)
    {
        var go = new GameObject("KnobGlow", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(sliderRoot, false);
        var img = go.GetComponent<Image>();
        img.sprite = GlowSprite;
        img.color = GlowTint;
        img.raycastTarget = false;
        glow = go.GetComponent<RectTransform>();
        glow.sizeDelta = size;
        // 渲染序：轨道(0) 填充(1) [光晕] 滑钮区(末) ——插到滑钮区之前，光晕透出滑钮透明边且不遮钮体
        glow.SetSiblingIndex(Mathf.Max(0, sliderRoot.childCount - 2));
        glow.gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        Apply();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        pressing = false;
        Apply();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressing = true;
        Apply();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressing = false;
        Apply();
    }

    void Update()
    {
        // 光晕跟随滑钮（Slider 驱动 handleRect；光晕非滑钮子物体，需逐帧同步）
        if (glow != null && glow.gameObject.activeSelf)
            glow.position = transform.position;
    }

    void OnDestroy()
    {
        if (glow != null) Destroy(glow.gameObject);
    }

    private void Apply()
    {
        if (Img == null) return;

        // 滑钮本体始终 default 图，状态全用代码表现：按下压暗（光晕保留），离开才收光晕
        Img.color = pressing ? new Color(PressedDim, PressedDim, PressedDim, 1f) : Color.white;
        if (glow != null)
            glow.gameObject.SetActive(hovering);
    }
}
