using NUnit.Framework;
using UnityEngine;

/// <summary>
/// VS 第三批 状态系统门禁（V2 §8.2）：常量合同（叠层上限/周期/数值）——
/// 运行行为（协程 tick/协程自灭/OnDisable 清理）依赖 Play，EditMode 锁数值口径防手改漂移。
/// </summary>
public class EnemyStatusTests
{
    [Test]
    public void Constants_MatchVSDesignValues()
    {
        Assert.AreEqual(5, EnemyStatus.BleedMaxStacks, "流血叠层上限（V2 §8.2 多次命中叠层）");
        Assert.AreEqual(2f, EnemyStatus.BleedDamagePerTick, "每层每跳伤害");
        Assert.AreEqual(0.8f, EnemyStatus.BleedTickInterval, "流血跳周期");
        Assert.AreEqual(4f, EnemyStatus.BleedLayerDuration, "单层持续时间");
        Assert.AreEqual(6f, EnemyStatus.ArmorBreakReduction, "破甲减量");
        Assert.AreEqual(5f, EnemyStatus.ArmorBreakDuration, "破甲持续时间");
    }

    [Test]
    public void Ensure_IdempotentOnSameGameObject()
    {
        var go = new GameObject("StatusHost");
        try
        {
            EnemyStatus a = EnemyStatus.Ensure(go.transform);
            EnemyStatus b = EnemyStatus.Ensure(go.transform);
            Assert.AreSame(a, b, "Ensure 幂等（不重复挂容器）");
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void Apply_OnDeadOrMissingHealth_NoThrow()
    {
        var go = new GameObject("BareHost");   // 无 EnemyHealth
        try
        {
            EnemyStatus s = EnemyStatus.Ensure(go.transform);
            Assert.DoesNotThrow(() => s.ApplyBleed(), "无健康组件静默不炸");
            Assert.DoesNotThrow(() => s.ApplyArmorBreak());
            Assert.AreEqual(0, s.BleedStacks, "无效目标不挂层");
        }
        finally { Object.DestroyImmediate(go); }
    }
}
