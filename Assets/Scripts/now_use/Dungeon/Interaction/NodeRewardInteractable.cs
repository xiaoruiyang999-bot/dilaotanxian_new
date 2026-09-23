using UnityEngine;

/// <summary>
/// v2.1.2 临时奖励重开入口：贤者节点显示石碑，物资节点显示宝箱。
/// Esc 关闭三选一后本物件保持可交互；只有确认奖励后才进入已消耗态。
/// 正式美术与专属奖励逻辑分别由 v2.1.4/v2.1.5 替换，本组件只承载模态生命周期合同。
/// </summary>
public sealed class NodeRewardInteractable : Interactable
{
    private DungeonManager dungeon;
    private int nodeId;
    private static Sprite markerSprite;

    public static NodeRewardInteractable Create(Room room, DungeonGraphNode node, DungeonManager owner)
    {
        if (room == null || node == null || owner == null) return null;

        bool sage = node.Type == NodeType.Sage;
        var go = new GameObject(sage ? "SageRewardStone" : "SupplyRewardChest");
        go.transform.SetParent(room.ContentRoot, true);
        go.transform.position = new Vector3(room.Bounds.center.x, room.Bounds.center.y, 0f);

        var collider = go.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = sage ? new Vector2(1.1f, 1.8f) : new Vector2(1.6f, 1.1f);

        var sprite = go.AddComponent<SpriteRenderer>();
        if (markerSprite == null)
        {
            Texture2D texture = Texture2D.whiteTexture;
            markerSprite = Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), texture.width);
            markerSprite.name = "RT_NodeRewardMarker";
        }
        sprite.sprite = markerSprite;
        sprite.color = sage
            ? new Color(0.55f, 0.36f, 0.82f, 0.9f)
            : new Color(0.72f, 0.48f, 0.16f, 0.9f);
        sprite.sortingOrder = 2;
        sprite.transform.localScale = sage
            ? new Vector3(0.9f, 1.6f, 1f)
            : new Vector3(1.45f, 0.9f, 1f);

        var interactable = go.AddComponent<NodeRewardInteractable>();
        interactable.dungeon = owner;
        interactable.nodeId = node.NodeId;
        return interactable;
    }

    public override void Interact(Collider2D player)
    {
        if (consumed || dungeon == null) return;
        dungeon.OpenPendingReward(nodeId);
    }

    public void MarkResolved()
    {
        if (consumed) return;
        consumed = true;
        SetConsumedVisual();
    }

    protected override void ApplyEffect(Collider2D player) { }
}
