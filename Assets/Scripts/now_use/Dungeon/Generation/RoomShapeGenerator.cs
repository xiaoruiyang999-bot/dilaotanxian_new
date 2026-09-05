using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 职责 1/5 · 房形生成器（v1.1.46 收敛）：
/// 房间主体以矩形竞技场为主，只用小缺角/小切角提供轮廓变化。
/// 旧实现允许 30×18 房挖出最大 15×9 的角块，并逐格跳过 protect，容易留下指状长墙；
/// 新实现把单次缺口限制在约 6×4 内、总挖除量 ≤12%，且候选碰保护带时整体拒绝，
/// 不再裁切出残片。输出仍只包含 Carved，墙由 RoomBoundaryBuilder 推导。
/// </summary>
public static class RoomShapeGenerator
{
    private const float RectangleChance = 0.72f;
    private const float NotchChance = 0.20f;
    private const float MaxCarveRatio = 0.12f;

    public static HashSet<Vector2Int> Generate(RectInt interior, System.Random rng,
        HashSet<Vector2Int> protect)
    {
        var carved = new HashSet<Vector2Int>();
        double roll = rng.NextDouble();
        if (roll < RectangleChance) return carved;

        int limit = Mathf.FloorToInt(interior.width * interior.height * MaxCarveRatio);
        if (roll < RectangleChance + NotchChance)
            TryAddNotch(interior, rng, protect, carved, limit);
        else
            TryAddChamfers(interior, rng, protect, carved, limit);

        return carved;
    }

    private static void TryAddNotch(RectInt interior, System.Random rng,
        HashSet<Vector2Int> protect, HashSet<Vector2Int> carved, int limit)
    {
        int maxWidth = Mathf.Clamp(interior.width / 5, 3, 6);
        int maxHeight = Mathf.Clamp(interior.height / 5, 2, 4);
        int width = rng.Next(3, maxWidth + 1);
        int height = rng.Next(2, maxHeight + 1);
        int corner = rng.Next(4); // 0=左下 1=右下 2=左上 3=右上
        int x0 = corner % 2 == 0 ? interior.xMin : interior.xMax - width;
        int y0 = corner < 2 ? interior.yMin : interior.yMax - height;

        var proposal = new List<Vector2Int>(width * height);
        for (int y = y0; y < y0 + height; y++)
            for (int x = x0; x < x0 + width; x++)
                proposal.Add(new Vector2Int(x, y));
        TryCommit(proposal, protect, carved, limit);
    }

    private static void TryAddChamfers(RectInt interior, System.Random rng,
        HashSet<Vector2Int> protect, HashSet<Vector2Int> carved, int limit)
    {
        int size = rng.Next(2, 4);
        int firstCorner = rng.Next(4);
        int cornerCount = rng.NextDouble() < 0.35 ? 2 : 1;
        var proposal = new List<Vector2Int>(size * size * cornerCount);

        AddChamfer(interior, firstCorner, size, proposal);
        if (cornerCount == 2) AddChamfer(interior, (firstCorner + 2) % 4, size, proposal);
        TryCommit(proposal, protect, carved, limit);
    }

    private static void AddChamfer(RectInt interior, int corner, int size,
        List<Vector2Int> proposal)
    {
        for (int dy = 0; dy < size; dy++)
            for (int dx = 0; dx < size; dx++)
            {
                if (dx + dy >= size) continue;
                int x = corner % 2 == 0 ? interior.xMin + dx : interior.xMax - 1 - dx;
                int y = corner < 2 ? interior.yMin + dy : interior.yMax - 1 - dy;
                proposal.Add(new Vector2Int(x, y));
            }
    }

    /// <summary>完整形状提交：碰骨架或超过面积预算即全部放弃，杜绝逐格裁切残片。</summary>
    private static void TryCommit(List<Vector2Int> proposal, HashSet<Vector2Int> protect,
        HashSet<Vector2Int> carved, int limit)
    {
        if (carved.Count + proposal.Count > limit) return;
        for (int i = 0; i < proposal.Count; i++)
            if (protect.Contains(proposal[i])) return;
        for (int i = 0; i < proposal.Count; i++) carved.Add(proposal[i]);
    }
}
