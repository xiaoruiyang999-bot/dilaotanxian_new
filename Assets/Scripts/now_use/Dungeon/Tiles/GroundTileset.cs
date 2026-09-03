using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 地皮视觉层素材库（v1.1.4 第二步）：运行时从 Resources/Art/Tiles/Ground 加载精灵（FrameAnimator 同模式，
/// 编辑器与构建同源），按语义名分池；缺方向素材用 90° 旋转补全（Tile.transform 矩阵，RuleTile 同款手法）。
/// 旋转约定（+z 逆时针）：corner_tl（土斑在左上）rot0=TL，rot90=BL，rot180=BR，rot270=TR；
/// multi_v（上下贯通土带）rot90 = multi_h（左右贯通）。
/// Tile 实例运行时创建并静态缓存（同图块跨楼层复用，零重复创建）。
/// </summary>
public class GroundTileset
{
    private const string ResourcesDir = "Art/Tiles/Ground";

    private readonly Dictionary<string, Sprite[]> pools = new Dictionary<string, Sprite[]>();
    private readonly Dictionary<Sprite, Dictionary<int, Tile>> tileCache = new Dictionary<Sprite, Dictionary<int, Tile>>();

    private static GroundTileset cached;

    /// <summary>加载并缓存素材库；素材缺失（池不满）返回 null，调用方回退平色地板。</summary>
    public static GroundTileset Load()
    {
        if (cached != null) return cached;

        var set = new GroundTileset();
        foreach (Sprite s in Resources.LoadAll<Sprite>(ResourcesDir))
        {
            string pool = PoolOf(s.name);
            if (pool == null) continue;
            if (!set.pools.TryGetValue(pool, out var list)) set.pools[pool] = list = new Sprite[0];
            System.Array.Resize(ref list, list.Length + 1);
            list[list.Length - 1] = s;
            set.pools[pool] = list;
        }

        // 必需池校验：clean 与 interior 有图即可；corner_tl / multi_v 是旋转基座必须有
        if (!set.pools.ContainsKey("grass_base") || !set.pools.ContainsKey("dirt_base"))
        {
            Debug.LogWarning("[GroundTileset] 缺少 grass_base 或 dirt_base 主基底，回退平色地板。");
            return null;
        }
        cached = set;
        return set;
    }

    /// <summary>语义名 → 池名（素材入库命名即合同：grass_clean_1 / dirt_interior_2 / grass_edge_top_1 / grass_corner_tl_2 / grass_multi_v_1）。</summary>
    private static string PoolOf(string spriteName)
    {
        if (spriteName.StartsWith("grass_base")) return "grass_base";
        if (spriteName.StartsWith("dirt_base")) return "dirt_base";
        if (spriteName.StartsWith("grass_clean")) return "grass_decor";
        if (spriteName.StartsWith("dirt_core")) return "dirt_decor";
        if (spriteName.StartsWith("dirt_interior")) return "grass_decor";
        if (spriteName.StartsWith("grass_edge_top")) return "edge_n";
        if (spriteName.StartsWith("grass_edge_bottom")) return "edge_s";
        if (spriteName.StartsWith("grass_edge_left")) return "edge_w";
        if (spriteName.StartsWith("grass_edge_right")) return "edge_e";
        if (spriteName.StartsWith("grass_corner_tl")) return "corner_tl";
        if (spriteName.StartsWith("grass_corner_bl")) return "corner_bl";
        if (spriteName.StartsWith("grass_multi_v")) return "multi_v";
        return null;
    }

    /// <summary>按池名取图块（variant 越界自动取模；rotationQuarter ∈ 0~3 = 旋转 90°×N）。</summary>
    public Tile GetTile(string pool, int variant, int rotationQuarter = 0)
    {
        if (!pools.TryGetValue(pool, out var list) || list.Length == 0)
        {
            // 视觉素材缺失时退回主草地，禁止 SetTile(null) 形成透明缝。
            if (!pools.TryGetValue("grass_base", out list) || list.Length == 0) return null;
        }
        Sprite sprite = list[Mathf.Abs(variant) % list.Length];

        if (!tileCache.TryGetValue(sprite, out var byRot))
            tileCache[sprite] = byRot = new Dictionary<int, Tile>();
        int rot = ((rotationQuarter % 4) + 4) % 4;
        if (!byRot.TryGetValue(rot, out Tile tile))
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color = Color.white;
            if (rot != 0)
                tile.transform = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 90f * rot));
            byRot[rot] = tile;
        }
        return tile;
    }

    /// <summary>池内变体数（装饰层选型用）。</summary>
    public int VariantCount(string pool)
        => pools.TryGetValue(pool, out var list) ? list.Length : 0;
}
