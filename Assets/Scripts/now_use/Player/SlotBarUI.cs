using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 主 UI 槽位条（v0.7.2）：屏幕右下四槽（小技能/大招/武器技能/道具栏）+ 道具栏上方背包 3 格。
/// 全部运行时构建：RuntimeInitializeOnLoadMethod 自举 + sceneLoaded 自检，**自建常驻 Canvas**（DontDestroyOnLoad，
/// sortingOrder 50，不依附场景 Canvas，场景切换不销毁；切换后 RebindInventory 重绑新场景玩家数据源）；
/// 技能三槽由 SkillExecutor 每帧驱动（v0.7.4：SetSkillDisplay 技能名+技能色 / SetSkillCooldown 文本秒数，
/// 数据缺失的槽维持"—"；Refresh 写入的"—"会被同帧渲染前的每帧推送覆盖，无闪烁）；
/// 背包格为 Button，点击 → ItemInventory.SwapWithBackpack 与道具栏互换（无拖拽）。
/// 数量角标：槽位右下角 14pt，count≥2 才显示，超 99 显示 99+。
/// 布局边界核算（1920×1080）：面板 274×126 锚 BottomRight 留 20px 边距，
/// 占 x∈[1626,1900] y∈[20,146]；AmmoUI 在屏幕左下（PlayerStatsPanel 上方），无交叠。
/// 格子美术（v0.7.3 美术替换）：SlotFrame 石板框按固定路径加载（编辑器 AssetDatabase / 构建 Resources），
/// 主槽与背包格共用一张缩放适配；资产缺失时退回"白描边 + 深色底"占位，不留空。
/// </summary>
public class SlotBarUI : MonoBehaviour
{
    // ========== 布局常量（自检 §1 面板边界核算：274×126 ≤ 1920×1080 右下区） ==========
    private const float SlotSize = 64f;     // 四槽边长
    private const float PackSize = 56f;     // 背包格边长
    private const float Gap = 6f;
    private const float Margin = 20f;       // 距屏幕右/下边缘
    private const int SkillSlotCount = 3;   // 小技能/大招/武器技能
    private const float UiScale = 1.5f;    // 整体缩放（唯一调大小旋钮：1=64px 槽，1.25=80px；走 CanvasScaler.scaleFactor，边距同步缩放）

    private static readonly Color BgColor = new Color(0.12f, 0.12f, 0.12f, 0.9f);   // 深色底（SlotFrame 缺失时的占位回退）
    private static readonly string[] SlotLabels = { "小技能", "大招", "武器技能", "道具栏" };

    /// <summary>主槽按键角标（v1.0.6 用户需求④）：三技能槽 + 道具栏对应键位，背包格无。</summary>
    private static readonly string[] SlotKeyHints = { "F", "Q", "R", "C" };

    // ========== 格子美术（SlotFrame，v0.7.3） ==========

    /// <summary>SlotFrame 资产路径（已迁入 Resources/Art/UI/，编辑器与构建同源）。</summary>
    private const string SlotFrameEditorPath = "Assets/Resources/Art/UI/SlotFrame.jpg";
    private const string SlotFrameResourcesPath = "Art/UI/SlotFrame";
    private static Sprite slotFrame;

    /// <summary>加载格子框 Sprite（编辑器 AssetDatabase / 构建 Resources；导入为 Multiple 时取第一张切片）。缺失返回 null。</summary>
    private static Sprite LoadSlotFrame()
    {
        if (slotFrame != null) return slotFrame;
#if UNITY_EDITOR
        slotFrame = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(SlotFrameEditorPath);
        if (slotFrame == null)   // Sprite Mode = Multiple 时主资产不是 Sprite，扫子资产取第一张
        {
            foreach (Object o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(SlotFrameEditorPath))
                if (o is Sprite s) { slotFrame = s; break; }
        }
#else
        slotFrame = Resources.Load<Sprite>(SlotFrameResourcesPath);
        if (slotFrame == null)
        {
            Sprite[] all = Resources.LoadAll<Sprite>(SlotFrameResourcesPath);
            if (all.Length > 0) slotFrame = all[0];
        }
#endif
        if (slotFrame == null)
            Debug.LogWarning("[SlotBarUI] SlotFrame 未找到，格子退回纯色占位。");
        return slotFrame;
    }

