using UnityEngine;

/// <summary>
/// v2.0.7 守灯厅档案柜交互物（V2 §2.4）：E 打开碎片档案（NarrativeArchiveUI）。
/// 运行时构建视觉：深色立柜三叠块 + 发光书脊色条（占位素材，正式素材到位后替换）。
/// </summary>
public class ArchiveCabinet : Interactable
{
    private static readonly Color woodDark = new Color(0.22f, 0.16f, 0.12f);
    private static readonly Color woodMid = new Color(0.32f, 0.23f, 0.15f);
    private static readonly Color spineGlow = new Color(0.95f, 0.8f, 0.4f);

    public static ArchiveCabinet Create(Transform parent, Vector3 position)
    {
        GameObject go = new GameObject("ArchiveCabinet");
        go.transform.SetParent(parent, true);
        go.transform.position = position;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        ArchiveCabinet cabinet = go.AddComponent<ArchiveCabinet>();
        col.isTrigger = true;
        col.radius = 1.0f;
        cabinet.BuildVisual();
        return cabinet;
    }

    public override void Interact(Collider2D player)
    {
        if (NarrativeArchiveUI.IsOpen) NarrativeArchiveUI.Close();
        else NarrativeArchiveUI.Open();
    }

    protected override void ApplyEffect(Collider2D player) { }

    private void BuildVisual()
    {
        CreateBlock("Base", new Vector2(1.6f, 0.3f), new Vector3(0f, 0.15f, 0f), woodDark);
        CreateBlock("Body", new Vector2(1.3f, 1.9f), new Vector3(0f, 1.25f, 0f), woodMid);
        CreateBlock("Top", new Vector2(1.5f, 0.22f), new Vector3(0f, 2.3f, 0f), woodDark);

        // 三条书脊发光色条（档案内容感的占位视觉）
        for (int i = 0; i < 3; i++)
        {
            var spine = CreateBlock($"Spine{i}", new Vector2(0.14f, 1.1f),
                new Vector3(-0.35f + i * 0.35f, 1.3f, 0f), spineGlow);
            spine.sortingOrder = 1;
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

    private static Sprite whiteSprite;
    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), tex.width);
            whiteSprite.name = "RT_whiteSprite";
        }
        return whiteSprite;
    }
}
