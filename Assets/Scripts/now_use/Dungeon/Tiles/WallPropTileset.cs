using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 石墙素材库（v1.1.29.1 修复碰撞体膨胀）：**归一化烘焙进 Sprite，Tile 零 transform**。
/// v1.1.28/29 初版用 tile.transform 缩放做宽归一——TilemapCollider2D 会把 tile.transform 应用到
/// 碰撞形状（Grid 类型同样受影响），缩放系数 ≈1.6~2.0 直接把每格墙碰撞放大近两倍，膨胀碰撞盒
/// 互相重叠并盖住门洞（"碰撞体与素材大小不一致、门过不去"的根因）。
///
/// 现方案：运行时 Sprite.Create 重建——PPU = 纹理宽（宽恒 = 1 格），pivot=(0.5, 0.5/h)
/// 使精灵底边恰好落在格底（tilemap 将 sprite pivot 对齐格心，pivot 上移 0.5 单位 ⇒ 底=格底）。
/// 碰撞：ColliderType.Grid + 无 transform = **严格整格 1×1**（prop_01 宽度基准，R19 平面不受影响）。
/// 竖列（仅 prop_01）高 ≈1.47 上伸进上一格，配合 TopLeft 渲染序实现"下图扣上图"堆叠（纯视觉）。
/// 断链安全：素材缺失返回 null，调用方回退白方块墙瓦。
/// </summary>
public static class WallPropTileset
{
    private const string ResourceDir = "Art/Decor/WallProps";
    private const string VerticalSpriteName = "prop_01";   // 竖向墙列专用（用户指定）

    private static Tile verticalTile;
    private static Tile[] horizontalTiles;
    private static bool loadFailed;

    /// <summary>竖向墙瓦（仅 prop_01；素材缺失返回 null）。</summary>
    public static Tile GetVertical()
    {
        if (verticalTile == null && !loadFailed) Load();
        return verticalTile;
    }

    /// <summary>横向墙瓦（非 prop_01 池随机，越界取模；池空回退 prop_01，再缺返回 null）。</summary>
    public static Tile GetHorizontal(int variant)
    {
        if (horizontalTiles == null && !loadFailed) Load();
        if (horizontalTiles != null && horizontalTiles.Length > 0)
            return horizontalTiles[Mathf.Abs(variant) % horizontalTiles.Length];
        return verticalTile;
    }

    private static void Load()
    {
        var hList = new System.Collections.Generic.List<Tile>(8);
        foreach (Sprite s in Resources.LoadAll<Sprite>(ResourceDir))
        {
            float tw = s.textureRect.width, th = s.textureRect.height;
            if (tw <= 1f || s.texture == null) continue;

            // 宽归一烘焙：PPU=纹理宽 → 世界宽恒 1；高 h=th/tw 按原比例
            float h = th / tw;
            // pivot 上移 0.5/h（以高度为分数单位）：tilemap 把 pivot 对齐格心 ⇒ 精灵底边落在格底
            Sprite normalized = Sprite.Create(
                s.texture, s.textureRect,
                new Vector2(0.5f, 0.5f / h), tw);

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = normalized;
            tile.color = Color.white;
            tile.colliderType = Tile.ColliderType.Grid;   // 零 transform ⇒ 严格整格 1×1 碰撞
            if (s.name == VerticalSpriteName) verticalTile = tile;
            else hList.Add(tile);
        }
        if (verticalTile == null && hList.Count == 0)
        {
            loadFailed = true;
            Debug.Log($"[WallPropTileset] 无可用墙素材（Resources/{ResourceDir}），墙体回退白方块。");
            return;
        }
        if (verticalTile == null) verticalTile = hList[hList.Count - 1];
        horizontalTiles = hList.Count > 0 ? hList.ToArray() : new[] { verticalTile };
    }
}
