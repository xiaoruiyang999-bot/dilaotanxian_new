using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 房间尺寸扩展器（纯 C#，v0.5.3.1 修改计划 3.2）：类型分配完成后运行，
/// 按类型把大房间「整格扩展」为多格矩形——Boss 优先 2×2，Elite 2×1 或 1×2。
/// 只改 RoomNode 的 span 与 gridPos（锚点=最小角），不碰连接图：
/// 扩展只吞并空闲格，图边两端房间扩展后仍矩形相邻（构造保证）。
/// 失败回退 1×1（尽力满足，与 bossMinDistance 同策略），达成率由 Validate 统计。
/// 随机流：与类型分配共用独立流（seed*31+7），布局流零接触。
/// </summary>
public static class RoomSizeExpander
{
    public static void Expand(DungeonLayout layout, DungeonConfig config, System.Random rng)
    {
        var occupied = new Dictionary<Vector2Int, RoomNode>();
        foreach (RoomNode r in layout.rooms) occupied[r.gridPos] = r;

        // Boss 优先（大图优先保证）：N×N → 2×1/1×2 → 保持 1×1
        if (layout.bossRoom != null && config.bossCellSpan >= 2)
        {
            if (!TryExpandSquare(layout.bossRoom, config.bossCellSpan, occupied, rng))
                TryExpandRect(layout.bossRoom, occupied, rng);
        }

        // Combat（v1.1.46）：普通战斗房至少一倍大——2×2 优先（≈4×面积），
        // 失败回退 2×1/1×2（≈2×面积），再失败保 1×1（尽力满足，与 Boss 同策略）。
        // 排在 Elite 前：Combat 数量多（战斗体验主体），Elite 房本就强化、让位零星格子。
        foreach (RoomNode r in layout.rooms)
        {
            if (r.type != RoomType.Combat || config.combatCellSpan < 2) continue;
            if (!TryExpandSquare(r, config.combatCellSpan, occupied, rng))
                TryExpandRect(r, occupied, rng);
        }

        // Elite：向任一方向扩展 1 格（2×1 或 1×2），失败保持 1×1
        foreach (RoomNode r in layout.rooms)
        {
            if (r.type != RoomType.Elite || config.eliteCellSpan < 2) continue;
            TryExpandRect(r, occupied, rng);
        }
    }

    /// <summary>以房间格为 N×N 方块的任意一角（rng 打乱顺序），四角全部空闲则吞并。</summary>
    private static bool TryExpandSquare(RoomNode room, int n,
        Dictionary<Vector2Int, RoomNode> occupied, System.Random rng)
    {
        var corners = new List<Vector2Int>
        {
            Vector2Int.zero, new Vector2Int(-(n - 1), 0),
            new Vector2Int(0, -(n - 1)), new Vector2Int(-(n - 1), -(n - 1))
        };
        Shuffle(corners, rng);
        foreach (Vector2Int off in corners)
        {
            Vector2Int anchor = room.gridPos + off;
            if (!AreaFree(anchor, n, n, room, occupied)) continue;
            Apply(room, anchor, n, n, occupied);
            return true;
        }
        return false;
    }

    /// <summary>向 E/N/W/S 任一方向（rng 打乱顺序）扩展 1 格，目标格空闲则吞并。</summary>
    private static bool TryExpandRect(RoomNode room,
        Dictionary<Vector2Int, RoomNode> occupied, System.Random rng)
    {
        var dirs = new List<Vector2Int> { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
        Shuffle(dirs, rng);
        foreach (Vector2Int d in dirs)
        {
            Vector2Int other = room.gridPos + d;
            if (occupied.ContainsKey(other)) continue;
            var anchor = new Vector2Int(Mathf.Min(room.gridPos.x, other.x), Mathf.Min(room.gridPos.y, other.y));
            Apply(room, anchor, d.x != 0 ? 2 : 1, d.y != 0 ? 2 : 1, occupied);
            return true;
        }
        return false;
    }

    private static bool AreaFree(Vector2Int anchor, int sx, int sy, RoomNode self,
        Dictionary<Vector2Int, RoomNode> occupied)
    {
        for (int x = 0; x < sx; x++)
            for (int y = 0; y < sy; y++)
                if (occupied.TryGetValue(anchor + new Vector2Int(x, y), out RoomNode occ) && occ != self)
                    return false;
        return true;
    }

    private static void Apply(RoomNode room, Vector2Int anchor, int sx, int sy,
        Dictionary<Vector2Int, RoomNode> occupied)
    {
        room.gridPos = anchor;
        room.spanX = sx;
        room.spanY = sy;
        for (int x = 0; x < sx; x++)
            for (int y = 0; y < sy; y++)
                occupied[anchor + new Vector2Int(x, y)] = room;
    }

    private static void Shuffle(List<Vector2Int> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
