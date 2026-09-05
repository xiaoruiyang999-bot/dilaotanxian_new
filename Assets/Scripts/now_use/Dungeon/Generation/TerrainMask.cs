using UnityEngine;

/// <summary>
/// 地形逻辑层（v1.1.4 地皮系统第一步）：只存二值逻辑（false=草，true=土），不含任何视觉概念。
/// 采样用世界绝对格坐标（跨房间连续，门洞/相邻房间接缝处噪声值不割裂）；
/// 噪声为自带种子的 Value Noise FBM（Mathf.PerlinNoise 不可播种，无法满足地牢 seed 复现门禁）。
/// 后处理：删除面积过小的孤立土簇（防雪花噪点）与土中草洞，再计算到最近土区的距离场 D（装饰层消费）。
/// 全部数组预分配、确定性：同 (area, seed) 输出完全一致（EditMode 地牢 seed 测试同源）。
/// </summary>
public class TerrainMask
{
    // 生成参数默认值（Generate 入口可覆盖）
    public const float DefaultFrequency = 0.24f;    // 基波波长 ≈ 4.2 格 → 土斑直径 2~5 格
    public const float DefaultThreshold = 0.58f;    // > 阈值为土（离线仿真：约 29~36% 覆盖）
    public const int DefaultMinDirtCluster = 4;     // 小于此面积的孤立土簇回填为草
    public const int DefaultMinGrassHole = 3;       // 土中小草洞回填为土

    public readonly RectInt bounds;
    /// <summary>行优先二值图（true=土），索引 (y-bounds.yMin)*bounds.width + (x-bounds.xMin)。</summary>
    public readonly bool[] dirt;
    /// <summary>到最近土格的距离（土=0；草由近及远 1,2,3…），四连通 BFS。</summary>
    public readonly int[] distance;

    private TerrainMask(RectInt area)
    {
        bounds = area;
        dirt = new bool[area.width * area.height];
        distance = new int[area.width * area.height];
    }

    public bool IsDirt(int x, int y)
    {
        // 越界一律视为草：房间边缘外侧的未知区域不产生虚假过渡
        if (x < bounds.xMin || x >= bounds.xMax || y < bounds.yMin || y >= bounds.yMax) return false;
        return dirt[(y - bounds.yMin) * bounds.width + (x - bounds.xMin)];
    }

    public int Dist(int x, int y)
    {
        if (x < bounds.xMin || x >= bounds.xMax || y < bounds.yMin || y >= bounds.yMax) return int.MaxValue;
        return distance[(y - bounds.yMin) * bounds.width + (x - bounds.xMin)];
    }

    // ---------- 生成流水线（对应总纲第 3~4 步：噪声填充 → 修正逻辑 → 距离场） ----------

    public static TerrainMask Generate(RectInt area, int seed,
        float frequency = DefaultFrequency, float threshold = DefaultThreshold,
        int minDirtCluster = DefaultMinDirtCluster, int minGrassHole = DefaultMinGrassHole)
    {
        var mask = new TerrainMask(area);
        int w = area.width, h = area.height;

        // 1) 噪声填充：世界绝对坐标采样（非房间局部坐标）
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float n = Fbm(area.xMin + x, area.yMin + y, seed, frequency);
                mask.dirt[y * w + x] = n > threshold;
            }

        // 2) 连通性修正：孤立小土簇→草；土中小草洞→土
        mask.RemoveSmallClusters(true, minDirtCluster, false);
        mask.RemoveSmallClusters(false, minGrassHole, true);

