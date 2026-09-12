using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 死亡结算面板（v1.0.5）：玩家死亡瞬间弹出本局结算——抵达楼层 / 击杀数 / 存活时长，
/// Esc 或点击任意处立即返回准备房间；RunManager 的 restartDelay 作为超时兜底自动返回。
/// 数据后端：RunTracker（击杀/时长）+ RunManager.FloorNumber（楼层唯一真源）。
/// 挂载模式同 PausePanel：地牢场景空对象 DeathSystem；UI 运行时代码构建。
/// v1.1.9 已换为石板结算面板；楼层/击杀/时间图标暂用文字挂点占位。
/// v1.2.0：Label 构建迁移至 UIHelper 消除重复代码。
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
        PanelSprite.ConfigureCanvasScaler(canvasGo);
        canvasGo.AddComponent<GraphicRaycaster>();   // v1.1.12：无它"点击任意处返回"Mask 按钮收不到指针
        panelRoot = canvasGo;

        // 点击任意处返回
        Image mask = UIHelper.CreateFullscreenMask(canvasGo.transform, new Color(0.1f, 0f, 0f, 0.6f));
        mask.gameObject.AddComponent<Button>().onClick.AddListener(ReturnToPrep);

        Image panel = UIHelper.CreateStonePanel(canvasGo.transform, new Vector2(820f, 505f), new Color(0.08f, 0.04f, 0.04f, 0.96f));

        UIHelper.CreateLabel(panel.transform, "本 局 结 算", 36, new Color(0.95f, 0.35f, 0.3f), new Vector2(0.5f, 0.84f), new Vector2(500f, 50f));

        int minutes = Mathf.FloorToInt(elapsed / 60f);
        int seconds = Mathf.FloorToInt(elapsed % 60f);
        UIHelper.CreateLabel(panel.transform, $"[楼层图标待补]   抵达楼层  {floor}", 24, Color.white, new Vector2(0.5f, 0.60f), new Vector2(600f, 38f));
        UIHelper.CreateLabel(panel.transform, $"[击杀图标待补]   击杀敌人  {kills}", 24, Color.white, new Vector2(0.5f, 0.48f), new Vector2(600f, 38f));
        UIHelper.CreateLabel(panel.transform, $"[时间图标待补]   存活时长  {minutes:00}:{seconds:00}", 24, Color.white, new Vector2(0.5f, 0.36f), new Vector2(600f, 38f));

        // v1.1.47 魂晶结算入账（技能树货币）：每击杀 1 枚，Show 仅一次（panelRoot 判重）不会重复发放
        SkillTreeSave.AddEssence(kills);
        UIHelper.CreateLabel(panel.transform, $"[魂晶]   获得魂晶  +{kills}（已入账，可在准备房间技能石碑使用）", 24,
            new Color(1f, 0.82f, 0.35f), new Vector2(0.5f, 0.25f), new Vector2(640f, 34f));

        // v2.0.6 星蓝币死亡结算（V2 §13.1 测试值）：随身 30% 封存守灯厅，剩余清零
        PlayerStats stats = FindAnyObjectByType<PlayerStats>();
        int runCoins = stats != null ? stats.Coins : 0;
        int kept = StarCoinBank.BankOnDeath(runCoins);
        if (stats != null) stats.ResetCoins();
        UIHelper.CreateLabel(panel.transform,
            $"[星蓝币]   随身 {runCoins}  →  封存 +{kept}（30% 带回守灯厅）", 22,
            new Color(0.55f, 0.75f, 1f), new Vector2(0.5f, 0.18f), new Vector2(640f, 32f));

        UIHelper.CreateLabel(panel.transform, "Esc 或点击任意处 返回准备房间", 16, new Color(0.8f, 0.78f, 0.7f), new Vector2(0.5f, 0.10f), new Vector2(600f, 26f));

        Debug.Log($"[Death] 本局结算：楼层 {floor} / 击杀 {kills} / 存活 {minutes:00}:{seconds:00} / 魂晶 +{kills} / 星蓝币封存 +{kept}");
    }

    /// <summary>返回准备房间。RunManager 的延迟重开仍在跑——本场景卸载会终止其协程，不会二次加载。</summary>
    private void ReturnToPrep()
    {
        if (returning) return;
        returning = true;
        RunStateCarrier.Ensure().ResetWeaponToCharacterDefault();
        CharacterSelectUI.Close();
        Debug.Log("[Death] 返回准备房间");
        SceneManager.LoadScene(prepSceneName);
    }
}
