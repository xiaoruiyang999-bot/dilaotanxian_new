using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 死亡结算面板（v1.0.5）：玩家死亡瞬间弹出本局结算——抵达楼层 / 击杀数 / 存活时长，
/// Esc 或点击任意处立即返回准备房间；RunManager 的 restartDelay 作为超时兜底自动返回。
/// 数据后端：RunTracker（击杀/时长）+ RunManager.FloorNumber（楼层唯一真源）。
/// 挂载模式同 PausePanel：地牢场景空对象 DeathSystem；UI 运行时代码构建。
/// 【美术资产缺失】结算面板当前为纯色块+内置字体占位；待补：面板背景框、标题字效、
/// 数据图标（楼层/骷髅/沙漏）、死亡灰度滤镜（现仅角色变灰）。
/// </summary>
public class DeathPanel : MonoBehaviour
{
    public static DeathPanel Instance { get; private set; }

    [Tooltip("返回的准备场景名（与 RunManager.prepSceneName 一致，需在 Build Settings 中）")]
    [SerializeField] private string prepSceneName = "v0_7_PrepRoom";

    private GameObject panelRoot;
    private Health playerHealth;
    private PlayerInput playerInput;
    private bool subscribedDeath;
    private bool subscribedInput;
    private bool returning;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        UnsubscribeAll();
    }

    void Update()
    {
        // 玩家可能晚于本组件激活，惰性查找并订阅（PausePanel 同模式）
        if (!subscribedDeath)
        {
            if (playerHealth == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                playerHealth = p != null ? p.GetComponent<Health>() : null;
                if (playerHealth == null) return;
            }
            playerHealth.OnDeath += OnPlayerDied;
            subscribedDeath = true;
        }

        if (!subscribedInput)
        {
            if (playerInput == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                playerInput = p != null ? p.GetComponent<PlayerInput>() : null;
                if (playerInput == null) return;
            }
            playerInput.onActionTriggered += OnAction;
            subscribedInput = true;
        }
    }

    void OnDisable() => UnsubscribeAll();

    private void UnsubscribeAll()
    {
        if (playerHealth != null && subscribedDeath)
        {
            playerHealth.OnDeath -= OnPlayerDied;
            subscribedDeath = false;
        }
        if (playerInput != null && subscribedInput)
        {
            playerInput.onActionTriggered -= OnAction;
            subscribedInput = false;
        }
    }

    private void OnAction(InputAction.CallbackContext ctx)
    {
        if (ctx.action?.name == "Cancel" && ctx.performed && panelRoot != null)
            ReturnToPrep();
    }

    private void OnPlayerDied()
    {
        if (panelRoot != null) return;   // 已显示（防御重复事件）
        RunManager run = FindAnyObjectByType<RunManager>();
        int floor = run != null ? run.FloorNumber : 1;
        Show(floor, RunTracker.Kills, RunTracker.Elapsed);
    }

    // ========== 面板构建（程序员美术占位） ==========

    private void Show(int floor, int kills, float elapsed)
    {
        var canvasGo = new GameObject("DeathCanvas", typeof(Canvas));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 210;   // 低于 PausePanel(220)，高于常规 HUD
        panelRoot = canvasGo;

        // 点击任意处返回
        Image mask = new GameObject("Mask", typeof(Image), typeof(Button)).GetComponent<Image>();
        mask.transform.SetParent(canvasGo.transform, false);
        mask.color = new Color(0.1f, 0f, 0f, 0.6f);
        mask.rectTransform.anchorMin = Vector2.zero;
        mask.rectTransform.anchorMax = Vector2.one;
        mask.rectTransform.offsetMin = mask.rectTransform.offsetMax = Vector2.zero;
        mask.GetComponent<Button>().onClick.AddListener(ReturnToPrep);

        Label(canvasGo.transform, "本 局 结 算", 36, new Color(0.95f, 0.35f, 0.3f), new Vector2(0.5f, 0.72f), new Vector2(500f, 50f));

        int minutes = Mathf.FloorToInt(elapsed / 60f);
        int seconds = Mathf.FloorToInt(elapsed % 60f);
        Label(canvasGo.transform, $"抵达楼层  {floor}", 24, Color.white, new Vector2(0.5f, 0.56f), new Vector2(500f, 34f));
        Label(canvasGo.transform, $"击杀敌人  {kills}", 24, Color.white, new Vector2(0.5f, 0.46f), new Vector2(500f, 34f));
        Label(canvasGo.transform, $"存活时长  {minutes:00}:{seconds:00}", 24, Color.white, new Vector2(0.5f, 0.36f), new Vector2(500f, 34f));

        Label(canvasGo.transform, "Esc 或点击任意处 返回准备房间", 16, new Color(0.8f, 0.78f, 0.7f), new Vector2(0.5f, 0.18f), new Vector2(600f, 26f));

        Debug.Log($"[Death] 本局结算：楼层 {floor} / 击杀 {kills} / 存活 {minutes:00}:{seconds:00}");
    }

    /// <summary>返回准备房间。RunManager 的延迟重开仍在跑——本场景卸载会终止其协程，不会二次加载。</summary>
    private void ReturnToPrep()
    {
        if (returning) return;
        returning = true;
        RunStateCarrier.Ensure().ClearWeapon();
        ClassSelectUI.Close();
        Debug.Log("[Death] 返回准备房间");
        SceneManager.LoadScene(prepSceneName);
    }

    private static void Label(Transform parent, string text, int size, Color color, Vector2 anchor, Vector2 sizeDelta)
    {
        // v1.0.8：照 ClassSelectUI.CreateText 已验证模式——无参 GO + 单次 AddComponent + 先 text 后 font
        //（组件进 GameObject 构造参数会产生双 TMP 组件并触发 TMP 内部 NRE）
        GameObject go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.font = TMPFontProvider.Font;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        t.rectTransform.anchorMin = t.rectTransform.anchorMax = anchor;
        t.rectTransform.anchoredPosition = Vector2.zero;
        t.rectTransform.sizeDelta = sizeDelta;
    }
}
