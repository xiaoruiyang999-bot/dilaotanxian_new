using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 玩家交互器（v0.6.1 起，v0.7.2 实时拾取列表改版）：E 键交互统一入口。
/// 每 0.1s OverlapCircle（Unity 6 ContactFilter2D + 缓冲，零分配风格参照 WeaponHitbox）探测周围
/// Interactable / IPickupable（均在 Default 层、trigger，组件过滤）。
/// 普通交互物（宝箱/祭坛/补给/传送门）：最近者为候选，呼吸放大 1.1 + 头顶"按 E"标签，按 E 直接触发。
/// 可拾取物：**实时拾取列表**——靠近自动进列表、走远（detectRadius+listRemoveBuffer）自动出列表；
/// 列表常驻显示（有物品时），滚轮/数字键切换选中；选中项在场景中呼吸放大标识；按 E 拾取选中项。
/// 本组件由 PlayerController.Awake 运行时挂载（编辑器运行期间不改 prefab YAML）。
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [Header("探测")]
    [SerializeField] private float detectRadius = 1.2f;
    [SerializeField] private float detectInterval = 0.1f;

    [Header("候选反馈（普通交互物）")]
    [SerializeField] private float highlightScale = 1.25f;  // v0.7.5 与 Player.prefab 序列化值同步（prefab 生效值 1.25，自检清单双写规则）
    [SerializeField] private float highlightDuration = 0.6f;
    [SerializeField] private float hintHeightOffset = 0.9f;     // "按 E"标签相对候选中心的抬升
    [SerializeField] private int hintFontSize = 16;             // "按 E"字号
    [SerializeField] private Vector2 hintPanelSize = new Vector2(40f, 30f); // "按 E"底框尺寸（用户调定：40×30 + 缩放 0.03 + 字号 16）
    [SerializeField] private float hintWorldScale = 0.03f;      // "按 E"标签整体世界缩放（等比例放大用这项）

    [Header("实时拾取列表")]
    [SerializeField] private float listRemoveBuffer = 1.0f;     // 出列表距离 = detectRadius + 该缓冲（滞回防抖）
    [SerializeField] private float selectedScale = 1.15f;       // 选中项呼吸放大倍率
    [SerializeField] private float selectedDuration = 0.5f;     // 选中项呼吸周期

    private const int BufferSize = 32;
    private const string WorldUIRootName = "WorldUIRoot";
    private static readonly Key[] digitKeys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
        Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
    };

    private readonly Collider2D[] hitBuffer = new Collider2D[BufferSize];
    private ContactFilter2D detectFilter;

    private Health health;
    private Collider2D playerCollider;
    private Camera mainCamera;
    private float detectTimer;

    // 普通交互物候选
    private Interactable candidateInteractable;
    private Transform candidateTransform;
    private Vector3 candidateOriginalScale;
    private Tween highlightTween;

    // "按 E" 世界空间提示
    private GameObject hintCanvasGo;
    private Transform hintTransform;
    private TMP_Text hintText;

    // 临时提示（如"职业不符"）：优先于"按 E"显示，计时结束自动恢复
    private string tempHintText;
    private float tempHintTimer;

    // 实时拾取列表（屏幕空间，纯文字）
    private readonly List<IPickupable> listItems = new List<IPickupable>();
    private readonly List<TMP_Text> listRows = new List<TMP_Text>();
    private TMP_Text listFooter;
    private GameObject listCanvasGo;
    private RectTransform listPanelRect;
    private int listIndex;
    private float scrollCooldown;
    private bool listVisible;

    // 列表选中项场景反馈（呼吸放大）
    private Transform selectedTransform;
    private Vector3 selectedOriginalScale;
    private Tween selectedTween;

    void Awake()
    {
        health = GetComponent<Health>();
        playerCollider = GetComponent<Collider2D>();

        // 交互物/拾取物都是 trigger，无专用 layer → 全层 + 组件过滤（与 WeaponHitbox 同风格）
        detectFilter = new ContactFilter2D();
        detectFilter.useTriggers = true;
    }

    void OnDestroy()
    {
        ClearCandidateFeedback();
        ClearSelectedFeedback();
        if (hintCanvasGo != null) Destroy(hintCanvasGo);
        if (listCanvasGo != null) Destroy(listCanvasGo);
    }

    void Update()
    {
        detectTimer -= Time.deltaTime;
        if (detectTimer <= 0f)
        {
            detectTimer = detectInterval;
            RefreshInteractableCandidate();
            ReconcilePickupList();
        }

        // 临时提示计时结束 → 恢复候选提示
        if (tempHintTimer > 0f)
        {
            tempHintTimer -= Time.deltaTime;
            if (tempHintTimer <= 0f)
                UpdateHintVisibility();
        }

        if (listVisible) UpdateList();
    }

    void LateUpdate()
    {
        // "按 E"/临时提示标签跟随目标，只平移不旋转（与 PlayerWorldStatusBar 同模式）
        if (hintCanvasGo != null && hintCanvasGo.activeSelf)
        {
            if (tempHintTimer > 0f)
                hintTransform.position = transform.position + Vector3.up * hintHeightOffset;
            else if (candidateTransform != null)
                hintTransform.position = candidateTransform.position + Vector3.up * hintHeightOffset;
            else
                return;

            // 每帧应用字号/底框/缩放，保证 Inspector 调整即时生效（标签创建后被缓存复用）
            if (hintText != null)
            {
                hintText.fontSize = hintFontSize;
                ((RectTransform)hintCanvasGo.transform).sizeDelta = hintPanelSize;
                hintCanvasGo.transform.localScale = Vector3.one * hintWorldScale;
            }
        }
    }

    // ========== 输入入口（PlayerController 分发） ==========

    /// <summary>E 键按下：普通交互物优先直接触发；否则拾取列表选中项。</summary>
    public void OnInteractPressed()
    {
        if (health != null && health.IsDead) return;

        // 普通交互物（宝箱/祭坛/补给/传送门）→ 直接触发
        if (candidateInteractable != null)
        {
            candidateInteractable.Interact(playerCollider);
            ForceRefresh();
            return;
        }

        // 可拾取物：拾取列表当前选中项
        if (listItems.Count == 0) return;   // 无候选按 E 无副作用
        listIndex = Mathf.Clamp(listIndex, 0, listItems.Count - 1);
        IPickupable pick = listItems[listIndex];
        if (pick != null)
            pick.OnPickedUp(gameObject);
        ForceRefresh();
    }

    /// <summary>Esc：实时列表自动管理显隐，保留入口备用（当前无副作用）。</summary>
    public void OnCancelPressed()
    {
    }

    private void ForceRefresh()
    {
        detectTimer = detectInterval;
        RefreshInteractableCandidate();
        ReconcilePickupList();
    }

    // ========== 普通交互物候选探测与反馈 ==========

    private void RefreshInteractableCandidate()
    {
        Interactable bestInteractable = null;
        Transform bestTransform = null;
        float bestDist = float.MaxValue;
        Vector2 selfPos = transform.position;

        int count = Physics2D.OverlapCircle(selfPos, detectRadius, detectFilter, hitBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = hitBuffer[i];
            if (hit == null) continue;
            if (hit.transform.IsChildOf(transform)) continue;   // 跳过玩家自身

            if (hit.TryGetComponent(out Interactable it))
            {
                if (it.IsConsumed) continue;
                float d = ((Vector2)it.transform.position - selfPos).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    bestInteractable = it;
                    bestTransform = it.transform;
                }
            }
        }

        SetCandidate(bestInteractable, bestTransform);
    }

    private void SetCandidate(Interactable it, Transform t)
    {
        if (t == candidateTransform) return;   // 候选未变

        ClearCandidateFeedback();

        candidateInteractable = it;
        candidateTransform = t;

        if (candidateTransform != null)
        {
            candidateOriginalScale = candidateTransform.localScale;
            highlightTween = candidateTransform
                .DOScale(candidateOriginalScale * highlightScale, highlightDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(candidateTransform.gameObject);   // 目标销毁时自动 kill
        }

        UpdateHintVisibility();
    }

    private void ClearCandidateFeedback()
    {
        if (highlightTween != null)
        {
            highlightTween.Kill();
            highlightTween = null;
        }
        if (candidateTransform != null)
            candidateTransform.localScale = candidateOriginalScale;
    }

    // ========== "按 E" 世界空间标签（仅普通交互物/临时提示） ==========

    /// <summary>
    /// 临时提示（v0.6.2，如 WeaponPickup 的"职业不符"）：
    /// 优先于"按 E"显示在玩家头顶，duration 秒后自动恢复候选提示。
    /// </summary>
    public void ShowTemporaryHint(string text, float duration = 1.2f)
    {
        tempHintText = text;
        tempHintTimer = duration;
        UpdateHintVisibility();
    }

    private void UpdateHintVisibility()
    {
        bool temp = tempHintTimer > 0f;
        bool show = temp || candidateInteractable != null;
        if (show)
        {
            EnsureHintCanvas();
            if (hintText != null)
                hintText.text = temp ? tempHintText : "按 E";
        }
        if (hintCanvasGo != null) hintCanvasGo.SetActive(show);
    }

    private void EnsureHintCanvas()
    {
        if (hintCanvasGo != null) return;

        hintCanvasGo = new GameObject("InteractHintCanvas");
        hintCanvasGo.transform.SetParent(EnsureWorldUIRoot(), false);

        Canvas canvas = hintCanvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 11;
        hintCanvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 100f;

        RectTransform rect = (RectTransform)hintCanvasGo.transform;
        rect.sizeDelta = hintPanelSize;
        rect.localScale = Vector3.one * hintWorldScale;

        Image bg = hintCanvasGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.7f);   // 深底

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(hintCanvasGo.transform, false);
        TMP_Text text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = "按 E";
        text.font = TMPFontProvider.Font;
        text.fontSize = hintFontSize;
        text.color = Color.white;                 // 白字
        text.alignment = TextAlignmentOptions.Center;
        hintText = text;
        RectTransform textRect = (RectTransform)textGo.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        hintTransform = hintCanvasGo.transform;
    }

    // ========== 实时拾取列表 ==========

    /// <summary>
    /// 列表与实际周围物品对账：失效/走远（detectRadius+listRemoveBuffer）的移除，
    /// 新进入探测半径的按距离升序追加。成员变化才重建行，选中项反馈随之重指。
    /// </summary>
    private void ReconcilePickupList()
    {
        bool changed = false;
        Vector2 selfPos = transform.position;
        float removeSqr = (detectRadius + listRemoveBuffer) * (detectRadius + listRemoveBuffer);

        // 移除：已销毁 或 走远
        for (int i = listItems.Count - 1; i >= 0; i--)
        {
            if (!(listItems[i] is Component c) || c == null ||
                ((Vector2)c.transform.position - selfPos).sqrMagnitude > removeSqr)
            {
                listItems.RemoveAt(i);
                changed = true;
            }
        }

        // 新增：探测半径内且不在列表中的直接追加（不重排既有行，选中不跳；同帧多个新项按探测顺序）
        int count = Physics2D.OverlapCircle(selfPos, detectRadius, detectFilter, hitBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = hitBuffer[i];
            if (hit == null) continue;
            if (hit.transform.IsChildOf(transform)) continue;
            if (hit.TryGetComponent(out IPickupable pk) && !listItems.Contains(pk))
            {
                listItems.Add(pk);
                changed = true;
            }
        }

        if (changed)
        {
            listIndex = Mathf.Clamp(listIndex, 0, Mathf.Max(listItems.Count - 1, 0));
            RebuildListRows();
            SetListVisible(listItems.Count > 0);
            ApplySelectedFeedback();
        }
    }

    private void SetListVisible(bool visible)
    {
        if (listVisible == visible) return;
        listVisible = visible;
        if (visible) EnsureListCanvas();
        if (listCanvasGo != null) listCanvasGo.SetActive(visible);
    }

    /// <summary>切换选中项（滚轮/数字键）：刷新行高亮 + 场景呼吸反馈重指。</summary>
    private void SetListIndex(int index)
    {
        if (listItems.Count == 0) return;
        index = Mathf.Clamp(index, 0, listItems.Count - 1);
        if (index == listIndex) return;
        listIndex = index;
        RefreshListHighlight();
        ApplySelectedFeedback();
    }

    /// <summary>选中项场景反馈：呼吸放大（DOTween Yoyo + SetLink）；切换/移除时还原旧项缩放。</summary>
    private void ApplySelectedFeedback()
    {
        ClearSelectedFeedback();

        if (!listVisible || listItems.Count == 0) return;
        listIndex = Mathf.Clamp(listIndex, 0, listItems.Count - 1);
        if (!(listItems[listIndex] is Component c) || c == null) return;

        selectedTransform = c.transform;
        selectedOriginalScale = selectedTransform.localScale;
        selectedTween = selectedTransform
            .DOScale(selectedOriginalScale * selectedScale, selectedDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(selectedTransform.gameObject);
    }

    private void ClearSelectedFeedback()
    {
        if (selectedTween != null)
        {
            selectedTween.Kill();
            selectedTween = null;
        }
        if (selectedTransform != null)
            selectedTransform.localScale = selectedOriginalScale;
        selectedTransform = null;
    }

    private void EnsureListCanvas()
    {
        if (listCanvasGo != null) return;

        listCanvasGo = new GameObject("PickupListCanvas");
        Canvas canvas = listCanvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(listCanvasGo.transform, false);
        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.75f);
        listPanelRect = (RectTransform)panel.transform;
        listPanelRect.pivot = new Vector2(0f, 0.5f);   // 从玩家右侧展开

        listCanvasGo.SetActive(false);
    }

    private void RebuildListRows()
    {
        if (listItems.Count == 0)
        {
            foreach (TMP_Text row in listRows)
                if (row != null) Destroy(row.gameObject);
            listRows.Clear();
            if (listFooter != null) Destroy(listFooter.gameObject);
            return;
        }

        EnsureListCanvas();

        foreach (TMP_Text row in listRows)
            if (row != null) Destroy(row.gameObject);
        listRows.Clear();
        if (listFooter != null) Destroy(listFooter.gameObject);

        const float rowHeight = 24f;
        const float footerHeight = 20f;
        const float width = 200f;
        listPanelRect.sizeDelta = new Vector2(width, listItems.Count * rowHeight + footerHeight);

        for (int i = 0; i < listItems.Count; i++)
        {
            TMP_Text row = CreateListText(listPanelRect, $"Item{i}", listItems[i].DisplayName, 16);
            RectTransform r = (RectTransform)row.transform;
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -i * rowHeight);
            r.sizeDelta = new Vector2(-8f, rowHeight);
            listRows.Add(row);
        }

        listFooter = CreateListText(listPanelRect, "Footer", "E 拾取 / 滚轮切换", 12);
        RectTransform fr = (RectTransform)listFooter.transform;
        fr.anchorMin = new Vector2(0f, 1f);
        fr.anchorMax = new Vector2(1f, 1f);
        fr.pivot = new Vector2(0.5f, 1f);
        fr.anchoredPosition = new Vector2(0f, -listItems.Count * rowHeight);
        fr.sizeDelta = new Vector2(-8f, footerHeight);
        listFooter.color = new Color(1f, 1f, 1f, 0.6f);

        RefreshListHighlight();
    }

    private TMP_Text CreateListText(RectTransform parent, string name, string content, int fontSize)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.font = TMPFontProvider.Font;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        return text;
    }

    private void RefreshListHighlight()
    {
        for (int i = 0; i < listRows.Count; i++)
        {
            if (listRows[i] == null) continue;
            // 高亮项用黄色，其余白（">" 前缀标识选中；字体不支持 ▶，已修）
            listRows[i].color = i == listIndex
                ? new Color(0.9569f, 0.8157f, 0.2471f)
                : Color.white;
            listRows[i].text = (i == listIndex ? "> " : "　") + listItems[i].DisplayName;
        }
    }

    private void UpdateList()
    {
        // 玩家死亡 → 清空列表
        if (health != null && health.IsDead)
        {
            listItems.Clear();
            RebuildListRows();
            SetListVisible(false);
            ApplySelectedFeedback();
            return;
        }

        // 数字键直选
        if (Keyboard.current != null)
        {
            for (int i = 0; i < digitKeys.Length && i < listItems.Count; i++)
            {
                if (Keyboard.current[digitKeys[i]].wasPressedThisFrame)
                    SetListIndex(i);
            }
        }

        // 滚轮切换（节流）
        scrollCooldown -= Time.deltaTime;
        if (scrollCooldown <= 0f && Mouse.current != null && listItems.Count > 0)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (scroll != 0f)
            {
                int step = scroll > 0f ? -1 : 1;
                SetListIndex((listIndex + step + listItems.Count) % listItems.Count);
                scrollCooldown = 0.15f;
            }
        }

        // 面板跟随玩家（屏幕空间，WorldToScreenPoint 定位 + 屏幕边缘钳制）
        UpdateListPosition();
    }

    private void UpdateListPosition()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null || listPanelRect == null) return;

        Vector3 screen = mainCamera.WorldToScreenPoint(transform.position);
        float x = screen.x + 60f;
        float y = screen.y;
        x = Mathf.Clamp(x, 0f, Screen.width - listPanelRect.sizeDelta.x);
        y = Mathf.Clamp(y, listPanelRect.sizeDelta.y * 0.5f, Screen.height - listPanelRect.sizeDelta.y * 0.5f);
        listPanelRect.position = new Vector3(x, y, 0f);
    }

    // ========== 工具 ==========

    /// <summary>获取或创建全局 WorldUIRoot（与 PlayerWorldStatusBar 同一模式）。</summary>
    private static Transform EnsureWorldUIRoot()
    {
        GameObject root = GameObject.Find(WorldUIRootName);
        if (root == null)
        {
            root = new GameObject(WorldUIRootName);
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
        }
        return root.transform;
    }
}
