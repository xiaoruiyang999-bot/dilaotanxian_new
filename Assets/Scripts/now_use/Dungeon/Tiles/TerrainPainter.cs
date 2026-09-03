using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 地皮视觉层绘制器：逻辑层仍由 TerrainMask 提供草/土二值结果。
/// 当前预览阶段关闭所有边缘、角落和贯通过渡，只验证两张主基底的连续铺设效果。
/// 旧地皮仅以低概率作为装饰性替换，不改变底层草/土逻辑。
/// </summary>
public static class TerrainPainter
{
    private const float GrassDecorationChance = 0.07f;
    private const float DirtDecorationChance = 0.06f;

    public static void PaintCell(Tilemap map, Vector3Int pos, TerrainMask mask, GroundTileset tileset, int decoSeed)
    {
        int x = pos.x;
        int y = pos.y;
        bool isDirt = mask.IsDirt(x, y);

        string basePool = isDirt ? "dirt_base" : "grass_base";
        string decorationPool = isDirt ? "dirt_decor" : "grass_decor";
        float decorationChance = isDirt ? DirtDecorationChance : GrassDecorationChance;

        // 独立哈希保证装饰稀疏、可复现，并且不会反向影响 TerrainMask。
        float roll = TerrainMask.Hash01(x, y, decoSeed ^ 0x51ED270B);
        bool useDecoration = roll < decorationChance && tileset.VariantCount(decorationPool) > 0;
        string pool = useDecoration ? decorationPool : basePool;

        map.SetTile(pos, tileset.GetTile(pool, Variant(x, y, decoSeed)));
    }

    private static int Variant(int x, int y, int decoSeed)
        => Mathf.FloorToInt(TerrainMask.Hash01(x, y, decoSeed ^ 0x9E37) * 1024f);
}