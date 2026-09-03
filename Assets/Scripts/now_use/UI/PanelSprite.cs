using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 面板母版加载器（v1.1.5）：石板 9-Slice 面板母版的运行时统一入口。
/// 母版资产：Assets/UI/Panel/panel_stone.png（源）；运行时副本：Assets/Resources/UI/panel_stone.png
/// （项目代码 UI 资产惯例走 Resources——SlotFrame 同模式；改图后两处需同步）。
/// Border 实测：L34 / B34 / R37 / T49（砖框内沿，含内侧阴影环；非对称但 9-Slice 四边独立合法）。
///
/// 用法（代码构建 UI 三行接入）：
///     Image img = go.AddComponent&lt;Image&gt;();
///     img.sprite = PanelSprite.Stone;
///     img.type = Image.Type.Sliced;   // 四角不拉伸，砖框沿单轴拉伸，中央整体拉伸
/// 断链安全：素材缺失时 Stone 为 null，Image 显示为无图纯色（color 兜底），不抛错。
/// </summary>
public static class PanelSprite
{
    public static void ConfigureCanvasScaler(GameObject canvasGo)
    {
        if (canvasGo == null) return;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 1f;
    }

    private static Sprite stone;

    /// <summary>石板面板母版（9-Slice 已在导入设置配好 Border）；加载失败返回 null。</summary>
    public static Sprite Stone
    {
        get
        {
            if (stone == null)
                stone = Resources.Load<Sprite>("UI/panel_stone");
            return stone;
        }
    }

    // ---------- 三态石板按钮（v1.1.7：normal/hover/pressed，Border 三图统一 L13/B11/R13/T13 防切态跳动） ----------

    private static Sprite btnNormal, btnHover, btnPressed;

    public static Sprite BtnNormal => btnNormal != null ? btnNormal
        : btnNormal = Resources.Load<Sprite>("UI/btn_stone_normal");
    public static Sprite BtnHover => btnHover != null ? btnHover
        : btnHover = Resources.Load<Sprite>("UI/btn_stone_hover");
    public static Sprite BtnPressed => btnPressed != null ? btnPressed
        : btnPressed = Resources.Load<Sprite>("UI/btn_stone_pressed");

    /// <summary>
    /// 便捷接入：给按钮底图挂三态石板（Sliced + SpriteSwap，hover/pressed 自动换图；
    /// selectedHighlight 复用 hover 供键盘/手柄导航）。素材缺失时落回纯色 + 默认 ColorTint，不抛错。
    /// 注意 targetGraphic 会被指到 img（换图发生在 img 上），外部高亮框等装饰不受影响。
    /// </summary>
    public static void ApplyStoneButton(Button btn, Image img, Color fallbackColor)
    {
        if (btn == null || img == null) return;
        if (BtnNormal != null)
        {
            img.sprite = BtnNormal;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            var state = new SpriteState
            {
                highlightedSprite = BtnHover,
                pressedSprite = BtnPressed,
                selectedSprite = BtnHover
            };
            btn.spriteState = state;
            btn.transition = Selectable.Transition.SpriteSwap;
            btn.targetGraphic = img;
        }
        else
        {
            img.color = fallbackColor;   // 断链兜底：纯色按钮（旧观感）
        }
    }

    /// <summary>便捷接入：给 Image 挂母版并设 Sliced（素材缺失时仅设色，行为兜底）。</summary>
    public static void ApplyStonePanel(Image img, Color fallbackColor)
    {
        if (img == null) return;
        img.sprite = Stone;
        if (img.sprite != null)
        {
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }
        else
        {
            img.color = fallbackColor;   // 断链兜底：纯色面板（ClassSelectUI 旧观感）
        }
    }
}
