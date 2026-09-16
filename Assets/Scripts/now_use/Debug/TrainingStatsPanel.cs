using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// v2.0.11 训练房统计面板（任务计划 §4）：
/// 伤害追踪（EnemyHealth.OnTakeDamage 订阅/环形缓冲 64 非分配）、DPS 3 秒窗口、
/// 总伤害、击杀用时、存活怪物 HP 列表、玩家状态。F6 开关，0.2 秒节流刷新。
/// 石板面板视觉（PanelSprite），TMP 文本（TMPFontProvider.Font）。
/// </summary>
public class TrainingStatsPanel : MonoBehaviour
{
    private TrainingRoomManager manager;

    // 环形缓冲（§4.2：容量 64 非分配）
    private readonly float[] dmgBuf = new float[64];
    private readonly float[] timeBuf = new float[64];
    private int bufHead;
    private float totalDamage;

    // 存活追踪（刷出时注册，死亡自动移除）
    private readonly List<TrackedEnemy> tracked = new();
    private class TrackedEnemy
    {
        public EnemyHealth Health;
        public string Label;
        public float SpawnTime;
        public float DeathTime = -1f;
    }

    private float lastKillDuration = -1f;

    // UI
    private GameObject canvasGo;
    private TMP_Text dpsText, totalText, killText, enemiesText, playerText;
    private float refreshTimer;

    public void Initialize(TrainingRoomManager owner)
    {
        manager = owner;
    }

    public void Toggle()
    {
        if (canvasGo == null) Build();
        canvasGo.SetActive(!canvasGo.activeSelf);
    }

    void Update()
    {
        if (canvasGo == null || !canvasGo.activeSelf) return;
        refreshTimer -= Time.deltaTime;
        if (refreshTimer > 0f) return;
        refreshTimer = 0.2f;   // §4.3 节流
        RefreshTexts();
    }

    // ---------- 追踪（§4.2） ----------

    public void Track(EnemyHealth health, string label)
    {
        if (health == null) return;
        var t = new TrackedEnemy { Health = health, Label = label, SpawnTime = Time.time };
        tracked.Add(t);

        // 伤害追踪：OnHealthChanged 携带 (current,max)——delta 即实际扣血量
        float lastHp = health.CurrentHealth;
        health.OnHealthChanged += (cur, max) =>
        {
            float delta = lastHp - cur;
            if (delta > 0f) RecordDamage(delta);
            lastHp = cur;
        };
        health.OnDeath += () =>
        {
            t.DeathTime = Time.time;
            lastKillDuration = t.DeathTime - t.SpawnTime;
        };
    }

    /// <summary>伤害写入环形缓冲 + 总伤累计。</summary>
    private void RecordDamage(float amount)
    {
        dmgBuf[bufHead] = amount;
        timeBuf[bufHead] = Time.time;
        bufHead = (bufHead + 1) % dmgBuf.Length;
        totalDamage += amount;
    }

    /// <summary>3 秒窗口 DPS（§4.1）。</summary>
    private float CurrentDps()
    {
        float now = Time.time;
        float sum = 0f;
        for (int i = 0; i < dmgBuf.Length; i++)
        {
            if (timeBuf[i] > 0f && now - timeBuf[i] <= 3f)
                sum += dmgBuf[i];
        }
        return sum / 3f;
    }

    // ---------- UI（§4.3） ----------

    private void Build()
    {
        canvasGo = new GameObject("TrainingStatsCanvas", typeof(Canvas));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        PanelSprite.ConfigureCanvasScaler(canvasGo);
        canvasGo.SetActive(false);

        var panel = new GameObject("StonePanel", typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        Image img = panel.GetComponent<Image>();
        PanelSprite.ApplyStonePanel(img, new Color(0.05f, 0.05f, 0.06f, 0.92f));
        img.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        img.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        img.rectTransform.anchoredPosition = new Vector2(230f, 0f);
        img.rectTransform.sizeDelta = new Vector2(420f, 560f);

        dpsText = CreateText(panel.transform, "DPS", "DPS: 0", 30, TextAlignmentOptions.Left, new Color(1f, 0.82f, 0.35f));
        dpsText.rectTransform.anchoredPosition = new Vector2(20f, 245f);
        totalText = CreateText(panel.transform, "Total", "总伤害: 0", 20, TextAlignmentOptions.Left, Color.white);
        totalText.rectTransform.anchoredPosition = new Vector2(20f, 200f);
        killText = CreateText(panel.transform, "KillTime", "最近击杀: --", 20, TextAlignmentOptions.Left, Color.white);
        killText.rectTransform.anchoredPosition = new Vector2(20f, 165f);
        enemiesText = CreateText(panel.transform, "Enemies", "存活怪物:", 18, TextAlignmentOptions.Left, new Color(0.8f, 0.8f, 0.8f));
        enemiesText.rectTransform.anchoredPosition = new Vector2(20f, 100f);
        enemiesText.rectTransform.sizeDelta = new Vector2(380f, 180f);
        playerText = CreateText(panel.transform, "Player", "", 18, TextAlignmentOptions.Left, new Color(0.6f, 0.8f, 1f));
        playerText.rectTransform.anchoredPosition = new Vector2(20f, -240f);

        var hint = CreateText(panel.transform, "Hint", "F6 关闭 · 1~7 刷怪 · B Boss · K 清场 · I 无敌 · H 回满",
            13, TextAlignmentOptions.Left, new Color(0.7f, 0.68f, 0.62f));
        hint.rectTransform.anchoredPosition = new Vector2(20f, -265f);
        hint.rectTransform.sizeDelta = new Vector2(380f, 20f);
    }

    private void RefreshTexts()
    {
        if (dpsText == null) return;
        dpsText.text = $"DPS: {CurrentDps():F0}";
        totalText.text = $"总伤害: {totalDamage:F0}";
        killText.text = lastKillDuration >= 0f ? $"最近击杀用时: {lastKillDuration:F1}s" : "最近击杀: --";

        // 存活怪物列表（§4.1）
        tracked.RemoveAll(t => t.Health == null);
        var sb = new System.Text.StringBuilder("存活怪物:\n");
        int alive = 0;
        foreach (var t in tracked)
        {
            if (t.Health.IsDead) continue;
            alive++;
            if (alive <= 8)
                sb.AppendLine($"  {t.Label}  {t.Health.CurrentHealth:F0}/{t.Health.MaxHealth:F0}");
        }
        if (alive == 0) sb.AppendLine("  （无）");
        else if (alive > 8) sb.AppendLine($"  …共 {alive} 只");
        enemiesText.text = sb.ToString();

        // 玩家状态（§4.1）
        if (manager != null && manager.Stats != null)
        {
            string rage = manager.Rage != null ? $"{manager.Rage.Current:F0}" : "--";
            playerText.text = $"玩家: HP {manager.PlayerHealth?.CurrentHealth:F0}/{manager.Stats.MaxHP:F0}" +
                $"  甲 {manager.Stats.CurrentArmor:F0}/{manager.Stats.MaxArmor:F0}" +
                $"  怒痕 {rage}  币 {manager.Stats.Coins}";
        }
    }

    private TMP_Text CreateText(Transform parent, string name, string content, int fontSize,
        TextAlignmentOptions alignment, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.font = TMPFontProvider.Font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.rectTransform.sizeDelta = new Vector2(380f, 30f);
        return text;
    }
}
