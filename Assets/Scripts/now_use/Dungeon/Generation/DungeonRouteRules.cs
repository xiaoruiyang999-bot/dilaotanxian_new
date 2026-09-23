/// <summary>ActiveRun 中的路线状态机。调用者负责在成功状态转换后保存。</summary>
public static class DungeonRouteRules
{
    public static bool NeedsInitialization(SaveService.ActiveRunData run)
        => run != null && (run.routeInitializationPending || run.dungeonGraph == null
            || run.dungeonGraph.GenerationVersion != DungeonGraphGenerator.CurrentGenerationVersion);

    public static bool TryInitialize(SaveService.ActiveRunData run)
    {
        if (!NeedsInitialization(run))
            return false;
        if (run.mainSeed == 0) run.mainSeed = 0x21011001;
        DungeonGraphData graph = DungeonGraphGenerator.Generate(run.mainSeed);
        if (!graph.Validate(out _)) return false;
        DungeonGraphNode start = graph.Get(graph.StartNodeId);
        if (start == null || !start.TrySelect() || !start.TryVisit()) return false;
        run.dungeonGraph = graph;
        run.currentNodeId = start.NodeId;
        run.routeInitializationPending = false;
        return true;
    }

    public static DungeonGraphNode Current(SaveService.ActiveRunData run)
        => run?.dungeonGraph?.Get(run.currentNodeId);

    public static bool TryCompleteObjective(SaveService.ActiveRunData run, int nodeId)
    {
        DungeonGraphNode node = Current(run);
        return node != null && node.NodeId == nodeId && node.TryCompleteObjective();
    }

    public static bool TryResolveReward(SaveService.ActiveRunData run, int nodeId)
    {
        DungeonGraphNode node = Current(run);
        if (node == null || node.NodeId != nodeId || !node.TryResolveReward()) return false;
        return node.TryComplete();
    }

    public static bool CanChooseNext(SaveService.ActiveRunData run, int nextNodeId)
    {
        DungeonGraphNode current = Current(run);
        DungeonGraphNode next = run?.dungeonGraph?.Get(nextNodeId);
        return current != null && next != null && current.Completed
            && current.NextNodeIds.Contains(nextNodeId) && next.Discovered
            && !next.Selected && !next.Visited && !next.Completed;
    }

    public static bool TryChooseNext(SaveService.ActiveRunData run, int nextNodeId)
    {
        if (!CanChooseNext(run, nextNodeId)) return false;
        DungeonGraphNode next = run.dungeonGraph.Get(nextNodeId);
        if (!next.TrySelect() || !next.TryVisit()) return false;
        run.currentNodeId = nextNodeId;
        foreach (int successorId in next.NextNodeIds)
        {
            DungeonGraphNode successor = run.dungeonGraph.Get(successorId);
            if (successor != null) successor.Discovered = true;
        }
        return true;
    }
}
