using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 职责 2/5 · 轮廓墙生成器（v1.1.31 根因修复核心）：对挖除区**只画交界的一格轮廓**——
/// Carved 中与 Walkable 四相邻的格子成为 Outline 墙；更深的挖除格保持虚空（不铺地板也不填墙）。
/// 取代 v1.1.22~30 把整个 Carved 填实心墙的做法（缺口连外墙长成大片墙块的根因）。
/// </summary>
public static class RoomBoundaryBuilder
{
    public static IEnumerable<Vector2Int> Build(HashSet<Vector2Int> carved, HashSet<Vector2Int> walkable)
    {
        foreach (var c in carved)
        {
            if (walkable.Contains(new Vector2Int(c.x + 1, c.y))
                || walkable.Contains(new Vector2Int(c.x - 1, c.y))
                || walkable.Contains(new Vector2Int(c.x, c.y + 1))
                || walkable.Contains(new Vector2Int(c.x, c.y - 1)))
                yield return c;
        }
    }
}
