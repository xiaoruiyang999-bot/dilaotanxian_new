using System;
using System.Collections.Generic;

/// <summary>v2.1.1 固定三主分支路线图。主分支之间只共享 Start/Boss，内部可短暂分叉并回汇。</summary>
public static class DungeonGraphGenerator
{
    public const int CurrentGenerationVersion = 3;
    private const int MinColumns = 10;
    private const int MaxColumns = 12;
    private const int MainBranchCount = 3;
    private const double SecondForkChance = 0.55;

    private struct ForkSegment
    {
        public int Start;
        public int End;
        public ForkSegment(int start, int end) { Start = start; End = end; }
        public bool Contains(int column) => column >= Start && column <= End;
    }

    public static DungeonGraphData Generate(int seed, Random rngOverride = null)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Random rng = rngOverride ?? new Random(unchecked(seed * 7919 + attempt * 104729));
            DungeonGraphData graph = Build(seed, rng, false);
            if (graph.Validate(out _)) return graph;
        }
        return Build(seed, new Random(unchecked(seed ^ 0x52111003)), true);
    }

    private static DungeonGraphData Build(int seed, Random rng, bool fallback)
    {
        int columns = fallback ? MinColumns : rng.Next(MinColumns, MaxColumns + 1);
        var graph = new DungeonGraphData { GenerationVersion = CurrentGenerationVersion, Seed = seed };
        var forks = new List<ForkSegment>[MainBranchCount];
        for (int branch = 0; branch < MainBranchCount; branch++)
            forks[branch] = BuildForks(columns, rng, fallback, branch);

        var main = new DungeonGraphNode[columns, MainBranchCount];
        var side = new DungeonGraphNode[columns, MainBranchCount];
        int nextId = 0;
        var start = NewNode(nextId++, 0, 0, -1, 0);
        graph.Nodes.Add(start);
        graph.StartNodeId = start.NodeId;

        for (int col = 1; col < columns - 1; col++)
            for (int branch = 0; branch < MainBranchCount; branch++)
            {
                main[col, branch] = NewNode(nextId++, col, branch * 2, branch, 0);
                graph.Nodes.Add(main[col, branch]);
                if (!IsForkColumn(forks[branch], col)) continue;
                side[col, branch] = NewNode(nextId++, col, branch * 2 + 1, branch, 1);
                graph.Nodes.Add(side[col, branch]);
            }

        var boss = NewNode(nextId++, columns - 1, 0, -1, 0);
        graph.Nodes.Add(boss);
        graph.BossNodeId = boss.NodeId;

        for (int branch = 0; branch < MainBranchCount; branch++) Link(start, main[1, branch]);
        for (int col = 1; col < columns - 2; col++)
            for (int branch = 0; branch < MainBranchCount; branch++)
            {
                DungeonGraphNode currentMain = main[col, branch];
                DungeonGraphNode nextMain = main[col + 1, branch];
                DungeonGraphNode currentSide = side[col, branch];
                DungeonGraphNode nextSide = side[col + 1, branch];
                Link(currentMain, nextMain);
                if (currentSide == null && nextSide != null) Link(currentMain, nextSide);
                else if (currentSide != null && nextSide != null) Link(currentSide, nextSide);
                else if (currentSide != null) Link(currentSide, nextMain);
            }
        for (int branch = 0; branch < MainBranchCount; branch++)
            Link(main[columns - 2, branch], boss);

        int shopColumn = columns >= 11 && rng.Next(2) == 1 ? 6 : 5;
        foreach (DungeonGraphNode node in graph.Nodes)
        {
            int col = node.Column;
            node.Type = col == 0 ? NodeType.Start
                : col == columns - 1 ? NodeType.Boss
                : col == 2 || col == 4 || col == 7 ? NodeType.Sage
                : col == shopColumn ? NodeType.Shop
                : fallback || rng.NextDouble() >= 0.18 ? NodeType.Supply : NodeType.Sage;
            node.RoomArchetypeId = node.Type.ToString();
        }
        AssignNodeSeeds(graph);
        graph.RevealInitial();
        return graph;
    }

    private static List<ForkSegment> BuildForks(int columns, Random rng, bool fallback, int branch)
    {
        var result = new List<ForkSegment>();
        if (fallback)
        {
            int start = 2 + branch;
            result.Add(new ForkSegment(start, start + 1));
            return result;
        }
        AddRandomFork(result, columns, rng, true);
        if (rng.NextDouble() < SecondForkChance) AddRandomFork(result, columns, rng, false);
        return result;
    }

    private static void AddRandomFork(List<ForkSegment> segments, int columns, Random rng, bool required)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            int start = rng.Next(2, columns - 3);
            int maxLength = Math.Min(3, columns - 2 - start);
            int end = start + rng.Next(1, maxLength + 1) - 1;
            bool separated = true;
            foreach (ForkSegment existing in segments)
                if (start <= existing.End + 1 && end >= existing.Start - 1) { separated = false; break; }
            if (!separated) continue;
            segments.Add(new ForkSegment(start, end));
            return;
        }
        if (required && segments.Count == 0) segments.Add(new ForkSegment(2, 3));
    }

    private static bool IsForkColumn(List<ForkSegment> segments, int column)
    {
        foreach (ForkSegment segment in segments) if (segment.Contains(column)) return true;
        return false;
    }

    private static DungeonGraphNode NewNode(int id, int column, int row, int branch, int lane)
        => new DungeonGraphNode
        {
            NodeId = id, Column = column, Row = row,
            MainBranchId = branch, BranchLane = lane,
        };

    private static void Link(DungeonGraphNode from, DungeonGraphNode to)
    {
        if (from.NextNodeIds.Contains(to.NodeId)) return;
        from.NextNodeIds.Add(to.NodeId);
        to.PreviousNodeIds.Add(from.NodeId);
    }

    private static void AssignNodeSeeds(DungeonGraphData graph)
    {
        foreach (DungeonGraphNode node in graph.Nodes)
        {
            node.RoomSeed = DeriveSeed(graph.Seed, node.NodeId, 1);
            node.EncounterSeed = DeriveSeed(graph.Seed, node.NodeId, 2);
            node.RewardSeed = DeriveSeed(graph.Seed, node.NodeId, 3);
            node.ShopSeed = DeriveSeed(graph.Seed, node.NodeId, 4);
        }
    }

    private static int DeriveSeed(int graphSeed, int nodeId, int stream)
    {
        unchecked
        {
            uint value = (uint)graphSeed ^ ((uint)nodeId + 1u) * 0x9E3779B9u ^ (uint)stream * 0x85EBCA6Bu;
            value ^= value >> 16; value *= 0x7FEB352Du;
            value ^= value >> 15; value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (int)(value & 0x7FFFFFFFu);
        }
    }
}
