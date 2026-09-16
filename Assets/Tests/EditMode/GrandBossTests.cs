using NUnit.Framework;
using UnityEngine;
using UnityEditor;

public class GrandBossTests
{
    [Test]
    public void BossStates_MatchDesignContract()
    {
        string[] expected =
        {
            "Idle", "Approach", "BasicCombo", "Retreat", "TripleLeap", "PhaseWarning",
            "FourHitCombo", "PhaseTransition", "QuadrupedChase", "ChargeAttack", "MoonHunt",
            "Stunned", "Dead",
        };
        CollectionAssert.AreEquivalent(expected, System.Enum.GetNames(typeof(GrandBossBrain.BossState)));
    }

    [TestCase(0.56f, false)]
    [TestCase(0.55f, true)]
    [TestCase(0.51f, true)]
    [TestCase(0.50f, false)]
    public void PhaseWarning_OnlyOccursInsideLeadWindow(float ratio, bool expected)
    {
        Assert.AreEqual(expected,
            BossPhaseController.ShouldSendWarning(ratio, 0.50f, 0.05f, false, false));
    }

    [Test]
    public void PhaseCommands_AreIdempotent()
    {
        Assert.IsFalse(BossPhaseController.ShouldSendWarning(0.53f, 0.50f, 0.05f, true, false));
        Assert.IsFalse(BossPhaseController.ShouldSendWarning(0.53f, 0.50f, 0.05f, false, true));
        Assert.IsTrue(BossPhaseController.ShouldEnterPhaseTwo(0.50f, 0.50f, false));
        Assert.IsFalse(BossPhaseController.ShouldEnterPhaseTwo(0.25f, 0.50f, true));
    }

    [Test]
    public void TripleLeapOutcome_IsDeterministic()
    {
        Assert.AreEqual(GrandTripleLeap.Outcome.FollowUpCombo, GrandTripleLeap.ResolveOutcome(true));
        Assert.AreEqual(GrandTripleLeap.Outcome.Stunned, GrandTripleLeap.ResolveOutcome(false));
    }

    [Test]
    public void RuntimeAttackAssets_HaveUsableTargetLayers()
    {
        AttackData right = Resources.Load<AttackData>("Data/AttackData_GrandRightClaw");
        AttackData left = Resources.Load<AttackData>("Data/AttackData_GrandLeftClaw");
        AttackData leap = Resources.Load<AttackData>("Data/AttackData_GrandLeapLand");

        Assert.NotNull(right);
        Assert.NotNull(left);
        Assert.NotNull(leap);
        Assert.AreNotEqual(0, right.TargetLayer.value);
        Assert.AreNotEqual(0, left.TargetLayer.value);
        Assert.AreNotEqual(0, leap.TargetLayer.value);
        Assert.AreNotEqual(0, leap.TargetLayer.value & (1 << LayerMask.NameToLayer("Default")),
            "玩家 Prefab 当前位于 Default 层，跃击资产必须包含该层。");
    }

    [Test]
    public void BossPrefab_ContainsFormalGrandComponents()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy_Boss.prefab");
        Assert.NotNull(prefab);
        Assert.NotNull(prefab.GetComponent<GrandBossBrain>());
        Assert.NotNull(prefab.GetComponent<GrandBasicCombo>());
        Assert.NotNull(prefab.GetComponent<GrandTripleLeap>());
        Assert.NotNull(prefab.GetComponent<BossTelegraphController>());
        Assert.NotNull(prefab.GetComponent<GrandBossArtAdapter>());
    }

    [Test]
    public void EnsureOn_IsIdempotent_AndDisablesLegacyAiAuthority()
    {
        var boss = new GameObject("Enemy_Boss_Test");
        try
        {
            boss.AddComponent<Rigidbody2D>();
            boss.AddComponent<EnemyStats>();
            boss.AddComponent<EnemyHealth>();
            EnemyAI ai = boss.AddComponent<EnemyAI>();
            boss.AddComponent<EnemyCombat>();
            boss.AddComponent<EnemyController>();

            GrandBossBrain first = GrandBossBrain.EnsureOn(boss);
            GrandBossBrain second = GrandBossBrain.EnsureOn(boss);

            Assert.AreSame(first, second);
            Assert.IsFalse(ai.enabled, "格兰启用 GrandBossBrain 后，旧 EnemyAI 不得继续发起攻击。");
            Assert.AreEqual(1, boss.GetComponents<GrandBossBrain>().Length);
            Assert.AreEqual(1, boss.GetComponents<GrandBasicCombo>().Length);
            Assert.AreEqual(1, boss.GetComponents<GrandTripleLeap>().Length);
            Assert.AreEqual(1, boss.GetComponents<BossTelegraphController>().Length);

            GrandTripleLeap leap = boss.GetComponent<GrandTripleLeap>();
            AttackData leapData = Resources.Load<AttackData>("Data/AttackData_GrandLeapLand");
            Assert.AreEqual(leapData.TargetLayer.value, leap.TargetLayer.value);
        }
        finally
        {
            Object.DestroyImmediate(boss);
        }
    }
}