    /// <summary>单个槽位的运行时引用。</summary>
    private class SlotWidget
    {
        public Image Bg;
        public Image IconBlock;
        public TMP_Text Label;
        public TMP_Text CenterText;
        public TMP_Text CountBadge;
        public TMP_Text KeyBadge;   // v1.0.6：左下角按键角标（F/Q/R/C）
    }

    private ItemInventory inventory;
    private readonly SlotWidget[] mainSlots = new SlotWidget[4];
    private readonly SlotWidget[] packSlots = new SlotWidget[ItemInventory.BackpackSize];

    // ========== 运行时自举（不改场景 YAML，任何场景都能显示） ==========

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureExists();
        // 每次场景加载后自检：常驻 Canvas 不被场景卸载销毁，此处双保险
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        EnsureExists();
        // 常驻 UI 跨场景存活，但玩家（ItemInventory）每场景是新的——重新绑定数据源
        SlotBarUI ui = FindAnyObjectByType<SlotBarUI>();
        if (ui != null) ui.RebindInventory();
    }

    private static void EnsureExists()
    {
        if (FindAnyObjectByType<SlotBarUI>() != null) return;

        // 自建常驻 ScreenSpaceOverlay Canvas（DontDestroyOnLoad）：不依附场景 Canvas，
        // 避免场景切换时随场景卸载销毁；sortingOrder 50 压在既有场景 UI 之上。
        GameObject go = new GameObject("SlotBarUI", typeof(RectTransform));
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        // 整体缩放走 CanvasScaler.scaleFactor：ScreenSpaceOverlay 的根 RectTransform 被引擎驱动，
        // 直写 localScale 每帧被重置（UiScale 曾因此不生效）；该常驻 Canvas 只装槽位条，整画布缩放无副作用
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = UiScale;
        go.AddComponent<GraphicRaycaster>();
        go.AddComponent<SlotBarUI>();
        DontDestroyOnLoad(go);
        EnsureEventSystem();
    }

    /// <summary>无 EventSystem 时补建（项目走 Input System，用 InputSystemUIInputModule）。</summary>
    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    // ========== 生命周期 ==========

    void Start()
    {
        RebindInventory();
        BuildPanel();
        Refresh();
    }

    /// <summary>绑定/重绑当前场景的 ItemInventory（场景切换后玩家是新实例，必须重绑）。</summary>
    private void RebindInventory()
    {
        if (inventory != null) inventory.OnChanged -= Refresh;
        inventory = FindAnyObjectByType<ItemInventory>();
        if (inventory == null)
            Debug.LogWarning("[SlotBarUI] 未找到 ItemInventory，背包/道具栏不会刷新（场景无玩家时正常）。");
        else
            inventory.OnChanged += Refresh;
        if (mainSlots[0] != null) Refresh();   // 面板已建则立即刷新
    }

    void OnDestroy()
    {
        if (inventory != null) inventory.OnChanged -= Refresh;
    }

    // ========== 技能三槽接口（v0.7.4 已接线：SkillExecutor 每帧驱动，空槽维持下方 Refresh 的"—"） ==========

    /// <summary>设置技能槽显示内容（v0.7.4 技能框架接线；index 0=小技能 1=大招 2=武器技能）。</summary>
    public void SetSkillDisplay(int index, string text, Color color)
    {
        if (index < 0 || index >= SkillSlotCount || mainSlots[index] == null) return;
        mainSlots[index].CenterText.text = text;
        mainSlots[index].CenterText.color = color;
    }

    /// <summary>设置技能槽 CD 剩余秒数显示（v0.7.4 接线；remaining≤0 恢复空占位）。</summary>
    public void SetSkillCooldown(int index, float remaining, float total)
    {
        if (index < 0 || index >= SkillSlotCount || mainSlots[index] == null) return;
        bool cooling = remaining > 0f && total > 0f;
        mainSlots[index].CenterText.text = cooling ? remaining.ToString("0.0") : "—";
    }

    // ========== 刷新（缓存复用 UI：外观参数每次刷新应用，自检 §1） ==========

    private void Refresh()
    {
        // 技能三槽：默认"—"空占位（v0.7.4 起由 SkillExecutor 每帧推送技能名/CD 覆盖；数据缺失的槽维持"—"）
        for (int i = 0; i < SkillSlotCount; i++)
            ApplySlot(mainSlots[i], SlotLabels[i], null, 0, "—");

        // 道具栏
        ItemStack active = inventory != null ? inventory.ActiveSlot : null;
        ApplySlot(mainSlots[3], SlotLabels[3], active != null ? active.Data : null,
            active != null ? active.Count : 0, "—");

        // 背包 3 格
        for (int i = 0; i < packSlots.Length; i++)
        {
            ItemStack stack = inventory != null ? inventory.Backpack[i] : null;
            ApplySlot(packSlots[i], null, stack != null ? stack.Data : null,
                stack != null ? stack.Count : 0, "");
        }
    }

    /// <summary>应用一个槽位的全部外观参数（字号/颜色/角标每次刷新重写）。</summary>
    private void ApplySlot(SlotWidget slot, string label, ConsumableData data, int count, string emptyText)
    {
        if (slot == null) return;

        // 槽位底色在创建时定死（SlotFrame 美术 = 透明露框 / 缺失 = 深色占位），刷新不覆盖
        if (slot.Label != null)
        {
            slot.Label.text = label ?? "";
            slot.Label.fontSize = 12;
            slot.Label.color = new Color(1f, 1f, 1f, 0.8f);
        }

        bool hasItem = data != null;
        slot.IconBlock.gameObject.SetActive(hasItem);
        if (hasItem)
            slot.IconBlock.color = data.IconColor;

        slot.CenterText.gameObject.SetActive(!hasItem);
        if (!hasItem)
        {
            slot.CenterText.text = emptyText;
            slot.CenterText.fontSize = 20;
            slot.CenterText.color = new Color(1f, 1f, 1f, 0.35f);   // 空占位压暗
        }

        // 数量角标：count≥2 才显示，超 99 显示 99+
        slot.CountBadge.fontSize = 14;
        slot.CountBadge.color = Color.white;
        slot.CountBadge.text = count >= 2 ? (count > 99 ? "99+" : count.ToString()) : "";
    }

    // ========== 运行时构建 ==========

    private void BuildPanel()
    {
        RectTransform root = (RectTransform)transform;
        float panelWidth = 4f * SlotSize + 3f * Gap;                    // 274
        float panelHeight = SlotSize + Gap + PackSize;                  // 126
        root.anchorMin = new Vector2(1f, 0f);
        root.anchorMax = new Vector2(1f, 0f);
        root.pivot = new Vector2(1f, 0f);
        root.sizeDelta = new Vector2(panelWidth, panelHeight);
        root.anchoredPosition = new Vector2(-Margin, Margin);
        // 注：不要在 root 上设 localScale——Overlay Canvas 根 RectTransform 被引擎驱动会每帧重置，缩放用 CanvasScaler.scaleFactor（EnsureExists）

        // 四槽横排（pivot 右下：从右往左排，slot3 道具栏贴右缘）+ 按键角标
        for (int i = 0; i < 4; i++)
        {
            float centerX = -(SlotSize * 0.5f) - (3 - i) * (SlotSize + Gap);
            mainSlots[i] = CreateSlot($"Slot_{SlotLabels[i]}", root,
                new Vector2(centerX, SlotSize * 0.5f), SlotSize, false, -1);
            mainSlots[i].KeyBadge = CreateKeyBadge((RectTransform)mainSlots[i].Bg.transform, SlotKeyHints[i]);
        }

        // 背包 3 格：道具栏上方横排，右缘与道具栏对齐
        for (int i = 0; i < packSlots.Length; i++)
        {
            float centerX = -(PackSize * 0.5f) - (packSlots.Length - 1 - i) * (PackSize + Gap);
            float centerY = SlotSize + Gap + PackSize * 0.5f;
            packSlots[i] = CreateSlot($"Backpack_{i}", root,
                new Vector2(centerX, centerY), PackSize, true, i);
        }
    }

    /// <summary>左下角按键角标（v1.0.6 需求④）：金色小字标明槽位对应键位。</summary>
    private TMP_Text CreateKeyBadge(RectTransform slotBg, string key)
    {
        GameObject go = CreateUIObject($"Key_{key}", slotBg);
        TMP_Text t = go.AddComponent<TextMeshProUGUI>();
        t.font = TMPFontProvider.Font;
        t.text = key;
        t.fontSize = 14;
        t.fontStyle = FontStyles.Bold;
        t.color = new Color(1f, 0.82f, 0.25f, 0.95f);
        t.alignment = TextAlignmentOptions.BottomLeft;
        t.raycastTarget = false;
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.sizeDelta = new Vector2(20f, 16f);
        rect.anchoredPosition = new Vector2(4f, 2f);
        return t;
    }

    /// <summary>创建一个槽位：SlotFrame 石板框（缺失时退回白描边 1px + 深色底）+ 顶部名称 + 中央占位/色块 + 右下数量角标。</summary>
    private SlotWidget CreateSlot(string name, RectTransform parent, Vector2 center,
        float size, bool clickable, int packIndex)
    {
        Sprite frame = LoadSlotFrame();

        // 底层：SlotFrame 石板框（整框美术）；缺失时退回纯白（配内缩深色底形成 1px 描边）
        GameObject borderGo = CreateUIObject(name, parent);
        Image border = borderGo.AddComponent<Image>();
        border.color = Color.white;
        if (frame != null) border.sprite = frame;
        RectTransform borderRect = (RectTransform)borderGo.transform;
        borderRect.anchorMin = new Vector2(1f, 0f);
        borderRect.anchorMax = new Vector2(1f, 0f);
        borderRect.pivot = new Vector2(0.5f, 0.5f);
        borderRect.sizeDelta = new Vector2(size, size);
        borderRect.anchoredPosition = center;

        // 内底：有框图时透明（露出石板内部），无框图时深色内缩 1px 形成描边占位
        GameObject bgGo = CreateUIObject("Bg", borderRect);
        Image bg = bgGo.AddComponent<Image>();
        bg.color = frame != null ? Color.clear : BgColor;
        RectTransform bgRect = (RectTransform)bgGo.transform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = new Vector2(1f, 1f);
        bgRect.offsetMax = new Vector2(-1f, -1f);

        SlotWidget slot = new SlotWidget { Bg = bg };

        // 背包格：Button 点击 → 与道具栏互换；有框图时点击反馈着色打在框图上（Bg 透明无显示）
        if (clickable)
        {
            Button btn = borderGo.AddComponent<Button>();
            btn.targetGraphic = frame != null ? border : bg;
            int index = packIndex;   // 闭包捕获
            btn.onClick.AddListener(() =>
            {
                if (inventory != null) inventory.SwapWithBackpack(index);
            });
        }

        // 顶部名称文字
        GameObject labelGo = CreateUIObject("Label", bgRect);
        slot.Label = labelGo.AddComponent<TextMeshProUGUI>();
        slot.Label.font = TMPFontProvider.Font;
        slot.Label.alignment = TextAlignmentOptions.Top;
        RectTransform labelRect = (RectTransform)labelGo.transform;
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.sizeDelta = new Vector2(0f, 16f);
        labelRect.anchoredPosition = new Vector2(0f, -2f);

        // 中央占位文字（"—"）
        GameObject centerGo = CreateUIObject("Center", bgRect);
        slot.CenterText = centerGo.AddComponent<TextMeshProUGUI>();
        slot.CenterText.font = TMPFontProvider.Font;
        slot.CenterText.alignment = TextAlignmentOptions.Center;
        RectTransform centerRect = (RectTransform)centerGo.transform;
        centerRect.anchorMin = Vector2.zero;
        centerRect.anchorMax = Vector2.one;
        centerRect.offsetMin = Vector2.zero;
        centerRect.offsetMax = Vector2.zero;

        // 道具色块（占位图标）
        GameObject iconGo = CreateUIObject("Icon", bgRect);
        slot.IconBlock = iconGo.AddComponent<Image>();
        RectTransform iconRect = (RectTransform)iconGo.transform;
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(24f, 24f);
        iconRect.anchoredPosition = Vector2.zero;

        // 右下数量角标
        GameObject badgeGo = CreateUIObject("CountBadge", bgRect);
        slot.CountBadge = badgeGo.AddComponent<TextMeshProUGUI>();
        slot.CountBadge.font = TMPFontProvider.Font;
        slot.CountBadge.alignment = TextAlignmentOptions.BottomRight;
        RectTransform badgeRect = (RectTransform)badgeGo.transform;
        badgeRect.anchorMin = new Vector2(1f, 0f);
        badgeRect.anchorMax = new Vector2(1f, 0f);
        badgeRect.pivot = new Vector2(1f, 0f);
        badgeRect.sizeDelta = new Vector2(30f, 18f);
        badgeRect.anchoredPosition = new Vector2(-2f, 1f);

        return slot;
    }

    private GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }
}
