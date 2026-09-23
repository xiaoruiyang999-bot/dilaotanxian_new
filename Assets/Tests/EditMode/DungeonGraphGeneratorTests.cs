using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// v2.0.5 DAG 图门禁（V2 §10.2/§23.1）：结构合法性（无环/单向/唯一起汇/可达/双向一致）、
/// 列规模、每路径机会、seed 复现与退化保底。10,000 seed 批量 + 定向案例。
/// </summary>
public class DungeonGraphGeneratorTests
{
    [Test]
    public void Graphs_AreValidDags_AcrossSeeds()
    {
        var rngMeta = new System.Random(20260910);
        for (int i = 0; i < 10000; i++)
        {
            int seed = rngMeta.Next();
            DungeonGraphData graph = DungeonGraphGenerator.Generate(seed);
            Assert.IsTrue(graph.Validate(out string error), $"seed={seed}: {error}");
            Assert.GreaterOrEqual(graph.Nodes.Count, 29, $"seed={seed} 三主分支节点过少");
            Assert.AreEqual(DungeonGraphGenerator.CurrentGenerationVersion, graph.GenerationVersion);

            int maxColumn = int.MinValue;
            foreach (DungeonGraphNode n in graph.Nodes) maxColumn = System.Math.Max(maxColumn, n.Column);
            Assert.AreEqual(maxColumn, graph.Get(graph.BossNodeId).Column, $"seed={seed} Boss 不在最末列");
            Assert.AreEqual(0, graph.Get(graph.StartNodeId).Column, $"seed={seed} Start 不在列 0");
            Assert.AreEqual(3, graph.Get(graph.StartNodeId).NextNodeIds.Count,
                $"seed={seed} 起点应展开三条初始轨道");

            var counts = new Dictionary<int, int>();
            foreach (DungeonGraphNode n in graph.Nodes)
            {
                counts.TryGetValue(n.Column, out int count);
                counts[n.Column] = count + 1;
            }
            for (int col = 1; col < maxColumn; col++)
                Assert.That(counts[col], Is.InRange(3, 6), $"seed={seed} 第 {col} 层节点数非法");

            var splitsByBranch = new int[3];
            foreach (DungeonGraphNode n in graph.Nodes)
            {
                if (n.MainBranchId >= 0 && n.NextNodeIds.Count == 2)
                    splitsByBranch[n.MainBranchId]++;
                foreach (int nextId in n.NextNodeIds)
                {
                    DungeonGraphNode next = graph.Get(nextId);
                    if (n.Column == 0 || next.Column == maxColumn) continue;
                    Assert.AreEqual(n.MainBranchId, next.MainBranchId,
                        $"seed={seed} 边 {n.NodeId}->{nextId} 跨越主分支");
                }
            }
            for (int branch = 0; branch < 3; branch++)
                Assert.That(splitsByBranch[branch], Is.InRange(1, 2),
                    $"seed={seed} 主分支 {branch} 应有 1～2 个局部分叉");

            // 选择任意一条主分支后，所有可达的非 Boss 节点必须始终属于该主分支。
            foreach (int firstId in graph.Get(graph.StartNodeId).NextNodeIds)
            {
                int branch = graph.Get(firstId).MainBranchId;
                var reached = new HashSet<int> { firstId };
                var queue = new Queue<int>();
                queue.Enqueue(firstId);
                while (queue.Count > 0)
                    foreach (int nextId in graph.Get(queue.Dequeue()).NextNodeIds)
                    {
                        if (nextId == graph.BossNodeId) continue;
                        Assert.AreEqual(branch, graph.Get(nextId).MainBranchId,
                            $"seed={seed} 进入主分支 {branch} 后可切换到其他主分支");
                        if (reached.Add(nextId)) queue.Enqueue(nextId);
                    }
            }
        }
    }

