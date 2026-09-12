using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// v2.0.5 DAG 图门禁（V2 §10.2/§23.1）：结构合法性（无环/单向/唯一起汇/可达/双向一致）、
/// 列规模契约、类型间距、seed 复现、退化保底。200 seed 批量 + 定向案例。
/// </summary>
public class DungeonGraphGeneratorTests
{
    [Test]
    public void Graphs_AreValidDags_AcrossSeeds()
    {
        var rngMeta = new System.Random(20260910);
        for (int i = 0; i < 200; i++)
        {
            int seed = rngMeta.Next();
            DungeonGraphData graph = DungeonGraphGenerator.Generate(seed);
            Assert.IsTrue(graph.Validate(out string error), $"seed={seed}: {error}");
            Assert.GreaterOrEqual(graph.Nodes.Count, 5, $"seed={seed} 节点数低于 MVP 下限");

            int maxColumn = int.MinValue;
            foreach (DungeonGraphNode n in graph.Nodes) maxColumn = System.Math.Max(maxColumn, n.Column);
            Assert.AreEqual(maxColumn, graph.Get(graph.BossNodeId).Column, $"seed={seed} Boss 不在最末列");
            Assert.AreEqual(0, graph.Get(graph.StartNodeId).Column, $"seed={seed} Start 不在列 0");
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
            Assert.AreEqual(a.Nodes[i].Type, b.Nodes[i].Type);
            CollectionAssert.AreEqual(a.Nodes[i].NextNodeIds, b.Nodes[i].NextNodeIds);
        }
    }

    [Test]
    public void Graphs_SpecialTypeSpacing_Enforced()
    {
        var rngMeta = new System.Random(7);
        for (int s = 0; s < 80; s++)
        {
            DungeonGraphData graph = DungeonGraphGenerator.Generate(rngMeta.Next());
            var lastColumn = new Dictionary<NodeType, int>();
            foreach (DungeonGraphNode n in graph.Nodes)
            {
                if (n.Type == NodeType.Start || n.Type == NodeType.Boss || n.Type == NodeType.Combat) continue;
                Assert.IsFalse(
                    lastColumn.TryGetValue(n.Type, out int last) && n.Column - last < 2,
                    $"特殊类型 {n.Type} 列距不足（{last}->{n.Column}）");
                lastColumn[n.Type] = n.Column;
            }
        }
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
