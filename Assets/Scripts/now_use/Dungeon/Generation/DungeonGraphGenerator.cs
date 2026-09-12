using System;
using System.Collections.Generic;

/// <summary>
/// v2.0.5 DAG 图生成器（V2 §10.2/§10.6，纯 C#、seed 复现）：
/// 列式结构——列数 5~7（MVP 契约），每列 1~2 节点，分叉与汇聚并存；边只指向更大列
///（构造即无环）；Start 唯一（列 0）、Boss 唯一（最末列）。节点类型按 §10.4 权重分配
///（Boss 结构固定），特殊类型带最小间距（同类型不相邻列重复）。
/// 生成后 Validate 自检，失败内部重试（独立 rng 派生，同 seed 复现）。
/// </summary>
public static class DungeonGraphGenerator
{
    private const int MinColumns = 5;
    private const int MaxColumns = 7;
    private const int MaxRowsPerColumn = 2;
    private const int TypeSpacing = 2;   // 同类型特殊节点的最小列距

    public static DungeonGraphData Generate(int seed, System.Random rngOverride = null)
    {
        // 防御重试：验证失败换派生流重来（生成逻辑构造性强，正常一次通过）
        for (int attempt = 0; attempt < 8; attempt++)
        {
            var rng = rngOverride ?? new System.Random(seed * 7919 + attempt * 104729);
            DungeonGraphData graph = Build(seed, rng);
            if (graph.Validate(out _)) return graph;
        }
        // 理论不可达保底：纯直线（DAG 退化特例，永远合法）
        return BuildFallback(seed);
    }

    private static DungeonGraphData Build(int seed, System.Random rng)
    {
        var graph = new DungeonGraphData { Seed = seed };
        int columns = rng.Next(MinColumns, MaxColumns + 1);

        // 1) 逐列布点：首列 1 节点(Start)、末列 1 节点(Boss)、中间列 1~2 随机
        var nodesByColumn = new List<List<DungeonGraphNode>>();
        int nextId = 0;
        for (int col = 0; col < columns; col++)
        {
            var list = new List<DungeonGraphNode>();
            int count = col == 0 || col == columns - 1 ? 1 : rng.Next(1, MaxRowsPerColumn + 1);
            for (int row = 0; row < count; row++)
            {
                var node = new DungeonGraphNode { NodeId = nextId++, Column = col, Row = row };
                graph.Nodes.Add(node);
                list.Add(node);
            }
            nodesByColumn.Add(list);
        }

        // 2) 连边（分叉+汇聚）：每列每节点至少 1 条出边连到下一列；双列时随机双连
        for (int col = 0; col < columns - 1; col++)
        {
            var cur = nodesByColumn[col];
            var next = nodesByColumn[col + 1];
            foreach (DungeonGraphNode from in cur)
            {
                // 基础连：目标取与自身 Row 就近的下一列节点（汇聚自然发生）
                DungeonGraphNode target = next[Math.Min(from.Row, next.Count - 1)];
                Link(from, target);
                // 分叉：下一列比当前列多节点且掷中 → 追加第二条出边
                if (next.Count > 1 && cur.Count < next.Count && rng.NextDouble() < 0.55)
                    Link(from, next[(target.Row + 1) % next.Count]);
            }
            // 汇聚保证：下一列每个节点必须有入边——缺的从当前列就近节点补
            foreach (DungeonGraphNode to in next)
                if (to.PreviousNodeIds.Count == 0)
                    Link(cur[Math.Min(to.Row, cur.Count - 1)], to);
        }

        // 3) 类型：Start/Boss 结构固定（按 Id 显式判——Type 默认值是枚举首项 Start，
        //    不得用作跳过条件，否则中间节点永不被赋值、全图变多起点导致验证全线退 fallback 的根因）；
        //    其余按 §10.4 权重 + 最小间距
        graph.StartNodeId = nodesByColumn[0][0].NodeId;
        nodesByColumn[0][0].Type = NodeType.Start;
        DungeonGraphNode boss = nodesByColumn[columns - 1][0];
        boss.Type = NodeType.Boss;
        graph.BossNodeId = boss.NodeId;

        var lastTypeColumn = new Dictionary<NodeType, int>();
        foreach (DungeonGraphNode node in graph.Nodes)
        {
            if (node.NodeId == graph.StartNodeId || node.NodeId == graph.BossNodeId) continue;
            node.Type = PickType(rng, lastTypeColumn, node.Column);
            lastTypeColumn[node.Type] = node.Column;
        }

        graph.RevealInitial();
        return graph;
    }

    /// <summary>§10.4 权重：战斗 45/事件 15/宝箱 10/商店 10/恢复 10/精英 10（同类型列距不足时降级为战斗）。</summary>
    private static NodeType PickType(System.Random rng, Dictionary<NodeType, int> lastColumn, int column)
    {
        double roll = rng.NextDouble() * 100f;
        NodeType want = roll < 45 ? NodeType.Combat
            : roll < 60 ? NodeType.Event
            : roll < 70 ? NodeType.Treasure
            : roll < 80 ? NodeType.Shop
            : roll < 90 ? NodeType.Recovery
            : NodeType.Elite;

        if (want != NodeType.Combat
            && lastColumn != null
            && lastColumn.TryGetValue(want, out int lastCol)
            && column - lastCol < TypeSpacing)
            return NodeType.Combat;   // 间距约束：特殊类型降级
        return want;
    }

    private static void Link(DungeonGraphNode from, DungeonGraphNode to)
    {
        if (from.NextNodeIds.Contains(to.NodeId)) return;
        from.NextNodeIds.Add(to.NodeId);
        to.PreviousNodeIds.Add(from.NodeId);
    }

    private static DungeonGraphData BuildFallback(int seed)
    {
        var graph = new DungeonGraphData { Seed = seed };
        DungeonGraphNode prev = null;
        for (int i = 0; i < MinColumns; i++)
        {
            var node = new DungeonGraphNode
            {
                NodeId = i,
                Column = i,
                Type = i == 0 ? NodeType.Start : i == MinColumns - 1 ? NodeType.Boss : NodeType.Combat,
            };
            graph.Nodes.Add(node);
            if (prev != null) Link(prev, node);
            else graph.StartNodeId = node.NodeId;
            prev = node;
        }
        graph.BossNodeId = prev.NodeId;
        graph.RevealInitial();
        return graph;
    }
}
