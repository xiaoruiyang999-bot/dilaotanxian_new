using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 地皮视觉层绘制器（v1.1.25 恢复完整版）：消费 TerrainMask（逻辑真源）+ GroundTileset，
/// 按 8 邻域 Bitmask 选过渡图块，再叠独立噪声装饰层。绝不反写逻辑层。
///
/// v1.1.20 预览版曾临时关闭全部过渡只铺双基底；v1.1.25 按用户要求恢复原套件完整铺装
///（等比例 1 图=1 格，85px@PPU85 契约自适应），并融合新基底池：
/// - 主基底：grass_base / dirt_base（新图）；
/// - 装饰池：grass_decor（原 grass_clean + dirt_interior 斑驳）/ dirt_decor（dirt_core 8 变体）；
/// - 过渡：edge/corner/multi 全套恢复（草格邻土时贴边土斑朝土侧，四角旋转补全）。
/// 映射规则（草格的土邻域分类）与距离场装饰概率同 v1.1.4：
/// D=1 已是过渡路径；D=2 → 30% 装饰斑；D≥3 → 12% 装饰斑；土格 → dirt_decor 变体轮换。
/// </summary>
public static class TerrainPainter
{
    private const float BaldPatchChanceD2 = 0.30f;   // 边界第二圈装饰斑概率
    private const float BaldPatchChanceFar = 0.12f;  // 远离边界装饰斑概率（草地 ~88% 完整基底）

    public static void PaintCell(Tilemap map, Vector3Int pos, TerrainMask mask, GroundTileset tileset, int decoSeed)
    {
        int x = pos.x, y = pos.y;

        if (mask.IsDirt(x, y))
        {
            // 土区主体：纯土基底 + 独立噪声轮换 dirt_decor 变体（稀疏点缀，v1.1.25 起 dirt_core 出场）
            map.SetTile(pos, tileset.GetTile("dirt_base", 0));
            if (TerrainMask.Hash01(x, y, decoSeed ^ 0x51ED270B) < 0.35f)
                map.SetTile(pos, tileset.GetTile("dirt_decor", Variant(x, y, decoSeed)));
            return;
        }

        // 8 邻域 Bitmask（N/E/S/W 正向 + 四对角）
        bool n = mask.IsDirt(x, y + 1), s = mask.IsDirt(x, y - 1), w = mask.IsDirt(x - 1, y), e = mask.IsDirt(x + 1, y);
        bool nw = mask.IsDirt(x - 1, y + 1), ne = mask.IsDirt(x + 1, y + 1);
        bool sw = mask.IsDirt(x - 1, y - 1), se = mask.IsDirt(x + 1, y - 1);
        int cardinals = (n ? 1 : 0) + (e ? 1 : 0) + (s ? 1 : 0) + (w ? 1 : 0);

        if (cardinals >= 3)
        {
            // 三面围合：重度侵蚀观感，直接用纯土基底
            map.SetTile(pos, tileset.GetTile("dirt_base", 0));
        }
        else if (cardinals == 2)
        {
            if (n && s) map.SetTile(pos, tileset.GetTile("multi_v", Variant(x, y, decoSeed)));            // 纵向贯通
            else if (w && e) map.SetTile(pos, tileset.GetTile("multi_v", Variant(x, y, decoSeed), 1));    // rot90 横向贯通
            else map.SetTile(pos, QuadrantCorner(tileset, n, s, w, e, x, y, decoSeed));                    // 相邻两侧 → 象限角
        }
        else if (cardinals == 1)
        {
            string pool = n ? "edge_n" : s ? "edge_s" : w ? "edge_w" : "edge_e";
            map.SetTile(pos, tileset.GetTile(pool, Variant(x, y, decoSeed)));
        }
        else if (nw || ne || sw || se)
        {
            // 仅对角土邻：朝该象限的角块（TL rot0 / BL rot90 / BR rot180 / TR rot270）
            map.SetTile(pos, DiagonalCorner(tileset, nw, ne, sw, se, x, y, decoSeed));
        }
        else
        {
            // 纯草区：主基底 + 距离场驱动的装饰斑（独立随机场，不与地形逻辑挂钩）
            int d = mask.Dist(x, y);
            float roll = TerrainMask.Hash01(x, y, decoSeed ^ 0x51ED270B);
            bool bald = d == 2 ? roll < BaldPatchChanceD2 : roll < BaldPatchChanceFar;
            map.SetTile(pos, tileset.GetTile(
                bald && tileset.VariantCount("grass_decor") > 0 ? "grass_decor" : "grass_base",
                Variant(x, y, decoSeed)));
        }
    }

    /// <summary>相邻两侧正向（象限）→ 角块：N+W→TL(rot0)，S+W→BL(rot90)，S+E→BR(rot180)，N+E→TR(rot270)。</summary>
    private static Tile QuadrantCorner(GroundTileset tileset, bool n, bool s, bool w, bool e, int x, int y, int decoSeed)
    {
        if (n && w) return tileset.GetTile("corner_tl", Variant(x, y, decoSeed));
        if (s && w) return CornerBL(tileset, x, y, decoSeed);
        if (s && e) return tileset.GetTile("corner_tl", Variant(x, y, decoSeed), 2);
        return tileset.GetTile("corner_tl", Variant(x, y, decoSeed), 3);   // n && e
    }

    /// <summary>仅对角土邻 → 象限角块（同上象限约定）。</summary>
    private static Tile DiagonalCorner(GroundTileset tileset, bool nw, bool ne, bool sw, bool se, int x, int y, int decoSeed)
    {
        if (nw) return tileset.GetTile("corner_tl", Variant(x, y, decoSeed));
        if (sw) return CornerBL(tileset, x, y, decoSeed);
        if (se) return tileset.GetTile("corner_tl", Variant(x, y, decoSeed), 2);
        return tileset.GetTile("corner_tl", Variant(x, y, decoSeed), 3);   // ne
    }

    /// <summary>左下象限：优先专用 corner_bl 池，缺素材回退 corner_tl rot90（旋转等价）。</summary>
    private static Tile CornerBL(GroundTileset tileset, int x, int y, int decoSeed)
    {
        Tile t = tileset.GetTile("corner_bl", Variant(x, y, decoSeed));
        return t != null && tileset.VariantCount("corner_bl") > 0 ? t : tileset.GetTile("corner_tl", Variant(x, y, decoSeed), 1);
    }

    /// <summary>独立装饰噪声选变体索引（正整数）。</summary>
    private static int Variant(int x, int y, int decoSeed)
        => Mathf.FloorToInt(TerrainMask.Hash01(x, y, decoSeed ^ 0x9E37) * 1024f);
}