    [Test]
    public void Graphs_HaveBranchAndMerge()
    {
        int branches = 0, merges = 0, checkedSeeds = 0;
        var rngMeta = new System.Random(42);
        while (checkedSeeds < 60)
        {
            DungeonGraphData graph = DungeonGraphGenerator.Generate(rngMeta.Next());
            checkedSeeds++;
            foreach (DungeonGraphNode n in graph.Nodes)
            {
                if (n.NextNodeIds.Count >= 2) branches++;
                if (n.PreviousNodeIds.Count >= 2) merges++;
            }
        }
        Assert.Greater(branches, 0, "60 张图应存在分叉节点（否则 DAG 选择性缺失）");
        Assert.Greater(merges, 0, "60 张图应存在汇聚节点");
    }

    [Test]
    public void Graphs_SameSeed_Deterministic()
    {
        DungeonGraphData a = DungeonGraphGenerator.Generate(99991);
        DungeonGraphData b = DungeonGraphGenerator.Generate(99991);
        Assert.AreEqual(a.Nodes.Count, b.Nodes.Count);
        for (int i = 0; i < a.Nodes.Count; i++)
        {
            Assert.AreEqual(a.Nodes[i].Column, b.Nodes[i].Column);
            Assert.AreEqual(a.Nodes[i].Row, b.Nodes[i].Row);
            Assert.AreEqual(a.Nodes[i].MainBranchId, b.Nodes[i].MainBranchId);
            Assert.AreEqual(a.Nodes[i].BranchLane, b.Nodes[i].BranchLane);
            Assert.AreEqual(a.Nodes[i].Type, b.Nodes[i].Type);
            CollectionAssert.AreEqual(a.Nodes[i].NextNodeIds, b.Nodes[i].NextNodeIds);
            Assert.AreEqual(a.Nodes[i].RoomSeed, b.Nodes[i].RoomSeed);
            Assert.AreEqual(a.Nodes[i].EncounterSeed, b.Nodes[i].EncounterSeed);
            Assert.AreEqual(a.Nodes[i].RewardSeed, b.Nodes[i].RewardSeed);
            Assert.AreEqual(a.Nodes[i].ShopSeed, b.Nodes[i].ShopSeed);
        }
        Assert.AreEqual(a.GenerationVersion, b.GenerationVersion);
    }

    [Test]
    public void Node_RequiresRewardResolutionBeforeCompletion()
    {
        DungeonGraphData graph = DungeonGraphGenerator.Generate(20260917);
        DungeonGraphNode start = graph.Get(graph.StartNodeId);
        Assert.IsTrue(start.TrySelect());
        Assert.IsTrue(start.TryVisit());
        Assert.IsFalse(start.TryComplete(), "进入房间不等于完成节点");
        Assert.IsTrue(start.TryCompleteObjective());
        Assert.IsFalse(start.TryComplete(), "目标完成但未领奖时出口保持锁定");
        Assert.IsTrue(start.TryResolveReward());
        Assert.IsTrue(start.TryComplete());
        Assert.IsFalse(start.TrySelect(), "完成后不能再次进入");
    }

    [Test]
    public void Graphs_OnlyOfferV21NodeTypes_AndBossIsPublic()
    {
        var rngMeta = new System.Random(7);
        for (int s = 0; s < 80; s++)
        {
            DungeonGraphData graph = DungeonGraphGenerator.Generate(rngMeta.Next());
            foreach (DungeonGraphNode n in graph.Nodes)
            {
                Assert.IsTrue(n.Type == NodeType.Start || n.Type == NodeType.Boss
                    || n.Type == NodeType.Sage || n.Type == NodeType.Supply || n.Type == NodeType.Shop);
                Assert.IsFalse(string.IsNullOrEmpty(n.RoomArchetypeId));
            }
            Assert.IsTrue(graph.Get(graph.BossNodeId).Discovered);
        }
    }

