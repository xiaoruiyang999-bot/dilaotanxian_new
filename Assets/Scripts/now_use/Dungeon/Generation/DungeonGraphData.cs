using System;
using System.Collections.Generic;

/// <summary>
/// v2.0.5 DAG 地图数据层（V2 文档 §10.1，纯 C# 可序列化——不依赖 GameObject，
/// Generated/Discovered/Visited/Completed 不因房间卸载丢失；Active 属于房间运行实例，不入图）。
/// 由 DungeonGraphGenerator 构造，EditMode 可完整验证。
/// </summary>
[Serializable]
public class DungeonGraphNode
{
    public int NodeId;
    public NodeType Type;
    /// <summary>列（0 起，从左向右单调递增；边只允许指向更大列 → 构造即无环）。</summary>
    public int Column;
    /// <summary>列内行序（同列多节点垂直错位排布，纯可视化，不映射物理门向）。</summary>
    public int Row;
    public List<int> PreviousNodeIds = new List<int>();
    public List<int> NextNodeIds = new List<int>();

    // 状态位（V2 §10.1：跨房间卸载持久）
    public bool Generated;
    public bool Discovered;
    public bool Visited;
    public bool Completed;
}

/// <summary>节点类型（V2 §10.3）。Boss 由结构固定；其余按权重与间距约束分配。</summary>
public enum NodeType
{
    Start,
    Combat,
    Elite,
    Treasure,
    Shop,
    Event,
    Recovery,
    Boss,
}

/// <summary>
/// 一层 Run 的 DAG 图（纯数据）。同 seed 完全可复现；全部约束由生成器保证、
/// Validate 复核：唯一起点/Boss 汇点、全可达、边单向向右、无环。
/// </summary>
[Serializable]
public class DungeonGraphData
{
    public int Seed;
    public List<DungeonGraphNode> Nodes = new List<DungeonGraphNode>();
    public int StartNodeId = -1;
    public int BossNodeId = -1;

    public DungeonGraphNode Get(int nodeId)
    {
        foreach (DungeonGraphNode n in Nodes) if (n.NodeId == nodeId) return n;
        return null;
    }

    /// <summary>
    /// 结构验证（生成后必过；EditMode 门禁同源）：唯一 Start/Boss、边只指向更大列、
    /// 全节点从 Start 可达、Boss 可达、Next/Prev 双向一致。
    /// </summary>
    public bool Validate(out string error)
    {
        error = null;
        if (Nodes.Count < 3) { error = "节点数不足"; return false; }

        int startCount = 0, bossCount = 0;
        foreach (DungeonGraphNode n in Nodes)
        {
            if (n.Type == NodeType.Start) { startCount++; StartNodeId = n.NodeId; }
            if (n.Type == NodeType.Boss) { bossCount++; BossNodeId = n.NodeId; }
            foreach (int next in n.NextNodeIds)
            {
                DungeonGraphNode target = Get(next);
                if (target == null) { error = $"节点 {n.NodeId} 指向不存在的 {next}"; return false; }
                if (target.Column <= n.Column) { error = $"边 {n.NodeId}->{next} 未指向更大列（环）"; return false; }
                if (!target.PreviousNodeIds.Contains(n.NodeId))
                { error = $"Next/Prev 不一致：{n.NodeId}->{next}"; return false; }
            }
            foreach (int prev in n.PreviousNodeIds)
            {
                DungeonGraphNode source = Get(prev);
                if (source == null || !source.NextNodeIds.Contains(n.NodeId))
                { error = $"Prev/Next 不一致：{prev}->{n.NodeId}"; return false; }
            }
        }
        if (startCount != 1) { error = $"起点数 {startCount} ≠ 1"; return false; }
        if (bossCount != 1) { error = $"Boss 数 {bossCount} ≠ 1"; return false; }

        // 全可达（BFS 自 Start）
        var visited = new HashSet<int> { StartNodeId };
        var queue = new Queue<int>();
        queue.Enqueue(StartNodeId);
        while (queue.Count > 0)
        {
            DungeonGraphNode cur = Get(queue.Dequeue());
            foreach (int next in cur.NextNodeIds)
                if (visited.Add(next)) queue.Enqueue(next);
        }
        if (visited.Count != Nodes.Count)
        { error = $"{Nodes.Count - visited.Count} 个节点从起点不可达"; return false; }
        if (!visited.Contains(BossNodeId)) { error = "Boss 不可达"; return false; }
        return true;
    }

    /// <summary>当前节点的可选下一节点（V2 §3.1：只从 NextNodeIds 中选）。</summary>
    public List<DungeonGraphNode> NextOf(int nodeId)
    {
        var result = new List<DungeonGraphNode>();
        DungeonGraphNode n = Get(nodeId);
        if (n == null) return result;
        foreach (int id in n.NextNodeIds)
        {
            DungeonGraphNode t = Get(id);
            if (t != null) result.Add(t);
        }
        return result;
    }

    /// <summary>揭示规则（V2 §10.5 已定案：完整连接可见、部分节点类型未知）：
    /// 起点与其直接后继视为已揭示。</summary>
    public void RevealInitial()
    {
        DungeonGraphNode start = Get(StartNodeId);
        if (start == null) return;
        start.Discovered = true;
        foreach (int id in start.NextNodeIds)
        {
            DungeonGraphNode t = Get(id);
            if (t != null) t.Discovered = true;
        }
    }
}
