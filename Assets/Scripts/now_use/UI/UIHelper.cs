using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI 构建工具类：消除 DeathPanel / PausePanel / PlayerUI 间的重复代码。
/// 所有方法均为静态，无状态，可在任意 MonoBehaviour 或编辑器脚本中调用。
/// </summary>
public static class UIHelper
{
    /// <summary>
    /// 创建文本标签（TMP）。单次 AddComponent，先 text 后 font 避免 NRE。
    /// </summary>
    /// <param name="parent">父级 Transform</param>
    /// <param name="text">显示文本</param>
    /// <param name="fontSize">字号</param>
    /// <param name="color">颜色</param>
    /// <param name="anchor">锚点（anchorMin = anchorMax）</param>
    /// <param name="sizeDelta">尺寸</param>
    /// <param name="position">可选的 anchoredPosition 偏移（默认 Vector2.zero）</param>
    /// <param name="name">可选的 GameObject 名称（默认 "Label"）</param>
    /// <returns>创建的 TextMeshProUGUI 组件</returns>
    public static TextMeshProUGUI CreateLabel(
        Transform parent, string text, int fontSize, Color color,
        Vector2 anchor, Vector2 sizeDelta,
        Vector2? position = null, string name = "Label")
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.font = TMPFontProvider.Font;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        RectTransform rt = tmp.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = position ?? Vector2.zero;
        rt.sizeDelta = sizeDelta;

