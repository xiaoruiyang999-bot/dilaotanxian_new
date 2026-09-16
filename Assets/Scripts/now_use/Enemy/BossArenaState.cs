using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// v2.0.10 批2-1 石柱三态系统（开发文档 §七）：
/// Intact → Boss 撞击 → Cracked → Boss 撞击 → Collapsed（倒塌后开碎石区）。
/// 不复用 ObstacleHealth（玩家不该普通攻击拆柱——状态只由 Boss 撞击命令驱动）。
/// 碎石区：独立 Layer("BossRubble")——只影响 Boss 减速，不挡弹/不挡玩家。
/// Boss 重开时 ResetAll 恢复完整态。挂在 Boss 房 ContentRoot，由 Decorator 接线。
/// </summary>
public class BossArenaState : MonoBehaviour
{
    public enum PillarState { Intact, Cracked, Collapsed }

    [Serializable]
    public class Pillar
    {
        public int Id;
        public Vector2 Center;
        public PillarState State = PillarState.Intact;
        public GameObject Visual;       // Decorator 建的视觉 slot
        public BoxCollider2D Collider;  // 实体碰撞（倒塌后关闭）
        public GameObject RubbleZone;   // 倒塌后激活的碎石区
    }

    public IReadOnlyList<Pillar> Pillars => pillarList;
    private readonly List<Pillar> pillarList = new List<Pillar>();

    /// <summary>碎石带减速比（对 Boss 奔袭/四足移动;0.45 = 减速 55%）。</summary>
    public const float RubbleSlowMultiplier = 0.45f;
    /// <summary>碎石 Layer 名（独立于实体墙——弹/玩家不受阻挡）。</summary>
    public const string RubbleLayerName = "BossRubble";

    /// <summary>Decorator 建完视觉后注册柱（带碰撞体与碎石占位）。</summary>
    public Pillar RegisterPillar(int id, Vector2 center, GameObject visual, BoxCollider2D col)
    {
        var p = new Pillar { Id = id, Center = center, Visual = visual, Collider = col };
        pillarList.Add(p);
        return p;
    }

    public Pillar GetPillar(int id)
    {
        foreach (Pillar p in pillarList) if (p.Id == id) return p;
        return null;
    }

    /// <summary>最近的柱（指定状态过滤；奔袭撞击判定用）。</summary>
    public Pillar FindNearest(Vector2 pos, Predicate<PillarState> stateFilter)
    {
        Pillar best = null; float bestDist = float.MaxValue;
        foreach (Pillar p in pillarList)
        {
            if (!stateFilter(p.State)) continue;
            float d = (pos - p.Center).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = p; }
        }
        return best;
    }

    /// <summary>Boss 撞击命令（文档 §七：Intact→Cracked→Collapsed，状态只由 Boss 驱动）。</summary>
    /// <returns>撞击后的新状态（调用方据此决定失衡/眩晕时长）。</returns>
    public PillarState ApplyBossImpact(Pillar pillar)
    {
        if (pillar == null) return PillarState.Intact;
        switch (pillar.State)
        {
            case PillarState.Intact:
                pillar.State = PillarState.Collapsed == PillarState.Intact ? PillarState.Intact : PillarState.Cracked;
                ApplyCrackedVisual(pillar);
                break;
            case PillarState.Cracked:
                pillar.State = PillarState.Collapsed;
                ApplyCollapsed(pillar);
                break;
        }
        return pillar.State;
    }

    /// <summary>Boss 重开恢复（文档 §七：Boss 战重开时恢复完整状态）。</summary>
    public void ResetAll()
    {
        foreach (Pillar p in pillarList)
        {
            p.State = PillarState.Intact;
            if (p.Collider != null) p.Collider.enabled = true;
            if (p.RubbleZone != null) p.RubbleZone.SetActive(false);
            if (p.Visual != null) p.Visual.SetActive(true);
        }
    }

    private void ApplyCrackedVisual(Pillar p)
    {
        // 批2 占位：染色暗红裂纹感（正式素材到位后换 sprite）
        if (p.Visual != null)
        {
            foreach (var sr in p.Visual.GetComponentsInChildren<SpriteRenderer>())
                sr.color = new Color(0.8f, 0.6f, 0.5f);
        }
    }

    private void ApplyCollapsed(Pillar p)
    {
        if (p.Collider != null) p.Collider.enabled = false;
        if (p.Visual != null) p.Visual.SetActive(false);

        // 碎石区：触发器圆（Boss 减速查询用，不挡实体）
        if (p.RubbleZone == null)
        {
            var go = new GameObject($"RubbleZone_{p.Id}");
            go.transform.SetParent(transform, false);
            go.transform.position = p.Center;
            int layer = LayerMask.NameToLayer(RubbleLayerName);
            if (layer >= 0) go.layer = layer;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 1.8f;
            // 占位视觉
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateWhiteSprite();
            sr.color = new Color(0.4f, 0.38f, 0.35f, 0.5f);
            sr.sortingOrder = 1;
            go.transform.localScale = new Vector3(3.2f, 3.2f, 1f);
            p.RubbleZone = go;
        }
        p.RubbleZone.SetActive(true);
    }

    /// <summary>查询位置是否在碎石区内（Boss 移动减速用）。</summary>
    public bool IsInRubble(Vector2 pos)
    {
        foreach (Pillar p in pillarList)
        {
            if (p.State != PillarState.Collapsed || p.RubbleZone == null) continue;
            if ((pos - (Vector2)p.RubbleZone.transform.position).sqrMagnitude < 1.8f * 1.8f)
                return true;
        }
        return false;
    }

    private static Sprite whiteSprite;
    private static Sprite CreateWhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), tex.width);
            whiteSprite.name = "RT_BossArenaWhite";
        }
        return whiteSprite;
    }
}