    [Test]
    public void Route_RejectsSkippingBacktrackingAndUnclaimedRewards()
    {
        var run = SaveService.CreateNewRun(20260918, PlayableCharacterId.Werewolf);
        Assert.IsTrue(DungeonRouteRules.TryInitialize(run));
        DungeonGraphNode start = DungeonRouteRules.Current(run);
        Assert.AreEqual(run.dungeonGraph.StartNodeId, run.currentNodeId,
            "新 Run 必须从 Start 房开始，不能预选第一条主分支");
        Assert.IsTrue(start.Selected);
        Assert.IsTrue(start.Visited);
        foreach (int successorId in start.NextNodeIds)
        {
            DungeonGraphNode successor = run.dungeonGraph.Get(successorId);
            Assert.IsFalse(successor.Selected, $"起点后继 {successorId} 不得被自动选择");
            Assert.IsFalse(successor.Visited, $"起点后继 {successorId} 不得被自动访问");
        }
        int nextId = start.NextNodeIds[0];
        Assert.IsFalse(DungeonRouteRules.TryChooseNext(run, nextId));
        Assert.IsTrue(DungeonRouteRules.TryCompleteObjective(run, start.NodeId));
        Assert.IsFalse(DungeonRouteRules.TryChooseNext(run, nextId));
        Assert.IsTrue(DungeonRouteRules.TryResolveReward(run, start.NodeId));
        Assert.IsTrue(DungeonRouteRules.TryChooseNext(run, nextId));
        Assert.IsFalse(DungeonRouteRules.TryChooseNext(run, start.NodeId));
        Assert.IsFalse(DungeonRouteRules.TryChooseNext(run, nextId));
    }

    [Test]
    public void PendingLegacyRoute_ReplacesPreviewGraphAndPreservesResources()
    {
        var run = SaveService.CreateNewRun(81527, PlayableCharacterId.Werewolf);
        run.currentHp = 37;
        run.currentArmor = 12;
        run.currentMana = 8f;
        run.currentClassResource = 41f;
        run.runCoins = 23;
        run.killsThisRun = 6;
        run.relicIds.Add("legacy-relic");
        run.dungeonGraph = new DungeonGraphData();
        Assert.IsTrue(DungeonRouteRules.TryInitialize(run));
        Assert.AreEqual(run.dungeonGraph.StartNodeId, run.currentNodeId);
        Assert.IsFalse(run.routeInitializationPending);
        Assert.AreEqual(37, run.currentHp);
        Assert.AreEqual(12, run.currentArmor);
        Assert.AreEqual(8f, run.currentMana);
        Assert.AreEqual(41f, run.currentClassResource);
        Assert.AreEqual(23, run.runCoins);
        Assert.AreEqual(6, run.killsThisRun);
        CollectionAssert.AreEqual(new[] { "legacy-relic" }, run.relicIds);
    }

    [Test]
    public void SparseGraphVersion_RebuildsFromStartAndPreservesResources()
    {
        var run = SaveService.CreateNewRun(90210, PlayableCharacterId.Werewolf);
        Assert.IsTrue(DungeonRouteRules.TryInitialize(run));
        run.currentHp = 29;
        run.runCoins = 51;
        run.dungeonGraph.GenerationVersion = DungeonGraphGenerator.CurrentGenerationVersion - 1;
        run.routeInitializationPending = false;

        Assert.IsTrue(DungeonRouteRules.NeedsInitialization(run));
        Assert.IsTrue(DungeonRouteRules.TryInitialize(run));
        Assert.AreEqual(DungeonGraphGenerator.CurrentGenerationVersion,
            run.dungeonGraph.GenerationVersion);
        Assert.AreEqual(run.dungeonGraph.StartNodeId, run.currentNodeId);
        Assert.AreEqual(29, run.currentHp);
        Assert.AreEqual(51, run.runCoins);
    }

    [Test]
    public void Graphs_RevealRule_StartAndSuccessorsDiscovered()
    {
        DungeonGraphData graph = DungeonGraphGenerator.Generate(123);
        DungeonGraphNode start = graph.Get(graph.StartNodeId);
        Assert.IsTrue(start.Discovered, "起点必须已揭示");
        foreach (int id in start.NextNodeIds)
            Assert.IsTrue(graph.Get(id).Discovered, $"起点后继 {id} 必须已揭示");

        bool anyHidden = false;
        foreach (DungeonGraphNode n in graph.Nodes)
            if (!n.Discovered) { anyHidden = true; break; }
        Assert.IsTrue(anyHidden, "远处节点应保持未揭示（完整连接可见、类型未知规则）");
    }

    private static bool CheckAnyFarUndiscovered(DungeonGraphData graph)
    {
        foreach (DungeonGraphNode n in graph.Nodes)
            if (!n.Discovered) return true;
        return false;
    }
}
