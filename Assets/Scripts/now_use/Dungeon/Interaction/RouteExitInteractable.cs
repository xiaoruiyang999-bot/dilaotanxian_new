using UnityEngine;

/// <summary>当前房间右侧的路线出口。节点是否可离开始终由 ActiveRun 判定。</summary>
public sealed class RouteExitInteractable : Interactable
{
    private DungeonManager dungeon;
    private static Sprite markerSprite;

    public static RouteExitInteractable Create(Transform parent, Vector3 position, DungeonManager owner)
    {
        var go = new GameObject("RouteExit");
        go.transform.SetParent(parent, true);
        go.transform.position = position;
        var trigger = go.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(1.8f, 2.8f);
        var sprite = go.AddComponent<SpriteRenderer>();
        if (markerSprite == null)
        {
            Texture2D texture = Texture2D.whiteTexture;
            markerSprite = Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), texture.width);
            markerSprite.name = "RT_RouteExitMarker";
        }
        sprite.sprite = markerSprite;
        sprite.color = new Color(0.18f, 0.55f, 0.9f, 0.7f);
        sprite.sortingOrder = 2;
        sprite.transform.localScale = new Vector3(1.2f, 2f, 1f);
        var exit = go.AddComponent<RouteExitInteractable>();
        exit.dungeon = owner;
        return exit;
    }

    public override void Interact(Collider2D player)
    {
        dungeon?.OpenRouteChoice();
    }

    protected override void ApplyEffect(Collider2D player) { }
}
