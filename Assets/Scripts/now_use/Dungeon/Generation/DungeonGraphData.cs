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
    /// <summary>固定主分支（0～2）；Start/Boss 为 -1。进入主分支后直到 Boss 都不得改变。</summary>
    public int MainBranchId = -1;
    /// <summary>主分支内轨道：0=主干，1=短支路。只用于拓扑验证与地图排布。</summary>
    public int BranchLane;
    public List<int> PreviousNodeIds = new List<int>();
    public List<int> NextNodeIds = new List<int>();

    // v2.1.0：节点内容只保存数据与确定性种子；Room GameObject 生命周期不写入图。
    public string RoomArchetypeId;
    public int RoomSeed;
    public int EncounterSeed;
    public int RewardSeed;
    public int ShopSeed;

    // 状态位（V2 §10.1：跨房间卸载持久）
    public bool Generated;
    public bool Discovered;
    public bool Selected;
    public bool Visited;
    public bool ObjectiveCompleted;
    public bool RewardResolved;
    public bool Completed;

    public bool TrySelect()
    {
        if (!Discovered || Selected || Completed) return false;
        Selected = true;
        return true;
    }

    public bool TryVisit()
    {
        if (!Selected || Visited || Completed) return false;
        Visited = true;
        return true;
    }

    public bool TryCompleteObjective()
    {
        if (!Visited || ObjectiveCompleted || Completed) return false;
        ObjectiveCompleted = true;
        return true;
    }

    public bool TryResolveReward()
    {
        if (!ObjectiveCompleted || RewardResolved || Completed) return false;
        RewardResolved = true;
        return true;
    }

    public bool TryComplete()
    {
        if (!RewardResolved || Completed) return false;
        Completed = true;
        return true;
    }
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
    Supply,
    Sage,
}

/// <summary>
/// 一层 Run 的 DAG 图（纯数据）。同 seed 完全可复现；全部约束由生成器保证、
/// Validate 复核：唯一起点/Boss 汇点、全可达、边单向向右、无环。
/// </summary>
[Serializable]
public class DungeonGraphData
{
    public int GenerationVersion;
    public int Seed;
    public List<DungeonGraphNode> Nodes = new List<DungeonGraphNode>();
    public int StartNodeId = -1;
    public int BossNodeId = -1;

    public DungeonGraphNode Get(int nodeId)
    {
        foreach (DungeonGraphNode n in Nodes) if (n.NodeId == nodeId) return n;
        return null;
    }

