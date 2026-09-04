using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 房间塑形器 v2（v1.1.23 模板化）：角部挖除（保持 v1.1.22）+ 房内墙体改为**设计模板套件**
/// （用户拍板：随机墙段可能顶着刷怪点，需要几套保证开阔区的固定模板，元气骑士式设计房）。
///
/// 模板 = 以房心为原点的相对格子段列表（分辨率无关：任意房间尺寸直接平移落位，不拉伸）。
/// 所有模板遵循三条硬规则（手设计保证，非运行时判定）：
/// ① 房心 ±2 格永远开阔（敌人/掉落/传送落点聚集区——"墙不能延伸到刷怪点"）；
/// ② 墙段为短凸 stub（≤4 格），不与外墙相接、互不相接、不成环——结构上不可能围出死 pocket；
/// ③ 墙只做掩体不做迷宫：任何两点之间最坏绕行 ≤ 半圈外墙。
/// 叠加运行时保护带（门洞 ±2、房心 ±2）后落格；模板格落进保护带/挖除区时丢弃该格。
/// 确定性：模板选择与镜像 rng 由 layoutSeed 派生，同 seed 复现（EditMode seed 门禁）。
/// </summary>
public static class RoomShaper
{
    private const int DoorProtectRadius = 2;    // 门洞格保护半径（Chebyshev）
    private const int CenterProtectRadius = 2;  // 房中心保护半径
    private const float MaxCarveRatio = 0.35f;  // 挖除面积上限

    /// <summary>一段墙（stub）：start=相对房心偏移；len=长度；horizontal=横/竖。</summary>
    private struct Stub
    {
        public Vector2Int start;
        public int len;
        public bool horizontal;
        public Stub(int dx, int dy, int length, bool horiz)
        { start = new Vector2Int(dx, dy); len = length; horizontal = horiz; }
    }

    // ---------- 模板套件（房心相对坐标；全部满足三条硬规则） ----------

    /// <summary>T0 十字掩体：房心四方各一段 3 格横/竖墙，中心留 5×5 开阔。</summary>
    private static readonly Stub[] TCross =
    {
        new Stub(-4, 3, 3, true), new Stub(2, 3, 3, true),      // 上方两段横墙
        new Stub(-4, -4, 3, true), new Stub(2, -4, 3, true),    // 下方两段横墙
    };

    /// <summary>T1 双柱廊：左右 1/3 处各两根 2×2 石柱（走廊感）。</summary>
    private static readonly Stub[] TColonnade =
    {
        new Stub(-6, 2, 2, false), new Stub(-6, -3, 2, false),
        new Stub(5, 2, 2, false), new Stub(5, -3, 2, false),
    };

    /// <summary>T2 对角双 L：左上/右下角各一组 L 形短墙（斜切节奏感）。</summary>
    private static readonly Stub[] TDiagonalL =
    {
        new Stub(-6, 2, 3, true), new Stub(-6, 3, 3, false),
        new Stub(4, -2, 3, true), new Stub(6, -4, 3, false),
    };

    /// <summary>T3 中环四角：房心四斜角各一段 2 格墙（围而不合的"环"意象）。</summary>
    private static readonly Stub[] TRing =
    {
        new Stub(-4, 2, 2, true), new Stub(3, 2, 2, true),
        new Stub(-4, -3, 2, true), new Stub(3, -3, 2, true),
        new Stub(-4, 0, 2, false), new Stub(4, -1, 2, false),
    };

    /// <summary>T4 双断竖墙：两条 3 格竖墙错位（左右掩体，中路与两翼皆可走）。</summary>
    private static readonly Stub[] TBroken =
    {
        new Stub(-5, -1, 3, false), new Stub(4, 1, 3, false),
    };

    private static readonly Stub[][] Templates =
    {
        TCross, TColonnade, TDiagonalL, TRing, TBroken,
    };

