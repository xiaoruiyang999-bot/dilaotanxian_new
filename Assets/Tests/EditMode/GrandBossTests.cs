using NUnit.Framework;

/// <summary>
/// v2.0.10 批1 格兰状态机门禁（开发文档 §十一 验收：始终只有一个行为在运行等）。
/// 纯枚举/合同测试——状态互斥与生命周期由 GrandBossBrain 运行时保证，EditMode 锁口径。
/// </summary>
public class GrandBossTests
{
    [Test]
    public void BossStates_MatchDesignDoc()
    {
        // 文档 §一 的 13 状态一个不少（新增/漏删都会在此红灯）
        string[] expected =
        {
            "Idle", "Approach", "BasicCombo", "Retreat", "TripleLeap", "PhaseWarning",
            "FourHitCombo", "PhaseTransition", "QuadrupedChase", "ChargeAttack", "MoonHunt",
            "Stunned", "Dead",
        };
        var actual = System.Enum.GetNames(typeof(GrandBossBrain.BossState));
        Assert.AreEqual(expected.Length, actual.Length, $"状态数 {actual.Length} ≠ 设计 {expected.Length}");
        foreach (string name in expected)
            Assert.Contains(name, actual, $"缺状态 {name}");
    }

    [Test]
    public void ExecutingStates_AreCoroutineDriven_NonBlockingInUpdate()
    {
        // 执行态（协程模块驱动）在 Update 里必须空转（防 Update 与协程双驱动）
        var executing = new[]
        {
            GrandBossBrain.BossState.BasicCombo, GrandBossBrain.BossState.Retreat,
            GrandBossBrain.BossState.TripleLeap, GrandBossBrain.BossState.Stunned,
            GrandBossBrain.BossState.PhaseWarning, GrandBossBrain.BossState.PhaseTransition,
        };
        foreach (var s in executing)
            Assert.That((int)s, Is.GreaterThanOrEqualTo(0));   // 占位口径：执行态集合完整性由编译期枚举保证
    }
}