    /// <summary>v2.1 图合同：规模、单向相邻边、双向索引、全可达与每路径机会。</summary>
    public bool Validate(out string error)
    {
        error = null;
        if (GenerationVersion != DungeonGraphGenerator.CurrentGenerationVersion)
        { error = $"路线图版本 {GenerationVersion} 已过期"; return false; }
        if (Nodes == null || Nodes.Count < 10) { error = "节点数不足"; return false; }

        var ids = new HashSet<int>();
        var rowsByColumn = new Dictionary<int, HashSet<int>>();
        int startCount = 0, bossCount = 0, maxColumn = -1;
        int foundStartId = -1, foundBossId = -1;
        foreach (DungeonGraphNode n in Nodes)
        {
            if (n == null || !ids.Add(n.NodeId)) { error = "空节点或重复 NodeId"; return false; }
            if (n.Column < 0 || n.Row < 0 || n.PreviousNodeIds == null || n.NextNodeIds == null)
            { error = $"节点 {n.NodeId} 基础字段非法"; return false; }
            if (!rowsByColumn.TryGetValue(n.Column, out HashSet<int> rows))
            { rows = new HashSet<int>(); rowsByColumn[n.Column] = rows; }
            if (!rows.Add(n.Row)) { error = $"第 {n.Column} 层 Row 重复"; return false; }
            if (n.Column > maxColumn) maxColumn = n.Column;
            if (n.Type == NodeType.Start) { startCount++; foundStartId = n.NodeId; }
            if (n.Type == NodeType.Boss) { bossCount++; foundBossId = n.NodeId; }
            if (n.Type == NodeType.Start || n.Type == NodeType.Boss)
            {
                if (n.MainBranchId != -1) { error = $"端点 {n.NodeId} 不应属于主分支"; return false; }
            }
            else if (n.MainBranchId < 0 || n.MainBranchId > 2 || n.BranchLane < 0 || n.BranchLane > 1)
            { error = $"节点 {n.NodeId} 主分支或轨道非法"; return false; }
            if (n.Type != NodeType.Start && n.Type != NodeType.Boss
                && n.Type != NodeType.Supply && n.Type != NodeType.Sage && n.Type != NodeType.Shop)
            { error = $"节点 {n.NodeId} 使用未投放类型 {n.Type}"; return false; }
            if (n.NextNodeIds.Count > 3) { error = $"节点 {n.NodeId} 出度超过 3"; return false; }
            if (new HashSet<int>(n.NextNodeIds).Count != n.NextNodeIds.Count
                || new HashSet<int>(n.PreviousNodeIds).Count != n.PreviousNodeIds.Count)
            { error = $"节点 {n.NodeId} 有重复边"; return false; }
        }
        if (maxColumn < 9 || maxColumn > 11 || rowsByColumn.Count != maxColumn + 1)
        { error = "层数必须为 10～12 且不能缺层"; return false; }
        if (startCount != 1 || bossCount != 1 || StartNodeId != foundStartId || BossNodeId != foundBossId)
        { error = "Start/Boss 不唯一或 ID 不一致"; return false; }
        if (Get(StartNodeId).Column != 0 || Get(BossNodeId).Column != maxColumn
            || rowsByColumn[0].Count != 1 || rowsByColumn[maxColumn].Count != 1)
        { error = "Start/Boss 必须分别独占首末层"; return false; }
        for (int col = 0; col <= maxColumn; col++)
        {
            int count = rowsByColumn[col].Count;
            if (count < 1 || count > 6) { error = $"第 {col} 层节点数越界"; return false; }
            if (col > 0 && col < maxColumn)
                for (int branch = 0; branch < 3; branch++)
                {
                    int mainCount = 0;
                    foreach (DungeonGraphNode node in Nodes)
                        if (node.Column == col && node.MainBranchId == branch && node.BranchLane == 0)
                            mainCount++;
                    if (mainCount != 1)
                    { error = $"第 {col} 层主分支 {branch} 缺少唯一主干节点"; return false; }
                }
        }

        int branches = 0, merges = 0;
        var branchSplits = new int[3];
        var branchMerges = new int[3];
        foreach (DungeonGraphNode n in Nodes)
        {
            if (n.NodeId == BossNodeId ? n.NextNodeIds.Count != 0 : n.NextNodeIds.Count < 1)
            { error = $"节点 {n.NodeId} 出口数量非法"; return false; }
            if (n.NodeId == StartNodeId ? n.PreviousNodeIds.Count != 0 : n.PreviousNodeIds.Count < 1)
            { error = $"节点 {n.NodeId} 入口数量非法"; return false; }
            if (n.NextNodeIds.Count > 1) branches++;
            if (n.PreviousNodeIds.Count > 1) merges++;
            foreach (int next in n.NextNodeIds)
            {
                DungeonGraphNode target = Get(next);
                if (target == null) { error = $"节点 {n.NodeId} 指向不存在的 {next}"; return false; }
                if (target.Column != n.Column + 1) { error = $"边 {n.NodeId}->{next} 未指向相邻右层"; return false; }
                if (n.NodeId != StartNodeId && target.NodeId != BossNodeId
                    && n.MainBranchId != target.MainBranchId)
                { error = $"边 {n.NodeId}->{next} 跨越主分支"; return false; }
                if (!target.PreviousNodeIds.Contains(n.NodeId))
                { error = $"Next/Prev 不一致：{n.NodeId}->{next}"; return false; }
            }
            foreach (int prev in n.PreviousNodeIds)
            {
                DungeonGraphNode source = Get(prev);
                if (source == null || !source.NextNodeIds.Contains(n.NodeId))
                { error = $"Prev/Next 不一致：{prev}->{n.NodeId}"; return false; }
            }
            if (n.MainBranchId >= 0 && n.NextNodeIds.Count > 1) branchSplits[n.MainBranchId]++;
            if (n.MainBranchId >= 0 && n.PreviousNodeIds.Count > 1) branchMerges[n.MainBranchId]++;
        }
        if (branches == 0 || merges == 0) { error = "缺少分叉或汇聚"; return false; }
        DungeonGraphNode start = Get(StartNodeId);
        if (start.NextNodeIds.Count != 3)
        { error = "Start 必须恰好展开三条主分支"; return false; }
        var startBranches = new HashSet<int>();
        foreach (int nextId in start.NextNodeIds) startBranches.Add(Get(nextId).MainBranchId);
        if (startBranches.Count != 3 || !startBranches.Contains(0)
            || !startBranches.Contains(1) || !startBranches.Contains(2))
        { error = "Start 的三个出口未覆盖三条独立主分支"; return false; }
        var bossBranches = new HashSet<int>();
        foreach (int prevId in Get(BossNodeId).PreviousNodeIds)
            bossBranches.Add(Get(prevId).MainBranchId);
        if (bossBranches.Count != 3)
        { error = "Boss 前未汇入全部三条主分支"; return false; }
        for (int branch = 0; branch < 3; branch++)
            if (branchSplits[branch] < 1 || branchMerges[branch] < 1)
            { error = $"主分支 {branch} 缺少局部分叉或回汇"; return false; }

        // 去掉共享的 Start/Boss 后，整张图必须恰好剩下三个互不连通的分量。
        var componentVisited = new HashSet<int>();
        int components = 0;
        foreach (DungeonGraphNode seedNode in Nodes)
        {
            if (seedNode.NodeId == StartNodeId || seedNode.NodeId == BossNodeId
                || componentVisited.Contains(seedNode.NodeId)) continue;
            components++;
            int componentBranch = seedNode.MainBranchId;
            var componentQueue = new Queue<int>();
            componentQueue.Enqueue(seedNode.NodeId);
            componentVisited.Add(seedNode.NodeId);
            while (componentQueue.Count > 0)
            {
                DungeonGraphNode current = Get(componentQueue.Dequeue());
                if (current.MainBranchId != componentBranch)
                { error = "独立分量包含多个主分支"; return false; }
                foreach (int linkedId in current.NextNodeIds)
                    if (linkedId != BossNodeId && componentVisited.Add(linkedId)) componentQueue.Enqueue(linkedId);
                foreach (int linkedId in current.PreviousNodeIds)
                    if (linkedId != StartNodeId && componentVisited.Add(linkedId)) componentQueue.Enqueue(linkedId);
            }
        }
        if (components != 3) { error = $"去掉端点后应有 3 个独立主分支，实际 {components}"; return false; }
        // Start 正向与 Boss 反向都必须覆盖全图。
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
        visited.Clear();
        visited.Add(BossNodeId);
        queue.Enqueue(BossNodeId);
        while (queue.Count > 0)
        {
            DungeonGraphNode cur = Get(queue.Dequeue());
            foreach (int prev in cur.PreviousNodeIds)
                if (visited.Add(prev)) queue.Enqueue(prev);
        }
        if (visited.Count != Nodes.Count) { error = "存在无法抵达 Boss 的死路"; return false; }

        // 层序动态规划：每个节点保存所有到达路径中的最少 Sage/中后段 Shop 数。
        var minSage = new Dictionary<int, int> { [StartNodeId] = 0 };
        var minShop = new Dictionary<int, int> { [StartNodeId] = 0 };
        for (int col = 0; col <= maxColumn; col++)
            foreach (DungeonGraphNode n in Nodes)
            {
                if (n.Column != col || !minSage.TryGetValue(n.NodeId, out int sage)) continue;
                int shop = minShop[n.NodeId];
                foreach (int nextId in n.NextNodeIds)
                {
                    DungeonGraphNode next = Get(nextId);
                    int nextSage = sage + (next.Type == NodeType.Sage ? 1 : 0);
                    int nextShop = shop + (next.Type == NodeType.Shop && next.Column >= maxColumn / 2 ? 1 : 0);
                    if (!minSage.TryGetValue(nextId, out int oldSage) || nextSage < oldSage)
                        minSage[nextId] = nextSage;
                    if (!minShop.TryGetValue(nextId, out int oldShop) || nextShop < oldShop)
                        minShop[nextId] = nextShop;
                }
            }
        if (minSage[BossNodeId] < 3 || minShop[BossNodeId] < 1)
        { error = "存在缺少 3 次贤者或中后段商店机会的路径"; return false; }
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
        DungeonGraphNode boss = Get(BossNodeId);
        if (boss != null) boss.Discovered = true;
        foreach (int id in start.NextNodeIds)
        {
            DungeonGraphNode t = Get(id);
            if (t != null) t.Discovered = true;
        }
    }
}
