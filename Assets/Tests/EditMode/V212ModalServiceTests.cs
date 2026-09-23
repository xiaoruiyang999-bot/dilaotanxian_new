using NUnit.Framework;

public class V212ModalServiceTests
{
    [Test]
    public void NestedModal_ClosingTopRestoresPreviousLayerWithoutResumingWorld()
    {
        var stack = new GameModalStack();
        GameModalToken pause = stack.Push(
            GameModalKind.PauseMenu, pausesWorld: true, blocksWorldInput: true, cancel: null);
        GameModalToken settings = stack.Push(
            GameModalKind.PauseSettings, pausesWorld: true, blocksWorldInput: true, cancel: null);

        Assert.AreEqual(2, stack.Count);
        Assert.AreEqual(GameModalKind.PauseSettings, stack.TopKind);
        Assert.IsTrue(stack.PausesWorld);
        Assert.IsTrue(stack.BlocksWorldInput);

        Assert.IsTrue(stack.Release(settings));
        Assert.AreEqual(GameModalKind.PauseMenu, stack.TopKind);
        Assert.IsTrue(stack.PausesWorld, "关闭内层设置时不得恢复战斗");
        Assert.IsTrue(stack.BlocksWorldInput);

        Assert.IsTrue(stack.Release(pause));
        Assert.IsFalse(stack.PausesWorld);
        Assert.IsFalse(stack.BlocksWorldInput);
    }

    [Test]
    public void Cancel_OnlyClosesTopAndNeverLeaksToWorld()
    {
        var stack = new GameModalStack();
        int lowerCancelCount = 0;
        int upperCancelCount = 0;
        GameModalToken lower = default;
        GameModalToken upper = default;
        lower = stack.Push(GameModalKind.PauseMenu, true, true, () =>
        {
            lowerCancelCount++;
            stack.Release(lower);
        });
        upper = stack.Push(GameModalKind.SkillTree, true, true, () =>
        {
            upperCancelCount++;
            stack.Release(upper);
        });

        Assert.IsTrue(stack.TryCancelTop());
        Assert.AreEqual(1, upperCancelCount);
        Assert.AreEqual(0, lowerCancelCount);
        Assert.AreEqual(GameModalKind.PauseMenu, stack.TopKind);

        Assert.IsTrue(stack.TryCancelTop());
        Assert.AreEqual(1, lowerCancelCount);
        Assert.IsFalse(stack.HasModal);
        Assert.IsFalse(stack.TryCancelTop());
    }

    [Test]
    public void NonCancelableModalStillConsumesCancel()
    {
        var stack = new GameModalStack();
        stack.Push(GameModalKind.Inventory, true, true, null);

        Assert.IsTrue(stack.TryCancelTop());
        Assert.IsTrue(stack.HasModal);
    }

    [Test]
    public void HitStop_LongestRequestWinsAndModalPauseHasPriority()
    {
        var arbiter = new TimePauseArbiter();
        arbiter.RequestHitStop(10f, 0.05f);
        arbiter.RequestHitStop(10.01f, 0.02f);
        Assert.AreEqual(10.05f, arbiter.HitStopUntil, 0.0001f);
        Assert.IsTrue(arbiter.ShouldPause(false, 10.049f));
        Assert.IsFalse(arbiter.ShouldPause(false, 10.05f));
        Assert.IsTrue(arbiter.ShouldPause(true, 999f), "UI 模态暂停不能被 Hit Stop 到期覆盖");
    }

    [Test]
    public void RewardCandidates_ReopenFromSameSeedRemainStable()
    {
        var first = NodeRewardService.Roll(212031, false);
        var reopened = NodeRewardService.Roll(212031, false);

        Assert.AreEqual(first.Count, reopened.Count);
        for (int i = 0; i < first.Count; i++)
        {
            Assert.AreEqual(first[i].Kind, reopened[i].Kind);
            Assert.AreEqual(first[i].Value, reopened[i].Value);
            Assert.AreEqual(first[i].Title, reopened[i].Title);
        }
    }

    [Test]
    public void RouteChoice_RemainsLockedUntilRewardResolved()
    {
        var current = new DungeonGraphNode
        {
            NodeId = 1,
            Type = NodeType.Sage,
            Discovered = true,
            Selected = true,
            Visited = true,
            ObjectiveCompleted = true,
            RewardResolved = false,
            Completed = false,
            NextNodeIds = { 2 },
        };
        var next = new DungeonGraphNode
        {
            NodeId = 2,
            Type = NodeType.Supply,
            Discovered = true,
            PreviousNodeIds = { 1 },
        };
        var run = new SaveService.ActiveRunData
        {
            currentNodeId = 1,
            dungeonGraph = new DungeonGraphData { Nodes = { current, next } },
        };

        Assert.IsFalse(DungeonRouteRules.CanChooseNext(run, 2));
        Assert.IsTrue(DungeonRouteRules.TryResolveReward(run, 1));
        Assert.IsTrue(DungeonRouteRules.CanChooseNext(run, 2));
    }
}
