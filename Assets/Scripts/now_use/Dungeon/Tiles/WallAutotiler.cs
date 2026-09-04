using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 墙体自动拼接（v1.1.23，元气骑士规范）：4 方向掩码 Autotile——
/// 掩码 = 上×8 + 右×4 + 下×2 + 左×1（相邻格有墙即置位），**斜向邻居完全忽略**，索引 0~15 对应 16 张瓦片。
/// 覆盖范围：外墙、房内模板墙、角部挖除缘、门洞断口（开洞后再拼接，门洞两侧自动出封口/拐角瓦）。
///
/// 素材契约（16 图齐套才生效，缺任一张整体回退现有单块墙瓦，视觉不变）：
/// `Assets/Resources/Art/Tiles/Wall/wall_00.png ~ wall_15.png`（索引=掩码值，两位数字命名）；
/// 方形瓦片任意分辨率，建议 Point/不压缩/PPU=边长像素（=1 格 1 单位）。
/// Tile 运行时创建并静态缓存（GroundTileset 同模式）；瓦片为满幅方块时碰撞即满格。
/// </summary>
public static class WallAutotiler
{
    private const string ResourceDir = "Art/Tiles/Wall";
    private const int TileCount = 16;

    private static Tile[] tiles;
    private static bool loadFailed;

    /// <summary>对整张墙 Tilemap 做一次掩码拼接（DungeonBuilder 在全部开洞后调用一次）。</summary>
    public static void Apply(Tilemap walls)
    {
        if (walls == null) return;
        Tile[] set = LoadTiles();
        if (set == null) return;   // 素材未齐：保持原样

        BoundsInt bounds = walls.cellBounds;
        for (int y = bounds.yMin; y <= bounds.yMax; y++)
            for (int x = bounds.xMin; x <= bounds.xMax; x++)
            {
                var pos = new Vector3Int(x, y, 0);
                if (walls.GetTile(pos) == null) continue;

                int mask = (HasWall(walls, x, y + 1) ? 8 : 0)   // 上
                         | (HasWall(walls, x + 1, y) ? 4 : 0)   // 右
                         | (HasWall(walls, x, y - 1) ? 2 : 0)   // 下
                         | (HasWall(walls, x - 1, y) ? 1 : 0);  // 左
                walls.SetTile(pos, set[mask]);
            }
    }

    private static bool HasWall(Tilemap walls, int x, int y)
        => walls.GetTile(new Vector3Int(x, y, 0)) != null;

    /// <summary>加载 16 张掩码瓦片（缺任一张返回 null 并记忆失败，不重复尝试）。</summary>
    private static Tile[] LoadTiles()
    {
        if (tiles != null) return tiles;
        if (loadFailed) return null;

        var set = new Tile[TileCount];
        for (int i = 0; i < TileCount; i++)
        {
            Sprite s = Resources.Load<Sprite>($"{ResourceDir}/wall_{i:00}");
            if (s == null)
            {
                loadFailed = true;   // 素材未齐：静默回退（Console 一次提示）
                Debug.Log($"[WallAutotiler] 掩码瓦片未齐（缺 wall_{i:00}，共需 16 张于 Resources/{ResourceDir}），墙体保持单块瓦。");
                return null;
            }
            var t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = s;
            t.color = Color.white;
            set[i] = t;
        }
        tiles = set;
        return tiles;
    }
}
