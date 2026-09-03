using UnityEngine;

/// <summary>
/// 音量滑条素材库（v1.1.16 精简）：运行时只需三张图——空槽轨道 / 黄条 / 默认滑钮。
/// 悬停光晕与按下压暗均为纯代码表现（SliderHandleState：程序生成光晕 + 颜色调暗），
/// 不再加载 hover/pressed/selected/disabled 状态图（省运行时内存；母版归档仍在 Assets/UI/PauseMenu）。
/// 尺寸适配契约（与 PausePanel.CreateSlider 布局常量成对）：轨道 1206×147（8.2:1）等比 260×32；
/// 滑钮 182×182 与轨道原生配比 1.24 → 40×40；黄条 16px 居中内槽。
/// 断链安全：TrackEmpty 为 null 时调用方回退旧纯色滑条。
/// </summary>
public static class SliderSprites
{
    private static Sprite trackEmpty, fill, knobDefault;

    public static Sprite TrackEmpty => trackEmpty != null ? trackEmpty
        : trackEmpty = Resources.Load<Sprite>("UI/PauseMenu/slider_track_empty");

    public static Sprite Fill => fill != null ? fill
        : fill = Resources.Load<Sprite>("UI/PauseMenu/slider_fill");

    public static Sprite KnobDefault => knobDefault != null ? knobDefault
        : knobDefault = Resources.Load<Sprite>("UI/PauseMenu/slider_knob_default");
}