        return tmp;
    }

    /// <summary>
    /// 在指定条下方创建新状态条（克隆模板，同宽同锚点，左缘对齐，间距 2px）。
    /// 创建后不触碰其 Transform——布局归场景编辑，代码永不改已存在条的位置。
    /// </summary>
    /// <param name="templateBar">克隆源（通常为 HP/Armor/Mana 条）</param>
    /// <param name="aboveBar">新条应在其下方的条</param>
    /// <param name="barName">新条 GameObject 名称</param>
    /// <param name="color">填充颜色</param>
    /// <param name="height">条高度（默认 5px）</param>
    /// <param name="spacing">条间距（默认 2px）</param>
    /// <returns>创建的 Image 组件，失败返回 null</returns>
    public static Image CreateBarBelow(
        Image templateBar, Image aboveBar, string barName, Color color,
        float height = 5f, float spacing = 2f)
    {
        if (templateBar == null || aboveBar == null) return null;

        GameObject go = Object.Instantiate(templateBar.gameObject, templateBar.transform.parent);   // 编译修复：静态类需 Object. 前缀
        go.name = barName;

        Image img = go.GetComponent<Image>();
        img.color = color;
        img.fillAmount = 1f;

        RectTransform rect = (RectTransform)go.transform;
        RectTransform aboveRect = (RectTransform)aboveBar.transform;
        rect.anchorMin = aboveRect.anchorMin;
        rect.anchorMax = aboveRect.anchorMax;
        rect.pivot = aboveRect.pivot;
        rect.sizeDelta = new Vector2(aboveRect.sizeDelta.x, height);
        rect.anchoredPosition = new Vector2(
            aboveRect.anchoredPosition.x,
            aboveRect.anchoredPosition.y - aboveRect.sizeDelta.y * 0.5f - spacing - height * 0.5f);

        return img;
    }

    /// <summary>
    /// 创建全屏遮罩 Image（用于点击任意处关闭等场景）。
    /// </summary>
    /// <param name="parent">父级 Transform</param>
    /// <param name="color">遮罩颜色</param>
    /// <param name="name">GameObject 名称</param>
    /// <returns>创建的 Image 组件</returns>
    public static Image CreateFullscreenMask(Transform parent, Color color, string name = "Mask")
    {
        GameObject go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);

        Image img = go.GetComponent<Image>();
        img.color = color;

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        return img;
    }

    /// <summary>
    /// 创建 Stone 风格面板背景 Image。
    /// </summary>
    /// <param name="parent">父级 Transform</param>
    /// <param name="size">面板尺寸</param>
    /// <param name="color">背景颜色</param>
    /// <param name="name">GameObject 名称</param>
    /// <returns>创建的 Image 组件</returns>
    public static Image CreateStonePanel(
        Transform parent, Vector2 size, Color color, string name = "StonePanel")
    {
        GameObject go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);

        Image img = go.GetComponent<Image>();
        PanelSprite.ApplyStonePanel(img, color);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;

        return img;
    }

    /// <summary>
    /// 创建带 Stone 样式的菜单按钮（支持图片或纯色兜底）。
    /// </summary>
    /// <param name="parent">父级 Transform</param>
    /// <param name="spriteName">Resources/UI/PauseMenu/ 下的图片名（null = 纯色兜底）</param>
    /// <param name="fallbackText">纯色兜底时显示的文字</param>
    /// <param name="position">按钮位置</param>
    /// <param name="onClick">点击回调</param>
    /// <param name="size">可选尺寸（默认 520x110）</param>
    /// <returns>创建的 Button 组件</returns>
    public static Button CreateMenuButton(
        Transform parent, string spriteName, string fallbackText,
        Vector2 position, UnityEngine.Events.UnityAction onClick, Vector2? size = null)
    {
        var btnGo = new GameObject($"Btn_{spriteName ?? "Fallback"}", typeof(Image), typeof(Button));
        btnGo.transform.SetParent(parent, false);

        var btn = btnGo.GetComponent<Button>();
        Image img = btnGo.GetComponent<Image>();

        if (!string.IsNullOrEmpty(spriteName))
            img.sprite = Resources.Load<Sprite>($"UI/PauseMenu/{spriteName}");

        img.preserveAspect = true;
        RectTransform rt = (RectTransform)btnGo.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size ?? new Vector2(520f, 110f);

        if (img.sprite == null)
        {
            PanelSprite.ApplyStoneButton(btn, img, new Color(0.2f, 0.18f, 0.14f, 0.95f));
            CreateLabel(btnGo.transform, fallbackText, 20, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(460f, 42f));
        }
        else
        {
            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
            colors.selectedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.75f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;
        }

        if (onClick != null)
            btn.onClick.AddListener(onClick);

        return btn;
    }

    /// <summary>
    /// 创建滑动条（Slider），带填充条和拖动手柄。
    /// </summary>
    /// <param name="parent">父级 Transform</param>
    /// <param name="defaultValue">初始值（0~1）</param>
    /// <param name="onValueChanged">值变化回调</param>
    /// <param name="width">宽度（默认 400）</param>
    /// <param name="height">高度（默认 20）</param>
    /// <returns>创建的 Slider 组件</returns>
    public static Slider CreateSlider(
        Transform parent, float defaultValue, System.Action<float> onValueChanged,
        float width = 400f, float height = 20f)
    {
        var go = new GameObject("Slider", typeof(Slider));
        go.transform.SetParent(parent, false);
        var slider = go.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = defaultValue;
        slider.wholeNumbers = false;

        var bg = new GameObject("Background", typeof(Image));
        bg.transform.SetParent(go.transform, false);
        bg.GetComponent<Image>().color = new Color(0.15f, 0.12f, 0.1f, 1f);
        var bgRect = (RectTransform)bg.transform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        var fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRect.sizeDelta = Vector2.zero;

        var fill = new GameObject("Fill", typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        fill.GetComponent<Image>().color = new Color(0.4f, 0.78f, 0.4f, 1f);
        var fillRect = (RectTransform)fill.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.sizeDelta = Vector2.zero;
        slider.fillRect = fillRect;

        var handleArea = new GameObject("Handle Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        var handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.sizeDelta = new Vector2(-12f, 0f);

        var handle = new GameObject("Handle", typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        handle.GetComponent<Image>().color = Color.white;
        var handleRect = (RectTransform)handle.transform;
        handleRect.sizeDelta = new Vector2(14f, 18f);
        slider.handleRect = handleRect;
        slider.targetGraphic = handle.GetComponent<Image>();

        if (onValueChanged != null)
            slider.onValueChanged.AddListener(v => onValueChanged(v));

        return slider;
    }
}
