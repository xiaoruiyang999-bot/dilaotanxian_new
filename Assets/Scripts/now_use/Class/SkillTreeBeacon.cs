using DG.Tweening;
using UnityEngine;

/// <summary>
/// 准备室技能石碑（v1.1.47）：E 交互打开 SkillTreeUI（局外技能树）。
/// 视觉运行时构建（PrepPortal 模式）：石色碑体两叠块 + 支线三色符文（DOTween 呼吸脉动，SetLink）；
/// 摆放在传送门旁，出生即可见——"进本前先看一眼加点"的自然动线。
/// 可重复交互（覆盖 Interact，不走一次性消耗）。
/// </summary>
public class SkillTreeBeacon : Interactable
{
    private static readonly Color stoneGray = new Color(0.5f, 0.55f, 0.55f);
    private static readonly Color stoneDark = new Color(0.32f, 0.36f, 0.36f);
    private static readonly Color runeRed = new Color(0.90f, 0.35f, 0.30f);
    private static readonly Color runeGreen = new Color(0.35f, 0.80f, 0.45f);
    private static readonly Color runeBlue = new Color(0.35f, 0.60f, 0.90f);

    private static Sprite whiteSprite;

    /// <summary>运行时构建石碑（PrepRoomManager 调用）。</summary>
    public static SkillTreeBeacon Create(Vector3 position, Transform parent)
    {
        GameObject go = new GameObject("SkillTreeBeacon");
        go.transform.position = position;
        go.transform.SetParent(parent, true);

        // 先显式加碰撞体：基类 RequireComponent 抽象类型，后加会在 AddComponent 时自动补件抛 NRE（PrepPortal 同款注释）
        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.9f;

        SkillTreeBeacon beacon = go.AddComponent<SkillTreeBeacon>();
        beacon.BuildVisual();
        return beacon;
    }

    /// <summary>覆盖基类：可重复交互，不消耗——每次 E 都打开技能树。</summary>
    public override void Interact(Collider2D player)
    {
        if (SkillTreeUI.IsOpen) SkillTreeUI.Close();
        else SkillTreeUI.Open();
    }

    protected override void ApplyEffect(Collider2D player) { }   // 不走一次性消耗流程

    private void BuildVisual()
    {
        // 碑体：底座宽矮 + 主碑窄高（两层石块，与传送门石框同语言）
        CreateBlock("Base", new Vector2(1.5f, 0.35f), new Vector3(0f, 0.18f, 0f), stoneDark);
        CreateBlock("Stele", new Vector2(0.9f, 1.7f), new Vector3(0f, 1.2f, 0f), stoneGray);

        // 三支线符文：竖排三点（赤/翠/靛），呼吸脉动
        Color[] runes = { runeRed, runeGreen, runeBlue };
        for (int i = 0; i < runes.Length; i++)
        {
            SpriteRenderer r = CreateBlock($"Rune{i}", new Vector2(0.26f, 0.26f),
                new Vector3(0f, 0.95f + i * 0.5f, 0f), runes[i]);
            r.sortingOrder = 1;
            float delay = i * 0.35f;
            r.transform
                .DOScale(1.35f, 1.1f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                .SetDelay(delay)
                .SetLink(r.gameObject);
        }
    }

    private SpriteRenderer CreateBlock(string name, Vector2 size, Vector3 localPos, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetWhiteSprite();
        sr.color = color;
        return sr;
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), tex.width);
            whiteSprite.name = "RT_whiteSprite";   // v1.1.40：运行时精灵必须命名
        }
        return whiteSprite;
    }
}
