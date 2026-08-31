using DG.Tweening;
using UnityEngine;

/// <summary>
/// 法力球（v0.6.3 掉落闭环）：击杀掉落的小法力球，walk-over 吸附——
/// 不占 E 键、不实现 IPickupable、不进拾取列表。玩家走入触发半径即回复法力，
/// 随后收缩吸附到玩家身上销毁。数值来源 EnemyStats.manaOrbValue（普通 3 / 精英 8 / Boss 20）。
/// 视觉运行时构建：蓝色小圆（#3498DB，直径≈0.25）+ 白色高光小点（#ECF0F1），无需 prefab。
/// </summary>
public class ManaOrb : MonoBehaviour
{
    private static readonly Color OrbColor = new Color(0.204f, 0.596f, 0.859f);      // #3498DB 法力蓝
    private static readonly Color HighlightColor = new Color(0.925f, 0.941f, 0.945f); // #ECF0F1 白

    private static Sprite circleSprite;   // 局部缓存的圆点 sprite（首次使用时生成）

    private float amount;
    private bool collected;   // 防重复触发标记

    /// <summary>在指定位置掉落一颗法力球（amount 为回复量）。</summary>
    public static ManaOrb Spawn(Vector3 pos, float amount)
    {
        GameObject go = new GameObject("ManaOrb");
        go.transform.position = pos;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetCircleSprite();
        sr.color = OrbColor;
        sr.sortingOrder = 1;

        // 白色高光小点（直径≈0.08，右上偏移）
        GameObject highlight = new GameObject("Highlight");
        highlight.transform.SetParent(go.transform, false);
        highlight.transform.localPosition = new Vector3(0.05f, 0.06f, 0f);
        highlight.transform.localScale = Vector3.one * 0.32f;
        SpriteRenderer hsr = highlight.AddComponent<SpriteRenderer>();
        hsr.sprite = GetCircleSprite();
        hsr.color = HighlightColor;
        hsr.sortingOrder = 2;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.4f;

        ManaOrb orb = go.AddComponent<ManaOrb>();
        orb.amount = amount;

        // 轻微上下浮动（Y ±0.05 循环）
        go.transform.DOMoveY(pos.y + 0.05f, 0.6f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetLink(go);
        return orb;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;
        // 玩家判定：Tag 优先，PlayerStats 兜底
        if (!other.CompareTag("Player") && other.GetComponent<PlayerStats>() == null) return;

        collected = true;
        PlayerStats stats = other.GetComponent<PlayerStats>();
        if (stats != null) stats.AddMana(amount);

        // 吸附表现：飞向玩家 + 收缩到 0，随后销毁
        if (TryGetComponent<Collider2D>(out var col)) col.enabled = false;
        transform.DOMove(other.transform.position, 0.15f).SetLink(gameObject);
        transform.DOScale(Vector3.zero, 0.15f).SetLink(gameObject)
            .OnComplete(() => Destroy(gameObject));
    }

    /// <summary>生成/返回缓存的圆点 sprite（64px 圆，PPU=256 → 直径 0.25 单位）。</summary>
    private static Sprite GetCircleSprite()
    {
        if (circleSprite != null) return circleSprite;

        const int size = 64;
        const float radius = size * 0.5f - 1f;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(1f, 1f, 1f, 0f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                tex.SetPixel(x, y, dist <= radius ? Color.white : clear);
            }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;

        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), 256f);
        return circleSprite;
    }
}
