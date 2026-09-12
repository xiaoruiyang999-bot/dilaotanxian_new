using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// v2.0.7 叙事碎片门禁（V2 §2.3）：必得队列推进（读后不复投）、开场队列、
/// 已读持久、全读完退场。PlayerPrefs 测试隔离 Setup/TearDown。
/// </summary>
public class NarrativeServiceTests
{
    [SetUp]
    public void SetUp() => NarrativeService.ResetAll();

    [TearDown]
    public void TearDown() => NarrativeService.ResetAll();

    [Test]
    public void Opening_SequencePending_WhenUnread()
    {
        List<NarrativeService.Fragment> opening = NarrativeService.PendingOpening();
        Assert.GreaterOrEqual(opening.Count, 2, "开场至少两段（苏醒/声音）");
        Assert.IsTrue(opening.All(f => f.IsOpening));
    }

    [Test]
    public void RequiredProgress_AdvancesAndDoesNotRepeat()
    {
        NarrativeService.Fragment first = NarrativeService.NextRequiredProgress();
        Assert.NotNull(first, "未读时必有保底碎片");
        Assert.IsFalse(first.IsOpening, "进展碎片不与开场混队");

        NarrativeService.MarkRead(first.Id);
        NarrativeService.Fragment second = NarrativeService.NextRequiredProgress();
        Assert.AreNotEqual(first.Id, second?.Id, "读后不复投（队列推进）");
    }

    [Test]
    public void MarkRead_Idempotent()
    {
        NarrativeService.MarkRead("prog_lamp");
        NarrativeService.MarkRead("prog_lamp");
        Assert.AreEqual(1, NarrativeService.ReadIds().Count);
    }

    [Test]
    public void AllRead_RequiredReturnsNull()
    {
        foreach (NarrativeService.Fragment f in NarrativeService.Library)
            NarrativeService.MarkRead(f.Id);
        Assert.IsNull(NarrativeService.NextRequiredProgress(), "全读完不再强制投放（多局重释阶段）");
        Assert.AreEqual(0, NarrativeService.PendingOpening().Count);
    }

    [Test]
    public void Library_HasOpeningAndProgress()
    {
        Assert.GreaterOrEqual(NarrativeService.Library.Count(f => f.IsOpening), 2);
        Assert.GreaterOrEqual(NarrativeService.Library.Count(f => !f.IsOpening), 4, "进展碎片至少 4 条");
        foreach (NarrativeService.Fragment f in NarrativeService.Library)
        {
            Assert.IsNotEmpty(f.Id);
            Assert.IsNotEmpty(f.Title);
            Assert.GreaterOrEqual(f.Lines.Length, 1);
        }
    }
}
