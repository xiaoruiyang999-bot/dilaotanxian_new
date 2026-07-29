using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 玩家交互器（v0.6.1，计划书 4.3）：E 键交互统一入口 + 两段式拾取。
/// 每 0.1s OverlapCircle（Unity 6 ContactFilter2D + 缓冲，零分配风格参照 WeaponHitbox）
/// 探测周围 Interactable / IPickupable（均在 Default 层、trigger，组件过滤），取最近者为候选。
/// 候选反馈：呼吸放大 1.1（DOTween，SetLink）+ 头顶"按 E"世界空间标签（复用 WorldUIRoot 模式）。
/// 按 E：普通交互物直接 Interact()；可拾取物仅 1 个直接拾取，≥2 个弹出纯文字拾取列表
/// （滚轮/数字键切换，E 确认，Esc 或走远自动关闭）。无候选按 E 无副作用。
/// 本组件由 PlayerController.Awake 运行时挂载（编辑器运行期间不改 prefab YAML）。
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [Header("探测")]
    [SerializeField] private float detectRadius = 1.2f;
    [SerializeField] private float detectInterval = 0.1f;

    [Header("候选反馈")]
    [SerializeField] private float highlightScale = 1.1f;
    [SerializeField] private float highlightDuration = 0.6f;
    [SerializeField] private float hintHeightOffset = 0.9f;     // "按 E"标签相对候选中心的抬升
    [SerializeField] private int hintFontSize = 16;             // "按 E"字号
    [SerializeField] private Vector2 hintPanelSize = new Vector2(40f, 30f); // "按 E"底框尺寸（用户调定：40×30 + 缩放 0.03 + 字号 16）
    [SerializeField] private float hintWorldScale = 0.03f;      // "按 E"标签整体世界缩放（等比例放大用这项）

    [Header("拾取列表")]
    [SerializeField] private float listCloseDistance = 2.5f;    // 玩家与最近物品超过该距离自动关列表

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

    // 候选（Interactable 与 IPickupable 互斥持有）
    private Interactable candidateInteractable;
    private IPickupable candidatePickupable;
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

    // 拾取列表（屏幕空间，纯文字）
    private readonly List<IPickupable> listItems = new List<IPickupable>();
    private readonly List<TMP_Text> listRows = new List<TMP_Text>();
    private TMP_Text listFooter;
    private GameObject listCanvasGo;
    private RectTransform listPanelRect;
    private int listIndex;
    private float scrollCooldown;
    private bool listOpen;

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
        if (hintCanvasGo != null) Destroy(hintCanvasGo);
        if (listCanvasGo != null) Destroy(listCanvasGo);
    }

    void Update()
    {
        detectTimer -= Time.deltaTime;
        if (detectTimer <= 0f)
        {
            detectTimer = detectInterval;
            RefreshCandidate();
        }

        // 临时提示计时结束 → 恢复候选提示
        if (tempHintTimer > 0f)
        {
            tempHintTimer -= Time.deltaTime;
            if (tempHintTimer <= 0f)
                UpdateHintVisibility();
        }

        if (listOpen) UpdateList();
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
                // 可拾取物名签（两行）：底框按名字长度加宽、加高一行（v0.6.3）
                Vector2 panelSize = hintPanelSize;
                if (IsPickupNameHint)
                {
                    float nameWidth = candidatePickupable.DisplayName.Length * hintFontSize + 12f;
                    panelSize = new Vector2(
                        Mathf.Max(hintPanelSize.x, nameWidth),
                        hintPanelSize.y + hintFontSize + 4f);
                }
                ((RectTransform)hintCanvasGo.transform).sizeDelta = panelSize;
                hintCanvasGo.transform.localScale = Vector3.one * hintWorldScale;
            }
        }
    }

    // ========== 输入入口（PlayerController 分发） ==========

    /// <summary>E 键按下：列表打开时拾取高亮项；否则按候选类型分发。</summary>
    public void OnInteractPressed()
    {
        if (health != null && health.IsDead) return;

        if (listOpen)
        {
            ConfirmListSelection();
            return;
        }

        if (candidateTransform == null) return;   // 无候选按 E 无副作用

        // 普通交互物（宝箱/祭坛/补给/传送门）→ 直接触发
        if (candidateInteractable != null)
        {
            candidateInteractable.Interact(playerCollider);
            ForceRefresh();
            return;
        }

        // 可拾取物：统计半径内全部可拾取物
        CollectPickupables();
        if (listItems.Count == 0) { ForceRefresh(); return; }
        if (listItems.Count == 1)
        {
            // 单物品直拾，不弹列表
            listItems[0].OnPickedUp(gameObject);
            listItems.Clear();
            ForceRefresh();
        }
        else
        {
            OpenList();
        }
    }

    /// <summary>Esc：关闭拾取列表。</summary>
    public void OnCancelPressed()
    {
        if (listOpen) CloseList();
    }

    private void ForceRefresh()
    {
        detectTimer = detectInterval;
        RefreshCandidate();
    }

    // ========== 候选探测与反馈 ==========

    private void RefreshCandidate()
    {
        Interactable bestInteractable = null;
        IPickupable bestPickupable = null;
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
                    bestPickupable = null;
                    bestTransform = it.transform;
                }
            }
            else if (hit.TryGetComponent(out IPickupable pk))
            {
                Transform t = ((Component)pk).transform;
                float d = ((Vector2)t.position - selfPos).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    bestInteractable = null;
                    bestPickupable = pk;
                    bestTransform = t;
                }
            }
        }

        SetCandidate(bestInteractable, bestPickupable, bestTransform);
    }

    private void SetCandidate(Interactable it, IPickupable pk, Transform t)
    {
        if (t == candidateTransform) return;   // 候选未变

        ClearCandidateFeedback();

        candidateInteractable = it;
        candidatePickupable = pk;
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

    // ========== "按 E" 世界空间标签 ==========

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
        bool show = temp || (candidateTransform != null && !listOpen);
        if (show)
        {
            EnsureHintCanvas();
            if (hintText != null)
            {
                if (temp)
                    hintText.text = tempHintText;
                else if (candidatePickupable != null)
                    // 可拾取物（武器/法力瓶等）：第一行物品名，第二行"按 E"（v0.6.3）
                    hintText.text = candidatePickupable.DisplayName + "\n按 E";
                else
                    hintText.text = "按 E";
            }
        }
        if (hintCanvasGo != null) hintCanvasGo.SetActive(show);
    }

    /// <summary>当前提示是否为可拾取物名签（两行，需要更宽更高的底框）。</summary>
    private bool IsPickupNameHint => tempHintTimer <= 0f && candidatePickupable != null;

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

    // ========== 拾取列表（屏幕空间纯文字） ==========

    /// <summary>收集探测半径内全部可拾取物，按距离升序（默认高亮最近项）。</summary>
    private void CollectPickupables()
    {
        listItems.Clear();
        Vector2 selfPos = transform.position;

        int count = Physics2D.OverlapCircle(selfPos, detectRadius, detectFilter, hitBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = hitBuffer[i];
            if (hit == null) continue;
            if (hit.transform.IsChildOf(transform)) continue;
            if (hit.TryGetComponent(out IPickupable pk))
                listItems.Add(pk);
        }

        listItems.Sort((a, b) =>
            (((Component)a).transform.position - (Vector3)selfPos).sqrMagnitude.CompareTo(
            (((Component)b).transform.position - (Vector3)selfPos).sqrMagnitude));
    }

    private void OpenList()
    {
        listOpen = true;
        listIndex = 0;   // 已按距离排序，默认高亮最近项
        EnsureListCanvas();
        RebuildListRows();
        listCanvasGo.SetActive(true);
        UpdateHintVisibility();
    }

    private void CloseList()
    {
        listOpen = false;
        listItems.Clear();
        if (listCanvasGo != null) listCanvasGo.SetActive(false);
        UpdateHintVisibility();
        ForceRefresh();
    }

    private void ConfirmListSelection()
    {
        if (listItems.Count == 0) { CloseList(); return; }

        listIndex = Mathf.Clamp(listIndex, 0, listItems.Count - 1);
        IPickupable pick = listItems[listIndex];
        if (pick != null)
            pick.OnPickedUp(gameObject);

        CloseList();
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

        listFooter = CreateListText(listPanelRect, "Footer", "E 拾取 / Esc 关闭", 12);
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
            // 高亮项用体力黄，其余白
            listRows[i].color = i == listIndex
                ? new Color(0.9569f, 0.8157f, 0.2471f)
                : Color.white;
            listRows[i].text = (i == listIndex ? "▶ " : "　") + listItems[i].DisplayName;
        }
    }

    private void UpdateList()
    {
        // 玩家死亡 → 关闭
        if (health != null && health.IsDead) { CloseList(); return; }

        // 剔除已失效物品（被拾取/楼层切换销毁）
        bool changed = false;
        for (int i = listItems.Count - 1; i >= 0; i--)
        {
            if (!(listItems[i] is Component c) || c == null)
            {
                listItems.RemoveAt(i);
                changed = true;
            }
        }
        if (listItems.Count == 0) { CloseList(); return; }
        if (changed)
        {
            listIndex = Mathf.Clamp(listIndex, 0, listItems.Count - 1);
            RebuildListRows();
        }

        // 走远自动关闭
        Vector2 selfPos = transform.position;
        float nearest = float.MaxValue;
        foreach (IPickupable item in listItems)
        {
            float d = ((Vector2)((Component)item).transform.position - selfPos).sqrMagnitude;
            if (d < nearest) nearest = d;
        }
        if (nearest > listCloseDistance * listCloseDistance) { CloseList(); return; }

        // 数字键直选
        if (Keyboard.current != null)
        {
            for (int i = 0; i < digitKeys.Length && i < listItems.Count; i++)
            {
                if (Keyboard.current[digitKeys[i]].wasPressedThisFrame && listIndex != i)
                {
                    listIndex = i;
                    RefreshListHighlight();
                }
            }
        }

        // 滚轮切换（节流）
        scrollCooldown -= Time.deltaTime;
        if (scrollCooldown <= 0f && Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (scroll != 0f)
            {
                int step = scroll > 0f ? -1 : 1;
                listIndex = (listIndex + step + listItems.Count) % listItems.Count;
                RefreshListHighlight();
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