        // 3) 距离场：多源 BFS（土=0），供装饰层判定 D
        mask.ComputeDistance();
        return mask;
    }

    /// <summary>清除过小四连通簇；草洞清理时保留接触地图边界的草区，避免把正常外部草地误判成洞。</summary>
    private void RemoveSmallClusters(bool target, int minSize, bool preserveBoundary)
    {
        if (minSize <= 1) return;
        int w = bounds.width, h = bounds.height;
        bool[] visited = new bool[dirt.Length];
        int[] stack = new int[dirt.Length];
        int[] component = new int[dirt.Length];

        for (int start = 0; start < dirt.Length; start++)
        {
            if (visited[start] || dirt[start] != target) continue;

            int sp = 0, size = 0;
            bool touchesBoundary = false;
            stack[sp++] = start;
            visited[start] = true;

            while (sp > 0)
            {
                int idx = stack[--sp];
                component[size++] = idx;
                int x = idx % w, y = idx / w;
                touchesBoundary |= x == 0 || x == w - 1 || y == 0 || y == h - 1;

                if (x > 0) Visit(idx - 1);
                if (x < w - 1) Visit(idx + 1);
                if (y > 0) Visit(idx - w);
                if (y < h - 1) Visit(idx + w);
            }

            if (size < minSize && (!preserveBoundary || !touchesBoundary))
                for (int i = 0; i < size; i++) dirt[component[i]] = !target;

            void Visit(int idx)
            {
                if (visited[idx] || dirt[idx] != target) return;
                visited[idx] = true;
                stack[sp++] = idx;
            }
        }
    }
    private void ComputeDistance()
    {
        int w = bounds.width, h = bounds.height;
        int[] queue = new int[w * h];
        int head = 0, tail = 0;

        for (int i = 0; i < distance.Length; i++)
        {
            if (dirt[i]) { distance[i] = 0; queue[tail++] = i; }
            else distance[i] = int.MaxValue;
        }

        while (head < tail)
        {
            int idx = queue[head++];
            int d = distance[idx] + 1;
            int x = idx % w, y = idx / w;
            if (x > 0 && distance[idx - 1] > d) { distance[idx - 1] = d; queue[tail++] = idx - 1; }
            if (x < w - 1 && distance[idx + 1] > d) { distance[idx + 1] = d; queue[tail++] = idx + 1; }
            if (y > 0 && distance[idx - w] > d) { distance[idx - w] = d; queue[tail++] = idx - w; }
            if (y < h - 1 && distance[idx + w] > d) { distance[idx + w] = d; queue[tail++] = idx + w; }
        }
    }

    /// <summary>
    /// 骨架偏置（v1.1.41 地皮融入）：土区尽量包含路径骨架——骨架格按 chance 概率置土
    ///（非强制：保留噪声土斑的有机形态，骨架只是强吸引），随后重算距离场供装饰层。
    /// 确定性：偏置 rng 由 seed 派生，同 seed 复现不变。
    /// </summary>
    public void ApplySkeletonBias(System.Collections.Generic.IEnumerable<Vector2Int> skeletonCells,
        int seed, float chance = 0.8f)
    {
        bool changed = false;
        foreach (var cell in skeletonCells)
        {
            if (!bounds.Contains(cell)) continue;
            if (dirt[(cell.y - bounds.yMin) * bounds.width + (cell.x - bounds.xMin)]) continue;
            if (Hash01(cell.x, cell.y, seed) >= chance) continue;
            dirt[(cell.y - bounds.yMin) * bounds.width + (cell.x - bounds.xMin)] = true;
            changed = true;
        }
        if (changed) ComputeDistance();
    }

    // ---------- 可播种 Value Noise（Fbm 两倍频） ----------

    private static float Fbm(int x, int y, int seed, float frequency)
    {
        float fx = x * frequency, fy = y * frequency;
        // 两倍频：低频定形状（权重 0.65），高频扰边界（权重 0.35），频比取质数化 2.13 防周期对齐
        return SampleLattice(fx, fy, seed) * 0.65f + SampleLattice(fx * 2.13f + 57.3f, fy * 2.13f + 19.7f, seed ^ 0x9E37) * 0.35f;
    }

    private static float SampleLattice(float fx, float fy, int seed)
    {
        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
        float tx = Smooth(fx - x0), ty = Smooth(fy - y0);
        float a = Hash01(x0, y0, seed), b = Hash01(x0 + 1, y0, seed);
        float c = Hash01(x0, y0 + 1, seed), d = Hash01(x0 + 1, y0 + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    /// <summary>整数格哈希 → [0,1)，确定性可播种（异或乘散列，无分配）。装饰层独立随机场复用同款哈希。</summary>
    public static float Hash01(int x, int y, int seed)
    {
        uint h = (uint)(seed ^ (x * 374761393) ^ (y * 668265263));
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return (h & 0xFFFFFF) / 16777216f;
    }
}
