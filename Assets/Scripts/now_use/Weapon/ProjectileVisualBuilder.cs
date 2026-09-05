using DG.Tweening;
using UnityEngine;

/// <summary>
/// 子弹视觉构建器（v0.6.3，美术清单 §三）：运行时程序化多色块拼接，无 prefab。
/// - GetWhiteSprite/GetCircleSprite 全局缓存（PrepPedestal 同款白图做法；圆形为运行时生成 Texture2D→Sprite）；
/// - BuildVisual 按 visualKind 拼色块，部件沿 +X 为飞行方向（Projectile.Launch 根旋转对齐）；
/// - SpawnHitEffect：3~5 个小方块 DOTween 飞散+淡出销毁（通用一份代码，按子弹色染色）。
/// </summary>
public static class ProjectileVisualBuilder
{
    // 调色板（美术清单 1.2）
    private static readonly Color brown = new Color(0.553f, 0.431f, 0.388f);       // #8D6E63 箭杆
    private static readonly Color silver = new Color(0.741f, 0.765f, 0.780f);      // #BDC3C7 箭头
    private static readonly Color darkGray = new Color(0.498f, 0.549f, 0.553f);    // #7F8C8D 弩杆
    private static readonly Color archerGreen = new Color(0.153f, 0.682f, 0.376f); // #27AE60 箭羽
    private static readonly Color manaBlue = new Color(0.204f, 0.596f, 0.859f);    // #3498DB 能量弹
    private static readonly Color manaLight = new Color(0.522f, 0.757f, 0.914f);   // #85C1E9 内芯
    private static readonly Color gold = new Color(0.945f, 0.769f, 0.059f);        // #F1C40F 精灵弹高光

    private static Sprite whiteSprite;
    private static Sprite circleSprite;

    /// <summary>全局缓存的 1×1 白图方块 Sprite（Texture2D.whiteTexture）。</summary>
    public static Sprite GetWhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), tex.width);   // 1×1 单位方块
            whiteSprite.name = "RT_ProjWhite";   // v1.1.40：运行时精灵必须命名
        }
        return whiteSprite;
    }

    /// <summary>
    /// 全局缓存的 1×1 圆形 Sprite：运行时生成 32×32 Texture2D，按半径填 alpha，Apply 后 Sprite.Create。
    /// </summary>
    public static Sprite GetCircleSprite()
    {
        if (circleSprite == null)
        {
            const int size = 32;
            const float center = (size - 1) * 0.5f;
            const float radius = size * 0.5f - 0.5f;   // 留 1px 边缘做柔边
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01(radius + 0.5f - dist);   // 边缘 1px 过渡
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size);   // 1×1 单位圆
            circleSprite.name = "RT_ProjCircle";   // v1.1.40：运行时精灵必须命名
        }
        return circleSprite;
    }

    /// <summary>
    /// 构建子弹视觉（未设父级，调用方挂 Projectile 根下）。
    /// 部件沿 +X 为飞行方向；根不带旋转（由 Projectile 根负责朝向）。
    /// </summary>
    public static GameObject BuildVisual(ProjectileData data)
    {
        if (data == null) return null;

        GameObject root = new GameObject("ProjectileVisual");
        switch (data.VisualKind)
        {
            case ProjectileVisualKind.Arrow: BuildArrow(root.transform); break;
            case ProjectileVisualKind.Bolt: BuildBolt(root.transform); break;
            case ProjectileVisualKind.EnergyOrb: BuildEnergyOrb(root.transform); break;
            case ProjectileVisualKind.SpiritOrb: BuildSpiritOrb(root.transform); break;
        }
        return root;
    }

    /// <summary>箭矢：棕杆（细长方块）+ 银灰三角头（小方块旋转 45° 示意）+ 绿色箭羽两片。</summary>
    private static void BuildArrow(Transform root)
    {
        CreatePart(root, "Shaft", new Vector2(0.36f, 0.05f), new Vector3(-0.02f, 0f), brown);
        CreatePart(root, "Head", new Vector2(0.10f, 0.10f), new Vector3(0.20f, 0f), silver,
            rotation: 45f, sortingOrder: 6);
        CreatePart(root, "FeatherTop", new Vector2(0.10f, 0.05f), new Vector3(-0.20f, 0.05f), archerGreen,
            rotation: 30f, sortingOrder: 6);
        CreatePart(root, "FeatherBottom", new Vector2(0.10f, 0.05f), new Vector3(-0.20f, -0.05f), archerGreen,
            rotation: -30f, sortingOrder: 6);
    }

    /// <summary>弩矢：短粗深灰杆 + 小银头（与箭矢区分：短粗、无箭羽）。</summary>
    private static void BuildBolt(Transform root)
    {
        CreatePart(root, "Shaft", new Vector2(0.22f, 0.08f), new Vector3(-0.03f, 0f), darkGray);
        CreatePart(root, "Head", new Vector2(0.08f, 0.08f), new Vector3(0.11f, 0f), silver,
            rotation: 45f, sortingOrder: 6);
    }

    /// <summary>能量弹：蓝圆 + 浅蓝内芯小圆 + 白高光点（圆形发光）。</summary>
    private static void BuildEnergyOrb(Transform root)
    {
        CreatePart(root, "Body", new Vector2(0.24f, 0.24f), Vector3.zero, manaBlue, circle: true);
        CreatePart(root, "Core", new Vector2(0.13f, 0.13f), Vector3.zero, manaLight,
            circle: true, sortingOrder: 6);
        CreatePart(root, "Highlight", new Vector2(0.05f, 0.05f), new Vector3(-0.05f, 0.06f), Color.white,
            circle: true, sortingOrder: 7);
    }

    /// <summary>精灵弹：更小蓝圆 + 金高光（v0.6.5 宠物预留）。</summary>
    private static void BuildSpiritOrb(Transform root)
    {
        CreatePart(root, "Body", new Vector2(0.16f, 0.16f), Vector3.zero, manaBlue, circle: true);
        CreatePart(root, "Highlight", new Vector2(0.06f, 0.06f), new Vector3(-0.03f, 0.04f), gold,
            circle: true, sortingOrder: 6);
    }

    /// <summary>
    /// 通用命中特效（美术清单 §三/§八）：3~5 个 0.08 白色方块染 color，
    /// 随机方向 DOTween 飞散 0.25s + 淡出 0.3s 后销毁，全部 SetLink。
    /// </summary>
    public static void SpawnHitEffect(Vector2 pos, Color color)
    {
        int count = Random.Range(3, 6);   // 3~5
        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject("HitChip");
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.08f;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetWhiteSprite();
            sr.color = color;
            sr.sortingOrder = 5;

            Vector2 dir = Random.insideUnitCircle.normalized;
            go.transform.DOMove(pos + dir * 0.3f, 0.25f).SetEase(Ease.OutQuad).SetLink(go);
            sr.DOFade(0f, 0.3f).SetEase(Ease.InQuad)
                .OnComplete(() => { if (go != null) Object.Destroy(go); })
                .SetLink(go);
        }
    }

    /// <summary>创建一个染色方块/圆形部件（独立 SpriteRenderer，便于染色与分层）。</summary>
    private static SpriteRenderer CreatePart(Transform parent, string name, Vector2 size, Vector3 localPos,
        Color color, float rotation = 0f, bool circle = false, int sortingOrder = 5)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = circle ? GetCircleSprite() : GetWhiteSprite();
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        return sr;
    }
}
