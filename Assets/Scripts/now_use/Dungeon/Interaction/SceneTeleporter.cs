using DG.Tweening;
using UnityEngine;

/// <summary>
/// v2.0.7 同场景双向传送点（商店支线平台用）：E 交互把玩家瞬移到目标点并 Snap 相机。
/// 视觉复用传送门语言（石框 + 旋涡缓转，PrepPortal 同风格）。成对放置：
/// 主链战斗房北门 ↔ 商店平台南门，互为 destination。
/// </summary>
public class SceneTeleporter : Interactable
{
    private static readonly Color stoneGray = new Color(0.5f, 0.55f, 0.55f);
    private static readonly Color vortexBlue = new Color(0.35f, 0.6f, 0.95f);

    [SerializeField] private Vector3 destination;
    [SerializeField] private string label = "传送门";

    /// <summary>运行时构建传送点（DungeonBuilder 商店配对生成用）。</summary>
    public static SceneTeleporter Create(Transform parent, Vector3 position, Vector3 dest, string nameLabel)
    {
        GameObject go = new GameObject($"Teleporter_{nameLabel}");
        go.transform.SetParent(parent, true);
        go.transform.position = position;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        SceneTeleporter tp = go.AddComponent<SceneTeleporter>();
        col.isTrigger = true;
        col.radius = 0.8f;
        tp.destination = dest;
        tp.label = nameLabel;
        tp.BuildVisual();
        return tp;
    }

    /// <summary>E 交互：瞬移玩家到目标点 + 相机 Snap（同场景传送，不换场景）。</summary>
    public override void Interact(Collider2D player)
    {
        if (player == null) return;
        player.transform.position = destination;
        if (Camera.main != null && Camera.main.TryGetComponent(out CameraFollow cam))
            cam.SnapToTarget();
        Debug.Log($"[Teleport] {label}：玩家传送至 {destination}");
    }

    protected override void ApplyEffect(Collider2D player) { }   // 不走一次性消耗

    private void BuildVisual()
    {
        CreateBlock("PillarL", new Vector2(0.25f, 1.2f), new Vector3(-0.5f, 0.6f, 0f), stoneGray);
        CreateBlock("PillarR", new Vector2(0.25f, 1.2f), new Vector3(0.5f, 0.6f, 0f), stoneGray);
        CreateBlock("Lintel", new Vector2(1.25f, 0.22f), new Vector3(0f, 1.15f, 0f), stoneGray);

        SpriteRenderer vortex = CreateBlock("Vortex", new Vector2(0.7f, 0.7f), new Vector3(0f, 0.6f, 0f), vortexBlue);
        vortex.transform
            .DORotate(new Vector3(0f, 0f, -360f), 2.5f, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart)
            .SetLink(vortex.gameObject);
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
            whiteSprite.name = "RT_whiteSprite";   // v1.1.40：运行时精灵必须命名
        }
        return whiteSprite;
    }
}
