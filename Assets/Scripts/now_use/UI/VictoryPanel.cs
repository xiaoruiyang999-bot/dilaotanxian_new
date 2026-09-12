using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// v2.0.8 通关结算面板（V2 §14：击败 Boss 完成本区域——结算全部随身星蓝币并返回守灯厅）。
/// 石板面板：胜利标题 / 楼层 / 击杀 / 时长 / 星蓝币 100% 封存。数据来自 RunTracker 与
/// PlayerStats（面板只读）；星蓝币结算在 RunManager.CompleteRun 调用本面板前完成。
/// Esc/点击任意处返回守灯厅（单次，防重复触发）。
/// </summary>
public class VictoryPanel : MonoBehaviour
{
    [SerializeField] private string prepSceneName = "v0_7_PrepRoom";
    private GameObject panelRoot;
    private bool returning;

    public static void Show(int floor, int kills, float elapsed, int runCoins, int banked)
    {
        var host = new GameObject("VictoryPanelHost");
        var panel = host.AddComponent<VictoryPanel>();
        panel.Build(floor, kills, elapsed, runCoins, banked);
    }

    private void Build(int floor, int kills, float elapsed, int runCoins, int banked)
    {
        var canvasGo = new GameObject("VictoryCanvas", typeof(Canvas));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 215;   // 与死亡面板同级
        PanelSprite.ConfigureCanvasScaler(canvasGo);
        canvasGo.AddComponent<GraphicRaycaster>();
        panelRoot = canvasGo;

        Image mask = new GameObject("Mask", typeof(Image), typeof(Button)).GetComponent<Image>();
        mask.transform.SetParent(canvasGo.transform, false);
        mask.color = new Color(0f, 0.05f, 0f, 0.6f);
        mask.rectTransform.anchorMin = Vector2.zero;
        mask.rectTransform.anchorMax = Vector2.one;
        mask.rectTransform.offsetMin = mask.rectTransform.offsetMax = Vector2.zero;
        mask.GetComponent<Button>().onClick.AddListener(ReturnToPrep);

        var panel = new GameObject("StonePanel", typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        Image img = panel.GetComponent<Image>();
        PanelSprite.ApplyStonePanel(img, new Color(0.05f, 0.07f, 0.05f, 0.96f));
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        img.rectTransform.sizeDelta = new Vector2(820f, 505f);

        Label(panel.transform, "探 索 完 成", 36, new Color(0.55f, 0.95f, 0.6f), new Vector2(0.5f, 0.84f), new Vector2(500f, 50f));

        int minutes = Mathf.FloorToInt(elapsed / 60f);
        int seconds = Mathf.FloorToInt(elapsed % 60f);
        Label(panel.transform, $"[徽记]   抵达楼层  {floor}", 24, Color.white, new Vector2(0.5f, 0.60f), new Vector2(600f, 38f));
        Label(panel.transform, $"[剑]   击杀敌人  {kills}", 24, Color.white, new Vector2(0.5f, 0.48f), new Vector2(600f, 38f));
        Label(panel.transform, $"[沙漏]   探索时长  {minutes:00}:{seconds:00}", 24, Color.white, new Vector2(0.5f, 0.36f), new Vector2(600f, 38f));
        Label(panel.transform, $"[星蓝币]   随身 {runCoins}  →  封存 +{banked}（100% 带回守灯厅）", 22,
            new Color(0.55f, 0.75f, 1f), new Vector2(0.5f, 0.25f), new Vector2(640f, 34f));

        Label(panel.transform, "Esc 或点击任意处 返回守灯厅", 16, new Color(0.8f, 0.78f, 0.7f), new Vector2(0.5f, 0.14f), new Vector2(600f, 26f));

        Debug.Log($"[Victory] 通关结算：楼层 {floor} / 击杀 {kills} / 时长 {minutes:00}:{seconds:00} / 星蓝币封存 +{banked}");
    }

    private void Update()
    {
        if (InputSystemEscPressed()) ReturnToPrep();
    }

    private static bool InputSystemEscPressed()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
    }

    private void ReturnToPrep()
    {
        if (returning) return;
        returning = true;
        NarrativeArchiveUI.Close();
        NarrativePanelUI.Close();
        RunStateCarrier.Ensure().ResetWeaponToCharacterDefault();
        Debug.Log("[Victory] 返回守灯厅");
        SceneManager.LoadScene(prepSceneName);
    }

    private static void Label(Transform parent, string text, int size, Color color, Vector2 anchor, Vector2 sizeDelta)
    {
        var go = new GameObject($"Label_{text.Substring(0, Mathf.Min(4, text.Length))}");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.font = TMPFontProvider.Font;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        t.rectTransform.anchorMin = t.rectTransform.anchorMax = anchor;
        t.rectTransform.sizeDelta = sizeDelta;
    }
}