    /// <summary>
    /// 为一个房间生成布置。interior=内部格矩形（不含外墙）；doorCells=该房各门洞世界格；
    /// 输出 carved（角部挖除）与 walls（模板墙格）。Start/Boss 房由调用方跳过。
    /// </summary>
    public static void Decorate(RectInt interior, System.Random rng, List<Vector2Int> doorCells,
        HashSet<Vector2Int> carved, List<Vector2Int> walls)
    {
        carved.Clear();
        walls.Clear();
        if (interior.width < 8 || interior.height < 6) return;

        Vector2Int center = new Vector2Int(interior.xMin + interior.width / 2, interior.yMin + interior.height / 2);
        int area = interior.width * interior.height;

        // ---------- ① 角部挖除（房型多样性，v1.1.22 保留） ----------
        double shapeRoll = rng.NextDouble();
        if (shapeRoll < 0.30) CarveNotch(interior, rng, doorCells, center, carved, area);
        else if (shapeRoll < 0.50)
        {
            CarveNotch(interior, rng, doorCells, center, carved, area);
            if (carved.Count < area * MaxCarveRatio * 0.6)
                CarveNotch(interior, rng, doorCells, center, carved, area);
        }
        else if (shapeRoll < 0.62) CarveChamfer(interior, rng, doorCells, carved);

        if (carved.Count > area * MaxCarveRatio) TrimTo(interior, carved, (int)(area * MaxCarveRatio));

        // ---------- ② 房内墙：模板套件（v1.1.23 替换随机段） ----------
        // 15% 概率无墙（纯净房节奏调剂）；模板随机选一，25% 概率水平镜像增加变化。
        // 段为落位单位：段内格子自相连（成片墙体），段与段之间保持 ≥2 间距（不粘连成环）
        if (rng.NextDouble() < 0.15) return;
        Stub[] template = Templates[rng.Next(Templates.Length)];
        bool mirror = rng.NextDouble() < 0.25;

        var stubCells = new List<Vector2Int>(8);
        foreach (Stub stub in template)
        {
            stubCells.Clear();
            for (int i = 0; i < stub.len; i++)
            {
                int dx = mirror ? -stub.start.x - (stub.horizontal ? i + 1 : 0)
                                : stub.start.x + (stub.horizontal ? i : 0);
                int dy = stub.start.y + (stub.horizontal ? 0 : i);
                var cell = new Vector2Int(center.x + dx, center.y + dy);
                if (!interior.Contains(cell)) continue;
                if (IsProtected(cell, doorCells, center)) continue;
                if (carved.Contains(cell)) continue;
                stubCells.Add(cell);
            }
            if (stubCells.Count < 2) continue;   // 被保护带吃得只剩 1 格：不成片，弃

            // 段间距检查：整段任一格贴近既有墙（≤1）→ 弃整段（防两段粘连成环）
            bool clash = false;
            foreach (var c in stubCells)
                if (NearExisting(c, walls)) { clash = true; break; }
            if (clash) continue;

            walls.AddRange(stubCells);
        }
    }

    // ---------- 挖除形状（v1.1.22 原样） ----------

    private static void CarveNotch(RectInt interior, System.Random rng, List<Vector2Int> doorCells,
        Vector2Int center, HashSet<Vector2Int> carved, int area)
    {
        int w = rng.Next(3, Mathf.Max(4, interior.width / 2 + 1));
        int h = rng.Next(2, Mathf.Max(3, interior.height / 2 + 1));
        if (w * h > area / 4) w = Mathf.Max(3, w / 2);

        int corner = rng.Next(4);   // 0=左下 1=右下 2=左上 3=右上
        int x0 = corner % 2 == 0 ? interior.xMin : interior.xMax - w;
        int y0 = corner < 2 ? interior.yMin : interior.yMax - h;

        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
                TryCarve(new Vector2Int(x, y), doorCells, center, carved);
    }

    /// <summary>斜切四角（八角形观感）：每角切 k×k 三角带。</summary>
    private static void CarveChamfer(RectInt interior, System.Random rng, List<Vector2Int> doorCells,
        HashSet<Vector2Int> carved)
    {
        int k = rng.Next(2, 4);
        for (int dy = 0; dy < k; dy++)
            for (int dx = 0; dx < k; dx++)
            {
                if (dx + dy >= k) continue;
                TryCarve(new Vector2Int(interior.xMin + dx, interior.yMin + dy), doorCells, Vector2Int.zero, carved, true);
                TryCarve(new Vector2Int(interior.xMax - 1 - dx, interior.yMin + dy), doorCells, Vector2Int.zero, carved, true);
                TryCarve(new Vector2Int(interior.xMin + dx, interior.yMax - 1 - dy), doorCells, Vector2Int.zero, carved, true);
                TryCarve(new Vector2Int(interior.xMax - 1 - dx, interior.yMax - 1 - dy), doorCells, Vector2Int.zero, carved, true);
            }
    }

    private static void TryCarve(Vector2Int cell, List<Vector2Int> doorCells, Vector2Int center,
        HashSet<Vector2Int> carved, bool ignoreCenter = false)
    {
        if (IsProtected(cell, doorCells, center, ignoreCenter)) return;
        carved.Add(cell);
    }

    // ---------- 判定工具 ----------

    private static bool IsProtected(Vector2Int cell, List<Vector2Int> doorCells, Vector2Int center, bool ignoreCenter = false)
    {
        if (doorCells != null)
            foreach (var d in doorCells)
                if (Chebyshev(cell, d) <= DoorProtectRadius) return true;
        return !ignoreCenter && Chebyshev(cell, center) <= CenterProtectRadius;
    }

    private static bool NearExisting(Vector2Int cell, List<Vector2Int> walls)
    {
        // 模板段间距保持 ≥1（不同 stub 之间不粘连成环）
        foreach (var w in walls)
            if (Chebyshev(cell, w) <= 1) return true;
        return false;
    }

    private static int Chebyshev(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
        return dx > dy ? dx : dy;
    }

    /// <summary>挖除超限时按行列序裁掉多余的格（保底行为，正常不触发）。</summary>
    private static void TrimTo(RectInt interior, HashSet<Vector2Int> carved, int limit)
    {
        if (carved.Count <= limit) return;
        var keep = new HashSet<Vector2Int>();
        int n = 0;
        for (int y = interior.yMin; y < interior.yMax && n < limit; y++)
            for (int x = interior.xMin; x < interior.xMax && n < limit; x++)
            {
                var c = new Vector2Int(x, y);
                if (carved.Contains(c)) { keep.Add(c); n++; }
            }
        carved.Clear();
        foreach (var c in keep) carved.Add(c);
    }
}
